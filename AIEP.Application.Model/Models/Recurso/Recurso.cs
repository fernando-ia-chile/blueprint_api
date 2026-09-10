using System.Text.Json.Serialization;

namespace AIEP.Application.Model.Models.Recurso
{
    /// <summary>
    /// DTO de salida del recurso. Los nombres JSON conservan el estilo institucional
    /// (etiquetas legibles con espacios), igual que el servicio de Sedes.
    /// </summary>
    public class Recurso
    {
        [JsonPropertyName("Codigo Recurso")]
        public string? CodigoRecurso { get; set; }

        [JsonPropertyName("Nombre Recurso")]
        public string? NombreRecurso { get; set; }

        [JsonPropertyName("Activo")]
        public bool Activo { get; set; }

        [JsonPropertyName("Cupos")]
        public int? Cupos { get; set; }

        [JsonPropertyName("Fecha Alta")]
        public DateTime? FechaAlta { get; set; }

        [JsonPropertyName("Valor")]
        public decimal? Valor { get; set; }

        [JsonPropertyName("Detalles")]
        public List<RecursoDetalle> Detalles { get; set; } = new();
    }

    public class RecursoDetalle
    {
        [JsonPropertyName("Codigo Recurso")]
        public string? CodigoRecurso { get; set; }

        [JsonPropertyName("Codigo Detalle")]
        public string? CodigoDetalle { get; set; }

        [JsonPropertyName("Descripcion")]
        public string? Descripcion { get; set; }
    }
}
