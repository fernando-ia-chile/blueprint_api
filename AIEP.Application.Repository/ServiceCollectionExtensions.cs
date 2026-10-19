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
        /// (via <c>ConnectionStrings:&lt;Nombre&gt;</c>). Los repositorios concretos eligen, por
        /// configuracion, cual conexion keyed usan (ver <c>Conf:RecursoConnectionName</c>).
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

            // RecursoRepository: la conexion keyed que usa es configurable (Conf:RecursoConnectionName,
            // "OracleDb" en produccion/QA, "SqlServerDb" en Development) para poder probar el feature
            // contra el otro motor sin tocar codigo. No se resuelve por atributo porque la clave debe
            // poder variar en runtime segun el entorno.
            services.AddScoped<IRecursoRepository>(sp =>
            {
                var recursoConnectionName = configuration.GetValue<string>("Conf:RecursoConnectionName") ?? "OracleDb";
                var connectionFactory = sp.GetRequiredKeyedService<IDbConnectionFactory>(recursoConnectionName);
                var logger = sp.GetRequiredService<ILogger<RecursoRepository>>();

                return new RecursoRepository(connectionFactory, configuration, logger);
            });

            return services;
        }
    }
}
