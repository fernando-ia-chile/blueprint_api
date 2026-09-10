# Manual del Blueprint AIEP

> Guía práctica para construir un microservicio real sobre el blueprint: arquitectura,
> configuración sin secretos, y el camino completo de una feature (entidad, modelo,
> repositorio Oracle/SQL Server, mapper y endpoint) con un tutorial de punta a punta.
>
> **Solución:** `ApiBlueprint.sln` · **TargetFramework:** `net8.0` · v1 · 2026-09-09
> Versión navegable del mismo contenido: artefacto "Manual del Blueprint AIEP".

## Para quién es esto

Para el desarrollador que va a clonar el blueprint y publicar un microservicio nuevo.
Asume C#/.NET y SQL; no asume conocimiento previo de este repo.

- Secciones **1–3**: lectura obligada una sola vez.
- Secciones **4–10**: la referencia que abrirás cada vez que agregues un endpoint.
- Sección **14**: el tutorial. Si tienes prisa, cópialo y adapta.

En todo el manual, `Blueprint` / `Recurso` son los nombres de ejemplo del repo base. Al clonar
renombras `Blueprint → MiServicio` (ver `README.md`) y reemplazas la feature `Recurso` por la
tuya. El tutorial usa `Alumno` (ERP Banner) como caso real.

---

## Índice

**Fundamentos**
1. [Arquitectura en 5 minutos](#1-arquitectura-en-5-minutos)
2. [Configuración (appsettings)](#2-configuración-appsettings)
3. [Motor: Oracle vs SQL Server](#3-motor-oracle-vs-sql-server)

**Construir una feature**
4. [Entidades](#4-entidades)
5. [Modelos (DTOs)](#5-modelos-dtos)
6. [El mapper](#6-el-mapper)
7. [Repositorio](#7-repositorio)
8. [Lógica de negocio](#8-lógica-de-negocio)
9. [Endpoints](#9-endpoints)
10. [Manejo de errores](#10-manejo-de-errores)

**Operación**
11. [Seguridad](#11-seguridad)
12. [Logging](#12-logging)
13. [Pruebas](#13-pruebas)

**Práctica**
14. [Tutorial: feature "Alumno" (Banner) de punta a punta](#14-tutorial-feature-alumno-erp-banner-de-punta-a-punta)
15. [Aristas frecuentes / FAQ](#15-aristas-frecuentes--faq)
16. [Referencia rápida](#16-referencia-rápida)

---

## 1. Arquitectura en 5 minutos

Seis proyectos, una dependencia en línea recta. Cada capa tiene una única responsabilidad y
sólo conoce a la de abajo.

| Proyecto                      | Responsabilidad                                                                                        | Depende de        |
| ----------------------------- | ------------------------------------------------------------------------------------------------------ | ----------------- |
| `AIEP.Application.Api.*`      | Host web: `Program.cs`, controllers v1, middlewares, filtros, perfil de AutoMapper, Swagger.           | Logic             |
| `AIEP.Application.Logic`      | Reglas de negocio. Clases `*Logic` concretas (sin interfaz). Validan, orquestan, mapean entidad → DTO. | Model, Repository |
| `AIEP.Application.Repository` | Acceso a datos sin ORM: `IDbConnectionFactory`, `BaseRepository`, `DbReaderMapper`, repos concretos.   | Domain, Model     |
| `AIEP.Application.Model`      | DTOs de entrada/salida + jerarquía de excepciones + `ExceptionResponse`.                               | -                 |
| `AIEP.Domain.Data*`           | Entidades de datos: POCOs `*Entity` con `[Column]`. Sin lógica.                                        | -                 |
| `AIEP.Test`                   | Pruebas NUnit + Moq.                                                                                   | todos             |

### El recorrido de un `GET`

```
HTTP GET /api/v1/recurso?codigo=ABC
      │
      ▼
[middleware]  ApiKeyMiddleware        valida header x-xmsbs-apikey
[filtro]      RequiredScope           valida scope del token (Azure AD)
[filtro]      RequireClaim            valida claim "tid" ∈ Conf:ListTenantId
[filtro]      ExceptionHandler        envuelve la acción; traduce excepciones → HTTP
      │
      ▼
RecursoController.Get(request, ct)
      │  request?.codigo
      ▼
RecursoLogic.GetRecursos(codigo, ct)      ← valida parámetro, loguea
      │
      ▼
IRecursoRepository.GetRecursosAsync(codigo, ct)
      │  OpenConnectionAsync → CreateStoredProcCommand → AddParameter / AddRefCursor
      │  ExecuteReaderAsync → MapAsync<RecursoEntity> → NextResultAsync → MapAsync<...Detalle>
      │  ThrowIfSpError(P_CODIGO, P_MENSAJE)
      ▼
List<RecursoEntity>   ── AutoMapper ──▶  List<Recurso> (DTO)
      │
      ▼
200 OK  [ { "Codigo Recurso": "ABC", ... } ]
```

### Ciclos de vida en DI

| Servicio | Lifetime | Por qué |
|---|---|---|
| `IDbConnectionFactory` | Singleton | Stateless. Resuelve el proveedor y la cadena una vez; cada conexión se crea por llamada. |
| `I*Repository` | Scoped | Vive lo que dura la request; no comparte estado entre requests. |
| `*Logic` | Scoped | Igual que el repo que consume. |
| `IMapper` | Singleton | Lo registra AutoMapper; la configuración es inmutable. |

El registro vive en `AIEP.Application.Repository/ServiceCollectionExtensions.cs`
(`AddBlueprintRepository`) y en `Program.cs`.

---

## 2. Configuración (appsettings)

El principio rector: **los secretos no viajan en archivos**. `appsettings.json` sólo lleva
valores no sensibles. La cadena de conexión y la API key las entrega Azure App Configuration
como referencias a Key Vault, autenticado con `DefaultAzureCredential`.

### Claves y su origen

| Clave | Ejemplo / valor | Origen |
|---|---|---|
| `AzureAd:TenantId` / `ClientId` | `<guid>` | appsettings (no sensible; **sin** `ClientSecret`) |
| `AzureAppConfig:Endpoint` | `https://appcs-aiep-desa.azconfig.io` | appsettings; vacío en local |
| `Database:Provider` | `Oracle` \| `SqlServer` | appsettings / App Config / env |
| `Database:CommandTimeout` | `300` (segundos) | appsettings / App Config |
| `Database:ConnectionStringName` | `BlueprintDb` | appsettings |
| `ConnectionStrings:BlueprintDb` | cadena Oracle o SQL Server | **Key Vault** vía App Config · local: user-secrets |
| `Conf:ApiKey` | GUID / secreto | **Key Vault** vía App Config · local: user-secrets |
| `Conf:ApiKeyHeaderName` | `x-xmsbs-apikey` | appsettings |
| `Conf:StoredProcedureName` | `PKG_BLUEPRINT.P_GET_RECURSO` | appsettings / App Config (por entorno) |
| `Conf:ListTenantId` | `[ "<guid>" ]` | appsettings / App Config |
| `Cors:AllowedOrigins` | `[ "https://servicioalumno.aiep.cl" ]` | appsettings |

### Precedencia (gana el último)

```
appsettings.json
  → appsettings.{Environment}.json
    → user-secrets            (solo Development)
      → variables de entorno  (ConnectionStrings__BlueprintDb, Database__Provider, Conf__ApiKey)
        → Azure App Configuration + Key Vault   (entornos desplegados: gana)
```

### Desarrollo local (sin Azure)

En `Development`, `AzureAppConfig:Endpoint` está vacío y App Config se omite. Define lo
sensible con user-secrets:

```bash
cd AIEP.Application.Api.Blueprint
dotnet user-secrets set "ConnectionStrings:BlueprintDb" "Data Source=...;User Id=...;Password=...;"
dotnet user-secrets set "Conf:ApiKey" "CD1462A2-F8D0-49D3-91A2-123756434907"
```

Elige el motor en `appsettings.Development.json` (`"Database": { "Provider": "SqlServer" }`) o
con `Database__Provider=Oracle`.

### Azure App Configuration: esquema de claves

Prefijo por servicio `Blueprint:` (un store compartido por varios microservicios) y
**label = entorno**. El host hace dos `Select`: primero sin label (valores compartidos), luego
el del entorno (override).

| Clave en App Config | Tipo |
|---|---|
| `Blueprint:Database:Provider` | valor |
| `Blueprint:Conf:StoredProcedureName` | valor |
| `Blueprint:ConnectionStrings:BlueprintDb` | Key Vault reference → `blueprint-db-connectionstring` |
| `Blueprint:Conf:ApiKey` | Key Vault reference → `blueprint-api-key` |
| `Blueprint:Sentinel` | valor (cambiarlo dispara el refresco cada 5 min) |

> **Identidad.** La identidad que ejecuta el servicio necesita los roles
> `App Configuration Data Reader` y `Key Vault Secrets User`. Funciona con Managed Identity
> (Azure) y con la identidad de **Azure Arc** (on-prem): `DefaultAzureCredential` la detecta
> sola. En local usa `az login` o Visual Studio. Detalle completo en `README.md`.

### Opciones tipadas

No leas `IConfiguration` crudo en tu código. Usa las clases de opciones ya validadas al arranque:

| Clase | Sección | Se inyecta como |
|---|---|---|
| `DatabaseOptions` | `Database` | `IOptions<DatabaseOptions>` |
| `ApiKeyOptions` | `Conf` | `IOptionsMonitor<ApiKeyOptions>` (refrescable) |
| `TenantOptions` | `Conf` | `IOptionsMonitor<TenantOptions>` |

---

## 3. Motor: Oracle vs SQL Server

El motor se elige **en tiempo de ejecución** con `Database:Provider`. Tu repositorio se escribe
**una sola vez** contra `System.Data.Common` (`DbConnection`, `DbCommand`, `DbParameter`); la
fábrica resuelve el driver concreto.

```
IDbConnectionFactory
 ├─ Provider            Oracle | SqlServer   (de DatabaseOptions)
 ├─ CommandTimeout      segundos
 ├─ CreateConnection()          → OracleConnection | SqlConnection
 ├─ PrepareStoredProcCommand()  → Oracle: BindByName = true ; SQL: no-op
 └─ AddRefCursor(cmd, name)     → Oracle: OracleParameter RefCursor OUT ; SQL: null
```

Los únicos archivos que hacen `using Oracle.ManagedDataAccess.Client` /
`Microsoft.Data.SqlClient` son `OracleProviderAdapter.cs` y `SqlServerProviderAdapter.cs`. No
los toques desde tu repo.

### Qué cambia entre motores

| Aspecto               | Oracle                                                       | SQL Server                                                                     |
| --------------------- | ------------------------------------------------------------ | ------------------------------------------------------------------------------ |
| Múltiples result sets | Cada uno es un `OUT SYS_REFCURSOR`                           | `SELECT …; SELECT …;` consecutivos                                             |
| En el repo            | `AddRefCursor(cmd, "C_ALUMNOS")` por cada cursor             | Esas llamadas son no-op (devuelven `null`)                                     |
| Binding de parámetros | Por nombre - requiere `BindByName=true` (lo hace la fábrica) | Por nombre nativo                                                              |
| Parámetros de entrada | `P_TP_DOC` (sin `@`)                                         | `@P_TP_DOC` - `AddParameter` acepta ambos estilos; define uno y sé consistente |
| Tipo de `NUMBER`      | llega como `decimal`                                         | `int`/`decimal`/`bit` según columna                                            |

> **Tres reglas de oro**
>
> 1. **Orden de parámetros:** agrega *todos* los `AddRefCursor` **antes** que los escalares de
>    salida (`P_CODIGO`, `P_MENSAJE`). En Oracle, `NextResultAsync()` recorre los cursores en
>    el orden en que se agregaron.
> 2. **OUT params después del reader:** los valores de `P_CODIGO`/`P_MENSAJE` sólo están
>    disponibles cuando el `DbDataReader` se cerró. Léelos *fuera* del bloque
>    `await using (var reader = …)`.
> 3. **Orden canónico de result sets:** define un orden (p. ej. Alumno → Programas →
>    Semestres) y respétalo idéntico en el SP de Oracle y en el de SQL Server. Documéntalo en
>    el XML-doc del método y en los `.sql` de `db/`.

### Cómo cambiar de motor

```powershell
# local
$env:Database__Provider = "Oracle"     # o "SqlServer"
dotnet run

# entorno desplegado: cambiar Blueprint:Database:Provider en App Configuration
# (con el label del entorno) y tocar Blueprint:Sentinel para refrescar.
```

---

## 4. Entidades

Una entidad es un POCO que representa **una fila de un result set**. Vive en
`AIEP.Domain.Data*/Entities`. Sólo las propiedades con `[Column("NOMBRE")]` se mapean; el
nombre debe coincidir con la columna del SP (sin distinguir mayúsculas).

```csharp
// AIEP.Domain.DataBlueprint/Entities/AlumnoEntity.cs
using System.ComponentModel.DataAnnotations.Schema;

namespace AIEP.Domain.DataBlueprint.Entities;

public class AlumnoEntity
{
    [Column("PIDM")]              public int     Pidm { get; set; }
    [Column("ID_BANNER")]         public string? IdBanner { get; set; }
    [Column("RUT")]               public string? Rut { get; set; }
    [Column("NOMBRES")]           public string? Nombres { get; set; }
    [Column("APELLIDO_PATERNO")]  public string? ApellidoPaterno { get; set; }
    [Column("APELLIDO_MATERNO")]  public string? ApellidoMaterno { get; set; }
    [Column("EMAIL")]             public string? Email { get; set; }
    [Column("FECHA_NACIMIENTO")]  public DateTime? FechaNacimiento { get; set; }

    // 'Y'/'N' o 1/0 en la BD → bool en C#
    [Column("VIGENTE")]           public bool    Vigente { get; set; }

    // Propiedad de navegación: SIN [Column] ⇒ el mapper la ignora.
    // La llenas tú en el repositorio a partir de otro result set.
    public List<ProgramaAlumnoEntity> Programas { get; set; } = new();
}
```

### Nombres de columna con Banner

Banner expone nombres crudos (`SPRIDEN_PIDM`, `SPRIDEN_ID`, `SGBSTDN_STST_CODE`). No los uses
tal cual en la entidad: haz que **el stored procedure** devuelva alias limpios (`PIDM`,
`ID_BANNER`, `ESTADO`) y mapea contra esos alias. La entidad queda legible y desacoplada del
esquema físico.

### Tipos y conversiones que soporta `DbReaderMapper`

| Destino en C# | Qué acepta de la BD |
|---|---|
| `string` | cualquier valor (usa `ToString` invariante si hace falta) |
| `bool` | `"Y"/"N"`, `"1"/"0"`, `"true"/"false"`, `1/0` numérico, `bit` |
| `int/long/short/byte/decimal/double` | numéricos (incluye `NUMBER` de Oracle vía `Convert.ChangeType`) |
| `DateTime`, `DateTimeOffset`, `TimeSpan` | fecha/hora del proveedor o texto parseable |
| `Guid` | `Guid`, `byte[]` (16), o string |
| `enum` | entero o nombre (case-insensitive) |
| `T?` (nullable) | todo lo anterior; `NULL`/`DBNull` ⇒ queda el `default` de la propiedad |

> **Nulos.** Si la columna viene `NULL`, la propiedad **no se asigna** (mantiene su valor por
> defecto). Por eso un `int` no-nullable con valor `NULL` en la BD te dará `0`: declara `int?`
> si "sin dato" es distinto de "cero".

El escaneo de `[Column]` y la compilación de *setters* se hace **una vez por tipo** y se cachea
(`ConcurrentDictionary` + `Expression`). No hay costo de reflexión por fila.

---

## 5. Modelos (DTOs)

El DTO es **el contrato con el cliente**. Vive en `AIEP.Application.Model/Models/<Feature>`.
Nunca expongas la entidad directamente: desacopla el JSON del esquema de BD y evitas filtrar
campos internos (como `PIDM`).

### DTO de salida /`[JsonPropertyName]`

```csharp
// AIEP.Application.Model/Models/Alumno/Alumno.cs
using System.Text.Json.Serialization;

namespace AIEP.Application.Model.Models.Alumno;

public class Alumno
{
    [JsonPropertyName("RUT")]              public string? Rut { get; set; }
    [JsonPropertyName("Nombre Completo")]  public string? NombreCompleto { get; set; }
    [JsonPropertyName("Email")]            public string? Email { get; set; }
    [JsonPropertyName("Vigente")]          public bool    Vigente { get; set; }
    [JsonPropertyName("Programas")]        public List<ProgramaAlumno> Programas { get; set; } = new();
}

public class ProgramaAlumno
{
    [JsonPropertyName("Codigo")]  public string? Codigo { get; set; }
    [JsonPropertyName("Nombre")]  public string? Nombre { get; set; }
}
```

### DTO de entrada / DataAnnotations

El request se recibe por `[FromQuery]` (o `[FromBody]`). `[ApiController]` valida las
anotaciones automáticamente y responde `400` si fallan, antes de entrar a tu acción.

```csharp
// AIEP.Application.Model/Models/Alumno/AlumnoRequest.cs
using System.ComponentModel.DataAnnotations;

namespace AIEP.Application.Model.Models.Alumno;

public class AlumnoRequest
{
    [Required]
    [StringLength(20, MinimumLength = 1)]
    public string? valorDocumento { get; set; }

    [Required]
    [RegularExpression("RUT|PASAPORTE|DNI")]
    public string? tipoDocumento { get; set; }
}
```

> **Regla.** Entidad ≠ DTO aunque se parezcan. La entidad cambia cuando cambia el SP; el DTO
> cambia cuando cambia el contrato público. Mantenerlos separados es lo que te deja evolucionar
> uno sin romper el otro.

---

## 6. El mapper

Se usa AutoMapper. El perfil vive en el **proyecto API**
(`AIEP.Application.Api.*/Mapper/MapperConfig.cs`) y se descubre por el escaneo de ensamblados
en `Program.cs`:

```csharp
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
```

```csharp
// Mapper/MapperConfig.cs
using AIEP.Domain.DataBlueprint.Entities;
using AutoMapper;
using DtoAlumno = AIEP.Application.Model.Models.Alumno.Alumno;
using DtoPrograma = AIEP.Application.Model.Models.Alumno.ProgramaAlumno;

public class MapperConfig : Profile
{
    public MapperConfig()
    {
        // Registra el tipo anidado ANTES del contenedor.
        CreateMap<DtoPrograma, ProgramaAlumnoEntity>().ReverseMap();

        CreateMap<DtoAlumno, AlumnoEntity>()
            // nombres iguales se mapean solos; sólo declara lo que difiere
            .ForMember(d => d.Nombres, o => o.Ignore())
            .ReverseMap()
            .ForMember(d => d.NombreCompleto,
                       o => o.MapFrom(s => $"{s.Nombres} {s.ApellidoPaterno} {s.ApellidoMaterno}".Trim()));
    }
}
```

| Situación | Qué hacer |
|---|---|
| Propiedades con el mismo nombre | Nada. Se mapean por convención. |
| Nombres distintos | `.ForMember(d => d.X, o => o.MapFrom(s => s.Y))` |
| Colección anidada (`List<ProgramaAlumno>`) | Registrar también `CreateMap` del tipo del elemento. |
| Ida y vuelta | `.ReverseMap()`. Los `ForMember` posteriores aplican al mapa invertido. |
| Ignorar una propiedad | `.ForMember(d => d.X, o => o.Ignore())` |

En la lógica el mapeo es una línea:

```csharp
return _mapper.Map<List<Alumno>>(entidades);
```

> **Pitfall.** Si pones el `Profile` en otra capa, `AddAutoMapper(GetAssemblies())` puede no
> escanearlo hasta que ese ensamblado se cargue. **Mantén todos los `Profile` en el proyecto
> API.** Para verificar todos los mapas en un test:
> `new MapperConfiguration(c => c.AddProfile<MapperConfig>()).AssertConfigurationIsValid();`

---

## 7. Repositorio

Hereda de `BaseRepository` e implementa tu interfaz. Un método público por operación. Todo
`async` con `CancellationToken` y `await using`.

### Helpers de `BaseRepository`

| Miembro | Qué hace |
|---|---|
| `OpenConnectionAsync(ct)` | Crea y abre la `DbConnection` del motor activo. |
| `CreateStoredProcCommand(conn, sp)` | `CommandType.StoredProcedure` + `CommandTimeout` + ajustes del proveedor. |
| `AddParameter(cmd, name, value, dbType?, direction?, size?)` | Parámetro escalar. `null` ⇒ `DBNull.Value`. *(static)* |
| `AddRefCursor(cmd, name)` | Cursor OUT (Oracle) o no-op (SQL Server). |
| `MapAsync<T>(reader, ct)` | Mapea el result set actual a `List<T>`. *(static)* |
| `GetOutputString(cmd, name)` | Lee un OUT param como texto (`DBNull` ⇒ `null`). *(static)* |
| `ThrowIfSpError(codigo, mensaje)` | Traduce `(P_CODIGO, P_MENSAJE)` a excepción de negocio. |
| `ToDataTable<T>(items)` · `AddStructuredParameter(...)` | Opcionales, para Table-Valued Parameters (sólo SQL Server). |
| `ConnectionFactory` · `Options` · `Logger` | Propiedades protegidas. |

### Anatomía de un método

```csharp
// AIEP.Application.Repository/Repository/Alumno/AlumnoRepository.cs
public sealed class AlumnoRepository : BaseRepository, IAlumnoRepository
{
    private readonly string _sp;

    public AlumnoRepository(
        IDbConnectionFactory factory,
        IOptions<DatabaseOptions> dbOptions,
        IConfiguration configuration,
        ILogger<AlumnoRepository> logger)
        : base(factory, dbOptions, logger)
    {
        _sp = configuration.GetValue<string>("Conf:StoredProcedureName")
              ?? "PKG_BLUEPRINT.P_GET_ALUMNO";
    }

    public async Task<List<AlumnoEntity>> GetAlumnoAsync(
        string valorDocumento, string tipoDocumento, CancellationToken ct = default)
    {
        List<AlumnoEntity> alumnos;
        List<ProgramaAlumnoEntity> programas;

        await using var conn = await OpenConnectionAsync(ct);
        await using var cmd  = CreateStoredProcCommand(conn, _sp);

        AddParameter(cmd, "P_TP_DOC",    tipoDocumento,   DbType.String, size: 20);
        AddParameter(cmd, "P_DOCUMENTO", valorDocumento,  DbType.String, size: 20);

        // Cursores ANTES que los escalares OUT - orden canónico: #1 alumno, #2 programas
        AddRefCursor(cmd, "C_ALUMNO");
        AddRefCursor(cmd, "C_PROGRAMAS");

        AddParameter(cmd, "P_CODIGO",  null, DbType.String, ParameterDirection.Output, size: 4000);
        AddParameter(cmd, "P_MENSAJE", null, DbType.String, ParameterDirection.Output, size: 4000);

        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            alumnos   = await MapAsync<AlumnoEntity>(reader, ct);
            await reader.NextResultAsync(ct);
            programas = await MapAsync<ProgramaAlumnoEntity>(reader, ct);
        } // ← el reader se cierra aquí; recién ahora se pueden leer los OUT params

        ThrowIfSpError(GetOutputString(cmd, "P_CODIGO"), GetOutputString(cmd, "P_MENSAJE"));

        // Ensamblado 1→N en memoria
        var porPidm = programas.ToLookup(p => p.Pidm);
        foreach (var a in alumnos) a.Programas = porPidm[a.Pidm].ToList();

        return alumnos;
    }
}
```

### Contrato del SP: `P_CODIGO` / `P_MENSAJE`

| P_CODIGO | `ThrowIfSpError` lanza | HTTP final |
|---|---|---|
| `"OK"` o vacío | nada | 200 |
| empieza con `"400"` | `DataValidationException` | 400 |
| empieza con `"404"` | `NotFoundException<string>` | 404 |
| cualquier otro | `BusinessException` | 422 |

### Registrar el repo en DI

```csharp
// AIEP.Application.Repository/ServiceCollectionExtensions.cs
public static IServiceCollection AddBlueprintRepository(this IServiceCollection services, IConfiguration configuration)
{
    services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations();

    services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();

    services.AddScoped<IRecursoRepository, RecursoRepository>();
    services.AddScoped<IAlumnoRepository, AlumnoRepository>();   // ← agrega tu repo aquí

    return services;
}
```

> **Un SP con un solo result set.** Es el caso simple: una llamada a `MapAsync<T>`, cero
> `NextResultAsync`. Los `AddRefCursor` siguen siendo uno (Oracle) o ninguno (da igual en SQL Server).

---

## 8. Lógica de negocio

Clase concreta (**sin interfaz**), registrada `Scoped`. Aquí van: validación de parámetros,
reglas de negocio, orquestación de varios repos, y el mapeo entidad → DTO. **No** va acceso a
datos directo.

```csharp
// AIEP.Application.Logic/Logic/AlumnoLogic.cs
public class AlumnoLogic
{
    private readonly IAlumnoRepository _repo;
    private readonly IMapper _mapper;
    private readonly ILogger<AlumnoLogic> _logger;

    public AlumnoLogic(IAlumnoRepository repo, IMapper mapper, ILogger<AlumnoLogic> logger)
    {
        _repo = repo; _mapper = mapper; _logger = logger;
    }

    public async Task<List<Alumno>> GetAlumno(
        string? valorDocumento, string? tipoDocumento, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(valorDocumento))
            throw new RequiredParameterException(nameof(valorDocumento));   // → 400
        if (string.IsNullOrWhiteSpace(tipoDocumento))
            throw new RequiredParameterException(nameof(tipoDocumento));

        _logger.LogInformation("Consulta de alumno {Tipo}:{Doc}", tipoDocumento, valorDocumento);

        var data = await _repo.GetAlumnoAsync(valorDocumento, tipoDocumento, ct);
        return _mapper.Map<List<Alumno>>(data);
    }
}
```

> **CancellationToken.** Propágalo siempre: controller → logic → repo → `ExecuteReaderAsync(ct)`.
> Si el cliente corta la conexión, la consulta a la BD se cancela y no consumes un pool slot de más.

---

## 9. Endpoints

Controller MVC en `Controllers/v1/`, namespace `...Controllers.v1`. Devuelve el DTO "pelado"
(no hay envoltorio `ApiResponse<T>`).

```csharp
// Controllers/v1/AlumnoController.cs
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[Authorize]
public class AlumnoController : ControllerBase
{
    private readonly AlumnoLogic _logic;
    private const string _scope = "IntegrationService.Blueprint.Read";

    public AlumnoController(AlumnoLogic logic) => _logic = logic;

    [HttpGet]
    [RequiredScope(AcceptedScope = new[] { _scope })]
    [TypeFilter(typeof(ExceptionHandler))]
    [RequireClaim("ClaimTenantId")]
    public async Task<IActionResult> Get([FromQuery] AlumnoRequest? request, CancellationToken ct)
    {
        var output = await _logic.GetAlumno(request?.valorDocumento, request?.tipoDocumento, ct);
        return Ok(output);
    }
}
```

### Cada atributo, para qué

| Atributo | Efecto |
|---|---|
| `[ApiController]` | Valida `ModelState` (DataAnnotations del request) ⇒ `400` automático; binding inferido. |
| `[Route("api/v{version:apiVersion}/[controller]")]` | Ruta con segmento de versión. `[controller]` = nombre sin sufijo (`Alumno`). |
| `[ApiVersion("1.0")]` | Versión que sirve este controller. Default asumido si no se especifica. |
| `[Authorize]` | Exige JWT Bearer válido (Azure AD). |
| `[RequiredScope(AcceptedScope = …)]` | El token debe traer ese `scp`/`scope`. Si no ⇒ `403`. |
| `[RequireClaim("ClaimTenantId")]` | El claim `tid` debe estar en `Conf:ListTenantId`. Si no ⇒ `403`. |
| `[TypeFilter(typeof(ExceptionHandler))]` | Envuelve la acción: cualquier excepción sale como `ExceptionResponse` con el HTTP correcto. |

### Registrar la lógica

En `Program.cs`, junto a las demás:

```csharp
builder.Services.AddScoped<AlumnoLogic>();
```

### Agregar una versión v2

```
Controllers/v2/AlumnoController.cs     namespace ...Controllers.v2
[ApiVersion("2.0")]  →  GET /api/v2/alumno
```

Las rutas de v1 siguen intactas. Puedes tener ambos controllers a la vez y compartir `Logic` o no.

---

## 10. Manejo de errores

No devuelvas códigos HTTP a mano. **Lanza una excepción** de `AIEP.Application.Model.Models`;
el filtro `ExceptionHandler` la traduce y responde `{ "message": ..., "detail": ... }`.

| Lanza…                                 | Cuándo                                              | HTTP |
| -------------------------------------- | --------------------------------------------------- | ---- |
| `RequiredParameterException(nombre)`   | falta un parámetro obligatorio                      | 400  |
| `InvalidParameterException(nombre)`    | parámetro presente pero inválido                    | 400  |
| `DataValidationException(msg)`         | los datos no cumplen una regla de forma             | 400  |
| `NotFoundException<Guid>` / `<string>` | el recurso no existe                                | 404  |
| `BusinessValidationException(msg)`     | viola una regla de negocio                          | 422  |
| `BusinessException(msg)`               | error de negocio genérico (lo usa `ThrowIfSpError`) | 422  |
| `AuthException(msg)`                   | autorización insuficiente a nivel de dominio        | 401  |
| `DbException` (Oracle/Sql)             | lo lanza el driver; no lo captures                  | 500  |
| cualquier otra                         | -                                                   | 500  |

```csharp
if (rutInvalido)
    throw new InvalidParameterException(nameof(valorDocumento), "El RUT no supera dígito verificador.");

if (alumno is null)
    throw new NotFoundException<string>($"No existe alumno {tipo}:{doc}.");
```

> **El cuerpo del error.** `ExceptionResponse { string message; string detail; }`. En
> `400/404` el `detail` es la URL de la request; en `500` es un texto genérico (no filtra el
> stack). Todo se loguea con la excepción completa *antes* de responder.

---

## 11. Seguridad

Cuatro capas, en orden de ejecución:

1. **API key** - `ApiKeyMiddleware` compara el header `x-xmsbs-apikey` contra `Conf:ApiKey`
   (de Key Vault, refrescable) y `Conf:AdditionalKeys`. Sin match ⇒ `401` texto plano.
   `OPTIONS` pasa directo.
2. **JWT Bearer** - `[Authorize]` + `AddMicrosoftIdentityWebApiAuthentication`. Token de
   Azure AD (`AzureAd` en config, **sin** ClientSecret: sólo se validan tokens).
3. **Scope** - `[RequiredScope(...)]`. El token debe traer `IntegrationService.<Servicio>.Read`
   (o el que definas).
4. **Tenant** - `[RequireClaim("ClaimTenantId")]`. El claim `tid` debe estar en `Conf:ListTenantId`.

### Rotación de API key sin downtime

Publica la key nueva en `Conf:AdditionalKeys` (array). Durante la ventana se aceptan la vieja
(`Conf:ApiKey`) y la nueva. Luego promueve la nueva a `Conf:ApiKey` y vacía `AdditionalKeys`.
Como se leen por `IOptionsMonitor`, el cambio en App Config entra sin reiniciar (refresco cada
5 min al tocar `Blueprint:Sentinel`).

### Swagger con auth

El UI ya trae el botón *Authorize* (definición `Bearer`). Pega `Bearer <token>` y prueba
endpoints protegidos. Swagger se sirve **antes** del `ApiKeyMiddleware`, así que `/swagger` no
pide API key.

### CORS

Política `"CorsPolicy"`. Si `AllowedHosts = "*"` ⇒ abierto; si no, se restringe a
`Cors:AllowedOrigins`. Ajusta esa lista por entorno.

---

## 12. Logging

NLog, configurado en `nlog.config`. En contenedor los logs salen como **JSON a stdout**
(target `jsonConsole`) para Azure Monitor / `docker logs`; en disco sólo `Error+`.

### Logueo estructurado / no interpoles

```csharp
// bien: plantilla + parámetros nombrados (indexables en Azure Monitor)
_logger.LogInformation("Consulta de alumno {Tipo}:{Doc}", tipoDocumento, valorDocumento);

// mal: string interpolado (se pierde la estructura)
_logger.LogInformation($"Consulta de alumno {tipoDocumento}:{valorDocumento}");
```

| Nivel | Úsalo para |
|---|---|
| `LogDebug` | diagnóstico local; no en Production |
| `LogInformation` | hitos de negocio (consulta hecha, entidad no encontrada) |
| `LogWarning` | algo recuperable o inesperado pero manejado (lo usa `ThrowIfSpError`) |
| `LogError` | excepción no controlada; el `ExceptionHandler` ya lo hace por ti |

Nunca loguees secretos, tokens ni la cadena de conexión. El RUT/documento es dato personal:
acórtalo o hashéalo si tu política lo exige.

---

## 13. Pruebas

NUnit 3 + Moq. Tres tipos de prueba cubren una feature.

### 1 · Lógica / con `LogicTestBase`

```csharp
// AIEP.Test/Logic/AlumnoLogicTests.cs
[TestFixture]
public class AlumnoLogicTests : LogicTestBase<AlumnoLogic, MapperConfig, IAlumnoRepository>
{
    [Test]
    public async Task GetAlumno_MapeaYCuenta()
    {
        var entidades = new List<AlumnoEntity> { new() { Rut = "1-9", Nombres = "Ana" } };
        RepositoryMock
            .Setup(r => r.GetAlumnoAsync("1-9", "RUT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(entidades);

        var res = await Logic.GetAlumno("1-9", "RUT");

        Assert.AreEqual(1, res.Count);
    }

    [Test]
    public void GetAlumno_SinDocumento_Lanza() =>
        Assert.ThrowsAsync<RequiredParameterException>(() => Logic.GetAlumno(null, "RUT"));
}
```

### 2 · Mapeo / con `DataTableReader` (sin BD)

```csharp
var t = new DataTable();
t.Columns.Add("PIDM", typeof(int));
t.Columns.Add("VIGENTE", typeof(string));
t.Rows.Add(123, "Y");
t.Rows.Add(456, DBNull.Value);

using var reader = t.CreateDataReader();
var list = await DbReaderMapper.MapReaderToListAsync<AlumnoEntity>(reader);

Assert.IsTrue(list[0].Vigente);      // "Y" → true
Assert.IsFalse(list[1].Vigente);     // DBNull → default(bool)
```

### 3 · Fábrica / Oracle vs SQL Server

`DbConnectionFactoryTests` ya cubre: `CreateConnection()` devuelve el tipo correcto por
`Provider`, `AddRefCursor` agrega parámetro en Oracle y `null` en SQL Server, y falla si falta
la cadena. Cópialo si agregas comportamiento a la fábrica.

```bash
dotnet test ApiBlueprint.sln -c Release --collect:"XPlat Code Coverage"
```

---

## 14. Tutorial: feature "Alumno" (ERP Banner) de punta a punta

Objetivo: `GET /api/v1/alumno?valorDocumento=12345678&tipoDocumento=RUT` que consulta un SP de
Banner con dos result sets (alumno + programas) y devuelve el DTO ensamblado. Los archivos que
ya trae el repo para `Recurso` son tu plantilla.

### Paso 1 / Entidad

Crea `AIEP.Domain.DataBlueprint/Entities/AlumnoEntity.cs` y `ProgramaAlumnoEntity.cs` con
`[Column]` apuntando a los alias del SP (ver §4). `ProgramaAlumnoEntity` lleva
`[Column("PIDM")] int Pidm` para el join en memoria.

### Paso 2 / DTOs

`AIEP.Application.Model/Models/Alumno/Alumno.cs` (+ `ProgramaAlumno`) con `[JsonPropertyName]`,
y `AlumnoRequest.cs` con `[Required]` / `[StringLength]` (ver §5).

### Paso 3 / Contrato del SP (ambos motores)

Documenta el orden canónico en `db/oracle/` y `db/sqlserver/`.

```sql
-- Oracle
PROCEDURE P_GET_ALUMNO(
  P_TP_DOC     IN  VARCHAR2,
  P_DOCUMENTO  IN  VARCHAR2,
  C_ALUMNO     OUT SYS_REFCURSOR,   -- #1: PIDM, ID_BANNER, RUT, NOMBRES, ... , VIGENTE('Y'/'N')
  C_PROGRAMAS  OUT SYS_REFCURSOR,   -- #2: PIDM, CODIGO, NOMBRE
  P_CODIGO     OUT VARCHAR2,        -- 'OK' | '400...' | '404...' | otro
  P_MENSAJE    OUT VARCHAR2);

-- SQL Server: @P_TP_DOC, @P_DOCUMENTO, @P_CODIGO OUT, @P_MENSAJE OUT
--   SELECT ... (#1);  SELECT ... (#2);  SET @P_CODIGO='OK';
```

### Paso 4 / Interfaz + repositorio

`AIEP.Application.Repository/Repository/Alumno/IAlumnoRepository.cs`:

```csharp
Task<List<AlumnoEntity>> GetAlumnoAsync(string valorDocumento, string tipoDocumento, CancellationToken ct = default);
```

Y `AlumnoRepository.cs` heredando de `BaseRepository` - copia la anatomía de §7. Recuerda:
cursores antes de escalares, OUT params fuera del `using` del reader, `ThrowIfSpError`, join
por `ToLookup`.

### Paso 5 / Registrar el repo

En `ServiceCollectionExtensions.AddBlueprintRepository`:

```csharp
services.AddScoped<IAlumnoRepository, AlumnoRepository>();
```

### Paso 6 / Lógica

`AIEP.Application.Logic/Logic/AlumnoLogic.cs` / valida `valorDocumento` y `tipoDocumento`,
loguea, llama al repo, mapea (ver §8).

### Paso 7 / Mapper

En `Mapper/MapperConfig.cs` agrega los `CreateMap` (elemento anidado primero):

```csharp
CreateMap<ProgramaAlumno, ProgramaAlumnoEntity>().ReverseMap();
CreateMap<Alumno, AlumnoEntity>().ReverseMap()
    .ForMember(d => d.NombreCompleto,
               o => o.MapFrom(s => $"{s.Nombres} {s.ApellidoPaterno} {s.ApellidoMaterno}".Trim()));
```

### Paso 8 / Controller

`Controllers/v1/AlumnoController.cs`,  copia de §9. Ajusta el `_scope` si tu servicio usa otro.

### Paso 9 / Registrar la lógica + configurar

```csharp
// Program.cs
builder.Services.AddScoped<AlumnoLogic>();
```

En `appsettings.json`: `Conf:StoredProcedureName = "PKG_BLUEPRINT.P_GET_ALUMNO"` (o el de SQL
Server según entorno vía App Config).

### Paso10 / Pruebas

`AlumnoLogicTests` (con `LogicTestBase`) + un test de mapeo con `DataTableReader` (ver §13).
`dotnet test`.

### Paso 11 - Probar en local, con los dos motores

```powershell
dotnet user-secrets set "ConnectionStrings:BlueprintDb" "<cadena>"
dotnet user-secrets set "Conf:ApiKey" "<key>"

$env:Database__Provider = "Oracle";  dotnet run
# en otra terminal:
curl -k "https://localhost:7090/api/v1/alumno?valorDocumento=12345678&tipoDocumento=RUT" `
  -H "Authorization: Bearer <token>" -H "x-xmsbs-apikey: <key>"

# repetir con  $env:Database__Provider = "SqlServer"  - el DTO debe salir idéntico.
```

**Criterio de aceptación.** Mismo JSON con `Provider=Oracle` y `Provider=SqlServer`; `400` si
falta un parámetro; `401` sin API key; `403` si el `tid` no está autorizado; `200` con la
lista y sus `Programas` anidados.

---

## 15. Aristas frecuentes / FAQ

### Paginación

El blueprint no la trae. Patrón recomendado: parámetros `@P_PAGINA`/`@P_TAMANO` al SP + un
tercer result set (o un OUT param) con el total. En el DTO, un envoltorio
`PagedResult<T> { items, page, size, total }` - este sí es un caso legítimo de envoltorio.

### Transacciones

No hay soporte en `BaseRepository` (los SP de sólo-lectura no lo necesitan). Si escribes: abre
la conexión, `await using var tx = await conn.BeginTransactionAsync(ct)`, asigna
`cmd.Transaction = tx`, y `await tx.CommitAsync(ct)`. Mantenlo dentro de un único método del repo.

### Varios SP o varias conexiones en un método

Abre una `DbConnection` por SP (o reusa la misma para varios comandos secuenciales). Si
necesitas dos bases distintas a la vez, usa la variante de dos claves de conexión
(`ConnectionStrings:BlueprintDb.Oracle` / `.SqlServer`) y resuelve la segunda con
`IConfiguration.GetConnectionString` directamente.

### Consultas largas

`Database:CommandTimeout` (segundos) aplica a todos los comandos. Para un SP puntualmente
lento, súbelo por entorno en App Config; no lo bajes globalmente.

### Performance con muchos registros

`DbReaderMapper` ya lee la fila completa con `GetValues` y cachea los setters. Para volúmenes
grandes en Oracle, sube `FetchSize` en la cadena o en el comando (`((OracleCommand)cmd).FetchSize`
dentro de un adapter). Evita traer miles de filas por endpoint: pagina.

### Health checks

No incluidos (paridad con Sedes). Para agregarlos: `builder.Services.AddHealthChecks().AddCheck(...)`
con un ping a `SELECT 1` / `SELECT 1 FROM DUAL` vía `IDbConnectionFactory`, y
`app.MapHealthChecks("/health")` **antes** del `ApiKeyMiddleware`.

### OpenTelemetry / métricas

No incluido. Punto de extensión natural en `Program.cs` junto al logging; el `ExceptionHandler`
ya centraliza el registro de fallos.

### Qué NO hacer

- Leer `IConfiguration` crudo en Logic o Controller - usa `IOptions`.
- `using Oracle.*` / `using Microsoft.Data.SqlClient` fuera de los dos adapters.
- Devolver la entidad `*Entity` desde el controller - siempre el DTO.
- Capturar `Exception` en el repo para "loguear y relanzar" - deja que suba al `ExceptionHandler`.
- Leer `P_CODIGO` con el reader abierto.
- Interpolar strings en los logs.

---

## 16. Referencia rápida

### Archivos de una feature `X`

```
AIEP.Domain.DataBlueprint/Entities/XEntity.cs
AIEP.Application.Model/Models/X/X.cs
AIEP.Application.Model/Models/X/XRequest.cs
AIEP.Application.Repository/Repository/X/IXRepository.cs
AIEP.Application.Repository/Repository/X/XRepository.cs
AIEP.Application.Logic/Logic/XLogic.cs
AIEP.Application.Api.Blueprint/Controllers/v1/XController.cs
AIEP.Application.Api.Blueprint/Mapper/MapperConfig.cs        (+CreateMap)
AIEP.Application.Repository/ServiceCollectionExtensions.cs   (+AddScoped)
AIEP.Application.Api.Blueprint/Program.cs                    (+AddScoped<XLogic>)
AIEP.Test/Logic/XLogicTests.cs
db/oracle/…  db/sqlserver/…
```

### Firmas que más vas a usar

```csharp
Task<DbConnection>  OpenConnectionAsync(CancellationToken ct)
DbCommand           CreateStoredProcCommand(DbConnection conn, string sp)
DbParameter         AddParameter(DbCommand cmd, string name, object? value,
                                 DbType? dbType = null,
                                 ParameterDirection direction = ParameterDirection.Input,
                                 int? size = null)
DbParameter?        AddRefCursor(DbCommand cmd, string name)
Task<List<T>>       MapAsync<T>(DbDataReader reader, CancellationToken ct)   where T : new()
string?             GetOutputString(DbCommand cmd, string name)
void                ThrowIfSpError(string? codigo, string? mensaje)

DbReaderMapper.MapReaderToList<T>(IDataReader r)
DbReaderMapper.MapReaderToListAsync<T>(DbDataReader r, CancellationToken ct = default)
```

### Excepción → HTTP

```
RequiredParameterException / InvalidParameterException / DataValidationException → 400
NotFoundException<T>                                                             → 404
AuthException                                                                    → 401
BusinessException / BusinessValidationException                                  → 422
DbException / otras                                                              → 500
```

### Variables de entorno (local / contenedor)

```
ASPNETCORE_ENVIRONMENT = Development | QA | Production
Database__Provider     = Oracle | SqlServer
ConnectionStrings__BlueprintDb = <cadena>
Conf__ApiKey           = <key>
AzureAppConfig__Endpoint = https://appcs-...   (vacío = no usar App Config)
```

---

Renombrado `Blueprint → MiServicio` y provisión de Azure: ver `README.md`.
