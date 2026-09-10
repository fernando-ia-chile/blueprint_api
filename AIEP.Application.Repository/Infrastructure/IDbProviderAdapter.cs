using System.Data.Common;

namespace AIEP.Application.Repository.Infrastructure
{
    /// <summary>
    /// Contrato interno que aisla el codigo dependiente del driver concreto.
    /// Solo las implementaciones tocan <c>Oracle.ManagedDataAccess.Client</c> /
    /// <c>Microsoft.Data.SqlClient</c>.
    /// </summary>
    internal interface IDbProviderAdapter
    {
        DbConnection CreateConnection(string connectionString);

        void PrepareStoredProcCommand(DbCommand command);

        DbParameter? AddRefCursor(DbCommand command, string parameterName);
    }
}
