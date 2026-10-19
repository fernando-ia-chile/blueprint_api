using AIEP.Application.Repository.Infrastructure;
using AIEP.Application.Repository.Options;
using AIEP.Application.Repository.Repository.Recurso;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIEP.Application.Repository
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registra la capa de repositorio sin ORM. Por cada conexion declarada en
        /// <see cref="DatabaseOptions.ConnectionsSectionName"/> (ej. <c>OracleDb</c>,
        /// <c>SqlServerDb</c>) enlaza sus <see cref="DatabaseOptions"/> con nombre y registra un
        /// <see cref="IDbConnectionFactory"/> keyed con ese mismo nombre — permite mas de una
        /// conexion de BD simultanea, cada una resuelta desde su propio secreto de Key Vault
        /// (via <c>ConnectionStrings:&lt;Nombre&gt;</c>). Ademas, <see cref="IRecursoRepository"/>
        /// queda registrado dos veces: keyed (<c>"OracleDb"</c> / <c>"SqlServerDb"</c>, ambos
        /// resolubles en paralelo via <c>[FromKeyedServices]</c> — ver <c>RecursoLogicOracle</c> /
        /// <c>RecursoLogicSqlServer</c>) y sin key, delegando en el keyed que indique
        /// <c>Conf:RecursoConnectionName</c> (el que usan <c>RecursoLogic</c>/<c>RecursoController</c>).
        /// </summary>
        public static IServiceCollection AddBlueprintRepository(this IServiceCollection services, IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            var connectionsSection = configuration.GetSection(DatabaseOptions.ConnectionsSectionName);
            foreach (var connectionSection in connectionsSection.GetChildren())
            {
                var connectionName = connectionSection.Key;

                // Se omite por completo si no hay cadena usable (ni Vault ni valor literal) --
                // nodo borrado, "ConnectionStringName": "" sin respaldo, o Vault sin poblar y sin
                // respaldo caen todos aqui. No registrar nada para ella es lo que evita que el
                // arranque se caiga; si algo la pide despues, falla ahi, no antes.
                var candidate = new DatabaseOptions();
                connectionSection.Bind(candidate);
                if (string.IsNullOrWhiteSpace(candidate.ResolveConnectionString(configuration)))
                {
                    continue;
                }

                services.AddOptions<DatabaseOptions>(connectionName)
                        .Bind(connectionSection)
                        .ValidateDataAnnotations()
                        .ValidateOnStart();

                services.AddKeyedSingleton<IDbConnectionFactory>(connectionName, (sp, key) =>
                {
                    var namedOptions = sp.GetRequiredService<IOptionsMonitor<DatabaseOptions>>().Get((string)key!);
                    return new DbConnectionFactory(Microsoft.Extensions.Options.Options.Create(namedOptions), configuration);
                });
            }

            // RecursoRepository es agnostico de motor (ver su doc comment): el mismo codigo ADO.NET
            // sirve para Oracle y SQL Server, la diferencia la pone el IDbConnectionFactory que
            // recibe -- este helper lo construye contra cualquier conexion keyed ya registrada.
            RecursoRepository BuildRecursoRepository(IServiceProvider sp, string connectionName)
            {
                var connectionFactory = sp.GetRequiredKeyedService<IDbConnectionFactory>(connectionName);
                var logger = sp.GetRequiredService<ILogger<RecursoRepository>>();
                return new RecursoRepository(connectionFactory, configuration, logger);
            }

            // Se registra keyed explicitamente para los dos motores que este feature conoce -- no
            // en el foreach generico de arriba, que es para TODAS las conexiones declaradas, no solo
            // las que usa Recurso. Con esto, un segundo consumidor puede pedir el motor que no esta
            // configurado por defecto via [FromKeyedServices] (ver RecursoLogicOracle /
            // RecursoLogicSqlServer), con ambos disponibles en paralelo.
            services.AddKeyedScoped<IRecursoRepository>("OracleDb", (sp, _) => BuildRecursoRepository(sp, "OracleDb"));
            services.AddKeyedScoped<IRecursoRepository>("SqlServerDb", (sp, _) => BuildRecursoRepository(sp, "SqlServerDb"));

            // Acceso sin key (el que usa RecursoLogic/RecursoController hoy): generico para
            // cualquier conexion que indique Conf:RecursoConnectionName -- no solo las dos de
            // arriba, puede ser cualquier nombre declarado en Database:Connections (ej. "DirectoDb",
            // una conexion solo con ConnectionString literal, sin Vault). "OracleDb" en
            // produccion/QA, "SqlServerDb" en Development, para poder probar el feature contra el
            // otro motor sin tocar codigo. Mismo comportamiento que antes de este cambio.
            services.AddScoped<IRecursoRepository>(sp =>
            {
                var recursoConnectionName = configuration.GetValue<string>("Conf:RecursoConnectionName") ?? "OracleDb";
                return BuildRecursoRepository(sp, recursoConnectionName);
            });

            return services;
        }
    }
}
