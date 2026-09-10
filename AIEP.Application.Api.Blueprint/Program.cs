using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using AIEP.Application.Api.Blueprint.Options;
using AIEP.Application.Api.Blueprint.Utilities;
using AIEP.Application.Logic.Logic;
using AIEP.Application.Repository;
using AIEP.Application.Repository.Options;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Models;
using NLog;
using NLog.Web;

var logger = NLog.LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
logger.Debug("init main");
try
{
    var builder = WebApplication.CreateBuilder(args);

    // Credencial unica: Managed Identity en Azure, identidad de Azure Arc on-prem,
    // Azure CLI / Visual Studio en local. Nunca secretos en archivos de configuracion.
    var azureCredential = new DefaultAzureCredential();

    // -- Azure App Configuration (+ referencias a Key Vault) -------------------------------
    var appConfigEndpoint = builder.Configuration["AzureAppConfig:Endpoint"];
    var useAppConfiguration = !string.IsNullOrWhiteSpace(appConfigEndpoint);
    if (useAppConfiguration)
    {
        builder.Configuration.AddAzureAppConfiguration(options =>
        {
            options.Connect(new Uri(appConfigEndpoint!), azureCredential)
                   .ConfigureKeyVault(kv => kv.SetCredential(azureCredential))
                   .Select("Blueprint:*", LabelFilter.Null)
                   .Select("Blueprint:*", builder.Environment.EnvironmentName)
                   .TrimKeyPrefix("Blueprint:")
                   .ConfigureRefresh(refresh => refresh
                       .Register("Blueprint:Sentinel", refreshAll: true)
                       .SetRefreshInterval(TimeSpan.FromMinutes(5)));
        }, optional: true);

        builder.Services.AddAzureAppConfiguration();
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
    builder.Services.AddOptions<DatabaseOptions>().ValidateOnStart();
    builder.Services.AddScoped<RecursoLogic>();

    builder.Services.AddHttpClient();
    builder.Services.AddControllers();

    // SecretClient singleton para necesidades ad-hoc (NO se usa por request en el middleware).
    var keyVaultUri = builder.Configuration["Conf:KeyVaultUri"];
    if (!string.IsNullOrWhiteSpace(keyVaultUri))
    {
        builder.Services.AddSingleton(new SecretClient(new Uri(keyVaultUri), azureCredential));
    }

    // -- Versionado de API ----------------------------------------------------------
    builder.Services.AddApiVersioning(opt =>
    {
        opt.DefaultApiVersion = new Microsoft.AspNetCore.Mvc.ApiVersion(1, 0);
        opt.AssumeDefaultVersionWhenUnspecified = true;
        opt.ReportApiVersions = true;
        opt.ApiVersionReader = ApiVersionReader.Combine(
            new UrlSegmentApiVersionReader(),
            new HeaderApiVersionReader("x-api-version"),
            new MediaTypeApiVersionReader("x-api-version"));
    });

    // -- AutoMapper ---------------------------------------------------------------
    builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

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

    if (useAppConfiguration)
    {
        app.UseAzureAppConfiguration();
    }

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
