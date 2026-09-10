using AIEP.Application.Repository.Infrastructure;
using AIEP.Application.Repository.Options;
using AIEP.Application.Repository.Repository.Recurso;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIEP.Application.Repository
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registra la capa de repositorio sin ORM: opciones tipadas <see cref="DatabaseOptions"/>,
        /// la fabrica de conexiones agnostica de proveedor y los repositorios concretos.
        /// </summary>
        public static IServiceCollection AddBlueprintRepository(this IServiceCollection services, IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            services.AddOptions<DatabaseOptions>()
                    .Bind(configuration.GetSection(DatabaseOptions.SectionName))
                    .ValidateDataAnnotations();

            services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();

            services.AddScoped<IRecursoRepository, RecursoRepository>();

            return services;
        }
    }
}
