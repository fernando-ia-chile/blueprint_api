using System.ComponentModel.DataAnnotations.Schema;

namespace AIEP.Domain.DataBlueprint.Entities
{
    /// <summary>
    /// Entidad de datos del recurso. Solo las propiedades con <see cref="ColumnAttribute"/>
    /// participan en el mapeo automatico del <c>DbReaderMapper</c>; el nombre del atributo
    /// debe coincidir (sin distinguir mayusculas) con la columna devuelta por el stored procedure.
    /// </summary>
    public class RecursoEntity
    {
        [Column("CODIGO_RECURSO")]
        public string? CodigoRecurso { get; set; }

        [Column("NOMBRE_RECURSO")]
        public string? NombreRecurso { get; set; }

        /// <summary>Se mapea desde 'Y'/'N' o desde 1/0 numerico.</summary>
        [Column("ACTIVO")]
        public bool Activo { get; set; }

        [Column("CUPOS")]
        public int? Cupos { get; set; }

        [Column("FECHA_ALTA")]
        public DateTime? FechaAlta { get; set; }

        [Column("VALOR")]
        public decimal? Valor { get; set; }

        /// <summary>Propiedad de navegacion: SIN <see cref="ColumnAttribute"/>, el mapeo la ignora.</summary>
        public List<RecursoDetalleEntity> Detalles { get; set; } = new();
    }
}
