-- =====================================================================
--  Stored procedure de ejemplo (SQL Server) para la feature "Recurso" del blueprint.
--  Contrato de result sets (ORDEN CANONICO, igual que en Oracle):
--    #1  SELECT : CODIGO_RECURSO, NOMBRE_RECURSO, ACTIVO('Y'/'N' o 1/0), CUPOS, FECHA_ALTA, VALOR
--    #2  SELECT : CODIGO_RECURSO, CODIGO_DETALLE, DESCRIPCION
--  Parametros escalares de salida:
--    @P_CODIGO  : 'OK' en exito; codigo de error ('400...'/'404...'/otro) en caso contrario
--    @P_MENSAJE : descripcion del error
-- =====================================================================

CREATE OR ALTER PROCEDURE dbo.SP_GET_RECURSO
    @P_CODE     VARCHAR(20),
    @P_CODIGO   VARCHAR(50)  OUTPUT,
    @P_MENSAJE  VARCHAR(500) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF @P_CODE IS NULL OR LEN(@P_CODE) = 0
    BEGIN
        SET @P_CODIGO  = '400';
        SET @P_MENSAJE = 'El parametro @P_CODE es requerido.';
        SELECT TOP (0) CAST(NULL AS VARCHAR(20)) AS CODIGO_RECURSO;
        SELECT TOP (0) CAST(NULL AS VARCHAR(20)) AS CODIGO_RECURSO;
        RETURN;
    END

    BEGIN TRY
        -- Result set #1
        SELECT r.CODIGO_RECURSO,
               r.NOMBRE_RECURSO,
               r.ACTIVO,            -- 'Y' / 'N'  (o BIT 1 / 0)
               r.CUPOS,
               r.FECHA_ALTA,
               r.VALOR
          FROM dbo.RECURSO r
         WHERE r.CODIGO_RECURSO = @P_CODE;

        -- Result set #2
        SELECT d.CODIGO_RECURSO,
               d.CODIGO_DETALLE,
               d.DESCRIPCION
          FROM dbo.RECURSO_DETALLE d
         WHERE d.CODIGO_RECURSO = @P_CODE;

        SET @P_CODIGO  = 'OK';
        SET @P_MENSAJE = NULL;
    END TRY
    BEGIN CATCH
        SET @P_CODIGO  = 'ERR';
        SET @P_MENSAJE = ERROR_MESSAGE();
    END CATCH
END
GO
