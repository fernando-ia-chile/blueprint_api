using Azure.Security.KeyVault.Secrets;
using Azure.Extensions.AspNetCore.Configuration.Secrets;

namespace AIEP.Application.Api.Blueprint.Utilities
{
    /// <summary>
    /// Mapea los secretos del Key Vault directo a <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>
    /// (sin Azure App Configuration de por medio). Solo carga los que empiezan con <c>blueprint-</c>
    /// (el Vault es compartido por varios servicios de AIEP); el resto del nombre decide la clave:
    /// <list type="bullet">
    ///   <item><c>blueprint-&lt;nombre&gt;-connectionstring</c> -> <c>ConnectionStrings:&lt;nombre&gt;</c>
    ///   (ej. <c>blueprint-oracledb-connectionstring</c> -> <c>ConnectionStrings:OracleDb</c> --
    ///   la busqueda de <see cref="Microsoft.Extensions.Configuration"/> no distingue mayusculas).</item>
    ///   <item><c>blueprint-api-key</c> -> <c>Conf:ApiKey</c>.</item>
    ///   <item>Cualquier otro <c>blueprint-*</c>: igual que el comportamiento por defecto de la
    ///   libreria (<c>--</c> en el nombre se vuelve <c>:</c>).</item>
    /// </list>
    /// Agregar una conexion nueva (ej. "CostosDb") solo requiere el secreto
    /// <c>blueprint-costosdb-connectionstring</c> en el Vault -- no toca esta clase.
    /// </summary>
    public sealed class BlueprintKeyVaultSecretManager : KeyVaultSecretManager
    {
        private const string Prefix = "blueprint-";
        private const string ApiKeySuffix = "api-key";
        private const string ConnectionStringSuffix = "-connectionstring";

        public override bool Load(SecretProperties secret) =>
            secret.Name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

        public override string GetKey(KeyVaultSecret secret)
        {
            var name = secret.Name[Prefix.Length..];

            if (string.Equals(name, ApiKeySuffix, StringComparison.OrdinalIgnoreCase))
            {
                return "Conf:ApiKey";
            }

            if (name.EndsWith(ConnectionStringSuffix, StringComparison.OrdinalIgnoreCase))
            {
                var connectionName = name[..^ConnectionStringSuffix.Length];
                return $"ConnectionStrings:{connectionName}";
            }

            return base.GetKey(secret);
        }
    }
}
