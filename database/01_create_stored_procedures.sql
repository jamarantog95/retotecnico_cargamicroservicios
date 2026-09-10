USE retotecnico;
GO

CREATE OR ALTER PROCEDURE dbo.sp_ValidarDuplicidadPeriodo
    @Periodo NVARCHAR(7),
    @ResultadoAccion NVARCHAR(50) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @ResultadoAccion = N'PERMITIDO';

    IF EXISTS (
        SELECT 1
        FROM dbo.CargaArchivo
        WHERE Periodo = @Periodo
            AND Estado IN (N'Pendiente', N'En Proceso')
    )
    BEGIN
        SET @ResultadoAccion = N'BLOQUEADO';
        RETURN;
    END;

    IF EXISTS (
        SELECT 1
        FROM dbo.CargaArchivo
        WHERE Periodo = @Periodo
            AND Estado IN (N'Finalizado', N'Notificado')
    )
    BEGIN
        SET @ResultadoAccion = N'RECHAZADO';
    END;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_RegistrarCargaArchivo
    @NombreArchivo NVARCHAR(255),
    @RutaArchivo NVARCHAR(1000),
    @Usuario NVARCHAR(255),
    @Periodo NVARCHAR(7),
    @NuevoId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.CargaArchivo (Periodo, RutaArchivo, NombreArchivo, Usuario, Estado)
    VALUES (@Periodo, @RutaArchivo, @NombreArchivo, @Usuario, N'Pendiente');

    SET @NuevoId = CONVERT(INT, SCOPE_IDENTITY());
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_InsertarCargaAuditoria
    @Periodo NVARCHAR(7),
    @NombreArchivo NVARCHAR(255),
    @Usuario NVARCHAR(255),
    @Resultado NVARCHAR(20),
    @Motivo NVARCHAR(500),
    @CargaArchivoId INT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.CargaAuditoria (
        Periodo,
        NombreArchivo,
        Usuario,
        Resultado,
        Motivo,
        CargaArchivoIdReferencia,
        FechaIntento
    )
    VALUES (
        @Periodo,
        @NombreArchivo,
        @Usuario,
        @Resultado,
        @Motivo,
        @CargaArchivoId,
        GETDATE()
    );
END
GO
