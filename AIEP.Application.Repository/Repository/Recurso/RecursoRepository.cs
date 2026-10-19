using System.Data;
using AIEP.Application.Model.Models;
using AIEP.Application.Repository.Infrastructure;
using AIEP.Domain.DataBlueprint.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIEP.Application.Repository.Repository.Recurso
{
    /// <summary>
    /// Repositorio de ejemplo (feature vertical del blueprint). Un unico metodo publico que
    /// invoca un stored procedure con DOS result sets en el siguiente
    /// <b>orden canonico</b> (respetenlo en Oracle y en SQL Server por igual):
    /// <list type="number">
    ///   <item>Recursos (columnas de <see cref="RecursoEntity"/>).</item>
    ///   <item>Detalle de recursos (columnas de <see cref="RecursoDetalleEntity"/>).</item>
    /// </list>
    /// Los parametros escalares de salida <c>P_CODIGO</c> / <c>P_MENSAJE</c> se leen despues de
    /// cerrar el reader (requisito de ADO.NET para OUT params con Oracle y SqlClient).
    /// </summary>
    public sealed class RecursoRepository : BaseRepository, IRecursoRepository
    {
        private const string DefaultStoredProcedureName = "PKG_BLUEPRINT.P_GET_RECURSO";

        private readonly string _storedProcedureName;

        public RecursoRepository(
            IDbConnectionFactory connectionFactory,
            IConfiguration configuration,
            ILogger<RecursoRepository> logger)
            : base(connectionFactory, logger)
        {
            _storedProcedureName = configuration.GetValue<string>("Conf:StoredProcedureName")
                ?? DefaultStoredProcedureName;
        }

        public async Task<List<RecursoEntity>> GetRecursosAsync(string codigo, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(codigo))
            {
                throw new RequiredParameterException(nameof(codigo));
            }

            List<RecursoEntity> recursos;
            List<RecursoDetalleEntity> detalle;

            await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var command = CreateStoredProcCommand(connection, _storedProcedureName);

            AddParameter(command, "P_CODE", codigo, DbType.String, size: 20);

            // Cursores de salida ANTES que los escalares OUT (Oracle recorre los RefCursor en
            // el orden en que se agregaron). En SQL Server estas llamadas son no-op.
            AddRefCursor(command, "C_RECURSOS");
            AddRefCursor(command, "C_DETALLE");

            AddParameter(command, "P_CODIGO", null, DbType.String, ParameterDirection.Output, size: 4000);
            AddParameter(command, "P_MENSAJE", null, DbType.String, ParameterDirection.Output, size: 4000);

            await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                recursos = await MapAsync<RecursoEntity>(reader, cancellationToken).ConfigureAwait(false);
                await reader.NextResultAsync(cancellationToken).ConfigureAwait(false);
                detalle = await MapAsync<RecursoDetalleEntity>(reader, cancellationToken).ConfigureAwait(false);
            } // el reader se cierra AQUI, antes de leer los parametros de salida

            ThrowIfSpError(GetOutputString(command, "P_CODIGO"), GetOutputString(command, "P_MENSAJE"));

            // Ensamblado 1 -> N en memoria (equivalente reducido a EnsamblarJerarquia de la referencia).
            var detallePorRecurso = detalle
                .GroupBy(d => d.CodigoRecurso ?? string.Empty)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var recurso in recursos)
            {
                recurso.Detalles = detallePorRecurso.TryGetValue(recurso.CodigoRecurso ?? string.Empty, out var items)
                    ? items
                    : new List<RecursoDetalleEntity>();
            }

            return recursos;
        }
    }
}
