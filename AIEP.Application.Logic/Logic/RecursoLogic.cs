using AIEP.Application.Model.Models;
using AIEP.Application.Repository.Repository.Recurso;
using AutoMapper;
using Microsoft.Extensions.Logging;
using DtoRecurso = AIEP.Application.Model.Models.Recurso.Recurso;

namespace AIEP.Application.Logic.Logic
{
    /// <summary>
    /// Logica de negocio del recurso. Clase concreta (sin interfaz), registrada como scoped,
    /// siguiendo el patron del servicio de Sedes.
    /// </summary>
    public class RecursoLogic
    {
        private readonly IMapper _mapper;
        private readonly ILogger<RecursoLogic> _logger;
        private readonly IRecursoRepository _recursoRepository;

        public RecursoLogic(IRecursoRepository recursoRepository, IMapper mapper, ILogger<RecursoLogic> logger)
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

            _logger.LogInformation("Consultando recursos para el codigo {Codigo}.", codigo);

            var data = await _recursoRepository.GetRecursosAsync(codigo, cancellationToken);
            return _mapper.Map<List<DtoRecurso>>(data);
        }
    }
}
