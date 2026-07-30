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
        tenant.DisplayName AS TenantDisplayName,
        role.RoleName
    FROM dbo.UserTenantMembership AS membership
    INNER JOIN dbo.Tenant AS tenant
        ON tenant.TenantUid = membership.TenantUid
    LEFT JOIN dbo.UserTenantRole AS role
        ON role.UserId = membership.UserId
        AND role.TenantUid = membership.TenantUid
    WHERE membership.UserId = @UserId
        AND membership.MembershipStatus = 'Active'
        AND tenant.TenantStatus = 'Active'
    ORDER BY membership.IsDefaultTenant DESC, tenant.DisplayName, role.RoleName;
END;
GO

CREATE OR ALTER PROCEDURE dbo.UserTenantMembership_GetActiveByUserAndTenant
    @UserId NVARCHAR(450),
    @TenantUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        membership.UserId,
        membership.TenantUid,
        tenant.TenantKey,
        tenant.DisplayName AS TenantDisplayName,
        membership.MembershipStatus,
        membership.IsDefaultTenant,
        role.RoleName
    FROM dbo.UserTenantMembership AS membership
    INNER JOIN dbo.Tenant AS tenant
        ON tenant.TenantUid = membership.TenantUid
    LEFT JOIN dbo.UserTenantRole AS role
        ON role.UserId = membership.UserId
        AND role.TenantUid = membership.TenantUid
    WHERE membership.UserId = @UserId
        AND membership.TenantUid = @TenantUid
        AND membership.MembershipStatus = 'Active'
        AND tenant.TenantStatus = 'Active'
    ORDER BY role.RoleName;
END;
GO

CREATE OR ALTER PROCEDURE dbo.TenantDatabase_ProvisioningStarted
    @TenantUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    UPDATE dbo.TenantDatabase
    SET DatabaseStatus = 'Provisioning',
        UpdatedAt = SYSUTCDATETIME()
    WHERE TenantUid = @TenantUid;

    IF @@ROWCOUNT <> 1
        THROW 51100, 'Tenant database assignment was not found.', 1;

    UPDATE dbo.Tenant
    SET TenantStatus = 'Provisioning'
    WHERE TenantUid = @TenantUid;

    IF @@ROWCOUNT <> 1
        THROW 51101, 'Tenant was not found.', 1;

    COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE dbo.TenantDatabase_RegisterProvisioning
    @TenantUid UNIQUEIDENTIFIER,
    @TenantKey NVARCHAR(50),
    @DisplayName NVARCHAR(200),
    @DatabaseServerKey NVARCHAR(100),
    @DatabaseName SYSNAME,
    @SecretReference NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @TenantUid = '00000000-0000-0000-0000-000000000000'
        THROW 51110, 'Tenant UID must not be empty.', 1;
    IF NULLIF(LTRIM(RTRIM(@TenantKey)), N'') IS NULL
        OR NULLIF(LTRIM(RTRIM(@DisplayName)), N'') IS NULL
        OR NULLIF(LTRIM(RTRIM(@DatabaseServerKey)), N'') IS NULL
        OR NULLIF(LTRIM(RTRIM(@DatabaseName)), N'') IS NULL
        OR NULLIF(LTRIM(RTRIM(@SecretReference)), N'') IS NULL
        THROW 51111, 'Provisioning registration values must not be blank.', 1;

    BEGIN TRANSACTION;

    IF EXISTS (SELECT 1 FROM dbo.Tenant WHERE TenantKey = @TenantKey AND TenantUid <> @TenantUid)
        THROW 51112, 'Tenant key is already assigned to another tenant.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.Tenant WHERE TenantUid = @TenantUid)
    BEGIN
        INSERT dbo.Tenant
        (
            TenantUid, TenantKey, DisplayName, TenantStatus,
            DefaultTimeZoneId, CreatedAt
        )
        VALUES
        (
            @TenantUid, LTRIM(RTRIM(@TenantKey)), LTRIM(RTRIM(@DisplayName)),
            'Provisioning', 'America/Toronto', SYSUTCDATETIME()
        );
    END;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.TenantDatabase
        WHERE TenantUid = @TenantUid
          AND (DatabaseName <> @DatabaseName OR SecretReference <> @SecretReference)
    )
        THROW 51113, 'Tenant database assignment already exists with different metadata.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.TenantDatabase WHERE TenantUid = @TenantUid)
    BEGIN
        INSERT dbo.TenantDatabase
        (
            TenantUid, DatabaseServerKey, DatabaseName,
            SecretReference, DatabaseStatus, CreatedAt
        )
        VALUES
        (
            @TenantUid, LTRIM(RTRIM(@DatabaseServerKey)),
            LTRIM(RTRIM(@DatabaseName)), LTRIM(RTRIM(@SecretReference)),
            'Provisioning', SYSUTCDATETIME()
        );
    END;

    COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE dbo.TenantDatabase_ProvisioningCompleted
    @TenantUid UNIQUEIDENTIFIER,
    @CurrentSchemaVersion NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    UPDATE dbo.TenantDatabase
    SET DatabaseStatus = 'Active',
        CurrentSchemaVersion = @CurrentSchemaVersion,
        LastMigrationAt = SYSUTCDATETIME(),
        UpdatedAt = SYSUTCDATETIME()
    WHERE TenantUid = @TenantUid
      AND DatabaseStatus = 'Provisioning';

    IF @@ROWCOUNT <> 1
        THROW 51102, 'Tenant database is not in the expected provisioning state.', 1;

    UPDATE dbo.Tenant
    SET TenantStatus = 'Active',
        ActivatedAt = COALESCE(ActivatedAt, SYSUTCDATETIME()),
        SuspendedAt = NULL
    WHERE TenantUid = @TenantUid
      AND TenantStatus = 'Provisioning';

    IF @@ROWCOUNT <> 1
        THROW 51103, 'Tenant is not in the expected provisioning state.', 1;

    COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE dbo.TenantDatabase_ProvisioningFailed
    @TenantUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.TenantDatabase
    SET DatabaseStatus = 'MigrationFailed',
        UpdatedAt = SYSUTCDATETIME()
    WHERE TenantUid = @TenantUid
      AND DatabaseStatus = 'Provisioning';
END;
GO
