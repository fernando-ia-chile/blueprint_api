# AGENTS.md — AIEP Blueprint (microservicio API .NET 10)

> Para agentes de IA (Claude Code, Codex, Copilot, Cursor, Gemini…) y personas sin contexto del repo. Léelo antes de tocar código.
> **Verificado el 2026-10-22** en `main` @ `f86715c`: `dotnet build` 0 errores, `dotnet test` 32/32. Si algo de aquí contradice al código, **gana el código**: corrige este archivo en el mismo cambio.
> Idioma del proyecto: **español** (docs, comentarios, mensajes, commits). Responde en español salvo que pidan otro idioma.

## 0. Resumen

**Qué es.** Solución de referencia con la que AIEP (instituto de educación superior chileno) arranca nuevos microservicios API en .NET: una API de ejemplo completa (`Recurso`) que fija arquitectura, seguridad, acceso a datos, manejo de secretos, pruebas, Docker y pipeline. Se **clona, se renombra (`Blueprint` → `MiServicio`) y se adapta**; no es una plantilla `dotnet new` ni un servicio para desplegar tal cual.
**Cómo funciona.** `GET /api/v1/recurso?codigo=…` → API key → JWT Azure AD → scope → tenant → `RecursoLogic` → `RecursoRepository` → *stored procedure* en **Oracle o SQL Server** (ADO.NET, sin ORM) → AutoMapper → JSON.

**Reglas que más importan:**
1. Capas fijas: `Api → Logic → {Model, Repository}`, `Repository → {Domain, Model}`; `Model` y `Domain` no dependen de nada; las `*Logic` son clases concretas, sin interfaz (§3).
2. Solo stored procedures por ADO.NET: sin ORM ni SQL inline; los drivers viven solo en los dos *adapters* (§5).
3. Cero secretos en archivos o repo: Key Vault (`blueprint-*`) con credencial explícita (`AzureCredentialFactory`: Managed Identity / Azure Arc); en local, `dotnet user-secrets`. Prohibido `DefaultAzureCredential`, Azure App Configuration y consultar Key Vault por request (§4).
4. Varias conexiones de BD = una entrada por conexión en `Database:Connections:<Nombre>` → `IDbConnectionFactory` *keyed*. El repositorio es agnóstico de motor: **no se duplica por motor** (§5).
5. Errores = excepciones de `AIEP.Application.Model.Models`; `ExceptionHandler` las traduce a HTTP. Nunca armes códigos HTTP a mano (§6).
6. Cada controller lleva `[Authorize]` y cada acción `[RequiredScope]`, `[RequireClaim("ClaimTenantId")]` y `[TypeFilter(typeof(ExceptionHandler))]` (§6).
7. Fuente de verdad: código > `README.md` > manual HTML > resto. `docs/MANUAL-DESARROLLADOR.md` y el `.docx` de Vault están desactualizados (§13).
8. Imita el estilo del archivo vecino; español en comentarios, mensajes y commits (§10).
9. Verifica siempre con `dotnet build` + `dotnet test` (base: 0 errores, 32 tests verdes) (§8).
10. No inventes valores reales (cadenas, IDs, recursos Azure, IPs): usa `<placeholders>` y pregunta (§15).

```bash
dotnet build ApiBlueprint.sln -c Release && dotnet test ApiBlueprint.sln -c Release
cd AIEP.Application.Api.Blueprint && dotnet run      # https://localhost:7090/swagger (Development)
```

## 1. Propósito y alcance

**Propósitos.** (1) *Estandarizar*: todos los microservicios API comparten capas, librerías, seguridad, errores, logging, CI y Docker. (2) *Acelerar*: clonar, renombrar, reemplazar `Recurso`. (3) *Seguridad por diseño*: sin secretos en archivos; identidad administrada + Key Vault; JWT + API key + lista de tenants. (4) *Datos institucionales*: **Oracle** (p. ej. ERP Banner) y **SQL Server**, incluso simultáneos, vía stored procedures. (5) *Capacitar y normar*: manual, tutorial y lineamientos para publicar un servicio sin conocer el repo. (6) *Ser verificable*: tests, Dockerfile y pipeline de referencia.

**Origen.** Replica la arquitectura del servicio real `AIEP-Servicios-Sedes` (y de `_AzArc` toma el uso de Key Vault). Diferencias deliberadas: ADO.NET real con `DbReaderMapper` cacheado y `async` verdadero (Sedes usa `OracleDataAdapter.Fill`); Oracle **y** SQL Server; nunca `ClientSecret` ni credenciales de BD en `appsettings.json`; Key Vault cargado **una vez al arrancar** (`_AzArc` lo consulta en cada request); `IOptions` tipado y validado; NUnit 3 coherente (Sedes mezcla NUnit y xUnit); Dockerfile y pipeline.

**Contexto.** El Lineamiento de Integraciones (`docs/Lineamiento_desarrollo 1.pptx`) separa *Legados* (no se tocan), *En curso* y *Nuevos* («nacerán con la nueva arquitectura de implementación»); el Blueprint es la base de los nuevos y accede a datos solo por SP (el lineamiento habla de «Recursos (SP)»). Ningún servicio sale directo a internet (§12). Los servidores on-premise (IIS) tienen **Azure Arc** y obtienen una Managed Identity para leer Key Vault sin credenciales locales (en desarrollo, la cuenta de Visual Studio o `az login`). Entornos: **DESA, QA, PROD**; a producción pasa Infraestructura.

**Incluye:** API `Recurso` completa (entidad, DTO, repo, lógica, controller, mapper, tests, SP Oracle y SQL Server); seguridad en 4 capas; errores → HTTP; datos sin ORM con múltiples conexiones; Key Vault directo + `user-secrets`; versionado, Swagger con Bearer, CORS, NLog (JSON a stdout, archivo de errores, Event Log en Windows); `Dockerfile` no-root, `azure-pipelines.yml`; 32 tests; README, manual HTML con tutorial, lineamientos.
**No incluye (no lo des por hecho):** lógica de negocio real (`Recurso` es ejemplo; `Alumno` del manual es solo tutorial, sin código); escritura (solo hay un `GET`), transacciones, paginación; health checks, OpenTelemetry, Polly, caché, rate limiting, autorización por roles, `ProblemDetails`/handler global, tests de integración; ORM (decisión deliberada); infraestructura como código; despliegue automático (la etapa `Deploy` del pipeline está comentada).

## 2. Mapa del repo

```
AIEP.Application.Api.Blueprint/   Program.cs (composición) · Controllers/v1 · Mapper/MapperConfig.cs · Options/ (ApiKey, Tenant)
                                  Utilities/ (ApiKeyMiddleware, ExceptionHandler, RequireClaimAttribute, AzureCredentialFactory,
                                  BlueprintKeyVaultSecretManager) · appsettings(.Development).json · nlog.config · launchSettings
AIEP.Application.Logic/Logic/     RecursoLogic (+ RecursoLogicOracle, RecursoLogicSqlServer)
AIEP.Application.Repository/      ServiceCollectionExtensions.cs (AddBlueprintRepository) · Infrastructure/ (BaseRepository, DbReaderMapper,
                                  IDbConnectionFactory/DbConnectionFactory, adapters Oracle y SqlServer) · Options/ · Repository/Recurso/
AIEP.Application.Model/Models/    DTOs, excepciones, ExceptionResponse
AIEP.Domain.DataBlueprint/        Entities/*Entity con [Column]
AIEP.Test/                        NUnit 3 + Moq (Logic/, Repository/, Settings/, TestData/)
db/  scripts SP de ejemplo (documentales; la app no los ejecuta) · docs/  manuales y lineamientos (rutas con espacios: cítalas)
```
Lectura sugerida: `Program.cs` → `ServiceCollectionExtensions.cs` → `BaseRepository.cs` → `RecursoRepository.cs` → `RecursoLogic.cs` → `RecursoController.cs`. `Recurso` es el corte vertical canónico: cópialo.

## 3. Arquitectura

| Proyecto | Responsabilidad | Depende de |
|---|---|---|
| `Api` | Host: `Program.cs`, controllers, middlewares, filtros, perfil AutoMapper, Swagger | Logic |
| `Logic` | Reglas de negocio: valida, orquesta, mapea Entity → DTO | Model, Repository |
| `Repository` | Acceso a datos sin ORM | Domain, Model |
| `Model` / `Domain` | DTOs + excepciones / entidades `*Entity`, sin lógica | — |
| `Test` | NUnit 3 + Moq | todos |

`Api`, `Logic`, `Repository`, `Model`, `Domain`, `Test` abrevian `AIEP.Application.Api.Blueprint`, `AIEP.Application.Logic/Repository/Model`, `AIEP.Domain.DataBlueprint` y `AIEP.Test`. Todo es `net10.0` con `Nullable` e `ImplicitUsings`; el namespace de la lógica es `AIEP.Application.Logic.Logic`. AutoMapper 16.2.0 tiene **licencia comercial** comprada por AIEP.

**Pipeline** (verificado en ejecución):
```
Swagger/UI -> CORS -> ApiKeyMiddleware -> HttpsRedirection -> Authentication -> Authorization -> MVC
MVC: [Authorize] [RequiredScope] [RequireClaim] + acción envuelta por [TypeFilter(typeof(ExceptionHandler))]
Controller -> *Logic -> I*Repository -> BaseRepository (SP, ADO.NET) -> Oracle | SQL Server
```
`/swagger` responde 200 **sin** API key (va antes del middleware); cualquier otra ruta, aunque no exista, sin header → 401 `text/plain`; con key válida y sin Bearer → 401 + `WWW-Authenticate: Bearer`; el preflight `OPTIONS` lo responde CORS (204). Endpoint de ejemplo: `GET /api/v1/recurso?codigo=ABC` con `Authorization: Bearer <JWT>` (scope `IntegrationService.Blueprint.Read`), `x-xmsbs-apikey: <key>` y claim `tid` ∈ `Conf:ListTenantId`; devuelve `[{ "Codigo Recurso", "Nombre Recurso", "Activo", "Cupos", "Fecha Alta", "Valor", "Detalles": [{ "Codigo Recurso", "Codigo Detalle", "Descripcion" }] }]`. Ruta `api/v{version:apiVersion}/[controller]`, versión 1.0 por defecto, leída de URL, header `x-api-version` o media type.

**DI.** `IDbConnectionFactory` keyed por conexión: Singleton. `IRecursoRepository` keyed `"OracleDb"`/`"SqlServerDb"` y sin key (delega en `Conf:RecursoConnectionName`, por defecto `OracleDb`): Scoped. `RecursoLogic*`: Scoped. `IMapper`: **Transient** (`IConfigurationProvider`: Singleton; verificado). `ApiKeyOptions`/`TenantOptions` (sección `Conf`): `IOptionsMonitor`. `DatabaseOptions` con nombre: `ValidateDataAnnotations` + `ValidateOnStart`. `SecretClient`: Singleton solo si hay `Conf:KeyVaultUri`; uso ad-hoc.

## 4. Configuración y secretos

| Clave | Qué controla / valor en el repo |
|---|---|
| `AzureAd:*` | valida el JWT entrante; API = *resource server*, **nunca** `ClientSecret` |
| `Auth:UseManagedIdentity`, `Auth:ManagedIdentityClientId` | credencial de Key Vault: `true` en despliegues, `false` en Development; el id vacío = MI del sistema |
| `Conf:KeyVaultUri` | vault a cargar (el de DESA); **vacío = no se usa Key Vault** |
| `Conf:ApiKeyHeaderName`, `Conf:ApiKey`, `Conf:AdditionalKeys` | `x-xmsbs-apikey`; key vigente (**secreto**, no versionada); keys extra para rotar |
| `Conf:ListTenantId` | `tid` autorizados, **en minúsculas** |
| `Conf:StoredProcedureName` | SP de `Recurso`: `PKG_BLUEPRINT.P_GET_RECURSO`; **uno solo para todos los motores** |
| `Conf:RecursoConnectionName` | conexión de `IRecursoRepository`: `OracleDb` (Development: `SqlServerDb`) |
| `Conf:AutoMapperLicenseKey` | licencia (no secreta; no la copies a otros sitios) |
| `Database:Connections:<Nombre>` | `Provider` (`Oracle`/`SqlServer`), `ConnectionStringName`, `ConnectionString` literal de respaldo, `CommandTimeout` (1–3600, def. 300) |
| `ConnectionStrings:<Nombre>` | cadena real (**secreto**, no versionada) |
| `Cors:AllowedOrigins`, `AllowedHosts` | CORS (ver §14.1) |

Variables de entorno: `Seccion__Clave` (`Conf__ApiKey`, `ConnectionStrings__OracleDb`, `Database__Connections__OracleDb__Provider`…). No hay `appsettings.QA.json` ni `Production.json` en el repo.
**Precedencia** (gana el último): `appsettings.json` → `appsettings.{Environment}.json` → `user-secrets` (Development) → variables de entorno → **Key Vault** (se agrega al final: si resuelve algo, gana siempre, también sobre variables de entorno).

**Key Vault directo.** `Program.cs` hace `AddAzureKeyVault(uri, credencial, new BlueprintKeyVaultSecretManager())` **una vez al arrancar**, antes de `AddBlueprintRepository`. Solo carga secretos `blueprint-*` (vault compartido): `blueprint-<nombre>-connectionstring` → `ConnectionStrings:<nombre>`; `blueprint-api-key` → `Conf:ApiKey`; otro `blueprint-*` → regla por defecto de la librería (`--` → `:`). Un secreto **por cadena**, nunca compartido; agregar conexión no toca el manager. Crear: `az keyvault secret set --vault-name <vault> --name blueprint-oracledb-connectionstring --value "<cadena>"` (nunca pegues valores reales en chats, commits ni docs). Se carga una sola vez: **rotar `blueprint-api-key` exige reciclar la app**; para rotar sin downtime usa `Conf:AdditionalKeys`.

**Credencial** (`AzureCredentialFactory`): `UseManagedIdentity=true` → `ManagedIdentityCredential` (Azure o servidor con **Azure Arc**, que expone la identidad de la máquina vía `IDENTITY_ENDPOINT`/`IMDS_ENDPOINT`). Esa identidad **no tiene permisos por defecto**: dale acceso al vault. En Windows/IIS la identidad del *application pool* debe ser administrador local o miembro del grupo «Hybrid agent extension applications»; en Linux, del grupo `himds` ([doc de Microsoft](https://learn.microsoft.com/en-us/azure/azure-arc/servers/managed-identity-authentication)). `false` (Development) → `VisualStudioCredential`, luego `AzureCliCredential`; nunca en servidor. **No asumas RBAC**: RBAC (`enableRbacAuthorization: true`) → rol `Key Vault Secrets User`; access policy → `az keyvault set-policy --name <vault> --object-id <id> --secret-permissions get list`. Modo: `az keyvault show --name <vault> --query properties.enableRbacAuthorization`. El código es idéntico en ambos.

**Sin acceso al vault** (resiliencia verificada): la carga va en `try/catch`; si falla (sin acceso, sin red, vault inexistente) queda un `WARN` y el arranque **continúa** con `appsettings*`, `user-secrets` y variables de entorno. Respaldo oficial en local (el `.csproj` ya trae `UserSecretsId`):
```bash
cd AIEP.Application.Api.Blueprint
dotnet user-secrets set "ConnectionStrings:SqlServerDb" "<cadena-local>"
dotnet user-secrets set "Conf:ApiKey" "<api-key-local>"
```
Menos recomendable: `Database:Connections:<Nombre>:ConnectionString` literal (si el archivo está en git, la credencial queda en el historial). `.gitignore` ignora `*.local.json`, pero el código **no** los carga.

## 5. Acceso a datos

```
Database:Connections:OracleDb    { Provider: Oracle,    ConnectionStringName: OracleDb }      cada entrada con cadena utilizable
Database:Connections:SqlServerDb { Provider: SqlServer, ConnectionStringName: SqlServerDb }    -> AddKeyedSingleton<IDbConnectionFactory>(nombre)
   IRecursoRepository keyed "OracleDb"    -> RecursoLogicOracle    ([FromKeyedServices("OracleDb")])
   IRecursoRepository keyed "SqlServerDb" -> RecursoLogicSqlServer ([FromKeyedServices("SqlServerDb")])
   IRecursoRepository sin key -> conexión de Conf:RecursoConnectionName -> RecursoLogic -> RecursoController
```
**Cadena** (`DatabaseOptions.ResolveConnectionString`): (1) `ConnectionStrings:<ConnectionStringName>` (Key Vault/user-secrets/entorno) **gana siempre**; (2) si está vacía, el `ConnectionString` literal de esa entrada; (3) si no hay ninguna, la conexión **se omite**: no se registra y no rompe el arranque (falla con `InvalidOperationException` solo si algo la pide). Para apagar una conexión: borra su nodo o vacía ambos campos.

**Piezas** (`Repository/Infrastructure`): `DbConnectionFactory` crea la conexión y aísla lo no portable (REF CURSOR y `BindByName` de Oracle); `DbReaderMapper` mapea por `[Column]` (sin distinguir mayúsculas, setters compilados y cacheados; `"Y"/"N"` y `1/0` → `bool`; `NULL` deja el `default`); `BaseRepository` ofrece conexión, comando de SP, `AddParameter`, `AddRefCursor`, `MapAsync<T>`, `GetOutputString`, `ThrowIfSpError` y helpers de TVP (parámetros tabla, solo SQL Server). Cada operación de conexión tiene sobrecarga con `IDbConnectionFactory` explícito para repositorios con **dos conexiones a la vez** (la principal va en el constructor; la otra, inyectada con `[FromKeyedServices]`).

**Reglas de un repositorio** (si se olvidan, fallan en silencio): (1) agrega **todos** los `AddRefCursor` **antes** que los escalares OUT (`P_CODIGO`, `P_MENSAJE`); (2) lee los OUT **después** de cerrar el reader; (3) mismo **orden canónico de result sets** en Oracle y SQL Server; (4) `async` con `CancellationToken` de punta a punta y `await using`.

**Contrato del SP de `Recurso`** (`db/README.md`): entrada `P_CODE`; cursores `C_RECURSOS`, `C_DETALLE` (Oracle `OUT SYS_REFCURSOR`; en SQL Server, dos `SELECT`); salida `P_CODIGO`, `P_MENSAJE`. Oracle `PKG_BLUEPRINT.P_GET_RECURSO`; SQL Server `dbo.SP_GET_RECURSO`. `P_CODIGO`: `OK`/vacío → 200; prefijo `400` → `DataValidationException` (400); prefijo `404` → `NotFoundException<string>` (404); otro → `BusinessException` (422).

## 6. Seguridad y errores

**Cuatro capas:** (1) `ApiKeyMiddleware`: header contra `Conf:ApiKey` + `AdditionalKeys` (sin distinguir mayúsculas; `IOptionsMonitor`); (2) JWT Bearer de Azure AD (`DefaultMapInboundClaims=false`); (3) `[RequiredScope]` con `IntegrationService.<Servicio>.Read`; (4) `[RequireClaim("ClaimTenantId")]`: el claim `tid` (tenant) debe estar en `Conf:ListTenantId` (el argumento es decorativo; siempre valida `tid`).

**Excepción → HTTP** (`ExceptionHandler`, cuerpo `ExceptionResponse { message, detail }`, siempre logueada con la excepción): `RequiredParameterException`, `InvalidParameterException`, `DataValidationException` → 400 · `AuthException` → 401 · `NotFoundException<Guid|string>` → 404 · `BusinessException`, `BusinessValidationException` → 422 · `DbException` → 500 «Error de conectividad con la base de datos.» · cualquier otra → 500. Además: 401 sin API key o JWT válido; 403 por scope faltante o `tid` no autorizado; `[ApiController]` responde 400 ante DataAnnotations inválidas. Evita capturar excepciones en repositorios solo para «loguear y relanzar».

## 7. Lineamientos institucionales (`docs/Lineamiento_desarrollo 1.pptx`)
REST/JSON orientado a entidad; Azure AD; **.NET 10 o superior**; URL `https://host:puerto/api/v1/<sustantivo>/<subrecurso>` (`API/ALUMNOS`, `API/ALUMNOS/Matriculados`); verbos POST/GET/PUT/DELETE; versión en la ruta; JWT en el header de **todas** las APIs; API key y cadenas de conexión en Key Vault; sin secretos en el servidor (identidad administrada vía Azure Arc). **Documentación Swagger obligatoria:** descripción de la API (su entidad), de cada endpoint y de cada parámetro, **ejemplo del JSON de respuesta** y significado de cada código (200/401/500 y los que apliquen). `RecursoController` hoy solo tiene `<summary>`: en endpoints nuevos agrega `<param>`, `<response code="…">` y `[ProducesResponseType]` (Swagger ya lee el XML). Capas del lineamiento → repo (aprox.): Presentación = `Controllers/`; Autenticación y Útiles = `Utilities/` + `Mapper/`; Servicios = `Logic`; Logs = NLog + `ExceptionHandler`; Unit_test = `AIEP.Test`.

## 8. Cómo trabajar aquí
```bash
dotnet restore ApiBlueprint.sln && dotnet build ApiBlueprint.sln -c Release
dotnet test ApiBlueprint.sln -c Release --collect:"XPlat Code Coverage"      # como el pipeline
docker build -t aiep/blueprint:local .                                         # no-root, puerto 8080
```
- **Base (2026-10-22):** build sin errores y 7 warnings `CS8618` heredados en `Model` (`NotFoundException`, `ExceptionResponse`); 32/32 tests. No agregues warnings.
- **SDK:** `global.json` exige ≥ 10.0.400 (`rollForward: latestFeature`). Si tu sandbox trae menos, **no edites `global.json`**: compila una copia temporal sin él (así se verificó con 10.0.113).
- **Humo sin Azure ni BD** (verificado; prueba el pipeline HTTP, no los datos):
```bash
export ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5199 Conf__KeyVaultUri= Conf__ApiKey=clave-de-prueba
export "ConnectionStrings__SqlServerDb=Server=127.0.0.1,1;Database=x;User Id=x;Password=x;TrustServerCertificate=True"
dotnet run --project AIEP.Application.Api.Blueprint --no-launch-profile    # sin perfil, el entorno por defecto es Production
curl -i http://127.0.0.1:5199/swagger/index.html                           # 200
curl -i "http://127.0.0.1:5199/api/v1/recurso?codigo=A"                    # 401 «La Api Key no fue provista…»
curl -i -H "x-xmsbs-apikey: clave-de-prueba" "http://127.0.0.1:5199/api/v1/recurso?codigo=A"   # 401 + WWW-Authenticate: Bearer
```
Sin token real de Azure AD no se llega a la acción; en sandboxes no hay Oracle ni SQL Server (los tests usan cadenas ficticias y nunca abren conexión).

## 9. Recetas

**9.1 Feature `X` (copia el corte `Recurso`):** `Domain/Entities/XEntity.cs` (`[Column("UPPER_SNAKE")]`; navegación sin `[Column]`) · `Model/Models/X/X.cs` y `XRequest.cs` (`[JsonPropertyName]`; DataAnnotations) · `Repository/Repository/X/IXRepository.cs` y `XRepository.cs` (hereda de `BaseRepository`) y su registro en `ServiceCollectionExtensions.cs` · `Logic/Logic/XLogic.cs` (ctor `(IXRepository, IMapper, ILogger<XLogic>)`) · `Api/Mapper/MapperConfig.cs` (`CreateMap` con el tipo anidado primero, `.ReverseMap()`; **todo `Profile` va en el proyecto API**) · `Api/Controllers/v1/XController.cs` (atributos de §6 + documentación de §7) · `Program.cs` (`AddScoped<XLogic>()`) · `AIEP.Test/Logic/XLogicTests.cs` (`LogicTestBase<XLogic, MapperConfig, IXRepository>`) · SP en `db/`. Registro del repositorio: con **una conexión fija**, ctor con `[FromKeyedServices("<Nombre>")] IDbConnectionFactory` y `AddScoped<IXRepository, XRepository>()`; si debe cambiar de motor por ambiente, replica `Conf:RecursoConnectionName` y `BuildRecursoRepository`.

**9.2 Nueva conexión (p. ej. `CostosDb`):** (1) bloque en `Database:Connections` con `ConnectionStringName: "CostosDb"`; (2) secreto `blueprint-costosdb-connectionstring` en el vault + acceso de lectura para la identidad del servicio; (3) `[FromKeyedServices("CostosDb")] IDbConnectionFactory` en el repositorio (si es la segunda de una operación, usa las sobrecargas explícitas de `BaseRepository`). Las claves keyed distinguen mayúsculas: deben coincidir en configuración, atributo y registro.

**9.3 Probar `Recurso` en el otro motor:** `Conf__RecursoConnectionName=SqlServerDb` **y** `Conf__StoredProcedureName=dbo.SP_GET_RECURSO` (§14.2). Dos controllers independientes por motor: copia `RecursoController` apuntando a `RecursoLogicOracle`/`RecursoLogicSqlServer`; con un solo motor, bórralos con sus tests y registros.

**9.4 Servicio nuevo desde el Blueprint.** Pide nombre (`MiServicio`), entidades, motor(es), SP, scope y tenants; no inventes IDs ni recursos Azure. (1) Reemplazo **sensible a mayúsculas** `Blueprint` → `MiServicio` en `*.cs *.csproj *.sln *.json *.yml Dockerfile` sin `bin/`/`obj/`; el snippet PowerShell del README usa `-replace`, que **no** distingue mayúsculas: usa `-creplace`. (2) Renombra carpetas, proyectos y `.sln` (`AIEP.Application.Api.*`, `AIEP.Domain.Data*`; conserva los GUID; `Logic/Repository/Model` no cambian) y ajusta `Dockerfile` y `azure-pipelines.yml`. (3) A mano, porque el reemplazo no los toca: `UserSecretsId` del `.csproj` (`aiep-blueprint-api`: uno único, o dos servicios compartirían `user-secrets`), prefijo `blueprint-` (`BlueprintKeyVaultSecretManager.Prefix` y los nombres en el vault), `imageRepository`/`environment` del pipeline, `PKG_BLUEPRINT` (SQL, `appsettings.json`, `RecursoRepository`), `blueprint_test` en los tests. (4) Registro de aplicación propio en Entra ID con scope `IntegrationService.MiServicio.Read`; ajusta `AzureAd`, el scope del controller, `Conf:ListTenantId`, `Cors:AllowedOrigins`, Swagger y `Conf:KeyVaultUri` por ambiente. (5) Reemplaza o borra `Recurso*` (y `RecursoLogicOracle/SqlServer`, tests y SP de `db/`); ajusta `Database:Connections` y `Conf:RecursoConnectionName`. (6) `dotnet build`, `dotnet test` y `grep -rIn "Blueprint\|blueprint\|BLUEPRINT\|Recurso"` para restos. (7) Reescribe este `AGENTS.md` y el `README.md` para el servicio (conserva §3–§11).

## 10. Convenciones observadas
- **Nombres:** PascalCase, dominio en español (`Recurso`, `Codigo`); `I*Repository`, `*Repository`, `*Logic`, `*Entity`, `*Options`; métodos de repositorio con sufijo `Async`; tests `Metodo_Condicion_Resultado` en español.
- **Estilo:** imita el archivo vecino (hay namespaces de bloque y *file-scoped*). Comentarios y `///` en español, mayormente sin tildes en `.cs`; los mensajes a usuario o log sí llevan tildes. `CancellationToken cancellationToken = default` de controller a repositorio; `ConfigureAwait(false)` en Repository.
- **Logging:** plantillas con parámetros nombrados, **nunca** interpolación; no loguees secretos, tokens ni cadenas; RUT/documento son datos personales.
- **JSON:** los DTO de salida usan `[JsonPropertyName]` con etiquetas legibles (`"Codigo Recurso"`): estilo institucional, **no lo «normalices»**. Los de request usan propiedades en minúscula (`codigo`), los nombres del query string. Nunca expongas una `*Entity`.
- **SP:** `PKG_<X>.P_GET_<X>` (Oracle) / `dbo.SP_GET_<X>` (SQL Server); parámetros `P_*`, cursores `C_*`, salida `P_CODIGO`/`P_MENSAJE`.
- **Git:** `.gitattributes` normaliza a LF (`.sln` en CRLF); no conviertas finales de línea. Commits en español, una línea resumen.

## 11. Pruebas
NUnit 3.14 + Moq con **sintaxis clásica** (`Assert.AreEqual`; `NUnit2005/2031` suprimidos a propósito por coherencia institucional). `LogicTestBase<TLogic, TProfile, TRepo>` arma `IConfiguration` desde `Settings/appsettings.json`, un `IMapper` real y mocks, e instancia la lógica con `Activator.CreateInstance(repo, mapper, logger)`: el constructor debe ser `(IXRepository, IMapper, ILogger<XLogic>)`. Mapeo: `DataTable.CreateDataReader()` (sin BD). Fábrica y DI: `AddInMemoryCollection` + `GetRequiredKeyedService`. **Sin tests hoy** (si los tocas, agrégalos): `ApiKeyMiddleware`, `ExceptionHandler`, `RequireClaimFilter`, `BlueprintKeyVaultSecretManager`, `AzureCredentialFactory`, `ThrowIfSpError`, `RecursoRepository.GetRecursosAsync`, `RecursoController` e integración (falta `Microsoft.AspNetCore.Mvc.Testing`; `Program` ya es `public partial class`).

## 12. Infraestructura y despliegue (alto nivel; sin IPs, VMs ni puertos)
```
Internet -> Cloudflare (services{dev|qa|prod}.aiep.cl, TLS/WAF) -> Azure Application Gateway (VNet Hub-Integración)
  -> VNet por entorno: Azure Data Factory + API Management -> VPN Site-to-Site
  -> servidor on-premise del entorno: balanceador + API Gateway autoalojado + IIS + SQL Server + Self-Hosted Integration Runtime
  -> datos: SQL Server on-premise · Oracle en OCI (par primaria/réplica; DESA/Test/QA comparten par, PROD tiene el suyo)
```
**Publicar un servicio** (guías del equipo de Integración/Infraestructura; el `.docx` descargable del manual es la referencia autoritativa): (1) registro de aplicación en Entra ID (secreto, *Expose an API* con el scope, permisos delegados); (2) scripts de BD (esquema/usuario Banner, con autorizaciones previas); (3) **IIS** (copiar el paquete, editar `appsettings.json`, reiniciar el sitio); (4) **API Management** (importar el `swagger.json`); (5) **Application Gateway** (regla del ambiente → nuevo *backend target*). Un agente edita código, tests y docs y corre build, tests y la API local con configuración de prueba; **no** toca Key Vault ni BD reales ni Azure, IIS, APIM, Application Gateway o PROD. Dockerfile y pipeline son de referencia: los servicios actuales van en IIS.

## 13. Documentación: cuál manda
| Archivo | Vigencia |
|---|---|
| código (`Program.cs`, `appsettings*.json`, `.csproj`) | **Manda** |
| `README.md` | Vigente (errores abajo) |
| `docs/manual-blueprint 4.html` (manual con tutorial y guías; las capturas `assets/…` no están en el repo) | Vigente **si** habla de `net10.0`, Key Vault directo y `Database:Connections`; si menciona App Configuration o `net8.0`, es copia vieja |
| `docs/Lineamiento_desarrollo 1.pptx`, `db/**` | Vigentes (normativo / contrato de SP) |
| `docs/MANUAL-DESARROLLADOR.md` (aún rotulado «canónico») | **Desactualizado:** `net8.0`, App Configuration, `Database:Provider` único, `BaseRepository(factory, dbOptions, logger)`, `AddAutoMapper` sin licencia. Siguen válidos: error → HTTP, logging, convenciones |
| `docs/Manejo de conexiones BD con Vault.docx` | Obsoleto el paso de App Configuration; vale `az keyvault secret set` |
| `docs/manual-blueprint_old.html`, `docs/Lineamiento_desarrollo.pptx/.zip` | Históricos: no los uses |

**Errores conocidos:** (a) la fila «Secretos» de la tabla comparativa del README describe un diseño intermedio; manda su sección «Configuración y secretos». (b) «Ejecutar en local» del README sugiere valores reales en `appsettings.Development.json` (versionado): prefiere `user-secrets`. (c) Los manuales dicen `IMapper` Singleton: es Transient. (d) La cabecera del HTML declara «canónico» a `MANUAL-DESARROLLADOR.md`, que está desactualizado. (e) Ese `.md` trae una API key de ejemplo con aspecto real: no la reproduzcas. **Si editas el manual HTML:** reemplazos exactos, conserva CSS, estructura y navegación, valida el balance de etiquetas y verifica cada fragmento contra el repo.

## 14. Trampas conocidas (verificadas)
1. **CORS abierto de hecho:** con `AllowedHosts = "*"` (valor actual) `Program.cs` aplica `AllowAnyOrigin` e **ignora `Cors:AllowedOrigins`** (comprobado: `Access-Control-Allow-Origin: *`). `AllowedHosts` además filtra el header `Host` de ASP.NET Core.
2. **SP único para todos los motores:** `Development` usa `SqlServerDb`, pero `Conf:StoredProcedureName` sigue en `PKG_BLUEPRINT.P_GET_RECURSO` (Oracle) y el script de SQL Server crea `dbo.SP_GET_RECURSO`. Defínelo al probar SQL Server (`AIEP.Test/Settings/appsettings.json` ya usa `dbo.SP_GET_RECURSO`).
3. **Tenants en minúsculas:** `RequireClaimFilter` compara `tid.ToLowerInvariant()` con `List.Contains` (sensible a mayúsculas); un GUID en mayúsculas en `Conf:ListTenantId` nunca coincide.
4. **`[RequiredScope]` solo acepta tokens delegados (`scp`):** un consumidor *client credentials* trae `roles` y sería rechazado; requeriría un App Role y `RequiredScopeOrAppPermission`.
5. **`ExceptionHandler` es por acción, no global:** sin el `[TypeFilter]` no traduce nada; tampoco mapea `RequiredParametersException` (plural) ni `NotFoundException<T>` con `T` ≠ `Guid`/`string`: terminan en 500.
6. **Nombres de conexión acoplados:** los keyed `IRecursoRepository` están registrados para `"OracleDb"` y `"SqlServerDb"`; renombrarlos exige tocar `ServiceCollectionExtensions`, `RecursoLogicOracle/SqlServer`, tests y secretos.
7. **Fallos tardíos:** una conexión sin cadena se omite en silencio al arrancar y una cadena mal escrita no se detecta hasta la primera consulta.
8. **API key** sin distinguir mayúsculas y cargada una sola vez; **Swagger** habilitado en todos los entornos y sin API key.
9. **Identificadores internos en git** (`appsettings.json`: tenant, client id, vault de DESA, licencia): asume que lo versionado puede ser visible fuera de AIEP; no agregues más ni los copies a docs, issues o logs.

## 15. Qué no hacer
Además de §0: no pongas valores reales (cadenas, keys, `ClientSecret`, tokens, IPs, nombres de VM, IDs ajenos) en código, config versionada, tests, docs ni chats · no leas `IConfiguration` crudo en Logic ni controllers (usa `IOptions*`) · no cambies `global.json`, los GUID del `.sln`, el pin de `System.Security.Cryptography.Pkcs` (advisories) ni la versión de AutoMapper (licencia) sin revisar · no «modernices» por tu cuenta decisiones de diseño (§1, §13): propónlas a los mantenedores · no ejecutes nada contra Azure, BD o servidores reales ni despliegues a QA/PROD · no edites binarios de `docs/` salvo petición explícita · no dejes `bin/`, `obj/`, `Logs/` ni temporales en el repo.

## 16. Antes de entregar
1. `dotnet build` sin errores ni warnings nuevos; `dotnet test` verde (32 + los tuyos). 2. Tests para lo nuevo; en un endpoint: atributos de §6, `[JsonPropertyName]`, `CreateMap` y documentación Swagger (§7). 3. Sin secretos ni identificadores reales. 4. Si cambiaste configuración, secretos, DI, capas o comandos: actualiza `README.md`, el manual HTML y **este archivo** (fecha y commit del encabezado incluidos). 5. Commit en español.
