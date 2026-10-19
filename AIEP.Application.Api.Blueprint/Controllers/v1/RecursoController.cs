using AIEP.Application.Api.Blueprint.Utilities;
using AIEP.Application.Logic.Logic;
using AIEP.Application.Model.Models.Recurso;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;

namespace AIEP.Application.Api.Blueprint.Controllers.v1
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [Authorize]
    public class RecursoController : ControllerBase
    {
        private readonly RecursoLogic _logic;
        private const string _recursoReadScope = "IntegrationService.Blueprint.Read";

        public RecursoController(RecursoLogic logic)
        {
            _logic = logic;
        }

        /// <summary>Obtiene los recursos (y su detalle) para el codigo indicado.</summary>
        [HttpGet]
        [RequiredScope(AcceptedScope = new string[] { _recursoReadScope })]
        [TypeFilter(typeof(ExceptionHandler))]
        [RequireClaim("ClaimTenantId")]
        public async Task<IActionResult> Get([FromQuery] RecursoRequest? request, CancellationToken cancellationToken)
        {
            var output = await _logic.GetRecursos(request?.codigo, cancellationToken);
            return Ok(output);
        }
    }
}
