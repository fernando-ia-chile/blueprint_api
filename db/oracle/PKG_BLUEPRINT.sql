-- =====================================================================
--  Stored procedure de ejemplo (Oracle) para la feature "Recurso" del blueprint.
--  Contrato de result sets (ORDEN CANONICO, respetar tambien en SQL Server):
--    #1  C_RECURSOS : CODIGO_RECURSO, NOMBRE_RECURSO, ACTIVO('Y'/'N'), CUPOS, FECHA_ALTA, VALOR
--    #2  C_DETALLE  : CODIGO_RECURSO, CODIGO_DETALLE, DESCRIPCION
--  Parametros escalares de salida:
--    P_CODIGO  : 'OK' en exito; codigo de error en caso contrario ('400...'/'404...'/otro)
--    P_MENSAJE : descripcion del error
-- =====================================================================

CREATE OR REPLACE PACKAGE PKG_BLUEPRINT AS
    PROCEDURE P_GET_RECURSO(
        P_CODE      IN  VARCHAR2,
        C_RECURSOS  OUT SYS_REFCURSOR,
        C_DETALLE   OUT SYS_REFCURSOR,
        P_CODIGO    OUT VARCHAR2,
        P_MENSAJE   OUT VARCHAR2
    );
END PKG_BLUEPRINT;
/

CREATE OR REPLACE PACKAGE BODY PKG_BLUEPRINT AS

    PROCEDURE P_GET_RECURSO(
        P_CODE      IN  VARCHAR2,
        C_RECURSOS  OUT SYS_REFCURSOR,
        C_DETALLE   OUT SYS_REFCURSOR,
        P_CODIGO    OUT VARCHAR2,
        P_MENSAJE   OUT VARCHAR2
    ) IS
    BEGIN
        IF P_CODE IS NULL THEN
            P_CODIGO  := '400';
            P_MENSAJE := 'El parametro P_CODE es requerido.';
            OPEN C_RECURSOS FOR SELECT NULL FROM DUAL WHERE 1 = 0;
            OPEN C_DETALLE  FOR SELECT NULL FROM DUAL WHERE 1 = 0;
            RETURN;
        END IF;

        OPEN C_RECURSOS FOR
            SELECT r.CODIGO_RECURSO,
                   r.NOMBRE_RECURSO,
                   r.ACTIVO,          -- 'Y' / 'N'
                   r.CUPOS,
                   r.FECHA_ALTA,
                   r.VALOR
              FROM RECURSO r
             WHERE r.CODIGO_RECURSO = P_CODE;

        OPEN C_DETALLE FOR
            SELECT d.CODIGO_RECURSO,
                   d.CODIGO_DETALLE,
                   d.DESCRIPCION
              FROM RECURSO_DETALLE d
             WHERE d.CODIGO_RECURSO = P_CODE;

        P_CODIGO  := 'OK';
        P_MENSAJE := NULL;
    EXCEPTION
        WHEN OTHERS THEN
            P_CODIGO  := 'ERR';
            P_MENSAJE := SUBSTR(SQLERRM, 1, 4000);
    END P_GET_RECURSO;

END PKG_BLUEPRINT;
/
