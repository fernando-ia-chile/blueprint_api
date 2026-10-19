# HANDOFF — AIEP Blueprint

> Estado del proyecto al **2026-10-16**. Léelo antes de retomar trabajo en este repo, propio o con Claude.

## Qué es

Solución de referencia .NET 8 (`ApiBlueprint.sln`) para arrancar nuevos microservicios API de AIEP.
Se clona y se renombra `Blueprint` → `MiServicio` (pasos completos en `README.md`). Carpeta local:
`C:\Users\fvald\OneDrive\Escritorio\AIEP\AIEP Autenticacion\AIEP-Servicios-Blueprint`.

Build verde, 12/12 tests, imagen Docker verificada, pipeline de referencia incluido.

## Repositorio remoto

- Azure DevOps: `https://aiepdevops.visualstudio.com/AIEP-PORTAFOLIO-SERVICIOAPI/_git/AIEP.ApiBluePrint` (remote `origin`)
- Ramas `main` y `Develop`, ambas en un solo commit: `a3efacb` — "Blueprint inicial: microservicio API .NET 8"
- **Sin políticas de rama configuradas todavía** (pendiente: PR obligatorio + build de validación con el `azure-pipelines.yml` ya incluido)

## ⚠️ Cambios sin commitear (lo primero a resolver)

`git status` en la raíz del repo muestra:

| Archivo | Qué cambió |
|---|---|
| `AIEP.Application.Api.Blueprint/appsettings.json` | `Database:ConnectionStringName` → `"OracleDb"` (antes `"BlueprintDb"`) |
| `AIEP.Application.Api.Blueprint/appsettings.Development.json` | `Database:ConnectionStringName` → `"SqlServerDb"` |
| `docs/manual-blueprint.html` | Reskin institucional completo (596 líneas de diff) — ver detalle abajo |
| `docs/Lineamiento_desarrollo.pptx` | **sin trackear**, 504 KB — es el archivo que referencia el link de descarga del manual |
| `docs/Lineamiento_desarrollo.zip` | **sin trackear**, 619 KB — apareció en el filesystem, no lo creó Claude; decidir si se queda, se borra o es duplicado del pptx |

Ningún archivo versionado lleva secretos (contraseñas, connection strings completas). Los valores
reales de `ConnectionStrings:OracleDb` y `ConnectionStrings:SqlServerDb` (Oracle QA y SQL Server de
Desarrollo, `MatriculaAIEP`) están solo en `dotnet user-secrets` local — fuera del repo. **No se
subieron a Key Vault todavía**: la sesión de `az` expiró (`AADSTS50173`, contraseña reseteada el
24-sep-2026); quedó pendiente volver a autenticar (`az login --tenant dc09f18d-6ac3-4ef0-a01e-66525ed9a4d0`)
y crear los secretos `blueprint-oracledb-connectionstring` / `blueprint-sqlserverdb-connectionstring`
en el vault `keyvault-aiep-desa`, más las referencias `Blueprint:ConnectionStrings:OracleDb` /
`:SqlServerDb` en Azure App Configuration.

**Próximo paso lógico: confirmar si se comitea y pushea todo esto a `Develop` → `main`, y qué hacer
con el `.zip`.**

### Qué trae el reskin de `docs/manual-blueprint.html`

- Paleta y tipografía institucional real (skill `diseno-front-aiep`: azules `secondary-blue-2` /
  `secondary-cyan-100`, Ubuntu + Verdana), header/footer navy (`#002548` / `#182844`) con el logo
  AIEP real embebido (`img/logo_aiep_dark.svg`).
- Sección nueva **"Infraestructura (middleware)"**: resumen de alto nivel del diagrama
  `MDLW.pptx.png` — Cloudflare → Application Gateway → VNet peered por entorno → API Management,
  entornos Desarrollo/QA/Producción. **A propósito sin IPs, nombres de VM ni puertos.**
- Dos chips-botón en la cabecera que saltan directo a esa sección (QA / Desarrollo).
- Link de descarga de `docs/Lineamiento_desarrollo.pptx` al pie de la barra lateral.
- Republicado en el artefacto de Claude: `https://claude.ai/code/artifact/d399c2f7-e700-4b36-9d31-59a3bcd6ddec`
  (privado; el watch se cayó por conexión — si se vuelve a editar, republicar con el mismo archivo
  para mantener la misma URL).

## Documentación del proyecto

- `README.md` — qué es, renombrado `Blueprint → MiServicio`, provisión de Azure App
  Configuration/Key Vault y roles RBAC (incluye Azure Arc on-prem).
- `docs/MANUAL-DESARROLLADOR.md` — manual canónico (Markdown): arquitectura, `appsettings`,
  entidades, modelos, mapper, repositorio Oracle/SQL Server, endpoints, errores, seguridad,
  logging, pruebas, tutorial end-to-end (feature `Alumno`, ERP Banner). **No tiene todavía** la
  sección de Infraestructura que sí se agregó al HTML — pendiente portarla si se quiere mantener
  ambos documentos en paridad.
- `docs/manual-blueprint.html` — misma guía, versión navegable con diseño institucional.
- `db/README.md` + `db/oracle/PKG_BLUEPRINT.sql` + `db/sqlserver/SP_GET_RECURSO.sql` — contrato de
  los stored procedures de ejemplo.

## Arquitectura y decisiones clave

- Capas: `AIEP.Application.Api.* → AIEP.Application.Logic → {AIEP.Application.Model, AIEP.Application.Repository}`;
  `Repository → {AIEP.Domain.Data*, AIEP.Application.Model}`. `AIEP.Test` referencia a todos.
- Librerías: NLog, Microsoft.Identity.Web (JWT Azure AD), Microsoft.AspNetCore.Mvc.Versioning,
  Swashbuckle, AutoMapper 12.0.1 (paridad con Sedes pese al advisory NU1903 — decisión consciente).
  Clases `*Logic` concretas, sin interfaz.
- **Repository sin ORM**: `IDbConnectionFactory` + `BaseRepository` + `DbReaderMapper` (mapea
  `[Column]` por reflexión con caché estática de setters compilados; conserva `"Y"/"N"` y `1/0` →
  bool). Async real + `CancellationToken`.
- **Oracle y SQL Server** intercambiables por `Database:Provider`; los casts a drivers concretos
  viven solo en `OracleProviderAdapter` / `SqlServerProviderAdapter`. El RefCursor de Oracle se
  agrega ANTES que los parámetros OUT escalares.
- **Más de una conexión de BD simultánea** (si hace falta): patrón recomendado —
  `services.AddOptions<DatabaseOptions>("Nombre")` + `AddKeyedSingleton<IDbConnectionFactory>("Nombre", ...)`
  + inyectar `[FromKeyedServices("Nombre")] IDbConnectionFactory` en el repositorio nuevo. No
  implementado como ejemplo real todavía, solo documentado/explicado.
- **Sin secretos en archivos**: Azure App Configuration (prefijo `Blueprint:`, label por entorno) +
  Key Vault vía `DefaultAzureCredential` (Managed Identity / Azure Arc on-prem). Roles:
  `App Configuration Data Reader` + `Key Vault Secrets User` (runtime); `Key Vault Secrets Officer`
  para quien escribe secretos. Vault real: `keyvault-aiep-desa`, tenant AIEP
  `dc09f18d-6ac3-4ef0-a01e-66525ed9a4d0`.
- `IOptions` tipado (`DatabaseOptions`, `ApiKeyOptions`, `TenantOptions`) en vez de `IConfiguration` crudo.
- `global.json` fija SDK 8.0.x (SDK 10 genera `.slnx`; se necesita `.sln` clásico para VS2022).
- Tests: NUnit 3.14 (sintaxis clásica `Assert.AreEqual`); se descartó la mezcla NUnit+xUnit de Sedes.
- Extras: `Dockerfile` multi-stage (usuario no-root), `.dockerignore`, `azure-pipelines.yml`.
- Feature vertical de ejemplo: `Recurso` (scope `IntegrationService.Blueprint.Read`, SP
  `Conf:StoredProcedureName`, 2 result sets: recursos + detalle).

### Autenticación

OAuth2/OIDC Bearer contra Microsoft Entra ID vía `AddMicrosoftIdentityWebApiAuthentication` — la
API es *resource server puro* (nunca adquiere tokens; `AzureAd` en config nunca lleva
`ClientSecret`). Autorización en 3 capas:

1. `[RequiredScope]` — claim `scp`, requiere el scope expuesto `IntegrationService.<Servicio>.Read`
   + admin consent en la app consumidora.
2. `RequireClaimFilter` — claim `tid` contra un allow-list (`Conf:ListTenantId`).
3. `ApiKeyMiddleware` — header `x-xmsbs-apikey`, capa no-OAuth, valor real en Key Vault.

**Importante:** `[RequiredScope]` valida `scp` (permisos delegados). Si un consumidor nuevo llama
por **Client Credentials** (machine-to-machine, sin usuario), su token trae `roles` en vez de `scp`
y hoy sería rechazado — requeriría exponer un App Role y cambiar a `RequireScopeOrAppPermission`.

## Pendientes / próximos pasos

1. Confirmar si se comitea y pushea el trabajo pendiente (appsettings + `manual-blueprint.html`) a
   `Develop` → `main`.
2. Decidir qué hacer con `docs/Lineamiento_desarrollo.pptx` (agregarlo a git) y
   `docs/Lineamiento_desarrollo.zip` (¿descartar?).
3. Cuando se renueve `az login`: crear los secretos de Oracle/SQL Server en `keyvault-aiep-desa` y
   sus referencias en Azure App Configuration (comandos ya explicados, no ejecutados).
4. Portar la sección "Infraestructura" del HTML a `docs/MANUAL-DESARROLLADOR.md` si se quiere
   mantener ambos documentos en paridad.
5. Configurar políticas de rama en Azure DevOps (PR + build de validación).
6. Confirmar si el correo al equipo (objetivo, manuales, repo, cómo empezar) ya se envió.

---
Plan de implementación original (fases, diseño detallado): `C:\Users\fvald\.claude\plans\tengo-como-misi-n-un-humble-dusk.md`.
