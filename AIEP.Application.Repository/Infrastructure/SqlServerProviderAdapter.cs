using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace AIEP.Application.Repository.Infrastructure
{
    /// <summary>
    /// Adaptador para SQL Server. Unico archivo (junto con su gemelo de Oracle) que
    /// referencia tipos concretos de <c>Microsoft.Data.SqlClient</c>.
    /// </summary>
    internal sealed class SqlServerProviderAdapter : IDbProviderAdapter
    {
        public DbConnection CreateConnection(string connectionString)
            => new SqlConnection(connectionString);

        public void PrepareStoredProcCommand(DbCommand command)
        {
            // SQL Server no necesita ajustes: binding por nombre y multiples SELECT nativos.
        }

        public DbParameter? AddRefCursor(DbCommand command, string parameterName) => null;
    }
}
