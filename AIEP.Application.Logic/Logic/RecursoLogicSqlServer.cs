using AIEP.Application.Model.Models;
using AIEP.Application.Repository.Repository.Recurso;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DtoRecurso = AIEP.Application.Model.Models.Recurso.Recurso;

namespace AIEP.Application.Logic.Logic
{
    /// <summary>
    /// Ejemplo de referencia: misma logica que <see cref="RecursoLogic"/>, pero resuelta
    /// explicitamente contra la conexion "SqlServerDb" via <c>[FromKeyedServices]</c> -- pensado
    /// para un proyecto que necesite dos controllers independientes, cada uno contra su propia BD
    /// (ver README "Más de una conexión de BD"). Si el proyecto solo usa un motor, esta clase (y
    /// su par <see cref="RecursoLogicOracle"/>) se puede borrar sin afectar a
    /// <see cref="RecursoLogic"/>/<c>RecursoController</c>.
    /// </summary>
    public class RecursoLogicSqlServer
    {
        private readonly IMapper _mapper;
        private readonly ILogger<RecursoLogicSqlServer> _logger;
        private readonly IRecursoRepository _recursoRepository;

        public RecursoLogicSqlServer(
            [FromKeyedServices("SqlServerDb")] IRecursoRepository recursoRepository,
            IMapper mapper,
            ILogger<RecursoLogicSqlServer> logger)
        {
            _recursoRepository = recursoRepository;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<List<DtoRecurso>> GetRecursos(string? codigo, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(codigo))
            {
                throw new RequiredParameterException(nameof(codigo));
            }

            _logger.LogInformation("Consultando recursos (SqlServerDb) para el codigo {Codigo}.", codigo);

            var data = await _recursoRepository.GetRecursosAsync(codigo, cancellationToken);
            return _mapper.Map<List<DtoRecurso>>(data);
        }
    }
}
