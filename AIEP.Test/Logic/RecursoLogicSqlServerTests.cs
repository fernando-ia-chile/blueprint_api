using AIEP.Application.Api.Blueprint.Mapper;
using AIEP.Application.Logic.Logic;
using AIEP.Application.Model.Models;
using AIEP.Application.Model.Models.Recurso;
using AIEP.Application.Repository.Repository.Recurso;
using AIEP.Domain.DataBlueprint.Entities;
using AIEP.Test.TestData;
using Moq;

namespace AIEP.Test.Logic
{
    [TestFixture]
    public class RecursoLogicSqlServerTests : LogicTestBase<RecursoLogicSqlServer, MapperConfig, IRecursoRepository>
    {
        [TestCaseSource(typeof(RecursoRequestData))]
        public async Task GetRecursos_DevuelveElMismoNumeroDeElementosQueElRepositorio(RecursoRequest request)
        {
            var entidades = new List<RecursoEntity>
            {
                new() { CodigoRecurso = request.codigo, NombreRecurso = "Recurso A", Activo = true },
                new() { CodigoRecurso = request.codigo, NombreRecurso = "Recurso B", Activo = false },
            };

            RepositoryMock
                .Setup(r => r.GetRecursosAsync(request.codigo!, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entidades);

            var respuesta = await Logic.GetRecursos(request.codigo);

            Assert.IsNotNull(respuesta);
            Assert.AreEqual(entidades.Count, respuesta.Count);
            Assert.AreEqual("Recurso A", respuesta[0].NombreRecurso);
        }

        [Test]
        public void GetRecursos_ConCodigoVacio_LanzaRequiredParameterException()
        {
            Assert.ThrowsAsync<RequiredParameterException>(() => Logic.GetRecursos(null));
            Assert.ThrowsAsync<RequiredParameterException>(() => Logic.GetRecursos("  "));
        }
    }
}
