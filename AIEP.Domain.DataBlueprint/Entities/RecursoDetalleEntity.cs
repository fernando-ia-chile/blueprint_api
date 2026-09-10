using System.ComponentModel.DataAnnotations.Schema;

namespace AIEP.Domain.DataBlueprint.Entities
{
    public class RecursoDetalleEntity
    {
        [Column("CODIGO_RECURSO")]
        public string? CodigoRecurso { get; set; }

        [Column("CODIGO_DETALLE")]
        public string? CodigoDetalle { get; set; }

        [Column("DESCRIPCION")]
        public string? Descripcion { get; set; }
    }
}
