IF COL_LENGTH(N'dbo.ScheduleAppointment', N'IsCritical') IS NULL
BEGIN
    ALTER TABLE dbo.ScheduleAppointment
        ADD IsCritical BIT NOT NULL
            CONSTRAINT DF_ScheduleAppointment_IsCritical DEFAULT (0);
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
    @CreatedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Created TABLE
    (
        AppointmentUid UNIQUEIDENTIFIER,
        PatientDisplayName NVARCHAR(401),
        Reason NVARCHAR(500),
        AppointmentType NVARCHAR(100),
        StartDateTimeUtc DATETIME2,
        EndDateTimeUtc DATETIME2,
        PrimaryResourceUid UNIQUEIDENTIFIER
    );

    BEGIN TRANSACTION;
    INSERT INTO @Created
    EXEC dbo.ScheduleAppointment_Create
        @PatientUid = @PatientUid,
        @PrimaryResourceUid = @PrimaryResourceUid,
        @RoomResourceUid = @RoomResourceUid,
        @StartDateTimeUtc = @StartDateTimeUtc,
        @EndDateTimeUtc = @EndDateTimeUtc,
        @AppointmentType = @AppointmentType,
        @Reason = @Reason,
        @Notes = @Notes,
        @CreatedBy = @CreatedBy;

    UPDATE appointment
    SET IsCritical = @IsCritical
    FROM dbo.ScheduleAppointment AS appointment
    INNER JOIN @Created AS created
        ON created.AppointmentUid = appointment.AppointmentUid;
    COMMIT TRANSACTION;

    SELECT created.*, @IsCritical AS IsCritical
    FROM @Created AS created;
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
    @ModifiedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Updated TABLE
    (
        AppointmentUid UNIQUEIDENTIFIER, PatientUid UNIQUEIDENTIFIER,
        PrimaryResourceUid UNIQUEIDENTIFIER, RoomResourceUid UNIQUEIDENTIFIER NULL,
        StartDateTimeUtc DATETIME2, EndDateTimeUtc DATETIME2,
        AppointmentType NVARCHAR(100), Reason NVARCHAR(500), Notes NVARCHAR(1000),
        Status NVARCHAR(30), PatientDisplayName NVARCHAR(401), ChartNumber NVARCHAR(100),
        PrimaryResourceName NVARCHAR(200), RoomResourceName NVARCHAR(200),
        CreatedBy BIGINT, CreatedByDisplayName NVARCHAR(200),
        CreatedAt DATETIME2, UpdatedAt DATETIME2 NULL
    );

    BEGIN TRANSACTION;
    INSERT INTO @Updated
    EXEC dbo.ScheduleAppointment_Update
        @AppointmentUid = @AppointmentUid,
        @PrimaryResourceUid = @PrimaryResourceUid,
        @RoomResourceUid = @RoomResourceUid,
        @StartDateTimeUtc = @StartDateTimeUtc,
        @EndDateTimeUtc = @EndDateTimeUtc,
        @AppointmentType = @AppointmentType,
        @Reason = @Reason,
        @Notes = @Notes,
        @ModifiedBy = @ModifiedBy;

    UPDATE appointment
    SET IsCritical = @IsCritical
    FROM dbo.ScheduleAppointment AS appointment
    INNER JOIN @Updated AS updated
        ON updated.AppointmentUid = appointment.AppointmentUid;
    COMMIT TRANSACTION;

    SELECT updated.*, @IsCritical AS IsCritical
    FROM @Updated AS updated;
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
        a.IsCritical, a.AppointmentStatus AS Status,
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
