using AIEP.Application.Repository.Infrastructure;
using AIEP.Application.Repository.Options;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Oracle.ManagedDataAccess.Client;

namespace AIEP.Test.Repository
{
    [TestFixture]
    public class DbConnectionFactoryTests
    {
        private static IConfiguration BuildConfig(string? connectionString = "Data Source=dummy;User Id=x;Password=y;")
        {
            var data = new Dictionary<string, string?>();
            if (connectionString is not null)
            {
                data["ConnectionStrings:BlueprintDb"] = connectionString;
            }

            return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        }

        private static DbConnectionFactory CreateFactory(DbProviderType provider, IConfiguration? configuration = null)
        {
            var options = Options.Create(new DatabaseOptions
            {
                Provider = provider,
                ConnectionStringName = "BlueprintDb",
                CommandTimeout = 120
            });

            return new DbConnectionFactory(options, configuration ?? BuildConfig());
        }

        [Test]
        public void CreateConnection_Oracle_DevuelveOracleConnection()
        {
            var factory = CreateFactory(DbProviderType.Oracle);

            using var connection = factory.CreateConnection();

            Assert.IsInstanceOf<OracleConnection>(connection);
            Assert.AreEqual(DbProviderType.Oracle, factory.Provider);
            Assert.AreEqual(120, factory.CommandTimeout);
        }

        [Test]
        public void CreateConnection_SqlServer_DevuelveSqlConnection()
        {
            var factory = CreateFactory(DbProviderType.SqlServer);

            using var connection = factory.CreateConnection();

            Assert.IsInstanceOf<SqlConnection>(connection);
            Assert.AreEqual(DbProviderType.SqlServer, factory.Provider);
        }

        [Test]
        public void Constructor_SinConnectionString_Lanza()
        {
            var options = Options.Create(new DatabaseOptions { Provider = DbProviderType.Oracle, ConnectionStringName = "BlueprintDb" });

            Assert.Throws<InvalidOperationException>(() => new DbConnectionFactory(options, BuildConfig(connectionString: null)));
        }

        [Test]
        public void AddRefCursor_Oracle_AgregaParametro_SqlServer_DevuelveNull()
        {
            var oracle = CreateFactory(DbProviderType.Oracle);
            using var oracleCmd = oracle.CreateConnection().CreateCommand();
            var refCursor = oracle.AddRefCursor(oracleCmd, "C_DATA");

            Assert.IsNotNull(refCursor);
            Assert.AreEqual(1, oracleCmd.Parameters.Count);

            var sql = CreateFactory(DbProviderType.SqlServer);
            using var sqlCmd = sql.CreateConnection().CreateCommand();
            var none = sql.AddRefCursor(sqlCmd, "C_DATA");

            Assert.IsNull(none);
            Assert.AreEqual(0, sqlCmd.Parameters.Count);
        }

        [Test]
        public void PrepareStoredProcCommand_Oracle_ActivaBindByName()
        {
            var oracle = CreateFactory(DbProviderType.Oracle);
            using var cmd = (OracleCommand)oracle.CreateConnection().CreateCommand();

            oracle.PrepareStoredProcCommand(cmd);

            Assert.IsTrue(cmd.BindByName);
        }
    }
}
