using System.Data.Common;
using System.Net;
using AIEP.Application.Model.Models;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AIEP.Application.Api.Blueprint.Utilities
{
    /// <summary>
    /// Filtro de excepciones que traduce la jerarquia de <see cref="AIEP.Application.Model.Models"/>
    /// a codigos HTTP y a un cuerpo <see cref="ExceptionResponse"/>. Se aplica por accion con
    /// <c>[TypeFilter(typeof(ExceptionHandler))]</c>.
    /// </summary>
    [ApiExplorerSettings(IgnoreApi = true)]
    public class ExceptionHandler : ExceptionFilterAttribute
    {
        public override void OnException(ExceptionContext context)
        {
            var logger = context.HttpContext.RequestServices.GetService<ILogger<ExceptionHandler>>();
            var url = context.HttpContext.Request.GetDisplayUrl();

            ExceptionResponse response;
            HttpStatusCode statusCode;

            switch (context.Exception)
            {
                case BusinessException ex:
                    response = new ExceptionResponse(ex.Message) { message = ex.Message };
                    statusCode = HttpStatusCode.UnprocessableEntity; // 422
                    break;

                case BusinessValidationException ex:
                    response = new ExceptionResponse(ex.Message) { message = ex.Message };
                    statusCode = HttpStatusCode.UnprocessableEntity; // 422
                    break;

                case RequiredParameterException ex:
                    response = new ExceptionResponse(ex.Message) { message = ex.Message, detail = url };
                    statusCode = HttpStatusCode.BadRequest; // 400
                    break;

                case DataValidationException ex:
                    response = new ExceptionResponse(ex.Message) { message = ex.Message, detail = url };
                    statusCode = HttpStatusCode.BadRequest; // 400
                    break;

                case InvalidParameterException ex:
                    response = new ExceptionResponse(ex.Message) { message = ex.Message, detail = url };
                    statusCode = HttpStatusCode.BadRequest; // 400
                    break;

                case NotFoundException<Guid> ex:
                    response = new ExceptionResponse(ex.Message) { detail = url };
                    statusCode = HttpStatusCode.NotFound; // 404
                    break;

                case NotFoundException<string> ex:
                    response = new ExceptionResponse(ex.Message) { detail = url };
                    statusCode = HttpStatusCode.NotFound; // 404
                    break;

                case AuthException ex:
                    response = new ExceptionResponse(ex.Message)
                    {
                        detail = "No cuenta con los permisos requeridos para realizar la acción"
                    };
                    statusCode = HttpStatusCode.Unauthorized; // 401
                    break;

                // Base comun de OracleException y SqlException: evita acoplar esta capa a un driver concreto.
                case DbException ex:
                    response = new ExceptionResponse(ex.Message)
                    {
                        detail = "Error de conectividad con la base de datos."
                    };
                    statusCode = HttpStatusCode.InternalServerError; // 500
                    break;

                default:
                    response = new ExceptionResponse(context.Exception.Message)
                    {
                        detail = "Para más información contacte al administrador"
                    };
                    statusCode = HttpStatusCode.InternalServerError; // 500
                    break;
            }

            // Log ANTES de marcar la excepcion como manejada (el original perdia la excepcion al loguear).
            logger?.LogError(
                context.Exception,
                "Se ha producido una excepción en {Url}. Error: {Message}. Detail: {Detail}",
                url,
                response.message,
                response.detail);

            context.HttpContext.Response.StatusCode = (int)statusCode;
            context.Result = new JsonResult(response);
            context.ExceptionHandled = true;

            base.OnException(context);
        }
    }
}
