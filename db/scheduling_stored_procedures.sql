IF OBJECT_ID(N'dbo.ScheduleResource', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScheduleResource
    (
        ResourceId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ResourceUid UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        ResourceType NVARCHAR(50) NOT NULL,
        DisplayName NVARCHAR(200) NOT NULL,
        ColorCode NVARCHAR(20) NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        SortOrder INT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

        CONSTRAINT UQ_ScheduleResource_ResourceUid UNIQUE (ResourceUid),
        CONSTRAINT CK_ScheduleResource_ResourceType
            CHECK (ResourceType IN (N'Provider', N'Room', N'Equipment'))
    );
END;
GO

IF OBJECT_ID(N'dbo.ScheduleAppointment', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScheduleAppointment
    (
        ScheduleAppointmentId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        AppointmentUid UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        PatientUid UNIQUEIDENTIFIER NOT NULL,
        PrimaryResourceId BIGINT NOT NULL,
        RoomResourceId BIGINT NULL,
        StartDateTimeUtc DATETIME2 NOT NULL,
        EndDateTimeUtc DATETIME2 NOT NULL,
        AppointmentType NVARCHAR(100) NULL,
        Reason NVARCHAR(500) NULL,
        Notes NVARCHAR(1000) NULL,
        AppointmentStatus NVARCHAR(30) NOT NULL DEFAULT N'Booked',
        IsDeleted BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy BIGINT NULL,
        UpdatedAt DATETIME2 NULL,
        UpdatedBy BIGINT NULL,
        CancelledAt DATETIME2 NULL,
        CancelledBy BIGINT NULL,
        CancelReason NVARCHAR(500) NULL,

        CONSTRAINT UQ_ScheduleAppointment_AppointmentUid UNIQUE (AppointmentUid),
        CONSTRAINT FK_ScheduleAppointment_PrimaryResource
            FOREIGN KEY (PrimaryResourceId) REFERENCES dbo.ScheduleResource(ResourceId),
        CONSTRAINT FK_ScheduleAppointment_RoomResource
            FOREIGN KEY (RoomResourceId) REFERENCES dbo.ScheduleResource(ResourceId),
        CONSTRAINT CK_ScheduleAppointment_Time
            CHECK (EndDateTimeUtc > StartDateTimeUtc)
    );

    CREATE INDEX IX_ScheduleAppointment_PrimaryResource_Time
        ON dbo.ScheduleAppointment (PrimaryResourceId, StartDateTimeUtc, EndDateTimeUtc)
        WHERE IsDeleted = 0;

    CREATE INDEX IX_ScheduleAppointment_RoomResource_Time
        ON dbo.ScheduleAppointment (RoomResourceId, StartDateTimeUtc, EndDateTimeUtc)
        WHERE IsDeleted = 0 AND RoomResourceId IS NOT NULL;
END;
GO

IF COL_LENGTH(N'dbo.ScheduleAppointment', N'CancelledAt') IS NULL
    ALTER TABLE dbo.ScheduleAppointment ADD CancelledAt DATETIME2 NULL;
GO
IF COL_LENGTH(N'dbo.ScheduleAppointment', N'CancelledBy') IS NULL
    ALTER TABLE dbo.ScheduleAppointment ADD CancelledBy BIGINT NULL;
GO
IF COL_LENGTH(N'dbo.ScheduleAppointment', N'CancelReason') IS NULL
    ALTER TABLE dbo.ScheduleAppointment ADD CancelReason NVARCHAR(500) NULL;
GO

CREATE OR ALTER PROCEDURE dbo.ScheduleResource_GetActive
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ResourceUid,
        ResourceType,
        DisplayName,
        ColorCode,
        IsActive,
        SortOrder
    FROM dbo.ScheduleResource
    WHERE IsActive = 1
    ORDER BY
        ResourceType,
        SortOrder,
        DisplayName;
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

CREATE OR ALTER PROCEDURE dbo.ScheduleAppointment_Create
    @PatientUid UNIQUEIDENTIFIER,
    @PrimaryResourceUid UNIQUEIDENTIFIER,
    @RoomResourceUid UNIQUEIDENTIFIER = NULL,
    @StartDateTimeUtc DATETIME2,
    @EndDateTimeUtc DATETIME2,
    @AppointmentType NVARCHAR(100) = NULL,
    @Reason NVARCHAR(500) = NULL,
    @Notes NVARCHAR(1000) = NULL,
    @CreatedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

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

    IF EXISTS
    (
        SELECT 1
        FROM dbo.ScheduleAppointment AS a
        WHERE a.IsDeleted = 0
            AND a.AppointmentStatus <> N'Cancelled'
            AND
            (
                a.PrimaryResourceId IN (@PrimaryResourceId, ISNULL(@RoomResourceId, @PrimaryResourceId))
                OR a.RoomResourceId IN (@PrimaryResourceId, ISNULL(@RoomResourceId, @PrimaryResourceId))
            )
            AND a.StartDateTimeUtc < @EndDateTimeUtc
            AND a.EndDateTimeUtc > @StartDateTimeUtc
    )
        THROW 51063, 'The appointment conflicts with another appointment for this resource.', 1;

    BEGIN TRANSACTION;

    INSERT INTO dbo.ScheduleAppointment
    (
        AppointmentUid, PatientUid, PrimaryResourceId, RoomResourceId,
        StartDateTimeUtc, EndDateTimeUtc, AppointmentType, Reason, Notes,
        AppointmentStatus, IsDeleted, CreatedAt, CreatedBy
    )
    VALUES
    (
        @AppointmentUid, @PatientUid, @PrimaryResourceId, @RoomResourceId,
        @StartDateTimeUtc, @EndDateTimeUtc,
        NULLIF(LTRIM(RTRIM(@AppointmentType)), N''),
        NULLIF(LTRIM(RTRIM(@Reason)), N''), NULLIF(LTRIM(RTRIM(@Notes)), N''),
        N'Booked', 0, SYSUTCDATETIME(), @CreatedBy
    );

    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
    BEGIN
        INSERT INTO dbo.AuditLog
            (UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
        VALUES
            (@CreatedBy, (SELECT PatientId FROM dbo.Patient WHERE PatientUid = @PatientUid),
             N'Create', N'ScheduleAppointment', CONVERT(NVARCHAR(100), @AppointmentUid),
             NULL, N'Appointment created', SYSUTCDATETIME());
    END;

    COMMIT TRANSACTION;

    SELECT
        a.AppointmentUid,
        NULLIF(LTRIM(RTRIM(CONCAT(p.LastName, N', ', p.FirstName))), N',') AS PatientDisplayName,
        a.Reason,
        a.AppointmentType,
        a.StartDateTimeUtc,
        a.EndDateTimeUtc,
        @PrimaryResourceUid AS PrimaryResourceUid
    FROM dbo.ScheduleAppointment AS a
    INNER JOIN dbo.Patient AS p ON p.PatientUid = a.PatientUid
    WHERE a.AppointmentUid = @AppointmentUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ScheduleAppointment_GetByUid
    @AppointmentUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

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
        a.AppointmentStatus AS Status,
        NULLIF(
            LTRIM(RTRIM(CONCAT(p.LastName, N', ', p.FirstName))),
            N',') AS PatientDisplayName,
        p.ChartNumber,
        primaryResource.DisplayName AS PrimaryResourceName,
        roomResource.DisplayName AS RoomResourceName,
        a.CreatedBy,
        createdByUser.DisplayName AS CreatedByDisplayName,
        a.CreatedAt,
        a.UpdatedAt,
        CAST(NULL AS VARBINARY(8)) AS RowVersion
    FROM dbo.ScheduleAppointment AS a
    INNER JOIN dbo.Patient AS p
        ON p.PatientUid = a.PatientUid
    INNER JOIN dbo.ScheduleResource AS primaryResource
        ON primaryResource.ResourceId = a.PrimaryResourceId
    LEFT JOIN dbo.ScheduleResource AS roomResource
        ON roomResource.ResourceId = a.RoomResourceId
    LEFT JOIN dbo.ApplicationUser AS createdByUser
        ON createdByUser.UserId = a.CreatedBy
    WHERE a.AppointmentUid = @AppointmentUid
        AND a.IsDeleted = 0;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ScheduleAppointment_Cancel
    @AppointmentUid UNIQUEIDENTIFIER,
    @CancelReason NVARCHAR(500) = NULL,
    @CancelledBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @PatientId BIGINT;
    DECLARE @CurrentStatus NVARCHAR(30);

    BEGIN TRANSACTION;

    SELECT
        @PatientId = p.PatientId,
        @CurrentStatus = a.AppointmentStatus
    FROM dbo.ScheduleAppointment AS a WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN dbo.Patient AS p ON p.PatientUid = a.PatientUid
    WHERE a.AppointmentUid = @AppointmentUid
        AND a.IsDeleted = 0;

    IF @CurrentStatus IS NULL
    BEGIN
        ROLLBACK TRANSACTION;
        RETURN;
    END;
    IF @CurrentStatus = N'Cancelled'
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51066, 'The appointment is already cancelled.', 1;
    END;

    UPDATE dbo.ScheduleAppointment
    SET AppointmentStatus = N'Cancelled',
        CancelledAt = SYSUTCDATETIME(),
        CancelledBy = @CancelledBy,
        CancelReason = NULLIF(LTRIM(RTRIM(@CancelReason)), N''),
        UpdatedAt = SYSUTCDATETIME(),
        UpdatedBy = @CancelledBy
    WHERE AppointmentUid = @AppointmentUid
        AND IsDeleted = 0
        AND AppointmentStatus <> N'Cancelled';

    IF @@ROWCOUNT = 0
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51066, 'The appointment is already cancelled.', 1;
    END;

    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
    BEGIN
        INSERT INTO dbo.AuditLog
            (UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
        VALUES
            (@CancelledBy, @PatientId, N'Cancel', N'ScheduleAppointment',
             CONVERT(NVARCHAR(100), @AppointmentUid), @CurrentStatus,
             N'Appointment cancelled', SYSUTCDATETIME());
    END;

    COMMIT TRANSACTION;

    SELECT AppointmentUid, AppointmentStatus, CancelledAt, CancelReason
    FROM dbo.ScheduleAppointment
    WHERE AppointmentUid = @AppointmentUid;
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
    @ModifiedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @PrimaryResourceId BIGINT;
    DECLARE @RoomResourceId BIGINT;
    DECLARE @PatientId BIGINT;
    DECLARE @CurrentStatus NVARCHAR(30);

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

    BEGIN TRANSACTION;

    SELECT
        @PatientId = p.PatientId,
        @CurrentStatus = a.AppointmentStatus
    FROM dbo.ScheduleAppointment AS a WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN dbo.Patient AS p ON p.PatientUid = a.PatientUid
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

    IF EXISTS
    (
        SELECT 1
        FROM dbo.ScheduleAppointment AS existingAppointment WITH (UPDLOCK, HOLDLOCK)
        WHERE existingAppointment.IsDeleted = 0
            AND existingAppointment.AppointmentStatus <> N'Cancelled'
            AND existingAppointment.AppointmentUid <> @AppointmentUid
            AND
            (
                existingAppointment.PrimaryResourceId IN
                    (@PrimaryResourceId, ISNULL(@RoomResourceId, @PrimaryResourceId))
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

    UPDATE dbo.ScheduleAppointment
    SET PrimaryResourceId = @PrimaryResourceId,
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

    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
    BEGIN
        INSERT INTO dbo.AuditLog
            (UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
        VALUES
            (@ModifiedBy, @PatientId, N'Update', N'ScheduleAppointment',
             CONVERT(NVARCHAR(100), @AppointmentUid), NULL,
             N'Appointment rescheduled or updated', SYSUTCDATETIME());
    END;

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
        a.AppointmentStatus AS Status,
        NULLIF(LTRIM(RTRIM(CONCAT(p.LastName, N', ', p.FirstName))), N',') AS PatientDisplayName,
        p.ChartNumber,
        primaryResource.DisplayName AS PrimaryResourceName,
        roomResource.DisplayName AS RoomResourceName,
        a.CreatedBy,
        createdByUser.DisplayName AS CreatedByDisplayName,
        a.CreatedAt,
        a.UpdatedAt
    FROM dbo.ScheduleAppointment AS a
    INNER JOIN dbo.Patient AS p ON p.PatientUid = a.PatientUid
    INNER JOIN dbo.ScheduleResource AS primaryResource ON primaryResource.ResourceId = a.PrimaryResourceId
    LEFT JOIN dbo.ScheduleResource AS roomResource ON roomResource.ResourceId = a.RoomResourceId
    LEFT JOIN dbo.ApplicationUser AS createdByUser ON createdByUser.UserId = a.CreatedBy
    WHERE a.AppointmentUid = @AppointmentUid;
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

    DECLARE @PrimaryResourceId BIGINT;
    DECLARE @RoomResourceId BIGINT;
    DECLARE @PatientId BIGINT;
    DECLARE @CurrentStatus NVARCHAR(30);

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

    BEGIN TRANSACTION;

    SELECT
        @PatientId = p.PatientId,
        @CurrentStatus = a.AppointmentStatus,
        @RoomResourceId = COALESCE(@RoomResourceId, a.RoomResourceId)
    FROM dbo.ScheduleAppointment AS a WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN dbo.Patient AS p ON p.PatientUid = a.PatientUid
    WHERE a.AppointmentUid = @AppointmentUid AND a.IsDeleted = 0;

    IF @CurrentStatus IS NULL
    BEGIN
        ROLLBACK TRANSACTION;
        RETURN;
    END;
    IF @CurrentStatus = N'Cancelled'
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51067, 'Cancelled appointments cannot be rescheduled.', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.ScheduleAppointment AS existingAppointment WITH (UPDLOCK, HOLDLOCK)
        WHERE existingAppointment.IsDeleted = 0
            AND existingAppointment.AppointmentStatus <> N'Cancelled'
            AND existingAppointment.AppointmentUid <> @AppointmentUid
            AND
            (
                existingAppointment.PrimaryResourceId IN
                    (@PrimaryResourceId, ISNULL(@RoomResourceId, @PrimaryResourceId))
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

    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
    BEGIN
        INSERT INTO dbo.AuditLog
            (UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
        VALUES
            (@ModifiedBy, @PatientId, N'Reschedule', N'ScheduleAppointment',
             CONVERT(NVARCHAR(100), @AppointmentUid), NULL,
             N'Appointment rescheduled', SYSUTCDATETIME());
    END;

    COMMIT TRANSACTION;

    SELECT
        a.AppointmentUid, a.PatientUid,
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
    INNER JOIN dbo.Patient AS p ON p.PatientUid = a.PatientUid
    INNER JOIN dbo.ScheduleResource AS primaryResource ON primaryResource.ResourceId = a.PrimaryResourceId
    LEFT JOIN dbo.ScheduleResource AS roomResource ON roomResource.ResourceId = a.RoomResourceId
    LEFT JOIN dbo.ApplicationUser AS createdByUser ON createdByUser.UserId = a.CreatedBy
    WHERE a.AppointmentUid = @AppointmentUid;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.ScheduleResource
    WHERE ResourceType = N'Provider'
        AND DisplayName = N'Dr. Smith'
)
BEGIN
    INSERT INTO dbo.ScheduleResource
    (
        ResourceType,
        DisplayName,
        ColorCode,
        IsActive,
        SortOrder
    )
    VALUES
    (
        N'Provider',
        N'Dr. Smith',
        N'#0d6efd',
        1,
        10
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.ScheduleResource
    WHERE ResourceType = N'Provider'
        AND DisplayName = N'Dr. Ahmed'
)
BEGIN
    INSERT INTO dbo.ScheduleResource
    (
        ResourceType,
        DisplayName,
        ColorCode,
        IsActive,
        SortOrder
    )
    VALUES
    (
        N'Provider',
        N'Dr. Ahmed',
        N'#198754',
        1,
        20
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.ScheduleResource
    WHERE ResourceType = N'Room'
        AND DisplayName = N'Exam Room 1'
)
BEGIN
    INSERT INTO dbo.ScheduleResource
    (
        ResourceType,
        DisplayName,
        ColorCode,
        IsActive,
        SortOrder
    )
    VALUES
    (
        N'Room',
        N'Exam Room 1',
        N'#ffc107',
        1,
        10
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.ScheduleResource
    WHERE ResourceType = N'Room'
        AND DisplayName = N'Exam Room 2'
)
BEGIN
    INSERT INTO dbo.ScheduleResource
    (
        ResourceType,
        DisplayName,
        ColorCode,
        IsActive,
        SortOrder
    )
    VALUES
    (
        N'Room',
        N'Exam Room 2',
        N'#fd7e14',
        1,
        20
    );
END;
GO
