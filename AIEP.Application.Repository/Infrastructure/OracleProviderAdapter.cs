using System.Data;
using System.Data.Common;
using Oracle.ManagedDataAccess.Client;

namespace AIEP.Application.Repository.Infrastructure
{
    /// <summary>
    /// Adaptador para Oracle. Unico archivo (junto con su gemelo de SQL Server) que
    /// referencia tipos concretos de <c>Oracle.ManagedDataAccess.Client</c>.
    /// </summary>
    internal sealed class OracleProviderAdapter : IDbProviderAdapter
    {
        public DbConnection CreateConnection(string connectionString)
            => new OracleConnection(connectionString);

        public void PrepareStoredProcCommand(DbCommand command)
        {
            // Los parametros se agregan por nombre; sin esto Oracle hace binding posicional.
            if (command is OracleCommand oracleCommand)
            {
                oracleCommand.BindByName = true;
            }
        }

        public DbParameter? AddRefCursor(DbCommand command, string parameterName)
        {
            var parameter = new OracleParameter(parameterName, OracleDbType.RefCursor)
            {
                Direction = ParameterDirection.Output
            };
            command.Parameters.Add(parameter);
            return parameter;
        }
    }
}
