using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace AIEP.Test
{
    /// <summary>
    /// Base para pruebas de clases de logica. Arma una <see cref="IConfiguration"/> desde
    /// <c>Settings/appsettings.json</c>, un <see cref="IMapper"/> real con el perfil indicado,
    /// y construye la logica inyectando un mock del repositorio y del logger.
    /// </summary>
    /// <typeparam name="TLogic">Clase de logica bajo prueba.</typeparam>
    /// <typeparam name="TProfile">Perfil de AutoMapper a aplicar.</typeparam>
    /// <typeparam name="TRepo">Contrato de repositorio a mockear.</typeparam>
    public abstract class LogicTestBase<TLogic, TProfile, TRepo>
        where TProfile : Profile, new()
        where TRepo : class
    {
        protected readonly IConfiguration Configuration;
        protected readonly IMapper Mapper;
        protected readonly Mock<TRepo> RepositoryMock = new();
        protected readonly Mock<ILogger<TLogic>> LoggerMock = new();
        protected readonly TLogic Logic;

        protected LogicTestBase()
        {
            Configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("Settings/appsettings.json", optional: false)
                .Build();

            Mapper = new MapperConfiguration(cfg => cfg.AddProfile(new TProfile())).CreateMapper();

            Logic = (TLogic)Activator.CreateInstance(
                typeof(TLogic),
                RepositoryMock.Object,
                Mapper,
                LoggerMock.Object)!;
        }
    }
}
