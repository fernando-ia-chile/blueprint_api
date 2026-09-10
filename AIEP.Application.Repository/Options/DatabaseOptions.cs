using System.ComponentModel.DataAnnotations;

namespace AIEP.Application.Repository.Options
{
    /// <summary>
    /// Opciones tipadas de la capa de datos. Se enlazan de la seccion <see cref="SectionName"/>.
    /// </summary>
    public class DatabaseOptions
    {
        public const string SectionName = "Database";

        /// <summary>Motor a utilizar: <c>Oracle</c> o <c>SqlServer</c>.</summary>
        [Required]
        public DbProviderType Provider { get; set; } = DbProviderType.Oracle;

        /// <summary>
        /// Nombre logico de la cadena de conexion (seccion <c>ConnectionStrings</c>).
        /// El valor concreto lo entrega App Configuration / Key Vault en los entornos desplegados.
        /// </summary>
        [Required]
        [MinLength(1)]
        public string ConnectionStringName { get; set; } = "BlueprintDb";

        /// <summary>Timeout de comando en segundos (equivale al <c>CommandTimeout = 300</c> de la referencia).</summary>
        [Range(1, 3600)]
        public int CommandTimeout { get; set; } = 300;
    }
}
