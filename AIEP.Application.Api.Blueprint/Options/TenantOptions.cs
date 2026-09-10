namespace AIEP.Application.Api.Blueprint.Options
{
    /// <summary>
    /// Tenants de Azure AD autorizados para consumir el servicio. Se enlaza de la seccion <c>Conf</c>
    /// (<c>Conf:ListTenantId</c>). Lo consume <c>RequireClaimFilter</c> validando el claim <c>tid</c>.
    /// </summary>
    public class TenantOptions
    {
        public const string SectionName = "Conf";

        public List<string> ListTenantId { get; set; } = new();
    }
}
