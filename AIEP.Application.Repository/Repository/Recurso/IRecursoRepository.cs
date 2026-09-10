using AIEP.Domain.DataBlueprint.Entities;

namespace AIEP.Application.Repository.Repository.Recurso
{
    public interface IRecursoRepository
    {
        /// <summary>
        /// Ejecuta el stored procedure de recursos y devuelve la jerarquia recurso -> detalles.
        /// Funciona igual con Oracle (REF CURSOR) y con SQL Server (multiples SELECT).
        /// </summary>
        Task<List<RecursoEntity>> GetRecursosAsync(string codigo, CancellationToken cancellationToken = default);
    }
}
