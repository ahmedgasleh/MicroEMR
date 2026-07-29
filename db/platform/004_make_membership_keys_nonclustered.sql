USE MicroEMR_Platform;
GO

IF OBJECT_ID(N'dbo.UserTenantRole', N'U') IS NULL
    OR OBJECT_ID(N'dbo.UserTenantMembership', N'U') IS NULL
BEGIN
    THROW 51000, 'Required platform membership tables were not found.', 1;
END;
GO

IF EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name IN (N'PK_UserTenantMembership', N'PK_UserTenantRole')
        AND object_id IN
            (OBJECT_ID(N'dbo.UserTenantMembership'), OBJECT_ID(N'dbo.UserTenantRole'))
        AND type = 1
)
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'dbo.UserTenantRole')
            AND name = N'FK_UserTenantRole_UserTenantMembership'
    )
    BEGIN
        ALTER TABLE dbo.UserTenantRole
            DROP CONSTRAINT FK_UserTenantRole_UserTenantMembership;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.UserTenantRole')
            AND name = N'PK_UserTenantRole'
            AND type = 1
    )
    BEGIN
        ALTER TABLE dbo.UserTenantRole
            DROP CONSTRAINT PK_UserTenantRole;

        ALTER TABLE dbo.UserTenantRole
            ADD CONSTRAINT PK_UserTenantRole PRIMARY KEY NONCLUSTERED
                (UserId, TenantUid, RoleName);
    END;

    IF EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.UserTenantMembership')
            AND name = N'PK_UserTenantMembership'
            AND type = 1
    )
    BEGIN
        ALTER TABLE dbo.UserTenantMembership
            DROP CONSTRAINT PK_UserTenantMembership;

        ALTER TABLE dbo.UserTenantMembership
            ADD CONSTRAINT PK_UserTenantMembership PRIMARY KEY NONCLUSTERED
                (UserId, TenantUid);
    END;

    ALTER TABLE dbo.UserTenantRole
        ADD CONSTRAINT FK_UserTenantRole_UserTenantMembership
            FOREIGN KEY (UserId, TenantUid)
            REFERENCES dbo.UserTenantMembership(UserId, TenantUid);
END;
GO
