using System.Data.Common;
using AIEP.Application.Repository.Options;

namespace AIEP.Application.Repository.Infrastructure
{
    /// <summary>
    /// Fabrica de conexiones agnostica de proveedor. Concentra en un unico punto todo lo
    /// que <see cref="DbCommand"/> / <see cref="DbParameter"/> no cubren de forma portable
    /// (REF CURSOR y binding por nombre de Oracle). El resto de la capa de repositorio
    /// trabaja solo contra <c>System.Data.Common</c>.
    /// </summary>
    public interface IDbConnectionFactory
    {
        /// <summary>Motor activo, resuelto desde <c>Database:Provider</c>.</summary>
        DbProviderType Provider { get; }

        /// <summary>Timeout de comando (segundos) configurado en <c>Database:CommandTimeout</c>.</summary>
        int CommandTimeout { get; }

        /// <summary>
        /// Crea una conexion cerrada del proveedor activo
        /// (<c>OracleConnection</c> o <c>SqlConnection</c>). El llamador la abre y la libera.
        /// </summary>
        DbConnection CreateConnection();

        /// <summary>
        /// Ajustes especificos del proveedor sobre un comando de stored procedure.
        /// En Oracle activa <c>BindByName = true</c>; en SQL Server no hace nada.
        /// </summary>
        void PrepareStoredProcCommand(DbCommand command);

        /// <summary>
        /// Agrega un parametro de cursor de salida. En Oracle añade un
        /// <c>OracleParameter</c> de tipo <c>RefCursor</c> y lo devuelve; en SQL Server
        /// devuelve <c>null</c> (los result sets llegan directamente por el reader).
        /// Debe invocarse ANTES que los parametros escalares de salida.
        /// </summary>
        DbParameter? AddRefCursor(DbCommand command, string parameterName);
    }
}
