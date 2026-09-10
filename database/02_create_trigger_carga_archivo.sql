USE retotecnico;
GO

CREATE OR ALTER TRIGGER dbo.TR_CargaArchivo_SetFechaFin
ON dbo.CargaArchivo
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE ca
    SET FechaFin = GETDATE()
    FROM dbo.CargaArchivo AS ca
    INNER JOIN inserted AS i
        ON i.Id = ca.Id
    INNER JOIN deleted AS d
        ON d.Id = i.Id
    WHERE i.Estado = N'Notificado'
        AND ISNULL(d.Estado, N'') <> N'Notificado'
        AND ca.FechaFin IS NULL;
END;
GO
