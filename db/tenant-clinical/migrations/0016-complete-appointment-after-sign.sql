CREATE OR ALTER PROCEDURE dbo.PatientEncounter_Sign
    @PatientUid UNIQUEIDENTIFIER,
    @EncounterUid UNIQUEIDENTIFIER,
    @SignedBy BIGINT = NULL,
    @ExpectedAppointmentStatus NVARCHAR(30),
    @CompletedAppointmentStatus NVARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @PatientId BIGINT;
    DECLARE @EncounterStatus NVARCHAR(30);
    DECLARE @AppointmentUid UNIQUEIDENTIFIER;
    DECLARE @AppointmentStatus NVARCHAR(30);
    DECLARE @AppointmentFound BIT;
    DECLARE @EncounterFound BIT;
    SET @EncounterFound = CONVERT(BIT, @@ROWCOUNT - @@ROWCOUNT);
    SET @AppointmentFound = CONVERT(BIT, @@ROWCOUNT - @@ROWCOUNT);

    BEGIN TRANSACTION;

    SELECT
        @PatientId = pe.PatientId,
        @EncounterStatus = pe.EncounterStatus,
        @AppointmentUid = pe.AppointmentUid,
        @EncounterFound = 1
    FROM dbo.PatientEncounter AS pe WITH (UPDLOCK, HOLDLOCK)
    WHERE pe.PatientUid = @PatientUid
        AND pe.EncounterUid = @EncounterUid;

    IF @EncounterFound = 0
    BEGIN
        ROLLBACK TRANSACTION;
        RETURN;
    END;

    IF @EncounterStatus = N'Signed'
    BEGIN
        COMMIT TRANSACTION;
        EXEC dbo.PatientEncounter_GetByUid @EncounterUid = @EncounterUid;
        RETURN;
    END;

    IF ISNULL(@EncounterStatus, N'') <> N'Open'
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51072, 'The encounter cannot be signed in its current status.', 1;
    END;

    IF @AppointmentUid IS NOT NULL
    BEGIN
        SELECT
            @AppointmentStatus = appointment.AppointmentStatus,
            @AppointmentFound = 1
        FROM dbo.ScheduleAppointment AS appointment WITH (UPDLOCK, HOLDLOCK)
        WHERE appointment.AppointmentUid = @AppointmentUid
            AND appointment.IsDeleted = 0;

        IF @AppointmentFound = 0
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 51086, 'The linked appointment was not found.', 1;
        END;

        IF @AppointmentStatus IS NULL OR @AppointmentStatus NOT IN
            (@ExpectedAppointmentStatus, @CompletedAppointmentStatus)
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 51085, 'The linked appointment cannot be completed from its current status.', 1;
        END;
    END;

    UPDATE dbo.PatientEncounter
    SET EncounterStatus = N'Signed',
        Status = N'Signed',
        SignedAt = SYSUTCDATETIME(),
        SignedBy = @SignedBy,
        UpdatedAt = SYSUTCDATETIME(),
        UpdatedBy = @SignedBy
    WHERE PatientUid = @PatientUid
        AND EncounterUid = @EncounterUid;

    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
    BEGIN
        INSERT INTO dbo.AuditLog
        (
            UserId, PatientId, ActionName, EntityName, EntityId,
            OldValue, NewValue, CreatedAt
        )
        VALUES
        (
            @SignedBy, @PatientId, N'Sign', N'PatientEncounter',
            CONVERT(NVARCHAR(100), @EncounterUid), N'Open', N'Signed',
            SYSUTCDATETIME()
        );
    END;

    EXEC dbo.PatientEncounterHistory_Create
        @EncounterUid, @PatientUid, N'Signed', N'Encounter signed.',
        @EncounterStatus, N'Signed', NULL, @SignedBy, 0;

    IF @AppointmentUid IS NOT NULL
        AND @AppointmentStatus = @ExpectedAppointmentStatus
    BEGIN
        UPDATE dbo.ScheduleAppointment
        SET AppointmentStatus = @CompletedAppointmentStatus,
            UpdatedAt = SYSUTCDATETIME(),
            UpdatedBy = @SignedBy
        WHERE AppointmentUid = @AppointmentUid
            AND IsDeleted = 0
            AND AppointmentStatus = @ExpectedAppointmentStatus;

        IF @@ROWCOUNT <> 1
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 51085, 'The linked appointment was updated by another user.', 1;
        END;

        IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
        BEGIN
            INSERT INTO dbo.AuditLog
            (
                UserId, PatientId, ActionName, EntityName, EntityId,
                OldValue, NewValue, CreatedAt
            )
            VALUES
            (
                @SignedBy, @PatientId, N'UpdateStatus', N'ScheduleAppointment',
                CONVERT(NVARCHAR(100), @AppointmentUid),
                @AppointmentStatus, @CompletedAppointmentStatus,
                SYSUTCDATETIME()
            );
        END;

        EXEC dbo.AppointmentHistory_Create
            @AppointmentUid = @AppointmentUid,
            @ActionType = N'StatusChanged',
            @ActionDescription = N'Appointment completed when encounter was signed.',
            @OldStatus = @AppointmentStatus,
            @NewStatus = @CompletedAppointmentStatus,
            @CreatedBy = @SignedBy,
            @ReturnResult = 0;
    END;

    COMMIT TRANSACTION;

    EXEC dbo.PatientEncounter_GetByUid
        @EncounterUid = @EncounterUid;
END;
GO
