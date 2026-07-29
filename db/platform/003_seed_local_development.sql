USE MicroEMR_Platform;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.Tenant
    WHERE TenantKey = N'local-dev'
)
BEGIN
    INSERT INTO dbo.Tenant
    (
        TenantUid,
        TenantKey,
        DisplayName,
        TenantStatus,
        DefaultTimeZoneId,
        CreatedAt,
        ActivatedAt
    )
    VALUES
    (
        NEWID(),
        N'local-dev',
        N'Local Development Clinic',
        'Active',
        N'America/Toronto',
        SYSUTCDATETIME(),
        SYSUTCDATETIME()
    );
END;

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.TenantDatabase AS tenantDatabase
    INNER JOIN dbo.Tenant AS tenant
        ON tenant.TenantUid = tenantDatabase.TenantUid
    WHERE tenant.TenantKey = N'local-dev'
)
BEGIN
    INSERT INTO dbo.TenantDatabase
    (
        TenantUid,
        DatabaseServerKey,
        DatabaseName,
        SecretReference,
        DatabaseStatus,
        CreatedAt
    )
    SELECT
        tenant.TenantUid,
        N'local-sql',
        N'MicroEMR_Db',
        N'development:MicroEMR_Db',
        'Active',
        SYSUTCDATETIME()
    FROM dbo.Tenant AS tenant
    WHERE tenant.TenantKey = N'local-dev';
END;
GO
