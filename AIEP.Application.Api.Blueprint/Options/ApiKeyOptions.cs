namespace AIEP.Application.Api.Blueprint.Options
{
    /// <summary>
    /// Opciones del <c>ApiKeyMiddleware</c>. Se enlazan de la seccion <c>Conf</c>.
    /// El valor de <see cref="ApiKey"/> llega de Key Vault directo (ver
    /// <c>BlueprintKeyVaultSecretManager</c>); si el Vault no es alcanzable, cae a lo que haya en
    /// appsettings/user-secrets/variables de entorno (ver README "Si no tienes acceso al Key Vault").
    /// Nunca se versiona el valor real en <c>appsettings*.json</c>.
    /// </summary>
    public class ApiKeyOptions
    {
        public const string SectionName = "Conf";

        /// <summary>Nombre del header que transporta la API key.</summary>
        public string ApiKeyHeaderName { get; set; } = "x-xmsbs-apikey";

        /// <summary>API key vigente (Key Vault directo, o respaldo en appsettings/user-secrets).</summary>
        public string? ApiKey { get; set; }

        /// <summary>Keys adicionales aceptadas durante ventanas de rotacion. Vacio por defecto.</summary>
        public string[] AdditionalKeys { get; set; } = Array.Empty<string>();
    }
}
