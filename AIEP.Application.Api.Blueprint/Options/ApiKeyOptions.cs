namespace AIEP.Application.Api.Blueprint.Options
{
    /// <summary>
    /// Opciones del <c>ApiKeyMiddleware</c>. Se enlazan de la seccion <c>Conf</c>.
    /// El valor de <see cref="ApiKey"/> lo entrega Azure App Configuration como referencia a
    /// Key Vault; nunca se versiona en <c>appsettings*.json</c>.
    /// </summary>
    public class ApiKeyOptions
    {
        public const string SectionName = "Conf";

        /// <summary>Nombre del header que transporta la API key.</summary>
        public string ApiKeyHeaderName { get; set; } = "x-xmsbs-apikey";

        /// <summary>API key vigente (desde App Configuration / Key Vault).</summary>
        public string? ApiKey { get; set; }

        /// <summary>Keys adicionales aceptadas durante ventanas de rotacion. Vacio por defecto.</summary>
        public string[] AdditionalKeys { get; set; } = Array.Empty<string>();
    }
}
