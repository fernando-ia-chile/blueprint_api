using System.Security.Claims;
using AIEP.Application.Api.Blueprint.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace AIEP.Application.Api.Blueprint.Utilities
{
    /// <summary>
    /// Exige que el token presente un claim <c>tid</c> perteneciente a un tenant autorizado
    /// (<c>Conf:ListTenantId</c>). Uso: <c>[RequireClaim("ClaimTenantId")]</c>.
    /// </summary>
    public class RequireClaimAttribute : TypeFilterAttribute
    {
        public RequireClaimAttribute(string claimType) : base(typeof(RequireClaimFilter))
        {
            Arguments = new object[] { new Claim(claimType, "true") };
        }
    }

    public class RequireClaimFilter : IAuthorizationFilter
    {
        private readonly Claim _claim;
        private readonly IOptionsMonitor<TenantOptions> _tenantOptions;

        public RequireClaimFilter(Claim claim, IOptionsMonitor<TenantOptions> tenantOptions)
        {
            _claim = claim;
            _tenantOptions = tenantOptions;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var tenantIdClaim = context.HttpContext.User.FindFirst(c => c.Type == "tid");

            // Null-guard: el original lanzaba NullReferenceException si el token no traia 'tid'.
            if (tenantIdClaim is null || string.IsNullOrWhiteSpace(tenantIdClaim.Value))
            {
                context.Result = new ForbidResult();
                return;
            }

            var listTenantId = _tenantOptions.CurrentValue.ListTenantId ?? new List<string>();

            if (!listTenantId.Contains(tenantIdClaim.Value.ToLowerInvariant()))
            {
                context.Result = new ForbidResult();
            }
        }
    }
}
