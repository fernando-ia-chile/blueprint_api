# AIEP · Blueprint de microservicio API (.NET 10)

Solución de referencia para arrancar nuevos microservicios AIEP. **No es una plantilla
`dotnet new`**: se clona, se renombra y se adapta.

Replica la arquitectura y las librerías del servicio `AIEP-Servicios-Sedes`
(net10.0, capas `AIEP.Application.*` / `AIEP.Domain.*`, NLog, Microsoft.Identity.Web,
Asp.Versioning, Swashbuckle, AutoMapper, `ApiKeyMiddleware` + filtro de tenant + `ExceptionHandler`),
con estas diferencias deliberadas:

| Tema | Sedes | Blueprint |
|------|-------|-----------|
| Acceso a datos | `OracleDataAdapter.Fill` + mapeo manual, async falso | ADO.NET real + `BaseRepository` + `DbReaderMapper` (reflexión cacheada), `async`/`CancellationToken` |
| Motor | Solo Oracle | **Oracle y SQL Server**, incluso **simultaneos**, por `Database:Connections` (una entrada con nombre por conexión) + `IDbConnectionFactory` keyed |
| Secretos | `ClientSecret` y credenciales de BD en `appsettings.json` | **Nunca `ClientSecret`**; cadenas de conexión y API key por `appsettings.{Environment}.json`/variables de entorno, credencial explícita (`AzureCredentialFactory`: Managed Identity en Azure/Azure Arc, nunca `DefaultAzureCredential` implícito) para lo que sí usa Azure (Key Vault, vía `SecretClient` ad-hoc) |
| Config | `IConfiguration` crudo | `IOptions` tipado (`DatabaseOptions` por conexión, `ApiKeyOptions`, `TenantOptions`) validado al arranque |
| Contenedor / CI | — | `Dockerfile` multi-stage + `.dockerignore` + `azure-pipelines.yml` |
| Tests | NUnit + xUnit mezclados (rotos) | NUnit 3 + Moq, `LogicTestBase` genérico, tests de mapeo, de factory y de registro DI (keyed) |

## Documentación

- **[`docs/MANUAL-DESARROLLADOR.md`](docs/MANUAL-DESARROLLADOR.md)** — manual de desarrollo:
  arquitectura, `appsettings`, entidades, modelos, mapper, repositorios Oracle/SQL Server,
  endpoints, errores, seguridad, logging, pruebas y un tutorial de una feature end-to-end
  (`Alumno` de ERP Banner).
- Este `README.md` — qué es, cómo renombrar `Blueprint → MiServicio`, y provisión de Azure.
- `db/README.md` — contrato de los stored procedures de ejemplo.

## Estructura

```
AIEP.Application.Api.Blueprint   Web API (controllers v1, Program.cs, middlewares, perfil AutoMapper)
AIEP.Application.Logic           Lógica de negocio (clases *Logic concretas)
AIEP.Application.Repository      Acceso a datos sin ORM (factory + BaseRepository + DbReaderMapper + repos)
AIEP.Application.Model           DTOs + jerarquía de excepciones + ExceptionResponse
AIEP.Domain.DataBlueprint        Entidades de datos (*Entity con [Column])
AIEP.Test                        Pruebas (NUnit)
```

Grafo de referencias: `Api → Logic → {Model, Repository}`, `Repository → {Domain, Model}`.

## La capa de repositorio

- **`IDbConnectionFactory` / `DbConnectionFactory`** (singleton): crea la `DbConnection` del
  motor activo y concentra lo que `System.Data.Common` no expresa de forma portable
  (REF CURSOR y `BindByName` de Oracle). Los únicos archivos que referencian un driver
  concreto son `OracleProviderAdapter` y `SqlServerProviderAdapter`.
- **`DbReaderMapper`**: mapea `DbDataReader` → entidades por `[Column]` (mismo criterio que la
  referencia), pero escanea atributos y compila los *setters* **una vez por tipo** (caché
  estática), lee la fila completa con `GetValues` y ofrece variante `async`. Conserva la
  semántica de conversión original (`"Y"/"N"` y `1/0` → `bool`, nulos dejan el `default`).
- **`BaseRepository`**: apertura de conexión, creación de comandos de SP, alta de parámetros,
  cursores, lectura de OUT params y traducción de `(P_CODIGO, P_MENSAJE)` a excepción de
  negocio. Todo `async` con `CancellationToken` y `await using`.
- **`RecursoRepository`**: ejemplo end-to-end. Un único método público que lee 2 result sets
  con `NextResultAsync()` y ensambla la jerarquía 1→N en memoria. Ver `db/` para el contrato
  del SP y los scripts de ejemplo (Oracle y SQL Server).

## Cómo renombrar el blueprint (`Blueprint` → `MiServicio`)

1. Elegir el nombre `MiServicio`.
2. Buscar/reemplazar `Blueprint` → `MiServicio` (respetando mayúsculas/minúsculas) en
   `*.cs`, `*.csproj`, `*.sln`, `*.json`, `*.yml`, `Dockerfile`.
3. Tratar aparte el literal en minúscula `blueprint`: nombres de secretos en Key Vault
   (`blueprint-*`) e `imageRepository` del pipeline.
4. Renombrar carpetas y proyectos:
   `AIEP.Application.Api.Blueprint` → `AIEP.Application.Api.MiServicio`;
   `AIEP.Domain.DataBlueprint` → `AIEP.Domain.DataMiServicio`.
   Renombrar los `.csproj` correspondientes y `ApiBlueprint.sln` → `ApiMiServicio.sln`
   (actualizar las rutas en el `.sln`; los GUID se conservan).
5. Namespaces: cambian solo `AIEP.Application.Api.Blueprint` y `AIEP.Domain.DataBlueprint`;
   `AIEP.Application.Logic/Repository/Model` no cambian.
6. Renombrar la feature `Recurso` a tu entidad real (o borrar `Recurso*` y crear la tuya con
   el mismo corte vertical: entidad → DTO → `I*Repository`/`*Repository` → `*Logic` →
   `*Controller` → `MapperConfig`).
7. Ajustar: `Database:Connections` (nombres/proveedores de tus conexiones reales),
   `Conf:RecursoConnectionName`, `Conf:StoredProcedureName`, el scope
   (`IntegrationService.MiServicio.Read`), el título de Swagger y `Cors:AllowedOrigins`.
8. `dotnet build ApiMiServicio.sln` para validar.

Snippet de ayuda (PowerShell; lo ejecuta quien clona, **no** forma parte del blueprint):

```powershell
Get-ChildItem -Recurse -File -Include *.cs,*.csproj,*.sln,*.json,*.yml,Dockerfile |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
  ForEach-Object {
    (Get-Content -Raw $_.FullName) -replace 'Blueprint','MiServicio' | Set-Content -NoNewline $_.FullName
  }
Get-ChildItem -Recurse -Directory -Filter '*Blueprint*' |
  Sort-Object { $_.FullName.Length } -Descending |
  ForEach-Object { Rename-Item $_.FullName ($_.Name -replace 'Blueprint','MiServicio') }
```

## Configuración y secretos

**Sin Azure App Configuration, Key Vault directo** (como `AIEP-Servicios-Sedes_AzArc`, pero sin su
antipatrón de llamar a Key Vault en cada request): en entornos desplegados, cada
`ConnectionStrings:<Nombre>` (una por conexión declarada en `Database:Connections`) y
`Conf:ApiKey` los entrega **Key Vault**, cargado directo a `IConfiguration` **una sola vez al
arrancar** (`Program.cs` → `AddAzureKeyVault` + `BlueprintKeyVaultSecretManager`) — **un secreto
de Key Vault por cada cadena de conexión**, nunca uno compartido. La credencial la resuelve
`AzureCredentialFactory` (ver "Identidad" abajo), no `DefaultAzureCredential`.

Precedencia (gana el último): `appsettings.json` → `appsettings.{Environment}.json` → variables de
entorno → **Key Vault** (cargado antes de que el host termine de armar la configuración).

### Más de una conexión de BD (Database:Connections)

```json
"Database": {
  "Connections": {
    "OracleDb":    { "Provider": "Oracle",    "ConnectionStringName": "OracleDb",    "CommandTimeout": 300 },
    "SqlServerDb": { "Provider": "SqlServer", "ConnectionStringName": "SqlServerDb", "CommandTimeout": 300 }
  }
},
"Conf": { "RecursoConnectionName": "OracleDb" }
```

Cada entrada de `Database:Connections` queda registrada como un `IDbConnectionFactory` **keyed**
(`AddKeyedSingleton`) con ese mismo nombre — pueden estar activas varias conexiones a la vez,
cada una con su propio proveedor, timeout y secreto de Key Vault. Para sumar una conexión nueva:
agregar su bloque a `Database:Connections` y su secreto en Key Vault
(`blueprint-<nombre>-connectionstring`), e inyectar `[FromKeyedServices("Nombre")] IDbConnectionFactory`
en el repositorio que la use.

`IRecursoRepository` sigue el mismo patrón, un nivel más arriba: `AddBlueprintRepository` lo
registra **keyed** (`"OracleDb"` / `"SqlServerDb"`, explícito para los dos motores que el feature
conoce — no por el `foreach` genérico de conexiones) y **además**, sin key, delegando en el keyed
que indique `Conf:RecursoConnectionName` (`OracleDb` en producción/QA, `SqlServerDb` en
Development, para poder probar el feature contra el otro motor sin tocar código) — ese es el que
usan `RecursoLogic`/`RecursoController` hoy.

Las dos vías quedan disponibles en paralelo: `RecursoLogicOracle` y `RecursoLogicSqlServer`
(`AIEP.Application.Logic/Logic/`) son el ejemplo de referencia que pide cada motor explícito —
`[FromKeyedServices("OracleDb")] IRecursoRepository` / `[FromKeyedServices("SqlServerDb")] IRecursoRepository`
en el constructor, misma lógica de negocio que `RecursoLogic`. Pensado para un proyecto real con
dos controllers independientes, cada uno contra su propia BD (ese Controller no viene incluido —
se arma copiando `RecursoController` tal como se copiaría para `RecursoLogic`). Si el proyecto solo
necesita un motor, ambas clases (y sus tests) se pueden borrar sin afectar a
`RecursoLogic`/`RecursoController`.

**Cómo se resuelve cada cadena — Vault con prioridad, valor directo como respaldo:**
`DatabaseOptions.ResolveConnectionString` intenta, en orden:
1. `ConnectionStrings:<ConnectionStringName>` (lo que entrega Key Vault directo, cargado al
   arrancar — ver "Configuración y secretos" arriba). Si resuelve algo, **gana siempre**, sin
   excepción.
2. Si eso viene vacío, el campo `"ConnectionString"` escrito **directo** en esa misma entrada de
   `Database:Connections` (sin pasar por Vault en absoluto):
   ```json
   "OracleDb": { "Provider": "Oracle", "ConnectionString": "Data Source=...;User Id=...;Password=...;" }
   ```
   Pensado para desarrolladores sin rol asignado en el Key Vault (pasa con algunas instalaciones
   de Visual Studio + Azure Arc) — pueden seguir trabajando sin tocar código ni esperar a que les
   den acceso, mientras QA/Producción sigan resolviendo por Vault normalmente.
3. Si ninguna de las dos tiene valor, **esa conexión se omite por completo** — no se registra, y
   **no rompe el arranque de la app**. Solo falla si algo la pide explícitamente después
   (`Conf:RecursoConnectionName` mal apuntado, o un repositorio que la inyecte por
   `[FromKeyedServices]`), con un error claro de qué conexión falta. Para "apagar" una conexión a
   propósito: borrar su nodo de `Database:Connections`, o dejar `ConnectionStringName` **y**
   `ConnectionString` vacíos — cualquiera de las dos formas es segura, un desarrollador nunca
   rompe el arranque por no usar una de las conexiones.

> Nota: si el valor directo lleva usuario/clave reales y el archivo donde se escribe se commitea
> a git, queda en el historial del repo. Conviene escribirlo en `appsettings.Development.json` (o
> un archivo propio por desarrollador) en vez de la base compartida — decisión del equipo, no una
> restricción del código.

### Repositorio que usa dos conexiones a la vez (ej. Oracle Banner + SQL Server simultáneas)

`BaseRepository` expone sobrecargas explícitas de `OpenConnectionAsync`, `CreateStoredProcCommand`,
`AddRefCursor` y `AddStructuredParameter` que reciben el `IDbConnectionFactory` como parámetro, para
el repositorio que necesita **dos motores en la misma operación**, no solo "uno u otro" como
`RecursoRepository`. La conexión "principal" sigue usando las sobrecargas sin parámetro (la que
pasó al constructor de `BaseRepository`); la adicional se inyecta aparte y se pasa explícita:

```csharp
public sealed class BancoUnificadoRepository : BaseRepository, IBancoUnificadoRepository
{
    private readonly IDbConnectionFactory _sqlServerFactory;

    public BancoUnificadoRepository(
        [FromKeyedServices("OracleDb")] IDbConnectionFactory oracleFactory,
        [FromKeyedServices("SqlServerDb")] IDbConnectionFactory sqlServerFactory,
        ILogger<BancoUnificadoRepository> logger)
        : base(oracleFactory, logger)
    {
        _sqlServerFactory = sqlServerFactory;
    }

    public async Task<Resultado> HacerAlgoAsync(CancellationToken ct)
    {
        await using var oracleConn = await OpenConnectionAsync(ct);                  // la principal
        await using var sqlConn = await OpenConnectionAsync(_sqlServerFactory, ct);   // la explicita
        // ... usar ambas en la misma operacion
    }
}
```

Nombres fijos (`"OracleDb"`, `"SqlServerDb"`) porque una feature que siempre necesita las dos no
depende del ambiente — a diferencia de `RecursoConnectionName`, que sí varía por entorno. Sin
este patrón, un repositorio que necesite dos conexiones terminaría tocando `BaseRepository` a
mano — justo lo que generó el caso reportado que motivó este diseño.

### Key Vault directo (sin App Configuration)

No hay Azure App Configuration en este Blueprint (se evaluó y se descartó — ver "Notas"):
`Program.cs` carga los secretos del Key Vault **directo** a `IConfiguration`, una sola vez al
arrancar, con `AddAzureKeyVault(uri, azureCredential, new BlueprintKeyVaultSecretManager())`. El
manager filtra solo los secretos `blueprint-*` (el Vault es compartido por varios servicios de
AIEP) y mapea el resto del nombre a la clave real:

| Secreto en Key Vault | Clave en `IConfiguration` |
|---|---|
| `blueprint-oracledb-connectionstring` | `ConnectionStrings:OracleDb` |
| `blueprint-sqlserverdb-connectionstring` | `ConnectionStrings:SqlServerDb` |
| `blueprint-api-key` | `Conf:ApiKey` |

El patrón `blueprint-<nombre>-connectionstring` es genérico — agregar una conexión nueva (ver
"Más de una conexión de BD" arriba) solo requiere el secreto correspondiente en el Vault, **sin
tocar `BlueprintKeyVaultSecretManager` ni ningún otro código**.

1. Crear el Key Vault (o reusar uno existente) y cargar ahí **un secreto por cadena de
   conexión**: `blueprint-oracledb-connectionstring`, `blueprint-sqlserverdb-connectionstring`, y
   `blueprint-api-key`.
2. Darle a la identidad del servicio acceso de lectura (ver tabla de abajo).
3. Poner el endpoint del vault en `Conf:KeyVaultUri` (`appsettings.json` o variable de entorno).

Además, un `SecretClient` singleton queda registrado (`Conf:KeyVaultUri`, ver `Program.cs`) para
cualquier necesidad **ad-hoc** de código que quiera leer un secreto puntual — nunca una llamada a
Key Vault por request, a diferencia de `AIEP-Servicios-Sedes_AzArc`.

### Si no tienes acceso al Key Vault

`Program.cs` envuelve la carga del Vault en un `try/catch`: si falla (sin rol RBAC asignado, sin
red, etc.), queda un `logger.Warn` y **el arranque sigue** con lo que ya haya en
`appsettings*.json`, variables de entorno o `user-secrets` — nunca revienta la app por no tener
acceso al Vault.

El respaldo **oficial** para desarrollo local es [`dotnet user-secrets`](https://learn.microsoft.com/aspnet/core/security/app-secrets)
(mecanismo propio de .NET; `WebApplication.CreateBuilder` ya lo carga solo en Development). Nunca
se sube al repo — vive fuera del proyecto, en `%APPDATA%\Microsoft\UserSecrets` (Windows) o
`~/.microsoft/usersecrets` (Linux/Mac):

```bash
cd AIEP.Application.Api.Blueprint
dotnet user-secrets init                               # una sola vez por proyecto (ya tiene UserSecretsId)
dotnet user-secrets set "ConnectionStrings:SqlServerDb" "cadena-local"
dotnet user-secrets set "Conf:ApiKey" "valor-local"
```

Mismas claves que usa Key Vault (`ConnectionStrings:<Nombre>`, `Conf:ApiKey`), misma prioridad:
Vault gana si llega a resolver algo; si no (falló, o directamente no hay `Conf:KeyVaultUri`), lo que
haya en user-secrets queda vigente — cubre tanto las conexiones (mismo mecanismo que el campo
`ConnectionString` directo en `Database:Connections`, documentado arriba) como `Conf:ApiKey`, que
antes dependía 100% del Vault y no tenía respaldo. Preferible al campo `ConnectionString` directo en
`appsettings.Development.json` cuando el valor es una credencial real, justamente para no arriesgar
que termine commiteada.

### Identidad (roles RBAC / access policy)

La identidad que ejecuta el servicio necesita acceso de lectura al Key Vault — **no asumir que usa
RBAC**, revisar cuál le corresponde antes de dar acceso:

| Mecanismo | Cómo dar acceso |
|-----------|-----------------|
| RBAC (`enableRbacAuthorization: true`) | Rol `Key Vault Secrets User` |
| Access policy (`enableRbacAuthorization: false`) | Lista de políticas propia del Vault, no roles de Azure: `az keyvault set-policy --name <vault> --object-id <id> --secret-permissions get list` |

Para saber cuál es el modo de un Vault: `az keyvault show --name <vault> --query properties.enableRbacAuthorization`.
El código (`AzureCredentialFactory`, `SecretClient`) es **idéntico en ambos casos** — la
autorización la decide Azure del lado del servidor; el SDK solo autentica. Solo cambia el comando
de infraestructura, nunca el código C#.

La credencial la resuelve `AzureCredentialFactory` (no `DefaultAzureCredential`): una cadena
implícita prueba varias credenciales en silencio y no deja saber si la Managed Identity de la
máquina realmente está funcionando; esta fábrica obliga a declarar explícitamente cuál se usa.

- **`Auth:UseManagedIdentity = true`** (por defecto en despliegues) → `ManagedIdentityCredential`,
  system- o user-assigned (`Auth:ManagedIdentityClientId`). Cubre:
  - **Azure** (App Service / Container Apps / AKS Workload Identity): habilitar la identidad y
    darle acceso al Key Vault (rol o access policy según corresponda, ver tabla arriba) — sin
    esto, `AddAzureKeyVault` no resuelve ninguna cadena (se atrapa el error, ver "Si no tienes
    acceso al Key Vault" arriba) y el arranque depende por completo del valor directo en
    `Database:Connections` o de `Conf:ApiKey` puesto a mano.
  - **Azure Arc** (servidores on-prem): instalar el agente Arc; la máquina obtiene la misma
    Managed Identity, expuesta vía `IDENTITY_ENDPOINT` / `IMDS_ENDPOINT`. Darle el mismo acceso a
    la identidad de la máquina Arc. La cuenta del proceso debe pertenecer al grupo `himds`
    (Linux) o tener acceso al archivo de clave de Arc (Windows).
- **`Auth:UseManagedIdentity = false`** (Development) → identidad del desarrollador
  (`VisualStudioCredential` / `az login`), nunca usada en servidor; el usuario AAD necesita el
  mismo acceso al Key Vault. **Si el desarrollador no lo tiene** (pasa con algunas instalaciones
  de Visual Studio + Azure Arc): la app **ya no se cae** — ver "Si no tienes acceso al Key Vault"
  arriba. El respaldo recomendado es `dotnet user-secrets` (cubre tanto conexiones como
  `Conf:ApiKey`); el campo `ConnectionString` directo en `Database:Connections` (ver "Más de una
  conexión de BD") sigue disponible como alternativa, con el mismo riesgo de commit si se escribe
  en la base compartida.

## Ejecutar en local

Escribir los valores reales en `appsettings.Development.json` (no en la base compartida):
```json
"ConnectionStrings": { "OracleDb": "<...>", "SqlServerDb": "<...>" },
"Conf": { "ApiKey": "<...>" }
```
Alternativa sin tocar `ConnectionStrings` en absoluto: poner el valor directo en
`Database:Connections:<Nombre>:ConnectionString` (ver "Más de una conexión de BD" arriba).

```
cd AIEP.Application.Api.Blueprint
dotnet run
```

`ASPNETCORE_ENVIRONMENT=Development` (perfil por defecto). Abrir `https://localhost:7090/swagger`.
`Auth:UseManagedIdentity=false` hace que `AzureCredentialFactory` use tu identidad local (Visual
Studio / `az login`) para leer Key Vault directo al arrancar. Ambas conexiones
(`Database:Connections:OracleDb` / `:SqlServerDb`) ya están declaradas; cuál usa `RecursoRepository`
se cambia con `Conf:RecursoConnectionName` (`appsettings.Development.json` o
`Conf__RecursoConnectionName=OracleDb|SqlServerDb`).

### Endpoint de ejemplo

`GET /api/v1/recurso?codigo=ABC` con:
- header `Authorization: Bearer <token Azure AD>` con el scope `IntegrationService.Blueprint.Read`
- header `x-xmsbs-apikey: <api key>`
- claim `tid` del token dentro de `Conf:ListTenantId`

Respuestas: `200` lista; `400` código vacío; `401` sin API key; `403` tenant no autorizado;
`500` con `ExceptionResponse` si la BD no responde.

## Docker

```
docker build -t aiep/blueprint:local .
docker run --rm -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e ConnectionStrings__OracleDb="<...>" \
  -e ConnectionStrings__SqlServerDb="<...>" \
  -e Conf__ApiKey="<...>" \
  aiep/blueprint:local
```

`/swagger` queda en `:8080`. La imagen corre como usuario no-root (`$APP_UID`).

## Tests

```
dotnet test ApiBlueprint.sln -c Release --collect:"XPlat Code Coverage"
```

Cubre `RecursoLogic`, `DbReaderMapper` (con `DataTableReader`), `DbConnectionFactory` y el registro
DI de `AddBlueprintRepository` (conexiones keyed, `Conf:RecursoConnectionName`).

## Notas

- **SDK**: `global.json` fija .NET SDK 10.0.x (`rollForward: latestFeature`) para builds
  reproducibles y para que `dotnet new`/VS generen el `.sln` clásico.
- **NUnit 3**: se conserva la sintaxis clásica (`Assert.AreEqual`) por consistencia con el
  `Usings.cs` institucional. Migración futura a NUnit 4 → `Assert.That(actual, Is.EqualTo(...))`.
- **`AutoMapper` 16.2.0, con licencia comercial de LuckyPenny Software** (sube desde 12.0.1).
  El tier Community gratuito no aplica: excluye explícitamente a "una universidad o institución
  de educación superior que use la librería para software institucional/operacional"
  (administrativo, registraduría, financiero — exactamente lo que es este servicio), así que AIEP
  compró la licencia. Con eso resuelto, 16.2.0 también trae parchado el advisory `NU1903`
  (recursión no controlada, CVE-2026-32933 — fijo desde 15.1.1/16.1.1). Desde la 13,
  `AutoMapper.Extensions.Microsoft.DependencyInjection` quedó fusionado en el paquete principal —
  **no** referenciarlo aparte. Desde la 15, `AddAutoMapper(...)` exige una acción de configuración
  y `MapperConfiguration` pide `ILoggerFactory` como segundo argumento (ver `Program.cs` y
  `AIEP.Test/LogicTestBase.cs`). La key de licencia va en `Conf:AutoMapperLicenseKey` — no es
  secreta (solo de auditoría), no necesita Key Vault, por eso se escribe directo en
  `appsettings.json`. Sin ella, o con una inválida, AutoMapper sigue funcionando igual, solo deja
  un log de advertencia.
  Nota aparte: `AIEP-Servicios-Sedes_AzArc` (real, no plantilla) está en AutoMapper 15.1.3 —
  confirmar si esa licencia también está cubierta, no asumir que lo está solo porque Blueprint lo
  está.
- **`Azure.Identity` 1.21.x** y **`Microsoft.Identity.Web` 4.16.x**: se suben respecto a Sedes
  (seguridad + soporte maduro de Managed Identity / Azure Arc).
- **API Versioning**: `Asp.Versioning.Mvc` + `Asp.Versioning.Mvc.ApiExplorer` en vez de
  `Microsoft.AspNetCore.Mvc.Versioning` (sin mantenimiento). Tipos ahora en el namespace
  `Asp.Versioning` (`ApiVersion`, `ApiVersionReader`, etc.).
- **Identidad explícita para Key Vault**: `AzureCredentialFactory` reemplaza
  `DefaultAzureCredential` (ver sección "Identidad (roles RBAC / access policy)"). No incluye un
  nivel de `ClientSecretCredential` de respaldo como el de `AIEP-Servicios-Sedes_AzArc`: esta API
  nunca almacena `ClientSecret` (es resource server puro), así que ese nivel no aplica aquí.
- **Sin Azure App Configuration, Key Vault directo**: se evaluó agregar App Configuration para
  centralizar configuración/secretos por entorno, pero no está en el store real de AIEP
  (`AIEP-Servicios-Sedes_AzArc`, la referencia, tampoco lo usa) y agregaba una capa de nombres
  (prefijo `Blueprint:`, *label*, *trim*) que complicaba innecesariamente la capacitación de
  nuevos desarrolladores — se retiró. `ConnectionStrings:<Nombre>`/`Conf:ApiKey` siguen viniendo
  de Key Vault, pero cargados **directo** a `IConfiguration` al arrancar
  (`AddAzureKeyVault` + `BlueprintKeyVaultSecretManager`, ver "Configuración y secretos" y "Key
  Vault directo" arriba) — sin la llamada a Key Vault por request que sí tiene
  `AIEP-Servicios-Sedes_AzArc` (antipatrón, no se copió).
