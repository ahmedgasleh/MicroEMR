SET XACT_ABORT ON;
IF COL_LENGTH(N'dbo.ScheduleResource', N'ProviderId') IS NULL
    ALTER TABLE dbo.ScheduleResource ADD ProviderId BIGINT NULL;
IF COL_LENGTH(N'dbo.ScheduleResource', N'RowVersion') IS NULL
    ALTER TABLE dbo.ScheduleResource ADD RowVersion ROWVERSION NOT NULL;
IF COL_LENGTH(N'dbo.ScheduleResource', N'UpdatedAt') IS NULL
    ALTER TABLE dbo.ScheduleResource ADD UpdatedAt DATETIME2 NULL;
IF COL_LENGTH(N'dbo.ScheduleResource', N'UpdatedBy') IS NULL
    ALTER TABLE dbo.ScheduleResource ADD UpdatedBy BIGINT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_ScheduleResource_Provider')
    ALTER TABLE dbo.ScheduleResource ADD CONSTRAINT FK_ScheduleResource_Provider
        FOREIGN KEY (ProviderId) REFERENCES dbo.Provider(ProviderId);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name=N'CK_ScheduleResource_ProviderLink')
    ALTER TABLE dbo.ScheduleResource ADD CONSTRAINT CK_ScheduleResource_ProviderLink
        CHECK (ProviderId IS NULL OR ResourceType=N'Provider');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.ScheduleResource') AND name=N'UX_ScheduleResource_ActiveProvider')
    CREATE UNIQUE INDEX UX_ScheduleResource_ActiveProvider ON dbo.ScheduleResource(ProviderId)
        WHERE ProviderId IS NOT NULL AND IsActive=1;
GO
CREATE OR ALTER PROCEDURE dbo.ScheduleResource_AdminList @Status NVARCHAR(10)=N'All', @ResourceUid UNIQUEIDENTIFIER=NULL AS
BEGIN
    SET NOCOUNT ON;
    IF @Status NOT IN(N'Active',N'Inactive',N'All') THROW 51800,'Invalid resource status filter.',1;
    SELECT r.ResourceUid,r.ResourceType,r.DisplayName,r.ColorCode,r.SortOrder,r.IsActive,
        p.ProviderUid,p.DisplayName AS ProviderDisplayName,r.RowVersion
    FROM dbo.ScheduleResource r LEFT JOIN dbo.Provider p ON p.ProviderId=r.ProviderId
    WHERE (@ResourceUid IS NULL OR r.ResourceUid=@ResourceUid)
        AND (@Status=N'All' OR r.IsActive=CASE WHEN @Status=N'Active' THEN 1 ELSE 0 END)
    ORDER BY r.ResourceType,r.SortOrder,r.DisplayName,r.ResourceUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.ScheduleResource_ProviderChoices AS
BEGIN
    SET NOCOUNT ON;
    SELECT ProviderUid,DisplayName FROM dbo.Provider WHERE IsActive=1 ORDER BY DisplayName,ProviderUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.ScheduleResource_AdminSave
    @ResourceUid UNIQUEIDENTIFIER=NULL, @ResourceType NVARCHAR(50), @DisplayName NVARCHAR(200),
    @ColorCode NVARCHAR(20)=NULL, @SortOrder INT=0, @ProviderUid UNIQUEIDENTIFIER=NULL,
    @ExpectedRowVersion BINARY(8)=NULL, @ActorUserId BIGINT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF @ResourceType IS NULL OR @ResourceType NOT IN(N'Provider',N'Room',N'Equipment')
        OR NULLIF(LTRIM(RTRIM(@DisplayName)),N'') IS NULL THROW 51800,'Resource type and display name are required.',1;
    IF @ResourceType<>N'Provider' AND @ProviderUid IS NOT NULL THROW 51800,'Only Provider resources may link a clinical Provider.',1;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @LockResult INT;
        -- Same transaction lock as appointment create/edit/reschedule; no appointment changes here.
        EXEC @LockResult=sys.sp_getapplock @Resource=N'SchedulingAppointmentSave',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000;
        IF @LockResult<0 THROW 51801,'Scheduling changed. Reload and retry.',1;
        IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@ActorUserId AND IsActive=1)
            THROW 51802,'An active clinical actor is required.',1;
        DECLARE @TenantUid UNIQUEIDENTIFIER=(SELECT TenantUid FROM dbo.TenantDatabaseIdentity);
        IF @TenantUid IS NULL THROW 51802,'Tenant database identity is required.',1;
        DECLARE @Current BINARY(8),@OldProviderId BIGINT,@OldType NVARCHAR(50),@ProviderId BIGINT,
            @IsActive BIT=1,@OldValue NVARCHAR(MAX),@NewValue NVARCHAR(MAX),@Action NVARCHAR(100);
        IF @ResourceUid IS NOT NULL
        BEGIN
            SELECT @Current=RowVersion,@OldProviderId=ProviderId,@OldType=ResourceType,@IsActive=IsActive
            FROM dbo.ScheduleResource WITH(UPDLOCK,HOLDLOCK) WHERE ResourceUid=@ResourceUid;
            IF @Current IS NULL BEGIN ROLLBACK; RETURN; END;
            IF @ExpectedRowVersion IS NULL OR @Current<>@ExpectedRowVersion THROW 51801,'Resource changed. Reload and retry.',1;
            IF @OldType<>@ResourceType THROW 51803,'Resource type cannot be changed. Historical references must be preserved.',1;
            SET @OldValue=(SELECT @TenantUid TenantUid,r.ResourceUid,r.ResourceType,r.DisplayName,r.ColorCode,r.SortOrder,r.IsActive,p.ProviderUid
                FROM dbo.ScheduleResource r LEFT JOIN dbo.Provider p ON p.ProviderId=r.ProviderId
                WHERE r.ResourceUid=@ResourceUid FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES);
        END;
        IF @ProviderUid IS NOT NULL
        BEGIN
            SELECT @ProviderId=ProviderId FROM dbo.Provider WITH(UPDLOCK,HOLDLOCK) WHERE ProviderUid=@ProviderUid
                AND (IsActive=1 OR ProviderId=@OldProviderId);
            IF @ProviderId IS NULL THROW 51800,'Select an active Provider from the current clinic.',1;
        END;
        IF @OldProviderId IS NOT NULL AND (@ProviderId IS NULL OR @OldProviderId<>@ProviderId)
            THROW 51803,'A linked Provider cannot be removed or reassigned. Historical references must be preserved.',1;
        IF @ResourceUid IS NULL AND @ResourceType=N'Provider' AND @ProviderId IS NULL
            THROW 51800,'Select an existing clinical Provider.',1;
        IF @IsActive=1 AND @ProviderId IS NOT NULL AND EXISTS
            (SELECT 1 FROM dbo.ScheduleResource WITH(UPDLOCK,HOLDLOCK) WHERE ProviderId=@ProviderId AND IsActive=1
                AND (@ResourceUid IS NULL OR ResourceUid<>@ResourceUid))
            THROW 51803,'This Provider already has an active scheduling resource.',1;
        IF @ResourceUid IS NULL
        BEGIN
            SET @ResourceUid=NEWID(); SET @Action=N'SchedulingResourceCreated';
            INSERT dbo.ScheduleResource(ResourceUid,ResourceType,DisplayName,ColorCode,SortOrder,IsActive,ProviderId,UpdatedAt,UpdatedBy)
                VALUES(@ResourceUid,@ResourceType,LTRIM(RTRIM(@DisplayName)),@ColorCode,@SortOrder,1,@ProviderId,SYSUTCDATETIME(),@ActorUserId);
        END
        ELSE
        BEGIN
            SET @Action=CASE WHEN @OldProviderId IS NULL AND @ProviderId IS NOT NULL THEN N'SchedulingResourceProviderLinked' ELSE N'SchedulingResourceUpdated' END;
            UPDATE dbo.ScheduleResource SET DisplayName=LTRIM(RTRIM(@DisplayName)),ColorCode=@ColorCode,SortOrder=@SortOrder,
                ProviderId=@ProviderId,UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@ActorUserId WHERE ResourceUid=@ResourceUid;
        END;
        SET @NewValue=(SELECT @TenantUid TenantUid,r.ResourceUid,r.ResourceType,r.DisplayName,r.ColorCode,r.SortOrder,r.IsActive,p.ProviderUid
            FROM dbo.ScheduleResource r LEFT JOIN dbo.Provider p ON p.ProviderId=r.ProviderId
            WHERE r.ResourceUid=@ResourceUid FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES);
        INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
            VALUES(@ActorUserId,NULL,@Action,N'ScheduleResource',CONVERT(NVARCHAR(100),@ResourceUid),@OldValue,@NewValue,SYSUTCDATETIME());
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
    EXEC dbo.ScheduleResource_AdminList @ResourceUid=@ResourceUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.ScheduleResource_AdminSetActive
    @ResourceUid UNIQUEIDENTIFIER,@IsActive BIT,@ExpectedRowVersion BINARY(8),@ActorUserId BIGINT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @LockResult INT;
        EXEC @LockResult=sys.sp_getapplock @Resource=N'SchedulingAppointmentSave',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000;
        IF @LockResult<0 THROW 51801,'Scheduling changed. Reload and retry.',1;
        IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@ActorUserId AND IsActive=1)
            THROW 51802,'An active clinical actor is required.',1;
        DECLARE @TenantUid UNIQUEIDENTIFIER=(SELECT TenantUid FROM dbo.TenantDatabaseIdentity);
        IF @TenantUid IS NULL THROW 51802,'Tenant database identity is required.',1;
        DECLARE @Current BINARY(8),@Old BIT,@ResourceId BIGINT,@ProviderId BIGINT,@OldValue NVARCHAR(MAX),@NewValue NVARCHAR(MAX);
        SELECT @Current=RowVersion,@Old=IsActive,@ResourceId=ResourceId,@ProviderId=ProviderId
            FROM dbo.ScheduleResource WITH(UPDLOCK,HOLDLOCK) WHERE ResourceUid=@ResourceUid;
        IF @Current IS NULL BEGIN ROLLBACK; RETURN; END;
        IF @ExpectedRowVersion IS NULL OR @Current<>@ExpectedRowVersion THROW 51801,'Resource changed. Reload and retry.',1;
        IF @Old=@IsActive THROW 51803,'Resource is already in the requested status.',1;
        IF @IsActive=0 AND EXISTS(SELECT 1 FROM dbo.ScheduleAppointment WITH(UPDLOCK,HOLDLOCK)
            WHERE IsDeleted=0 AND AppointmentStatus<>N'Cancelled' AND EndDateTimeUtc>SYSUTCDATETIME()
                AND (PrimaryResourceId=@ResourceId OR RoomResourceId=@ResourceId))
            THROW 51803,'Cannot deactivate a resource with uncancelled current or future appointments. Resolve those appointments first.',1;
        IF @IsActive=1 AND @ProviderId IS NOT NULL
        BEGIN
            IF NOT EXISTS(SELECT 1 FROM dbo.Provider WITH(UPDLOCK,HOLDLOCK) WHERE ProviderId=@ProviderId AND IsActive=1)
                THROW 51803,'Reactivate the linked clinical Provider before activating this resource.',1;
            IF EXISTS(SELECT 1 FROM dbo.ScheduleResource WITH(UPDLOCK,HOLDLOCK) WHERE ProviderId=@ProviderId AND IsActive=1 AND ResourceUid<>@ResourceUid)
                THROW 51803,'This Provider already has an active scheduling resource.',1;
        END;
        SET @OldValue=(SELECT @TenantUid TenantUid,ResourceUid,ResourceType,DisplayName,ColorCode,SortOrder,IsActive,ProviderId
            FROM dbo.ScheduleResource WHERE ResourceUid=@ResourceUid FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES);
        UPDATE dbo.ScheduleResource SET IsActive=@IsActive,UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@ActorUserId WHERE ResourceUid=@ResourceUid;
        SET @NewValue=(SELECT @TenantUid TenantUid,ResourceUid,ResourceType,DisplayName,ColorCode,SortOrder,IsActive,ProviderId
            FROM dbo.ScheduleResource WHERE ResourceUid=@ResourceUid FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES);
        INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
            VALUES(@ActorUserId,NULL,CASE WHEN @IsActive=1 THEN N'SchedulingResourceActivated' ELSE N'SchedulingResourceDeactivated' END,
                N'ScheduleResource',CONVERT(NVARCHAR(100),@ResourceUid),@OldValue,@NewValue,SYSUTCDATETIME());
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
    EXEC dbo.ScheduleResource_AdminList @ResourceUid=@ResourceUid;
END;
GO
