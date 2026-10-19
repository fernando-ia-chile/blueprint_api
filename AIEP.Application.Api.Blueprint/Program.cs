using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using AIEP.Application.Api.Blueprint.Options;
using AIEP.Application.Api.Blueprint.Utilities;
using AIEP.Application.Logic.Logic;
using AIEP.Application.Repository;
using Asp.Versioning;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Models;
using NLog;
using NLog.Web;

var logger = NLog.LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
logger.Debug("init main");
try
{
    var builder = WebApplication.CreateBuilder(args);

    // Credencial explicita (ver AzureCredentialFactory): Managed Identity en Azure o en un
    // servidor con agente de Azure Arc, identidad de Azure CLI / Visual Studio en local.
    // Nunca secretos en archivos de configuracion.
    var azureCredential = AzureCredentialFactory.Create(builder.Configuration);

    // -- Key Vault directo (sin Azure App Configuration) -----------------------------------
    // Carga, una sola vez al arrancar, los secretos "blueprint-*" del Vault directo a
    // IConfiguration -- BlueprintKeyVaultSecretManager mapea "blueprint-<nombre>-connectionstring"
    // a ConnectionStrings:<nombre> y "blueprint-api-key" a Conf:ApiKey. Sin llamadas a Key Vault
    // por request (a diferencia de AIEP-Servicios-Sedes_AzArc). Debe ir ANTES de
    // AddBlueprintRepository: esa extension resuelve ConnectionStrings:<Nombre> al registrar cada
    // conexion.
    //
    // Si el Vault no es alcanzable (ej. un desarrollador sin rol RBAC asignado), no debe tirar
    // abajo el arranque completo: se registra un warning y la app sigue con lo que ya haya en
    // appsettings/user-secrets/variables de entorno (ResolveConnectionString y Conf:ApiKey usan
    // esa misma cadena de prioridad -- ver README "Si no tienes acceso al Key Vault").
    var keyVaultUri = builder.Configuration["Conf:KeyVaultUri"];
    if (!string.IsNullOrWhiteSpace(keyVaultUri))
    {
        try
        {
            builder.Configuration.AddAzureKeyVault(
                new Uri(keyVaultUri),
                azureCredential,
                new BlueprintKeyVaultSecretManager());
        }
        catch (Exception ex)
        {
            logger.Warn(ex,
                "No se pudo leer Key Vault ({KeyVaultUri}) -- se usaran appsettings/user-secrets/" +
                "variables de entorno como respaldo (ConnectionStrings:<Nombre>, Conf:ApiKey).",
                keyVaultUri);
        }
    }

    // -- CORS ---------------------------------------------------------------------------
    builder.Services.AddCors(options => options.AddPolicy("CorsPolicy", policy =>
    {
        var allowedHosts = builder.Configuration.GetSection("AllowedHosts").Value;
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

        if (allowedHosts == "*")
        {
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        }
        else if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader();
        }
    }));

    // -- AuthN (Azure AD / JWT Bearer) -------------------------------------------------
    JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
    builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration);

    // -- Opciones tipadas -------------------------------------------------------------
    builder.Services.Configure<ApiKeyOptions>(builder.Configuration.GetSection(ApiKeyOptions.SectionName));
    builder.Services.Configure<TenantOptions>(builder.Configuration.GetSection(TenantOptions.SectionName));

    // -- Capas de la aplicacion -----------------------------------------------------
    // AddBlueprintRepository enlaza y valida (DataAnnotations) DatabaseOptions; aqui solo
    // añadimos la validacion al arranque (extension del host, disponible en el proyecto API).
    builder.Services.AddBlueprintRepository(builder.Configuration);
    builder.Services.AddScoped<RecursoLogic>();

    // Ejemplo de referencia (ver RecursoLogicOracle/RecursoLogicSqlServer): demuestra el
    // repositorio keyed por motor. Seguro de borrar junto con esas clases si el proyecto no las
    // necesita.
    builder.Services.AddScoped<RecursoLogicOracle>();
    builder.Services.AddScoped<RecursoLogicSqlServer>();

    builder.Services.AddHttpClient();
    builder.Services.AddControllers();

    // SecretClient singleton para necesidades ad-hoc (NO se usa por request en el middleware).
    if (!string.IsNullOrWhiteSpace(keyVaultUri))
    {
        builder.Services.AddSingleton(new SecretClient(new Uri(keyVaultUri), azureCredential));
    }

    // -- Versionado de API ----------------------------------------------------------
    builder.Services.AddApiVersioning(opt =>
    {
        opt.DefaultApiVersion = new ApiVersion(1, 0);
        opt.AssumeDefaultVersionWhenUnspecified = true;
        opt.ReportApiVersions = true;
        opt.ApiVersionReader = ApiVersionReader.Combine(
            new UrlSegmentApiVersionReader(),
            new HeaderApiVersionReader("x-api-version"),
            new MediaTypeApiVersionReader("x-api-version"));
    }).AddMvc().AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

    // -- AutoMapper ---------------------------------------------------------------
    // AutoMapper 16.2.0 con licencia comercial de LuckyPenny Software (ver
    // AIEP.Application.Api.Blueprint.csproj). Desde la 15, AddAutoMapper exige una accion de
    // configuracion; la key real se lee de Conf:AutoMapperLicenseKey (appsettings / variable de
    // entorno -- no es secreta, solo de auditoria, no necesita Key Vault).
    builder.Services.AddAutoMapper(cfg =>
    {
        var autoMapperLicenseKey = builder.Configuration["Conf:AutoMapperLicenseKey"];
        if (!string.IsNullOrWhiteSpace(autoMapperLicenseKey))
        {
            cfg.LicenseKey = autoMapperLicenseKey;
        }
    }, AppDomain.CurrentDomain.GetAssemblies());

    // -- Swagger ----------------------------------------------------------------
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "API - Blueprint",
            Version = "v1",
            Description = "Servicio base (blueprint) para microservicios AIEP.",
        });

        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            c.IncludeXmlComments(xmlPath);
        }

        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Token JWT de Azure AD. Ejemplo: 'Bearer {token}'."
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });

    // -- Logging (NLog) -------------------------------------------------------
    builder.Logging.ClearProviders();
    if (OperatingSystem.IsWindows())
    {
        builder.Logging.AddEventLog();
    }

    builder.Host.UseNLog();

    var app = builder.Build();

    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("CorsPolicy");
    app.UseMiddleware<ApiKeyMiddleware>();
    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception exception)
{
    logger.Error(exception, $"Stopped program because of exception: {exception.Message}");
    throw;
}
finally
{
    // Ensure to flush and stop internal timers/threads before application-exit (avoid segfault on Linux).
    NLog.LogManager.Shutdown();
}

/// <summary>Punto de entrada explicito para permitir pruebas de integracion con WebApplicationFactory.</summary>
public partial class Program
{
}
