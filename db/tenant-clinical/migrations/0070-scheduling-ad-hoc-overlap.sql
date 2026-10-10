-- PC09.12: explicit persisted clinician-only ad-hoc overlap; ordinary remains default.
IF COL_LENGTH(N'dbo.ScheduleAppointment', N'IsAdHoc') IS NULL
    ALTER TABLE dbo.ScheduleAppointment ADD IsAdHoc BIT NOT NULL
        CONSTRAINT DF_ScheduleAppointment_IsAdHoc DEFAULT (0);
GO

CREATE OR ALTER PROCEDURE dbo.ScheduleAppointment_Create
    @PatientUid UNIQUEIDENTIFIER,
    @PrimaryResourceUid UNIQUEIDENTIFIER,
    @RoomResourceUid UNIQUEIDENTIFIER = NULL,
    @StartDateTimeUtc DATETIME2,
    @EndDateTimeUtc DATETIME2,
    @AppointmentType NVARCHAR(100) = NULL,
    @Reason NVARCHAR(500) = NULL,
    @Notes NVARCHAR(1000) = NULL,
    @CreatedBy BIGINT = NULL,
    @IsAdHoc BIT = 0,
    @IsCritical BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NULL
        THROW 51064, 'Clinical audit storage is required.', 1;
    BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @LockResult INT;
    EXEC @LockResult = sys.sp_getapplock @Resource=N'SchedulingAppointmentSave',
        @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000;
    IF @LockResult < 0 THROW 51063, 'Scheduling changed. Retry the save.', 1;

    DECLARE @PrimaryResourceId BIGINT;
    DECLARE @RoomResourceId BIGINT;
    DECLARE @AppointmentUid UNIQUEIDENTIFIER = NEWID();

    IF @EndDateTimeUtc <= @StartDateTimeUtc
        THROW 51060, 'The end time must be after the start time.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.Patient
        WHERE PatientUid = @PatientUid AND IsDeleted = 0
    )
        THROW 51061, 'The requested patient was not found.', 1;

    SELECT @PrimaryResourceId = ResourceId
    FROM dbo.ScheduleResource
    WHERE ResourceUid = @PrimaryResourceUid
        AND IsActive = 1;
    IF @PrimaryResourceId IS NULL
        THROW 51062, 'The requested primary resource was not found.', 1;
    IF @RoomResourceUid IS NOT NULL
    BEGIN
        SELECT @RoomResourceId = ResourceId
        FROM dbo.ScheduleResource
        WHERE ResourceUid = @RoomResourceUid
            AND ResourceType = N'Room'
            AND IsActive = 1;
        IF @RoomResourceId IS NULL
            THROW 51062, 'The requested room resource was not found.', 1;
    END;

    IF @IsAdHoc = 1 AND (@CreatedBy IS NULL OR NOT EXISTS
        (SELECT 1 FROM dbo.ApplicationUser WHERE UserId = @CreatedBy) OR NOT EXISTS
        (SELECT 1 FROM dbo.ScheduleResource WHERE ResourceId = @PrimaryResourceId AND ResourceType=N'Provider'))
        THROW 51064, 'Ad-hoc appointments require a valid clinical actor and clinician resource.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.ScheduleAppointment AS a WITH (UPDLOCK, HOLDLOCK)
        WHERE a.IsDeleted = 0
            AND a.AppointmentStatus <> N'Cancelled'
            AND
            (
                ((@IsAdHoc = 0 AND a.PrimaryResourceId = @PrimaryResourceId)
                    OR (@RoomResourceId IS NOT NULL AND a.PrimaryResourceId = @RoomResourceId))
                OR a.RoomResourceId IN (@PrimaryResourceId, ISNULL(@RoomResourceId, @PrimaryResourceId))
            )
            AND a.StartDateTimeUtc < @EndDateTimeUtc
            AND a.EndDateTimeUtc > @StartDateTimeUtc
    )
        THROW 51063, 'The appointment conflicts with another appointment for this resource.', 1;

    IF EXISTS
    (
        SELECT 1 FROM dbo.SchedulingBlockedTime WITH (UPDLOCK, HOLDLOCK)
        WHERE IsActive = 1
          AND ResourceUid IN (@PrimaryResourceUid, ISNULL(@RoomResourceUid, @PrimaryResourceUid))
          AND StartDateTimeUtc < @EndDateTimeUtc
          AND EndDateTimeUtc > @StartDateTimeUtc
    )
        THROW 51073, 'This resource is blocked during the selected time.', 1;


    INSERT INTO dbo.ScheduleAppointment
    (
        AppointmentUid, PatientUid, PrimaryResourceId, RoomResourceId,
        StartDateTimeUtc, EndDateTimeUtc, AppointmentType, Reason, Notes,
        AppointmentStatus, IsDeleted, CreatedAt, CreatedBy, IsAdHoc, IsCritical
    )
    VALUES
    (
        @AppointmentUid, @PatientUid, @PrimaryResourceId, @RoomResourceId,
        @StartDateTimeUtc, @EndDateTimeUtc,
        NULLIF(LTRIM(RTRIM(@AppointmentType)), N''),
        NULLIF(LTRIM(RTRIM(@Reason)), N''), NULLIF(LTRIM(RTRIM(@Notes)), N''),
        N'Scheduled', 0, SYSUTCDATETIME(), @CreatedBy, @IsAdHoc, @IsCritical
    );

    DECLARE @NewAudit NVARCHAR(MAX) = (SELECT @IsAdHoc AS IsAdHoc,
        @StartDateTimeUtc AS StartDateTimeUtc, @EndDateTimeUtc AS EndDateTimeUtc,
        @PrimaryResourceUid AS PrimaryResourceUid FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
    DECLARE @HistoryDescription NVARCHAR(500) = CASE WHEN @IsAdHoc=1 THEN N'Ad-hoc appointment' ELSE N'Ordinary appointment' END;

    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
    BEGIN
        INSERT INTO dbo.AuditLog
            (UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
        VALUES
            (@CreatedBy, (SELECT PatientId FROM dbo.Patient WHERE PatientUid = @PatientUid),
             N'Create', N'ScheduleAppointment', CONVERT(NVARCHAR(100), @AppointmentUid),
             NULL, @NewAudit, SYSUTCDATETIME());
    END;

    EXEC dbo.AppointmentHistory_Create
        @AppointmentUid = @AppointmentUid,
        @ActionType = N'Created',
        @ActionDescription = @HistoryDescription,
        @NewStartDateTimeUtc = @StartDateTimeUtc,
        @NewEndDateTimeUtc = @EndDateTimeUtc,
        @NewStatus = N'Scheduled',
        @NewResourceUid = @PrimaryResourceUid,
        @CreatedBy = @CreatedBy,
        @ReturnResult = 0;

    COMMIT TRANSACTION;

    SELECT
        a.AppointmentUid,
        NULLIF(LTRIM(RTRIM(CONCAT(p.LastName, N', ', p.FirstName))), N',') AS PatientDisplayName,
        a.Reason,
        a.AppointmentType,
        a.StartDateTimeUtc,
        a.EndDateTimeUtc,
        @PrimaryResourceUid AS PrimaryResourceUid, a.IsAdHoc, a.IsCritical
    FROM dbo.ScheduleAppointment AS a
    INNER JOIN dbo.Patient AS p ON p.PatientUid = a.PatientUid
    WHERE a.AppointmentUid = @AppointmentUid;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ScheduleAppointment_Update
    @AppointmentUid UNIQUEIDENTIFIER,
    @PrimaryResourceUid UNIQUEIDENTIFIER,
    @RoomResourceUid UNIQUEIDENTIFIER = NULL,
    @StartDateTimeUtc DATETIME2,
    @EndDateTimeUtc DATETIME2,
    @AppointmentType NVARCHAR(100) = NULL,
    @Reason NVARCHAR(500) = NULL,
    @Notes NVARCHAR(1000) = NULL,
    @ModifiedBy BIGINT = NULL,
    @IsAdHoc BIT = 0,
    @IsCritical BIT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NULL
        THROW 51064, 'Clinical audit storage is required.', 1;
    BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @LockResult INT;
    EXEC @LockResult = sys.sp_getapplock @Resource=N'SchedulingAppointmentSave',
        @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000;
    IF @LockResult < 0 THROW 51063, 'Scheduling changed. Retry the save.', 1;

    DECLARE @PrimaryResourceId BIGINT;
    DECLARE @RoomResourceId BIGINT;
    DECLARE @OldIsAdHoc BIT;
    DECLARE @PatientId BIGINT;
    DECLARE @CurrentStatus NVARCHAR(30);
    DECLARE @OldStartDateTimeUtc DATETIME2(0);
    DECLARE @OldEndDateTimeUtc DATETIME2(0);
    DECLARE @OldResourceUid UNIQUEIDENTIFIER;

    IF @EndDateTimeUtc <= @StartDateTimeUtc
        THROW 51060, 'The end time must be after the start time.', 1;

    SELECT @PrimaryResourceId = ResourceId
    FROM dbo.ScheduleResource
    WHERE ResourceUid = @PrimaryResourceUid AND IsActive = 1;
    IF @PrimaryResourceId IS NULL
        THROW 51062, 'The requested primary resource was not found.', 1;

    IF @RoomResourceUid IS NOT NULL
    BEGIN
        SELECT @RoomResourceId = ResourceId
        FROM dbo.ScheduleResource
        WHERE ResourceUid = @RoomResourceUid
            AND ResourceType = N'Room'
            AND IsActive = 1;
        IF @RoomResourceId IS NULL
            THROW 51062, 'The requested room resource was not found.', 1;
    END;


    SELECT
        @PatientId = p.PatientId,
        @CurrentStatus = a.AppointmentStatus,
        @OldIsAdHoc = a.IsAdHoc,
        @OldStartDateTimeUtc = a.StartDateTimeUtc,
        @OldEndDateTimeUtc = a.EndDateTimeUtc,
        @OldResourceUid = oldResource.ResourceUid
    FROM dbo.ScheduleAppointment AS a WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN dbo.Patient AS p ON p.PatientUid = a.PatientUid AND p.IsDeleted = 0
    INNER JOIN dbo.ScheduleResource AS oldResource
        ON oldResource.ResourceId = a.PrimaryResourceId
    WHERE a.AppointmentUid = @AppointmentUid AND a.IsDeleted = 0;

    IF @CurrentStatus IS NULL
    BEGIN
        ROLLBACK TRANSACTION;
        RETURN;
    END;
    IF @CurrentStatus = N'Cancelled'
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51067, 'Cancelled appointments cannot be edited.', 1;
    END;

    IF @IsAdHoc = 1 AND (@ModifiedBy IS NULL OR NOT EXISTS
        (SELECT 1 FROM dbo.ApplicationUser WHERE UserId = @ModifiedBy) OR NOT EXISTS
        (SELECT 1 FROM dbo.ScheduleResource WHERE ResourceId = @PrimaryResourceId AND ResourceType=N'Provider'))
        THROW 51064, 'Ad-hoc appointments require a valid clinical actor and clinician resource.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.ScheduleAppointment AS existingAppointment WITH (UPDLOCK, HOLDLOCK)
        WHERE existingAppointment.IsDeleted = 0
            AND existingAppointment.AppointmentStatus <> N'Cancelled'
            AND existingAppointment.AppointmentUid <> @AppointmentUid
            AND
            (
                ((@IsAdHoc = 0 AND existingAppointment.PrimaryResourceId = @PrimaryResourceId)
                    OR (@RoomResourceId IS NOT NULL AND existingAppointment.PrimaryResourceId = @RoomResourceId))
                OR existingAppointment.RoomResourceId IN
                    (@PrimaryResourceId, ISNULL(@RoomResourceId, @PrimaryResourceId))
            )
            AND existingAppointment.StartDateTimeUtc < @EndDateTimeUtc
            AND existingAppointment.EndDateTimeUtc > @StartDateTimeUtc
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51063, 'The appointment conflicts with another appointment for this resource.', 1;
    END;

    IF EXISTS
    (
        SELECT 1 FROM dbo.SchedulingBlockedTime WITH (UPDLOCK, HOLDLOCK)
        WHERE IsActive = 1
          AND ResourceUid IN (@PrimaryResourceUid, ISNULL(@RoomResourceUid, @PrimaryResourceUid))
          AND StartDateTimeUtc < @EndDateTimeUtc
          AND EndDateTimeUtc > @StartDateTimeUtc
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51073, 'This resource is blocked during the selected time.', 1;
    END;

    UPDATE dbo.ScheduleAppointment
    SET IsAdHoc = @IsAdHoc,
        IsCritical = COALESCE(@IsCritical, IsCritical),
        PrimaryResourceId = @PrimaryResourceId,
        RoomResourceId = @RoomResourceId,
        StartDateTimeUtc = @StartDateTimeUtc,
        EndDateTimeUtc = @EndDateTimeUtc,
        AppointmentType = NULLIF(LTRIM(RTRIM(@AppointmentType)), N''),
        Reason = NULLIF(LTRIM(RTRIM(@Reason)), N''),
        Notes = NULLIF(LTRIM(RTRIM(@Notes)), N''),
        UpdatedAt = SYSUTCDATETIME(),
        UpdatedBy = @ModifiedBy
    WHERE AppointmentUid = @AppointmentUid
        AND IsDeleted = 0
        AND AppointmentStatus <> N'Cancelled';

    IF @@ROWCOUNT = 0
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51067, 'Cancelled appointments cannot be edited.', 1;
    END;

    DECLARE @NewAudit NVARCHAR(MAX) = (SELECT @IsAdHoc AS IsAdHoc,
        @StartDateTimeUtc AS StartDateTimeUtc, @EndDateTimeUtc AS EndDateTimeUtc,
        @PrimaryResourceUid AS PrimaryResourceUid FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
    DECLARE @HistoryDescription NVARCHAR(500) = CASE WHEN @IsAdHoc=1 THEN N'Ad-hoc appointment' ELSE N'Ordinary appointment' END;
    DECLARE @OldAudit NVARCHAR(MAX) = (SELECT @OldIsAdHoc AS IsAdHoc,
        @OldStartDateTimeUtc AS StartDateTimeUtc, @OldEndDateTimeUtc AS EndDateTimeUtc,
        @OldResourceUid AS PrimaryResourceUid FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
    SET @HistoryDescription = @HistoryDescription + CASE WHEN @OldIsAdHoc<>@IsAdHoc THEN N'; scheduling mode changed.' ELSE N'; details saved.' END;

    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
    BEGIN
        INSERT INTO dbo.AuditLog
            (UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
        VALUES
            (@ModifiedBy, @PatientId, N'Update', N'ScheduleAppointment',
             CONVERT(NVARCHAR(100), @AppointmentUid), @OldAudit,
             @NewAudit, SYSUTCDATETIME());
    END;

    EXEC dbo.AppointmentHistory_Create
        @AppointmentUid = @AppointmentUid,
        @ActionType = N'Edited',
        @ActionDescription = @HistoryDescription,
        @OldStartDateTimeUtc = @OldStartDateTimeUtc,
        @NewStartDateTimeUtc = @StartDateTimeUtc,
        @OldEndDateTimeUtc = @OldEndDateTimeUtc,
        @NewEndDateTimeUtc = @EndDateTimeUtc,
        @OldStatus = @CurrentStatus,
        @NewStatus = @CurrentStatus,
        @OldResourceUid = @OldResourceUid,
        @NewResourceUid = @PrimaryResourceUid,
        @CreatedBy = @ModifiedBy,
        @ReturnResult = 0;

    COMMIT TRANSACTION;

    SELECT
        a.AppointmentUid,
        a.PatientUid,
        primaryResource.ResourceUid AS PrimaryResourceUid,
        roomResource.ResourceUid AS RoomResourceUid,
        a.StartDateTimeUtc,
        a.EndDateTimeUtc,
        a.AppointmentType,
        a.Reason,
        a.Notes,
        a.IsAdHoc, a.IsCritical, a.AppointmentStatus AS Status,
        NULLIF(LTRIM(RTRIM(CONCAT(p.LastName, N', ', p.FirstName))), N',') AS PatientDisplayName,
        p.ChartNumber,
        primaryResource.DisplayName AS PrimaryResourceName,
        roomResource.DisplayName AS RoomResourceName,
        a.CreatedBy,
        createdByUser.DisplayName AS CreatedByDisplayName,
        a.CreatedAt,
        a.UpdatedAt
    FROM dbo.ScheduleAppointment AS a
    INNER JOIN dbo.Patient AS p ON p.PatientUid = a.PatientUid AND p.IsDeleted = 0
    INNER JOIN dbo.ScheduleResource AS primaryResource ON primaryResource.ResourceId = a.PrimaryResourceId
    LEFT JOIN dbo.ScheduleResource AS roomResource ON roomResource.ResourceId = a.RoomResourceId
    LEFT JOIN dbo.ApplicationUser AS createdByUser ON createdByUser.UserId = a.CreatedBy
    WHERE a.AppointmentUid = @AppointmentUid;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ScheduleAppointment_Reschedule
    @AppointmentUid UNIQUEIDENTIFIER,
    @PrimaryResourceUid UNIQUEIDENTIFIER,
    @RoomResourceUid UNIQUEIDENTIFIER = NULL,
    @StartDateTimeUtc DATETIME2,
    @EndDateTimeUtc DATETIME2,
    @ModifiedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NULL
        THROW 51064, 'Clinical audit storage is required.', 1;
    BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @LockResult INT;
    EXEC @LockResult = sys.sp_getapplock @Resource=N'SchedulingAppointmentSave',
        @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000;
    IF @LockResult < 0 THROW 51063, 'Scheduling changed. Retry the save.', 1;

    DECLARE @IsAdHoc BIT;
    DECLARE @PrimaryResourceId BIGINT;
    DECLARE @RoomResourceId BIGINT;
    DECLARE @OldIsAdHoc BIT;
    DECLARE @PatientId BIGINT;
    DECLARE @CurrentStatus NVARCHAR(30);
    DECLARE @OldStartDateTimeUtc DATETIME2(0);
    DECLARE @OldEndDateTimeUtc DATETIME2(0);
    DECLARE @OldResourceUid UNIQUEIDENTIFIER;

    IF @EndDateTimeUtc <= @StartDateTimeUtc
        THROW 51060, 'The end time must be after the start time.', 1;

    SELECT @PrimaryResourceId = ResourceId
    FROM dbo.ScheduleResource
    WHERE ResourceUid = @PrimaryResourceUid AND IsActive = 1;
    IF @PrimaryResourceId IS NULL
        THROW 51062, 'The requested primary resource was not found.', 1;

    IF @RoomResourceUid IS NOT NULL
    BEGIN
        SELECT @RoomResourceId = ResourceId
        FROM dbo.ScheduleResource
        WHERE ResourceUid = @RoomResourceUid AND ResourceType = N'Room' AND IsActive = 1;
        IF @RoomResourceId IS NULL
            THROW 51062, 'The requested room resource was not found.', 1;
    END;


    SELECT
        @PatientId = p.PatientId,
        @CurrentStatus = a.AppointmentStatus,
        @OldIsAdHoc = a.IsAdHoc,
        @IsAdHoc = a.IsAdHoc,
        @RoomResourceId = COALESCE(@RoomResourceId, a.RoomResourceId),
        @OldStartDateTimeUtc = a.StartDateTimeUtc,
        @OldEndDateTimeUtc = a.EndDateTimeUtc,
        @OldResourceUid = oldResource.ResourceUid
    FROM dbo.ScheduleAppointment AS a WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN dbo.Patient AS p ON p.PatientUid = a.PatientUid AND p.IsDeleted = 0
    INNER JOIN dbo.ScheduleResource AS oldResource
        ON oldResource.ResourceId = a.PrimaryResourceId
    WHERE a.AppointmentUid = @AppointmentUid AND a.IsDeleted = 0;

    IF @CurrentStatus IS NULL
    BEGIN
        ROLLBACK TRANSACTION;
        RETURN;
    END;
    IF @RoomResourceId IS NOT NULL
    BEGIN
        SELECT @RoomResourceUid = ResourceUid FROM dbo.ScheduleResource WHERE ResourceId = @RoomResourceId;
        IF NOT EXISTS (SELECT 1 FROM dbo.ScheduleResource WHERE ResourceId=@RoomResourceId AND ResourceType=N'Room' AND IsActive=1)
            THROW 51062, 'The requested room resource was not found.', 1;
    END;
    IF @CurrentStatus = N'Cancelled'
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51067, 'Cancelled appointments cannot be rescheduled.', 1;
    END;

    IF @IsAdHoc = 1 AND (@ModifiedBy IS NULL OR NOT EXISTS
        (SELECT 1 FROM dbo.ApplicationUser WHERE UserId = @ModifiedBy) OR NOT EXISTS
        (SELECT 1 FROM dbo.ScheduleResource WHERE ResourceId = @PrimaryResourceId AND ResourceType=N'Provider'))
        THROW 51064, 'Ad-hoc appointments require a valid clinical actor and clinician resource.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.ScheduleAppointment AS existingAppointment WITH (UPDLOCK, HOLDLOCK)
        WHERE existingAppointment.IsDeleted = 0
            AND existingAppointment.AppointmentStatus <> N'Cancelled'
            AND existingAppointment.AppointmentUid <> @AppointmentUid
            AND
            (
                ((@IsAdHoc = 0 AND existingAppointment.PrimaryResourceId = @PrimaryResourceId)
                    OR (@RoomResourceId IS NOT NULL AND existingAppointment.PrimaryResourceId = @RoomResourceId))
                OR existingAppointment.RoomResourceId IN
                    (@PrimaryResourceId, ISNULL(@RoomResourceId, @PrimaryResourceId))
            )
            AND existingAppointment.StartDateTimeUtc < @EndDateTimeUtc
            AND existingAppointment.EndDateTimeUtc > @StartDateTimeUtc
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51063, 'The appointment conflicts with another appointment for this resource.', 1;
    END;

    IF EXISTS
    (
        SELECT 1 FROM dbo.SchedulingBlockedTime WITH (UPDLOCK, HOLDLOCK)
        WHERE IsActive = 1
          AND ResourceUid IN (@PrimaryResourceUid, ISNULL(@RoomResourceUid, @PrimaryResourceUid))
          AND StartDateTimeUtc < @EndDateTimeUtc
          AND EndDateTimeUtc > @StartDateTimeUtc
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51073, 'This resource is blocked during the selected time.', 1;
    END;

    UPDATE dbo.ScheduleAppointment
    SET PrimaryResourceId = @PrimaryResourceId,
        RoomResourceId = @RoomResourceId,
        StartDateTimeUtc = @StartDateTimeUtc,
        EndDateTimeUtc = @EndDateTimeUtc,
        UpdatedAt = SYSUTCDATETIME(),
        UpdatedBy = @ModifiedBy
    WHERE AppointmentUid = @AppointmentUid
        AND IsDeleted = 0
        AND AppointmentStatus <> N'Cancelled';

    IF @@ROWCOUNT = 0
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51067, 'Cancelled appointments cannot be rescheduled.', 1;
    END;

    DECLARE @NewAudit NVARCHAR(MAX) = (SELECT @IsAdHoc AS IsAdHoc,
        @StartDateTimeUtc AS StartDateTimeUtc, @EndDateTimeUtc AS EndDateTimeUtc,
        @PrimaryResourceUid AS PrimaryResourceUid FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
    DECLARE @HistoryDescription NVARCHAR(500) = CASE WHEN @IsAdHoc=1 THEN N'Ad-hoc appointment' ELSE N'Ordinary appointment' END;
    DECLARE @OldAudit NVARCHAR(MAX) = (SELECT @OldIsAdHoc AS IsAdHoc,
        @OldStartDateTimeUtc AS StartDateTimeUtc, @OldEndDateTimeUtc AS EndDateTimeUtc,
        @OldResourceUid AS PrimaryResourceUid FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
    SET @HistoryDescription = @HistoryDescription + CASE WHEN @OldIsAdHoc<>@IsAdHoc THEN N'; scheduling mode changed.' ELSE N'; details saved.' END;

    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
    BEGIN
        INSERT INTO dbo.AuditLog
            (UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
        VALUES
            (@ModifiedBy, @PatientId, N'Reschedule', N'ScheduleAppointment',
             CONVERT(NVARCHAR(100), @AppointmentUid), @OldAudit,
             @NewAudit, SYSUTCDATETIME());
    END;

    EXEC dbo.AppointmentHistory_Create
        @AppointmentUid = @AppointmentUid,
        @ActionType = N'Rescheduled',
        @ActionDescription = @HistoryDescription,
        @OldStartDateTimeUtc = @OldStartDateTimeUtc,
        @NewStartDateTimeUtc = @StartDateTimeUtc,
        @OldEndDateTimeUtc = @OldEndDateTimeUtc,
        @NewEndDateTimeUtc = @EndDateTimeUtc,
        @OldResourceUid = @OldResourceUid,
        @NewResourceUid = @PrimaryResourceUid,
        @CreatedBy = @ModifiedBy,
        @ReturnResult = 0;

    COMMIT TRANSACTION;

    SELECT
        a.AppointmentUid, a.PatientUid, a.IsAdHoc, a.IsCritical,
        primaryResource.ResourceUid AS PrimaryResourceUid,
        roomResource.ResourceUid AS RoomResourceUid,
        a.StartDateTimeUtc, a.EndDateTimeUtc, a.AppointmentType, a.Reason, a.Notes,
        a.AppointmentStatus AS Status,
        NULLIF(LTRIM(RTRIM(CONCAT(p.LastName, N', ', p.FirstName))), N',') AS PatientDisplayName,
        p.ChartNumber,
        primaryResource.DisplayName AS PrimaryResourceName,
        roomResource.DisplayName AS RoomResourceName,
        a.CreatedBy, createdByUser.DisplayName AS CreatedByDisplayName,
        a.CreatedAt, a.UpdatedAt
    FROM dbo.ScheduleAppointment AS a
    INNER JOIN dbo.Patient AS p ON p.PatientUid = a.PatientUid AND p.IsDeleted = 0
    INNER JOIN dbo.ScheduleResource AS primaryResource ON primaryResource.ResourceId = a.PrimaryResourceId
    LEFT JOIN dbo.ScheduleResource AS roomResource ON roomResource.ResourceId = a.RoomResourceId
    LEFT JOIN dbo.ApplicationUser AS createdByUser ON createdByUser.UserId = a.CreatedBy
    WHERE a.AppointmentUid = @AppointmentUid;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ScheduleAppointment_CreateWithCriticalFlag
    @PatientUid UNIQUEIDENTIFIER,
    @PrimaryResourceUid UNIQUEIDENTIFIER,
    @RoomResourceUid UNIQUEIDENTIFIER = NULL,
    @StartDateTimeUtc DATETIME2,
    @EndDateTimeUtc DATETIME2,
    @AppointmentType NVARCHAR(100) = NULL,
    @Reason NVARCHAR(500) = NULL,
    @Notes NVARCHAR(1000) = NULL,
    @IsCritical BIT = 0,
    @CreatedBy BIGINT = NULL,
    @IsAdHoc BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.ScheduleAppointment_Create
        @PatientUid = @PatientUid,
        @PrimaryResourceUid = @PrimaryResourceUid,
        @RoomResourceUid = @RoomResourceUid,
        @StartDateTimeUtc = @StartDateTimeUtc,
        @EndDateTimeUtc = @EndDateTimeUtc,
        @AppointmentType = @AppointmentType,
        @Reason = @Reason,
        @Notes = @Notes,
        @IsCritical = @IsCritical,
        @IsAdHoc = @IsAdHoc,
        @CreatedBy = @CreatedBy;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ScheduleAppointment_UpdateWithCriticalFlag
    @AppointmentUid UNIQUEIDENTIFIER,
    @PrimaryResourceUid UNIQUEIDENTIFIER,
    @RoomResourceUid UNIQUEIDENTIFIER = NULL,
    @StartDateTimeUtc DATETIME2,
    @EndDateTimeUtc DATETIME2,
    @AppointmentType NVARCHAR(100) = NULL,
    @Reason NVARCHAR(500) = NULL,
    @Notes NVARCHAR(1000) = NULL,
    @IsCritical BIT = 0,
    @ModifiedBy BIGINT = NULL,
    @IsAdHoc BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.ScheduleAppointment_Update
        @AppointmentUid = @AppointmentUid,
        @PrimaryResourceUid = @PrimaryResourceUid,
        @RoomResourceUid = @RoomResourceUid,
        @StartDateTimeUtc = @StartDateTimeUtc,
        @EndDateTimeUtc = @EndDateTimeUtc,
        @AppointmentType = @AppointmentType,
        @Reason = @Reason,
        @Notes = @Notes,
        @IsCritical = @IsCritical,
        @IsAdHoc = @IsAdHoc,
        @ModifiedBy = @ModifiedBy;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ScheduleAppointment_GetByUidWithCriticalFlag
    @AppointmentUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        a.AppointmentUid, a.PatientUid,
        primaryResource.ResourceUid AS PrimaryResourceUid,
        roomResource.ResourceUid AS RoomResourceUid,
        a.StartDateTimeUtc, a.EndDateTimeUtc, a.AppointmentType, a.Reason, a.Notes,
        a.IsCritical, a.IsAdHoc, a.AppointmentStatus AS Status,
        NULLIF(LTRIM(RTRIM(CONCAT(p.LastName, N', ', p.FirstName))), N',') AS PatientDisplayName,
        p.ChartNumber, primaryResource.DisplayName AS PrimaryResourceName,
        roomResource.DisplayName AS RoomResourceName, a.CreatedBy,
        createdByUser.DisplayName AS CreatedByDisplayName, a.CreatedAt, a.UpdatedAt,
        linkedEncounter.EncounterUid AS LinkedEncounterUid,
        linkedEncounter.EncounterStatus AS LinkedEncounterStatus,
        CAST(NULL AS VARBINARY(8)) AS RowVersion
    FROM dbo.ScheduleAppointment AS a
    INNER JOIN dbo.Patient AS p ON p.PatientUid = a.PatientUid
    INNER JOIN dbo.ScheduleResource AS primaryResource ON primaryResource.ResourceId = a.PrimaryResourceId
    LEFT JOIN dbo.ScheduleResource AS roomResource ON roomResource.ResourceId = a.RoomResourceId
    LEFT JOIN dbo.ApplicationUser AS createdByUser ON createdByUser.UserId = a.CreatedBy
    LEFT JOIN dbo.PatientEncounter AS linkedEncounter ON linkedEncounter.AppointmentUid = a.AppointmentUid
    WHERE a.AppointmentUid = @AppointmentUid AND a.IsDeleted = 0;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ScheduleAppointment_GetMonthSummary
    @StartDateTimeUtc DATETIME2(0),
    @EndDateTimeUtc DATETIME2(0)
AS
BEGIN
    SET NOCOUNT ON;

    -- TODO: Group by the configured clinic timezone when clinic timezone settings are available.
    SELECT
        CAST(a.StartDateTimeUtc AS DATE) AS AppointmentDate,
        COUNT(*) AS AppointmentCount,
        SUM(CASE WHEN a.IsAdHoc=1 THEN 1 ELSE 0 END) AS AdHocCount,
        COUNT(DISTINCT a.PrimaryResourceId) AS ProviderCount,
        CASE
            WHEN COUNT(*) >= 10 THEN N'Busy'
            ELSE N'Scheduled'
        END AS Status
    FROM dbo.ScheduleAppointment AS a
    WHERE a.IsDeleted = 0
        AND ISNULL(a.AppointmentStatus, N'') <> N'Cancelled'
        AND a.StartDateTimeUtc >= @StartDateTimeUtc
        AND a.StartDateTimeUtc < @EndDateTimeUtc
    GROUP BY CAST(a.StartDateTimeUtc AS DATE)
    ORDER BY AppointmentDate;
END;
GO
