using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace AIEP.Application.Api.Blueprint.Utilities
{
    /// <summary>
    /// Resuelve explicitamente la credencial de Azure usada para leer Azure App Configuration /
    /// Key Vault, en vez de <c>DefaultAzureCredential</c>. A proposito: una cadena implicita
    /// prueba varias credenciales en silencio y no deja saber si la Managed Identity de la
    /// maquina (Azure o servidor Azure Arc on-prem) realmente esta funcionando.
    /// </summary>
    public static class AzureCredentialFactory
    {
        /// <summary>
        /// <c>Auth:UseManagedIdentity=true</c> (por defecto en despliegues): Managed Identity,
        /// system- o user-assigned (<c>Auth:ManagedIdentityClientId</c>). Cubre tanto Azure
        /// (App Service / Container Apps / AKS) como un servidor con el agente de Azure Arc
        /// instalado (expone la misma Managed Identity via <c>IDENTITY_ENDPOINT</c>/<c>IMDS_ENDPOINT</c>).
        /// <c>Auth:UseManagedIdentity=false</c> (Development): identidad propia del desarrollador
        /// (Visual Studio o <c>az login</c>), nunca usada en servidor.
        /// </summary>
        public static TokenCredential Create(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var useManagedIdentity = configuration.GetValue<bool>("Auth:UseManagedIdentity");
            if (useManagedIdentity)
            {
                var managedIdentityClientId = configuration.GetValue<string?>("Auth:ManagedIdentityClientId");
                var managedIdentityId = string.IsNullOrWhiteSpace(managedIdentityClientId)
                    ? ManagedIdentityId.SystemAssigned
                    : ManagedIdentityId.FromUserAssignedClientId(managedIdentityClientId);
                return new ManagedIdentityCredential(managedIdentityId);
            }

            return new ChainedTokenCredential(
                new VisualStudioCredential(),
                new AzureCliCredential());
        }
    }
}
