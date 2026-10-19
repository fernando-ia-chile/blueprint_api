using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Reflection;
using AIEP.Application.Model.Models;
using AIEP.Application.Repository.Options;
using Microsoft.Extensions.Logging;

namespace AIEP.Application.Repository.Infrastructure
{
    /// <summary>
    /// Clase base para repositorios ADO.NET sin ORM. Concentra la apertura de conexion,
    /// la creacion de comandos de stored procedure, el alta de parametros, la lectura de
    /// parametros de salida y el mapeo de result sets, de forma agnostica de proveedor.
    /// Provider y CommandTimeout los entrega <paramref name="connectionFactory"/> (ya resuelto
    /// para la conexion con nombre que corresponda); esta clase no depende de
    /// <see cref="DatabaseOptions"/> directamente.
    /// </summary>
    public abstract class BaseRepository
    {
        protected BaseRepository(IDbConnectionFactory connectionFactory, ILogger logger)
        {
            ConnectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected IDbConnectionFactory ConnectionFactory { get; }

        protected ILogger Logger { get; }

        /// <summary>Crea y abre una conexion del proveedor activo (el del constructor).</summary>
        protected Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
            => OpenConnectionAsync(ConnectionFactory, cancellationToken);

        /// <summary>
        /// Igual que <see cref="OpenConnectionAsync(CancellationToken)"/> pero contra una conexion
        /// explicita -- para repositorios que usan mas de una conexion a la vez (ej. Oracle Banner +
        /// SQL Server simultaneas). La conexion "principal" (la del constructor) usa la sobrecarga
        /// sin parametro; cualquier otra que el repositorio reciba aparte (ej. con
        /// <c>[FromKeyedServices]</c>) se pasa aqui explicitamente.
        /// </summary>
        protected async Task<DbConnection> OpenConnectionAsync(IDbConnectionFactory connectionFactory, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(connectionFactory);

            var connection = connectionFactory.CreateConnection();
            try
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                return connection;
            }
            catch (DbException ex)
            {
                Logger.LogError(ex, "No se pudo abrir la conexion con el proveedor {Provider}.", connectionFactory.Provider);
                await connection.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>Crea un comando de stored procedure con el timeout configurado y los ajustes del proveedor (el del constructor).</summary>
        protected DbCommand CreateStoredProcCommand(DbConnection connection, string storedProcedureName)
            => CreateStoredProcCommand(ConnectionFactory, connection, storedProcedureName);

        /// <summary>Igual que <see cref="CreateStoredProcCommand(DbConnection, string)"/> pero contra una conexion explicita (ver <see cref="OpenConnectionAsync(IDbConnectionFactory, CancellationToken)"/>).</summary>
        protected static DbCommand CreateStoredProcCommand(IDbConnectionFactory connectionFactory, DbConnection connection, string storedProcedureName)
        {
            ArgumentNullException.ThrowIfNull(connectionFactory);
            ArgumentException.ThrowIfNullOrWhiteSpace(storedProcedureName);

            var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = storedProcedureName;
            command.CommandTimeout = connectionFactory.CommandTimeout;
            connectionFactory.PrepareStoredProcCommand(command);
            return command;
        }

        /// <summary>Agrega un parametro escalar. Los valores <c>null</c> se envian como <see cref="DBNull.Value"/>.</summary>
        protected static DbParameter AddParameter(
            DbCommand command,
            string name,
            object? value,
            DbType? dbType = null,
            ParameterDirection direction = ParameterDirection.Input,
            int? size = null)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            parameter.Direction = direction;
            if (dbType.HasValue)
            {
                parameter.DbType = dbType.Value;
            }

            if (size.HasValue)
            {
                parameter.Size = size.Value;
            }

            command.Parameters.Add(parameter);
            return parameter;
        }

        /// <summary>Agrega un cursor de salida (Oracle) o no hace nada (SQL Server), en la conexion del constructor. Ver <see cref="IDbConnectionFactory.AddRefCursor"/>.</summary>
        protected DbParameter? AddRefCursor(DbCommand command, string name)
            => AddRefCursor(ConnectionFactory, command, name);

        /// <summary>Igual que <see cref="AddRefCursor(DbCommand, string)"/> pero contra una conexion explicita (ver <see cref="OpenConnectionAsync(IDbConnectionFactory, CancellationToken)"/>).</summary>
        protected static DbParameter? AddRefCursor(IDbConnectionFactory connectionFactory, DbCommand command, string name)
        {
            ArgumentNullException.ThrowIfNull(connectionFactory);
            return connectionFactory.AddRefCursor(command, name);
        }

        /// <summary>Lee un parametro de salida como texto. Debe invocarse tras cerrar el <see cref="DbDataReader"/>.</summary>
        protected static string? GetOutputString(DbCommand command, string name)
        {
            ArgumentNullException.ThrowIfNull(command);
            var value = command.Parameters[name].Value;
            return value is null || value is DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        /// <summary>Mapea el result set actual del reader a una lista de entidades.</summary>
        protected static Task<List<T>> MapAsync<T>(DbDataReader reader, CancellationToken cancellationToken) where T : new()
            => DbReaderMapper.MapReaderToListAsync<T>(reader, cancellationToken);

        /// <summary>
        /// Traduce el par (codigo, mensaje) de salida del stored procedure a una excepcion de negocio.
        /// Contrato: <c>"OK"</c> o vacio = exito. Prefijos <c>"400"</c>/<c>"404"</c> se mapean a
        /// validacion / no encontrado; cualquier otro codigo a <see cref="BusinessException"/> (HTTP 422).
        /// </summary>
        protected void ThrowIfSpError(string? codigo, string? mensaje)
        {
            if (string.IsNullOrEmpty(codigo) || codigo.Equals("OK", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var detalle = $"SP Error [{codigo}]: {mensaje}";
            Logger.LogWarning("El stored procedure devolvio un codigo de error. {Detalle}", detalle);

            throw codigo switch
            {
                _ when codigo.StartsWith("400", StringComparison.Ordinal) => new DataValidationException(detalle),
                _ when codigo.StartsWith("404", StringComparison.Ordinal) => new NotFoundException<string>(detalle),
                _ => new BusinessException(detalle)
            };
        }

        // --------------------------------------------------------------------
        // Helpers opcionales para Table-Valued Parameters (solo SQL Server).
        // No se usan en la feature de ejemplo; se dejan como referencia del patron.
        // --------------------------------------------------------------------

        /// <summary>Convierte una lista de entidades en <see cref="DataTable"/> (una columna por propiedad publica).</summary>
        protected static DataTable ToDataTable<T>(IEnumerable<T> items)
        {
            ArgumentNullException.ThrowIfNull(items);

            var table = new DataTable(typeof(T).Name);
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                table.Columns.Add(property.Name, Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType);
            }

            foreach (var item in items)
            {
                var values = new object?[properties.Length];
                for (var i = 0; i < properties.Length; i++)
                {
                    values[i] = properties[i].GetValue(item) ?? DBNull.Value;
                }

                table.Rows.Add(values);
            }

            return table;
        }

        /// <summary>Agrega un Table-Valued Parameter en la conexion del constructor. Solo valido con <c>Database:Provider = SqlServer</c>.</summary>
        protected DbParameter AddStructuredParameter(DbCommand command, string name, string typeName, DataTable table)
            => AddStructuredParameter(ConnectionFactory, command, name, typeName, table);

        /// <summary>Igual que <see cref="AddStructuredParameter(DbCommand, string, string, DataTable)"/> pero contra una conexion explicita (ver <see cref="OpenConnectionAsync(IDbConnectionFactory, CancellationToken)"/>).</summary>
        protected static DbParameter AddStructuredParameter(IDbConnectionFactory connectionFactory, DbCommand command, string name, string typeName, DataTable table)
        {
            ArgumentNullException.ThrowIfNull(connectionFactory);
            ArgumentNullException.ThrowIfNull(command);

            if (connectionFactory.Provider != DbProviderType.SqlServer)
            {
                throw new NotSupportedException("Los Table-Valued Parameters solo estan soportados en SQL Server.");
            }

            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = table;
            // SqlParameter expone SqlDbType (System.Data.SqlDbType.Structured) y TypeName;
            // se ajustan por reflexion para no acoplar esta clase base a Microsoft.Data.SqlClient.
            parameter.GetType().GetProperty("SqlDbType")?.SetValue(parameter, SqlDbType.Structured);
            parameter.GetType().GetProperty("TypeName")?.SetValue(parameter, typeName);
            command.Parameters.Add(parameter);
            return parameter;
        }
    }
}
