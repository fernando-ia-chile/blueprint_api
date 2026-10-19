using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

namespace AIEP.Application.Repository.Options
{
    /// <summary>
    /// Opciones tipadas de UNA conexion de BD. Se enlazan, con nombre, desde cada hijo de
    /// <see cref="ConnectionsSectionName"/> (ej. <c>Database:Connections:OracleDb</c>) — soporta
    /// mas de una conexion simultanea, cada una con su propio secreto de Key Vault.
    /// </summary>
    public class DatabaseOptions
    {
        public const string ConnectionsSectionName = "Database:Connections";

        /// <summary>Motor a utilizar: <c>Oracle</c> o <c>SqlServer</c>.</summary>
        [Required]
        public DbProviderType Provider { get; set; } = DbProviderType.Oracle;

        /// <summary>
        /// Nombre logico de la cadena de conexion (seccion <c>ConnectionStrings</c>, poblada por
        /// Key Vault directo). Puede venir vacio si esta conexion no usa Vault
        /// (ver <see cref="ConnectionString"/>) o si no se usa en absoluto.
        /// </summary>
        public string? ConnectionStringName { get; set; }

        /// <summary>
        /// Cadena de conexion literal, escrita directo en <c>appsettings</c>. Es el respaldo
        /// cuando no se usa (o no se puede alcanzar) Key Vault -- por ejemplo, un desarrollador
        /// sin rol asignado en el Vault. <see cref="ResolveConnectionString"/> solo la usa si
        /// <see cref="ConnectionStringName"/> no resuelve nada via Vault.
        /// </summary>
        public string? ConnectionString { get; set; }

        /// <summary>Timeout de comando en segundos (equivale al <c>CommandTimeout = 300</c> de la referencia).</summary>
        [Range(1, 3600)]
        public int CommandTimeout { get; set; } = 300;

        /// <summary>
        /// Resuelve la cadena de conexion efectiva: Vault (<c>ConnectionStrings:ConnectionStringName</c>)
        /// tiene prioridad siempre que exista; si no, cae al valor literal de <see cref="ConnectionString"/>.
        /// Devuelve <c>null</c> si ninguna de las dos resuelve algo usable -- en ese caso la conexion
        /// se omite (ver <c>ServiceCollectionExtensions.AddBlueprintRepository</c>), no se lanza aqui.
        /// </summary>
        public string? ResolveConnectionString(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var fromVault = string.IsNullOrWhiteSpace(ConnectionStringName)
                ? null
                : configuration.GetConnectionString(ConnectionStringName);

            return !string.IsNullOrWhiteSpace(fromVault) ? fromVault : ConnectionString;
        }
    }
}
