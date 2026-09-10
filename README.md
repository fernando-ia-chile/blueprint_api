# AIEP · Blueprint de microservicio API (.NET 8)

Solución de referencia para arrancar nuevos microservicios AIEP. **No es una plantilla
`dotnet new`**: se clona, se renombra y se adapta.

Replica la arquitectura, la modalidad y las librerías del servicio `AIEP-Servicios-Sedes`
(net8.0, capas `AIEP.Application.*` / `AIEP.Domain.*`, NLog, Microsoft.Identity.Web,
API Versioning, Swashbuckle, AutoMapper, `ApiKeyMiddleware` + filtro de tenant + `ExceptionHandler`),
con estas diferencias deliberadas:

| Tema | Sedes | Blueprint |
|------|-------|-----------|
| Acceso a datos | `OracleDataAdapter.Fill` + mapeo manual, async falso | ADO.NET real + `BaseRepository` + `DbReaderMapper` (reflexión cacheada), `async`/`CancellationToken` |
| Motor | Solo Oracle | **Oracle y SQL Server** por configuración (`Database:Provider` + `IDbConnectionFactory`) |
| Secretos | `ClientSecret` y credenciales de BD en `appsettings.json` | **Sin secretos**: Azure App Configuration + Key Vault vía `DefaultAzureCredential` (Managed Identity / Azure Arc) |
| Config | `IConfiguration` crudo | `IOptions` tipado (`DatabaseOptions`, `ApiKeyOptions`, `TenantOptions`) validado al arranque |
| Contenedor / CI | — | `Dockerfile` multi-stage + `.dockerignore` + `azure-pipelines.yml` |
| Tests | NUnit + xUnit mezclados (rotos) | NUnit 3 + Moq, `LogicTestBase` genérico, tests de mapeo y de factory |

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
   (`blueprint-*`), `imageRepository` del pipeline, prefijo `Blueprint:` de App Configuration.
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
7. Ajustar: `Database:Provider`, `Conf:StoredProcedureName`, el scope
   (`IntegrationService.MiServicio.Read`), el título de Swagger, el prefijo de App Configuration
   y `Cors:AllowedOrigins`.
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

`appsettings.json` **no lleva secretos**. En entornos desplegados, `ConnectionStrings:BlueprintDb`
y `Conf:ApiKey` los entrega **Azure App Configuration** como *Key Vault references*, autenticado
con `DefaultAzureCredential`.

Precedencia (gana el último): `appsettings.json` → `appsettings.{Environment}.json` →
user-secrets (solo Development) → variables de entorno → **Azure App Configuration**.

### Azure App Configuration + Key Vault

1. Crear un App Configuration store y un Key Vault.
2. Cargar en Key Vault: `blueprint-db-connectionstring`, `blueprint-api-key`.
3. En App Configuration crear las claves con **label por entorno** (`Development` / `QA` /
   `Production`; sin label = valores compartidos):
   - `Blueprint:Database:Provider`, `Blueprint:Database:CommandTimeout`
   - `Blueprint:Conf:StoredProcedureName`
   - `Blueprint:ConnectionStrings:BlueprintDb` → *Key Vault reference* a `blueprint-db-connectionstring`
   - `Blueprint:Conf:ApiKey` → *Key Vault reference* a `blueprint-api-key`
   - `Blueprint:Sentinel` → cualquier valor (dispara el refresco cada 5 min)
4. Poner el endpoint del store en `AzureAppConfig:Endpoint` (`appsettings.json` o variable de entorno).

### Identidad (roles RBAC)

La identidad que ejecuta el servicio necesita:

| Recurso | Rol |
|---------|-----|
| App Configuration | `App Configuration Data Reader` |
| Key Vault (modo RBAC) | `Key Vault Secrets User` |

- **Managed Identity** (App Service / Container Apps / AKS Workload Identity): habilitar la
  identidad (system- o user-assigned) y asignarle los dos roles. Para user-assigned, exponer
  `AZURE_CLIENT_ID`.
- **Azure Arc** (servidores on-prem): instalar el agente Arc; la máquina obtiene identidad
  administrada y `DefaultAzureCredential` la detecta vía `IDENTITY_ENDPOINT` / `IMDS_ENDPOINT`.
  Asignar los mismos dos roles a la identidad de la máquina Arc. La cuenta del proceso debe
  pertenecer al grupo `himds` (Linux) o tener acceso al archivo de clave de Arc (Windows).
- **Local**: `az login` o una sesión de Visual Studio; el usuario AAD necesita los dos roles.
  Alternativa sin App Configuration:
  ```
  dotnet user-secrets set "ConnectionStrings:BlueprintDb" "<cadena real>"
  dotnet user-secrets set "Conf:ApiKey" "<api key de pruebas>"
  ```

## Ejecutar en local

```
cd AIEP.Application.Api.Blueprint
dotnet user-secrets set "ConnectionStrings:BlueprintDb" "<...>"
dotnet user-secrets set "Conf:ApiKey" "<...>"
dotnet run
```

`ASPNETCORE_ENVIRONMENT=Development` (perfil por defecto). Abrir `https://localhost:7090/swagger`.
En Development, `AzureAppConfig:Endpoint` está vacío, así que App Configuration se omite.
`Database:Provider` se puede cambiar en `appsettings.Development.json` o con
`Database__Provider=Oracle|SqlServer`.

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
  -e ConnectionStrings__BlueprintDb="<...>" \
  -e Conf__ApiKey="<...>" \
  aiep/blueprint:local
```

`/swagger` queda en `:8080`. La imagen corre como usuario no-root (`$APP_UID`).

## Tests

```
dotnet test ApiBlueprint.sln -c Release --collect:"XPlat Code Coverage"
```

Cubre `RecursoLogic`, `DbReaderMapper` (con `DataTableReader`) y `DbConnectionFactory`.

## Notas

- **SDK**: `global.json` fija .NET SDK 8.0.x (`rollForward: latestFeature`) para builds
  reproducibles y para que `dotnet new`/VS generen el `.sln` clásico.
- **NUnit 3**: se conserva la sintaxis clásica (`Assert.AreEqual`) por consistencia con el
  `Usings.cs` institucional. Migración futura a NUnit 4 → `Assert.That(actual, Is.EqualTo(...))`.
- **Advisory `AutoMapper` 12.0.1** (`NU1903`): se mantiene por paridad de librerías con Sedes.
  AutoMapper 13+ cambia licencia y fusiona `AutoMapper.Extensions.Microsoft.DependencyInjection`;
  evaluar el salto como cambio transversal del ecosistema, no en un servicio aislado.
- **`Azure.Identity` 1.13.x** y **`Microsoft.Identity.Web` 2.19.x**: se suben respecto a Sedes
  (seguridad + soporte maduro de Managed Identity / Azure Arc).
