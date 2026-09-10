using AIEP.Application.Api.Blueprint.Options;
using Microsoft.Extensions.Options;

namespace AIEP.Application.Api.Blueprint.Utilities
{
    /// <summary>
    /// Valida la API key entrante contra el valor vigente (entregado por App Configuration /
    /// Key Vault mediante <see cref="IOptionsMonitor{TOptions}"/>, refrescable en caliente).
    /// A diferencia del original: no consulta Key Vault por request, no usa
    /// <c>ClientSecretCredential</c> y no lleva keys embebidas en el codigo.
    /// </summary>
    public class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IOptionsMonitor<ApiKeyOptions> _options;
        private readonly ILogger<ApiKeyMiddleware> _logger;

        public ApiKeyMiddleware(RequestDelegate next, IOptionsMonitor<ApiKeyOptions> options, ILogger<ApiKeyMiddleware> logger)
        {
            _next = next;
            _options = options;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (HttpMethods.IsOptions(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return;
            }

            try
            {
                var options = _options.CurrentValue;

                if (!context.Request.Headers.TryGetValue(options.ApiKeyHeaderName, out var extractedApiKey))
                {
                    await WriteUnauthorizedAsync(context, "La Api Key no fue provista (usando ApiKeyMiddleware)");
                    return;
                }

                var allowed = new List<string>();
                if (!string.IsNullOrWhiteSpace(options.ApiKey))
                {
                    allowed.Add(options.ApiKey);
                }

                if (options.AdditionalKeys is { Length: > 0 })
                {
                    allowed.AddRange(options.AdditionalKeys.Where(k => !string.IsNullOrWhiteSpace(k)));
                }

                if (allowed.Count == 0)
                {
                    _logger.LogError("No hay ninguna API key configurada (Conf:ApiKey). Revise App Configuration / Key Vault.");
                    await WriteUnauthorizedAsync(context, "Aplicación no autorizada (usando ApiKeyMiddleware)");
                    return;
                }

                var received = extractedApiKey.ToString();
                var isValid = allowed.Any(k => string.Equals(k, received, StringComparison.OrdinalIgnoreCase));

                if (!isValid)
                {
                    await WriteUnauthorizedAsync(context, "Aplicación no autorizada (usando ApiKeyMiddleware)");
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado validando la API key en ApiKeyMiddleware.");
                await WriteUnauthorizedAsync(context, "Aplicación no autorizada");
                return;
            }

            await _next(context);
        }

        private static async Task WriteUnauthorizedAsync(HttpContext context, string message)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(message);
        }
    }
}
