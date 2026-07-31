USE master;
GO

IF DB_ID(N'MicroEMR_Platform') IS NULL
BEGIN
    CREATE DATABASE MicroEMR_Platform;
END;
GO

USE MicroEMR_Platform;
GO

IF OBJECT_ID(N'dbo.Tenant', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Tenant
    (
        TenantUid UNIQUEIDENTIFIER NOT NULL,
        TenantKey NVARCHAR(50) NOT NULL,
        DisplayName NVARCHAR(200) NOT NULL,
        TenantStatus VARCHAR(30) NOT NULL,
        DefaultTimeZoneId NVARCHAR(100) NOT NULL,
        CreatedAt DATETIME2(7) NOT NULL
            CONSTRAINT DF_Tenant_CreatedAt DEFAULT SYSUTCDATETIME(),
        ActivatedAt DATETIME2(7) NULL,
        SuspendedAt DATETIME2(7) NULL,

        CONSTRAINT PK_Tenant PRIMARY KEY (TenantUid),
        CONSTRAINT UQ_Tenant_TenantKey UNIQUE (TenantKey),
        CONSTRAINT CK_Tenant_TenantStatus CHECK
            (TenantStatus IN ('Provisioning', 'Active', 'Suspended', 'Archived')),
        CONSTRAINT CK_Tenant_TenantKey_NotBlank CHECK
            (LEN(LTRIM(RTRIM(TenantKey))) > 0),
        CONSTRAINT CK_Tenant_DisplayName_NotBlank CHECK
            (LEN(LTRIM(RTRIM(DisplayName))) > 0),
        CONSTRAINT CK_Tenant_DefaultTimeZoneId_NotBlank CHECK
            (LEN(LTRIM(RTRIM(DefaultTimeZoneId))) > 0)
    );
END;
GO

IF OBJECT_ID(N'dbo.TenantDatabase', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenantDatabase
    (
        TenantUid UNIQUEIDENTIFIER NOT NULL,
        DatabaseServerKey NVARCHAR(100) NOT NULL,
        DatabaseName SYSNAME NOT NULL,
        SecretReference NVARCHAR(500) NOT NULL,
        DatabaseStatus VARCHAR(30) NOT NULL,
        CurrentSchemaVersion NVARCHAR(50) NULL,
        LastMigrationAt DATETIME2(7) NULL,
        CreatedAt DATETIME2(7) NOT NULL
            CONSTRAINT DF_TenantDatabase_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2(7) NULL,

        CONSTRAINT PK_TenantDatabase PRIMARY KEY (TenantUid),
        CONSTRAINT FK_TenantDatabase_Tenant FOREIGN KEY (TenantUid)
            REFERENCES dbo.Tenant(TenantUid),
        CONSTRAINT CK_TenantDatabase_DatabaseStatus CHECK
            (DatabaseStatus IN
                ('Provisioning', 'Active', 'Unavailable', 'MigrationFailed', 'Archived')),
        CONSTRAINT CK_TenantDatabase_DatabaseServerKey_NotBlank CHECK
            (LEN(LTRIM(RTRIM(DatabaseServerKey))) > 0),
        CONSTRAINT CK_TenantDatabase_DatabaseName_NotBlank CHECK
            (LEN(LTRIM(RTRIM(DatabaseName))) > 0),
        CONSTRAINT CK_TenantDatabase_SecretReference_NotBlank CHECK
            (LEN(LTRIM(RTRIM(SecretReference))) > 0)
    );
END;
GO

IF OBJECT_ID(N'dbo.UserTenantMembership', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserTenantMembership
    (
        UserId NVARCHAR(450) NOT NULL,
        TenantUid UNIQUEIDENTIFIER NOT NULL,
        MembershipStatus VARCHAR(30) NOT NULL,
        IsDefaultTenant BIT NOT NULL
            CONSTRAINT DF_UserTenantMembership_IsDefaultTenant DEFAULT CONVERT(BIT, 0),
        CreatedAt DATETIME2(7) NOT NULL
            CONSTRAINT DF_UserTenantMembership_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2(7) NULL,

        CONSTRAINT PK_UserTenantMembership PRIMARY KEY NONCLUSTERED
            (UserId, TenantUid),
        CONSTRAINT FK_UserTenantMembership_Tenant FOREIGN KEY (TenantUid)
            REFERENCES dbo.Tenant(TenantUid),
        CONSTRAINT CK_UserTenantMembership_MembershipStatus CHECK
            (MembershipStatus IN ('Invited', 'Active', 'Suspended', 'Revoked')),
        CONSTRAINT CK_UserTenantMembership_UserId_NotBlank CHECK
            (LEN(LTRIM(RTRIM(UserId))) > 0)
    );
END;
GO

IF OBJECT_ID(N'dbo.UserTenantRole', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserTenantRole
    (
        UserId NVARCHAR(450) NOT NULL,
        TenantUid UNIQUEIDENTIFIER NOT NULL,
        RoleName NVARCHAR(100) NOT NULL,
        CreatedAt DATETIME2(7) NOT NULL
            CONSTRAINT DF_UserTenantRole_CreatedAt DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_UserTenantRole PRIMARY KEY NONCLUSTERED
            (UserId, TenantUid, RoleName),
        CONSTRAINT FK_UserTenantRole_UserTenantMembership
            FOREIGN KEY (UserId, TenantUid)
            REFERENCES dbo.UserTenantMembership(UserId, TenantUid),
        CONSTRAINT CK_UserTenantRole_RoleName_NotBlank CHECK
            (LEN(LTRIM(RTRIM(RoleName))) > 0)
    );
END;
GO
