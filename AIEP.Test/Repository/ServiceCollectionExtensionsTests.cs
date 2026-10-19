using AIEP.Application.Repository;
using AIEP.Application.Repository.Infrastructure;
using AIEP.Application.Repository.Options;
using AIEP.Application.Repository.Repository.Recurso;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIEP.Test.Repository
{
    /// <summary>
    /// Verifica el soporte de mas de una conexion de BD simultanea: cada entrada de
    /// <c>Database:Connections</c> queda registrada como <see cref="IDbConnectionFactory"/>
    /// keyed por nombre, y <see cref="IRecursoRepository"/> usa la conexion que indique
    /// <c>Conf:RecursoConnectionName</c> (configurable por entorno).
    /// </summary>
    [TestFixture]
    public class ServiceCollectionExtensionsTests
    {
        private static IConfiguration BuildConfig(
            string recursoConnectionName = "OracleDb",
            string? oracleConnectionString = "Data Source=dummy-oracle;User Id=x;Password=y;",
            string? sqlServerConnectionString = "Server=dummy-sql;Database=d;User Id=x;Password=y;")
        {
            var data = new Dictionary<string, string?>
            {
                ["Database:Connections:OracleDb:Provider"] = "Oracle",
                ["Database:Connections:OracleDb:ConnectionStringName"] = "OracleDb",
                ["Database:Connections:OracleDb:CommandTimeout"] = "300",
                ["Database:Connections:SqlServerDb:Provider"] = "SqlServer",
                ["Database:Connections:SqlServerDb:ConnectionStringName"] = "SqlServerDb",
                ["Database:Connections:SqlServerDb:CommandTimeout"] = "120",
                ["Conf:RecursoConnectionName"] = recursoConnectionName
            };

            if (oracleConnectionString is not null)
            {
                data["ConnectionStrings:OracleDb"] = oracleConnectionString;
            }

            if (sqlServerConnectionString is not null)
            {
                data["ConnectionStrings:SqlServerDb"] = sqlServerConnectionString;
            }

            return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        }

        private static ServiceProvider BuildProvider(IConfiguration configuration)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(configuration);
            services.AddBlueprintRepository(configuration);
            return services.BuildServiceProvider();
        }

        [Test]
        public void AddBlueprintRepository_RegistraUnaConexionKeyedPorNombre()
        {
            using var provider = BuildProvider(BuildConfig());

            var oracle = provider.GetRequiredKeyedService<IDbConnectionFactory>("OracleDb");
            var sqlServer = provider.GetRequiredKeyedService<IDbConnectionFactory>("SqlServerDb");

            Assert.AreEqual(DbProviderType.Oracle, oracle.Provider);
            Assert.AreEqual(DbProviderType.SqlServer, sqlServer.Provider);
            Assert.AreEqual(120, sqlServer.CommandTimeout);
        }

        [Test]
        public void AddBlueprintRepository_RecursoRepository_UsaLaConexionConfigurada_Oracle()
        {
            // SqlServerDb sin cadena de conexion: si RecursoRepository tomara la conexion
            // equivocada, esto fallaria al resolver IRecursoRepository.
            using var provider = BuildProvider(BuildConfig(recursoConnectionName: "OracleDb", sqlServerConnectionString: null));

            var recursoRepository = provider.GetRequiredService<IRecursoRepository>();

            Assert.IsNotNull(recursoRepository);
        }

        [Test]
        public void AddBlueprintRepository_RecursoRepository_UsaLaConexionConfigurada_SqlServer()
        {
            // OracleDb sin cadena de conexion: prueba el caso inverso (Development, donde
            // Conf:RecursoConnectionName = "SqlServerDb").
            using var provider = BuildProvider(BuildConfig(recursoConnectionName: "SqlServerDb", oracleConnectionString: null));

            var recursoRepository = provider.GetRequiredService<IRecursoRepository>();

            Assert.IsNotNull(recursoRepository);
        }

        [Test]
        public void AddBlueprintRepository_RecursoConnectionName_Desconocido_Lanza()
        {
            using var provider = BuildProvider(BuildConfig(recursoConnectionName: "NoExiste"));

            Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IRecursoRepository>());
        }

        [Test]
        public void AddBlueprintRepository_ConexionSinValorUsable_SeOmite_SinRomperElArranque()
        {
            // "VacioDb" no tiene ConnectionStringName ni ConnectionString ni secreto en ConnectionStrings:
            // el host debe construirse sin problema; solo falla si algo pide esa conexion puntual.
            var data = new Dictionary<string, string?>
            {
                ["Database:Connections:OracleDb:Provider"] = "Oracle",
                ["Database:Connections:OracleDb:ConnectionStringName"] = "OracleDb",
                ["Database:Connections:VacioDb:Provider"] = "SqlServer",
                ["Database:Connections:VacioDb:ConnectionStringName"] = "",
                ["ConnectionStrings:OracleDb"] = "Data Source=dummy-oracle;User Id=x;Password=y;",
                ["Conf:RecursoConnectionName"] = "OracleDb"
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(data).Build();

            using var provider = BuildProvider(configuration);

            Assert.IsNotNull(provider.GetRequiredKeyedService<IDbConnectionFactory>("OracleDb"));
            Assert.Throws<InvalidOperationException>(() => provider.GetRequiredKeyedService<IDbConnectionFactory>("VacioDb"));
        }

        [Test]
        public void AddBlueprintRepository_ConnectionStringDirecto_SeResuelveSinVault()
        {
            // "DirectoDb" solo tiene el valor literal (sin ConnectionStringName, sin ConnectionStrings:*):
            // el caso de un desarrollador sin acceso al Key Vault.
            var data = new Dictionary<string, string?>
            {
                ["Database:Connections:DirectoDb:Provider"] = "SqlServer",
                ["Database:Connections:DirectoDb:ConnectionString"] = "Data Source=directo;User Id=x;Password=y;",
                ["Conf:RecursoConnectionName"] = "DirectoDb"
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(data).Build();

            using var provider = BuildProvider(configuration);

            var recursoRepository = provider.GetRequiredService<IRecursoRepository>();
            Assert.IsNotNull(recursoRepository);
        }
    }
}
