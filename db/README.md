# Objetos de base de datos (ejemplo)

Estos scripts son **documentales**: muestran el contrato que la capa de repositorio
(`RecursoRepository`) espera del stored procedure. No se ejecutan desde la aplicacion.

## Contrato de la feature `Recurso`

| # | Result set | Columnas |
|---|------------|----------|
| 1 | Recursos | `CODIGO_RECURSO`, `NOMBRE_RECURSO`, `ACTIVO`, `CUPOS`, `FECHA_ALTA`, `VALOR` |
| 2 | Detalle  | `CODIGO_RECURSO`, `CODIGO_DETALLE`, `DESCRIPCION` |

Parametros escalares de salida (ambos motores):

| Parametro | Uso |
|-----------|-----|
| `P_CODIGO` / `@P_CODIGO` | `OK` = exito. Prefijo `400` → `DataValidationException` (HTTP 400); prefijo `404` → `NotFoundException` (HTTP 404); cualquier otro valor → `BusinessException` (HTTP 422). |
| `P_MENSAJE` / `@P_MENSAJE` | Descripcion del error. |

### Reglas

- El **orden de los result sets es canonico** y debe ser identico en Oracle y SQL Server.
  En Oracle, los `OUT SYS_REFCURSOR` se agregan como parametros **antes** que `P_CODIGO`/`P_MENSAJE`
  y en ese mismo orden.
- `ACTIVO` admite `'Y'`/`'N'` o `1`/`0`; el `DbReaderMapper` lo convierte a `bool`.
- Las columnas se mapean por nombre (sin distinguir mayusculas) contra el atributo
  `[Column("...")]` de las entidades en `AIEP.Domain.DataBlueprint/Entities`.

## Archivos

- `oracle/PKG_BLUEPRINT.sql` — paquete `PKG_BLUEPRINT` con `P_GET_RECURSO`.
- `sqlserver/SP_GET_RECURSO.sql` — procedimiento `dbo.SP_GET_RECURSO`.

El nombre del SP se resuelve desde `Conf:StoredProcedureName` en la configuracion.
