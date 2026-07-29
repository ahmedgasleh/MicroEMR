USE MicroEMR_Platform;
GO

CREATE OR ALTER PROCEDURE dbo.Tenant_GetByUid
    @TenantUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        TenantUid,
        TenantKey,
        DisplayName,
        TenantStatus,
        DefaultTimeZoneId,
        CreatedAt,
        ActivatedAt,
        SuspendedAt
    FROM dbo.Tenant
    WHERE TenantUid = @TenantUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.Tenant_GetByKey
    @TenantKey NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        TenantUid,
        TenantKey,
        DisplayName,
        TenantStatus,
        DefaultTimeZoneId,
        CreatedAt,
        ActivatedAt,
        SuspendedAt
    FROM dbo.Tenant
    WHERE TenantKey = @TenantKey;
END;
GO

CREATE OR ALTER PROCEDURE dbo.TenantDatabase_GetByTenantUid
    @TenantUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        TenantUid,
        DatabaseServerKey,
        DatabaseName,
        SecretReference,
        DatabaseStatus,
        CurrentSchemaVersion,
        LastMigrationAt
    FROM dbo.TenantDatabase
    WHERE TenantUid = @TenantUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.UserTenantMembership_GetActiveByUserId
    @UserId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        membership.UserId,
        membership.TenantUid,
        membership.MembershipStatus,
        membership.IsDefaultTenant,
        tenant.TenantKey,
        tenant.DisplayName,
        tenant.TenantStatus,
        tenant.DefaultTimeZoneId,
        role.RoleName
    FROM dbo.UserTenantMembership AS membership
    INNER JOIN dbo.Tenant AS tenant
        ON tenant.TenantUid = membership.TenantUid
    LEFT JOIN dbo.UserTenantRole AS role
        ON role.UserId = membership.UserId
        AND role.TenantUid = membership.TenantUid
    WHERE membership.UserId = @UserId
        AND membership.MembershipStatus = 'Active'
    ORDER BY membership.IsDefaultTenant DESC, tenant.DisplayName, role.RoleName;
END;
GO
