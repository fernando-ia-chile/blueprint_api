using AIEP.Application.Api.Blueprint.Mapper;
using AIEP.Application.Logic.Logic;
using AIEP.Application.Repository;
using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIEP.Test.Logic
{
    /// <summary>
    /// Prueba end-to-end (contenedor DI real, sin mocks) de que <see cref="RecursoLogicOracle"/> y
    /// <see cref="RecursoLogicSqlServer"/> resuelven via <c>[FromKeyedServices]</c> contra el
    /// repositorio keyed que registra <c>AddBlueprintRepository</c> -- confirma que las claves
    /// "OracleDb"/"SqlServerDb" del atributo coinciden con las del registro (un typo en cualquiera
    /// de los dos lados no lo detectaria ningun otro test de esta solucion, ya que
    /// RecursoLogicOracleTests/RecursoLogicSqlServerTests construyen la clase con un mock directo,
    /// sin pasar por DI).
    /// </summary>
    [TestFixture]
    public class RecursoLogicKeyedResolutionTests
    {
        private static ServiceProvider BuildProvider()
        {
            var data = new Dictionary<string, string?>
            {
                ["Database:Connections:OracleDb:Provider"] = "Oracle",
                ["Database:Connections:OracleDb:ConnectionStringName"] = "OracleDb",
                ["Database:Connections:SqlServerDb:Provider"] = "SqlServer",
                ["Database:Connections:SqlServerDb:ConnectionStringName"] = "SqlServerDb",
                ["ConnectionStrings:OracleDb"] = "Data Source=dummy-oracle;User Id=x;Password=y;",
                ["ConnectionStrings:SqlServerDb"] = "Server=dummy-sql;Database=d;User Id=x;Password=y;",
                ["Conf:RecursoConnectionName"] = "OracleDb"
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(data).Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(configuration);
            services.AddBlueprintRepository(configuration);

            // IMapper construido igual que LogicTestBase (MapperConfiguration directa), para no
            // depender de la licencia comercial que exige AddAutoMapper(IServiceCollection, ...).
            var mapper = new MapperConfiguration(cfg => cfg.AddProfile(new MapperConfig()), NullLoggerFactory.Instance).CreateMapper();
            services.AddSingleton<IMapper>(mapper);

            services.AddScoped<RecursoLogicOracle>();
            services.AddScoped<RecursoLogicSqlServer>();

            return services.BuildServiceProvider();
        }

        [Test]
        public void RecursoLogicOracle_Resuelve_ConElRepositorioKeyedOracleDb()
        {
            using var provider = BuildProvider();

            Assert.IsNotNull(provider.GetRequiredService<RecursoLogicOracle>());
        }

        [Test]
        public void RecursoLogicSqlServer_Resuelve_ConElRepositorioKeyedSqlServerDb()
        {
            using var provider = BuildProvider();

            Assert.IsNotNull(provider.GetRequiredService<RecursoLogicSqlServer>());
        }
    }
}
