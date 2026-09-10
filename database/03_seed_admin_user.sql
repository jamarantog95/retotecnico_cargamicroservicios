USE retotecnico;
GO

IF NOT EXISTS (
    SELECT 1 FROM dbo.Users WHERE Username = 'jamarantog95u@gmail.com'
)
BEGIN
    INSERT INTO dbo.Users ([Username], [PasswordHash], [Email], [Roles], [CreatedAt])
    VALUES (
        'jamarantog95u@gmail.com', -- Username
        '$2a$12$sS/4HQySftrInHxf4S3AV.uo2o1e6pD3GKIVcIWvmtcqGBnohECme', -- PasswordHash
        'jamarantog95u@gmail.com', -- Email
        'Admin', -- Roles
        GETDATE() -- CreatedAt
    );
END
GO
