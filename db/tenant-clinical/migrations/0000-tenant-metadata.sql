SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.TenantDatabaseIdentity', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenantDatabaseIdentity
    (
        TenantUid UNIQUEIDENTIFIER NOT NULL,
        TenantKey NVARCHAR(50) NOT NULL,
        DatabaseCreatedAt DATETIME2(7) NOT NULL
            CONSTRAINT DF_TenantDatabaseIdentity_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2(7) NULL,
        CONSTRAINT PK_TenantDatabaseIdentity PRIMARY KEY (TenantUid),
        CONSTRAINT CK_TenantDatabaseIdentity_TenantUid_NotEmpty
            CHECK (TenantUid <> '00000000-0000-0000-0000-000000000000'),
        CONSTRAINT CK_TenantDatabaseIdentity_TenantKey_NotBlank
            CHECK (LEN(LTRIM(RTRIM(TenantKey))) > 0)
    );
END;

IF OBJECT_ID(N'dbo.SchemaMigration', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchemaMigration
    (
        MigrationId NVARCHAR(100) NOT NULL,
        SchemaVersion NVARCHAR(50) NOT NULL,
        ScriptHash CHAR(64) NOT NULL,
        AppliedAt DATETIME2(7) NOT NULL
            CONSTRAINT DF_SchemaMigration_AppliedAt DEFAULT SYSUTCDATETIME(),
        AppliedBy NVARCHAR(200) NULL,
        CONSTRAINT PK_SchemaMigration PRIMARY KEY (MigrationId),
        CONSTRAINT CK_SchemaMigration_Hash_Length CHECK (LEN(ScriptHash) = 64)
    );
END;
