using AIEP.Domain.DataBlueprint.Entities;
using AutoMapper;
using DtoRecurso = AIEP.Application.Model.Models.Recurso.Recurso;
using DtoRecursoDetalle = AIEP.Application.Model.Models.Recurso.RecursoDetalle;

namespace AIEP.Application.Api.Blueprint.Mapper
{
    /// <summary>
    /// Perfil de AutoMapper del servicio. Vive en el proyecto API (igual que en Sedes) y se
    /// descubre por el escaneo de ensamblados en <c>Program.cs</c>.
    /// </summary>
    public class MapperConfig : Profile
    {
        public MapperConfig()
        {
            CreateMap<DtoRecursoDetalle, RecursoDetalleEntity>().ReverseMap();
            CreateMap<DtoRecurso, RecursoEntity>().ReverseMap();
        }
    }
}
