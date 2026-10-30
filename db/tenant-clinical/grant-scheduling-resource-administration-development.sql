-- Manual development deployment step; NOT a migration or automatic startup script.
-- Requires separate owner authorization. Connect directly to the named development database.
-- Confirm the API runtime's actual USER_NAME() is microemr_dev before applying.
-- No application/clinical record is changed. No schema-wide or role permission is granted.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF CONVERT(NVARCHAR(128), SERVERPROPERTY(N'ServerName')) <> N'EC2AMAZ-TC26CQK\TELEMED52'
    OR DB_NAME() <> N'MicroEMR_LocalDev_Fresh'
    THROW 51810, 'Stop: this script is only for the named MicroEMR development server and database.', 1;

IF OBJECT_ID(N'dbo.TenantDatabaseIdentity', N'U') IS NULL
    THROW 51810, 'Stop: tenant identity storage is missing.', 1;
IF (SELECT COUNT(*) FROM dbo.TenantDatabaseIdentity) <> 1
    OR NOT EXISTS (SELECT 1 FROM dbo.TenantDatabaseIdentity
        WHERE TenantKey=N'local-dev-fresh' AND TenantUid='f94c6ecd-4417-483e-8a94-0507cf8384c2')
    THROW 51810, 'Stop: development tenant identity does not match.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.database_principals
    WHERE name=N'microemr_dev' AND type=N'S' AND sid=SUSER_SID(N'microemr_dev'))
    THROW 51810, 'Stop: the expected development SQL user/login mapping is missing.', 1;
IF OBJECT_ID(N'dbo.ScheduleResource_AdminList', N'P') IS NULL
    OR OBJECT_ID(N'dbo.ScheduleResource_ProviderChoices', N'P') IS NULL
    OR OBJECT_ID(N'dbo.ScheduleResource_AdminSave', N'P') IS NULL
    OR OBJECT_ID(N'dbo.ScheduleResource_AdminSetActive', N'P') IS NULL
    THROW 51810, 'Stop: one or more resource-administration procedures are missing. Do not rerun migrations blindly.', 1;

DECLARE @Impersonating BIT=0;
BEGIN TRY
    BEGIN TRANSACTION;
    GRANT EXECUTE ON OBJECT::dbo.ScheduleResource_AdminList TO [microemr_dev];
    GRANT EXECUTE ON OBJECT::dbo.ScheduleResource_ProviderChoices TO [microemr_dev];
    GRANT EXECUTE ON OBJECT::dbo.ScheduleResource_AdminSave TO [microemr_dev];
    GRANT EXECUTE ON OBJECT::dbo.ScheduleResource_AdminSetActive TO [microemr_dev];

    -- Verify effective database-user permissions, including any inherited DENY.
    -- No resource mutation procedure is executed during verification.
    EXECUTE AS USER=N'microemr_dev';
    SET @Impersonating=1;
    DECLARE @List INT=HAS_PERMS_BY_NAME(N'dbo.ScheduleResource_AdminList',N'OBJECT',N'EXECUTE'),
        @Choices INT=HAS_PERMS_BY_NAME(N'dbo.ScheduleResource_ProviderChoices',N'OBJECT',N'EXECUTE'),
        @Save INT=HAS_PERMS_BY_NAME(N'dbo.ScheduleResource_AdminSave',N'OBJECT',N'EXECUTE'),
        @Status INT=HAS_PERMS_BY_NAME(N'dbo.ScheduleResource_AdminSetActive',N'OBJECT',N'EXECUTE');
    REVERT;
    SET @Impersonating=0;
    IF ISNULL(@List,0)<>1 OR ISNULL(@Choices,0)<>1 OR ISNULL(@Save,0)<>1 OR ISNULL(@Status,0)<>1
        THROW 51810, 'Effective EXECUTE verification failed. Grants rolled back; investigate any DENY without broadening access.', 1;
    COMMIT;
    SELECT N'microemr_dev' AS DatabaseUser,@List AS AdminListExecute,@Choices AS ProviderChoicesExecute,
        @Save AS AdminSaveExecute,@Status AS AdminSetActiveExecute;
END TRY
BEGIN CATCH
    IF @Impersonating=1 REVERT;
    IF XACT_STATE()<>0 ROLLBACK;
    THROW;
END CATCH;
