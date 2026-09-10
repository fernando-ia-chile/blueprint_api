using System.Data.Common;
using AIEP.Application.Repository.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AIEP.Application.Repository.Infrastructure
{
    /// <summary>
    /// Implementacion por defecto de <see cref="IDbConnectionFactory"/>. Es stateless y
    /// se registra como singleton; cada conexion se crea por invocacion.
    /// </summary>
    public sealed class DbConnectionFactory : IDbConnectionFactory
    {
        private readonly IDbProviderAdapter _adapter;
        private readonly string _connectionString;

        public DbProviderType Provider { get; }

        public int CommandTimeout { get; }

        public DbConnectionFactory(IOptions<DatabaseOptions> options, IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(configuration);

            var value = options.Value ?? new DatabaseOptions();

            Provider = value.Provider;
            CommandTimeout = value.CommandTimeout > 0 ? value.CommandTimeout : 300;

            _adapter = value.Provider switch
            {
                DbProviderType.Oracle => new OracleProviderAdapter(),
                DbProviderType.SqlServer => new SqlServerProviderAdapter(),
                _ => throw new InvalidOperationException(
                    $"Valor de configuracion 'Database:Provider' no soportado: '{value.Provider}'.")
            };

            var connectionStringName = string.IsNullOrWhiteSpace(value.ConnectionStringName)
                ? "BlueprintDb"
                : value.ConnectionStringName;

            _connectionString = configuration.GetConnectionString(connectionStringName)
                ?? throw new InvalidOperationException(
                    $"No se encontro la cadena de conexion 'ConnectionStrings:{connectionStringName}'. " +
                    "En entornos desplegados la entrega Azure App Configuration / Key Vault; " +
                    "en local use 'dotnet user-secrets' o variables de entorno.");
        }

        public DbConnection CreateConnection() => _adapter.CreateConnection(_connectionString);

        public void PrepareStoredProcCommand(DbCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);
            _adapter.PrepareStoredProcCommand(command);
        }

        public DbParameter? AddRefCursor(DbCommand command, string parameterName)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);
            return _adapter.AddRefCursor(command, parameterName);
        }
    }
}
