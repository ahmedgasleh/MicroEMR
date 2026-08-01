CREATE OR ALTER PROCEDURE dbo.ScheduleAppointment_MarkArrived
    @AppointmentUid UNIQUEIDENTIFIER,
    @ExpectedStatus NVARCHAR(30),
    @UpdatedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @PatientId BIGINT;
    DECLARE @CurrentStatus NVARCHAR(30);

    BEGIN TRANSACTION;

    SELECT
        @PatientId = patient.PatientId,
        @CurrentStatus = appointment.AppointmentStatus
    FROM dbo.ScheduleAppointment AS appointment WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN dbo.Patient AS patient ON patient.PatientUid = appointment.PatientUid
    WHERE appointment.AppointmentUid = @AppointmentUid
        AND appointment.IsDeleted = 0;

    IF @CurrentStatus IS NULL
    BEGIN
        ROLLBACK TRANSACTION;
        RETURN;
    END;

    IF @ExpectedStatus <> N'Scheduled'
        OR @CurrentStatus NOT IN (N'Scheduled', N'Booked')
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51082, 'The appointment status changed before it could be marked Arrived.', 1;
    END;

    UPDATE dbo.ScheduleAppointment
    SET AppointmentStatus = N'Arrived',
        UpdatedAt = SYSUTCDATETIME(),
        UpdatedBy = @UpdatedBy
    WHERE AppointmentUid = @AppointmentUid
        AND IsDeleted = 0;

    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
    BEGIN
        INSERT INTO dbo.AuditLog
            (UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
        VALUES
            (@UpdatedBy, @PatientId, N'UpdateStatus', N'ScheduleAppointment',
             CONVERT(NVARCHAR(100), @AppointmentUid), @CurrentStatus,
             N'Arrived', SYSUTCDATETIME());
    END;

    EXEC dbo.AppointmentHistory_Create
        @AppointmentUid = @AppointmentUid,
        @ActionType = N'StatusChanged',
        @ActionDescription = N'Appointment marked Arrived.',
        @OldStatus = @CurrentStatus,
        @NewStatus = N'Arrived',
        @CreatedBy = @UpdatedBy,
        @ReturnResult = 0;

    COMMIT TRANSACTION;

    SELECT AppointmentUid, AppointmentStatus, UpdatedAt
    FROM dbo.ScheduleAppointment
    WHERE AppointmentUid = @AppointmentUid;
END;
GO
