CREATE OR ALTER PROCEDURE dbo.PatientEncounter_StartFromAppointment
    @AppointmentUid UNIQUEIDENTIFIER,
    @CreatedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @PatientUid UNIQUEIDENTIFIER;
    DECLARE @PatientId BIGINT;
    DECLARE @AppointmentStatus NVARCHAR(30);
    DECLARE @AppointmentDateUtc DATETIME2(0);
    DECLARE @AppointmentType NVARCHAR(100);
    DECLARE @ReasonForVisit NVARCHAR(500);
    DECLARE @EncounterUid UNIQUEIDENTIFIER;
    DECLARE @WasCreated BIT;

    SET @WasCreated = CONVERT(BIT, @@ROWCOUNT - @@ROWCOUNT);

    BEGIN TRANSACTION;

    SELECT
        @PatientUid = appointment.PatientUid,
        @PatientId = patient.PatientId,
        @AppointmentStatus = appointment.AppointmentStatus,
        @AppointmentDateUtc = appointment.StartDateTimeUtc,
        @AppointmentType = appointment.AppointmentType,
        @ReasonForVisit = appointment.Reason
    FROM dbo.ScheduleAppointment AS appointment WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN dbo.Patient AS patient ON patient.PatientUid = appointment.PatientUid
    WHERE appointment.AppointmentUid = @AppointmentUid
        AND appointment.IsDeleted = 0
        AND patient.IsDeleted = 0;

    IF @PatientUid IS NULL
    BEGIN
        ROLLBACK TRANSACTION;
        RETURN;
    END;

    IF @AppointmentStatus = N'Cancelled'
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51069, 'Cancelled appointments cannot start encounters.', 1;
    END;

    IF @AppointmentStatus = N'Completed'
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51070, 'Completed appointments cannot start new encounters.', 1;
    END;

    IF @AppointmentStatus = N'NoShow'
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51083, 'No-show appointments cannot start encounters.', 1;
    END;

    SELECT @EncounterUid = EncounterUid
    FROM dbo.PatientEncounter WITH (UPDLOCK, HOLDLOCK)
    WHERE AppointmentUid = @AppointmentUid;

    IF @EncounterUid IS NOT NULL
    BEGIN
        IF @AppointmentStatus <> N'Seen'
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 51084, 'The linked encounter and appointment status are inconsistent.', 1;
        END;
    END
    ELSE
    BEGIN
        IF @AppointmentStatus NOT IN (N'Scheduled', N'Arrived', N'CheckedIn', N'Roomed')
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 51084, 'The appointment cannot start an encounter from its current status.', 1;
        END;

        SET @EncounterUid = NEWID();
        SET @WasCreated = CONVERT(BIT, SIGN(@@TRANCOUNT));

        INSERT INTO dbo.PatientEncounter
        (
            EncounterUid, AppointmentUid, PatientId, PatientUid,
            EncounterDateUtc, EncounterType, ReasonForVisit,
            EncounterStatus, Status, CreatedBy, CreatedAt
        )
        VALUES
        (
            @EncounterUid, @AppointmentUid, @PatientId, @PatientUid,
            @AppointmentDateUtc,
            COALESCE(NULLIF(LTRIM(RTRIM(@AppointmentType)), N''), N'Scheduled Visit'),
            NULLIF(LTRIM(RTRIM(@ReasonForVisit)), N''),
            N'Open', N'Open', @CreatedBy, SYSUTCDATETIME()
        );

        UPDATE dbo.ScheduleAppointment
        SET AppointmentStatus = N'Seen',
            UpdatedAt = SYSUTCDATETIME(),
            UpdatedBy = @CreatedBy
        WHERE AppointmentUid = @AppointmentUid
            AND IsDeleted = 0;

        IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
        BEGIN
            INSERT INTO dbo.AuditLog
                (UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
            VALUES
                (@CreatedBy, @PatientId, N'Create', N'PatientEncounter',
                 CONVERT(NVARCHAR(100), @EncounterUid), NULL,
                 N'Encounter started from appointment', SYSUTCDATETIME()),
                (@CreatedBy, @PatientId, N'UpdateStatus', N'ScheduleAppointment',
                 CONVERT(NVARCHAR(100), @AppointmentUid), @AppointmentStatus,
                 N'Seen', SYSUTCDATETIME());
        END;

        EXEC dbo.PatientEncounterHistory_Create
            @EncounterUid, @PatientUid, N'Created', N'Encounter created.',
            NULL, N'Open', NULL, @CreatedBy, 0;

        EXEC dbo.AppointmentHistory_Create
            @AppointmentUid = @AppointmentUid,
            @ActionType = N'StatusChanged',
            @ActionDescription = N'Encounter started from appointment.',
            @OldStatus = @AppointmentStatus,
            @NewStatus = N'Seen',
            @CreatedBy = @CreatedBy,
            @ReturnResult = 0;
    END;

    COMMIT TRANSACTION;

    SELECT
        EncounterUid,
        PatientUid,
        AppointmentUid,
        EncounterDateUtc AS EncounterDate,
        EncounterType,
        ReasonForVisit,
        EncounterStatus AS Status,
        @WasCreated AS WasCreated
    FROM dbo.PatientEncounter
    WHERE EncounterUid = @EncounterUid;
END;
GO
