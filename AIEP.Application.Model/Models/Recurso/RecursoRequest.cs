using System.ComponentModel.DataAnnotations;

namespace AIEP.Application.Model.Models.Recurso
{
    public class RecursoRequest
    {
        [StringLength(20, MinimumLength = 1)]
        public string? codigo { get; set; }
    }
}
