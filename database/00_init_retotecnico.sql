IF DB_ID(N'retotecnico') IS NULL
BEGIN
    CREATE DATABASE retotecnico;
END
GO

USE retotecnico;
GO

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Username NVARCHAR(100) NOT NULL UNIQUE,
        PasswordHash NVARCHAR(500) NOT NULL,
        Email NVARCHAR(255) NOT NULL,
        Roles NVARCHAR(255) NOT NULL CONSTRAINT DF_Users_Roles DEFAULT N'User',
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT GETDATE()
    );
END
GO

IF OBJECT_ID(N'dbo.CargaArchivo', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CargaArchivo (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Periodo NVARCHAR(7) NOT NULL,
        RutaArchivo NVARCHAR(1000) NOT NULL,
        NombreArchivo NVARCHAR(255) NOT NULL,
        Usuario NVARCHAR(255) NULL,
        Estado NVARCHAR(50) NOT NULL,
        FechaRegistro DATETIME NOT NULL CONSTRAINT DF_CargaArchivo_FechaRegistro DEFAULT GETDATE(),
        FechaFin DATETIME NULL
    );
END
GO

IF COL_LENGTH(N'dbo.CargaArchivo', N'FechaFin') IS NULL
BEGIN
    ALTER TABLE dbo.CargaArchivo ADD FechaFin DATETIME NULL;
END
GO

IF OBJECT_ID(N'dbo.DataProcesada', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DataProcesada (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        CargaArchivoId INT NOT NULL,
        CodigoProducto NVARCHAR(255) NOT NULL,
        FilaExcel INT NOT NULL,
        Descripcion NVARCHAR(1000) NULL,
        Monto DECIMAL(18,2) NOT NULL,
        FechaTransaccion DATETIME NULL,
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_DataProcesada_CreatedAt DEFAULT GETDATE(),
        CONSTRAINT FK_DataProcesada_CargaArchivo FOREIGN KEY (CargaArchivoId)
            REFERENCES dbo.CargaArchivo(Id)
    );
END
GO

IF OBJECT_ID(N'dbo.CargaAuditoria', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CargaAuditoria (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Periodo NVARCHAR(7) NOT NULL,
        NombreArchivo NVARCHAR(255) NULL,
        Usuario NVARCHAR(255) NULL,
        Resultado NVARCHAR(20) NOT NULL,
        Motivo NVARCHAR(500) NOT NULL,
        CargaArchivoIdReferencia INT NULL,
        EstadoEncontrado NVARCHAR(50) NULL,
        FechaIntento DATETIME NOT NULL CONSTRAINT DF_CargaAuditoria_FechaIntento DEFAULT GETDATE(),
        CONSTRAINT FK_CargaAuditoria_CargaArchivo FOREIGN KEY (CargaArchivoIdReferencia)
            REFERENCES dbo.CargaArchivo(Id) 
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_CargaArchivo_Periodo_Estado'
        AND object_id = OBJECT_ID(N'dbo.CargaArchivo')
)
BEGIN
    CREATE INDEX IX_CargaArchivo_Periodo_Estado ON dbo.CargaArchivo (Periodo, Estado);
END
GO
