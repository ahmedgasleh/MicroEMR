-- The Application transition policy authorizes the pair; SQL atomically preserves its source state.
-- No transition matrix, appointment state, table, permission or unrelated procedure is added here.
CREATE OR ALTER PROCEDURE dbo.ScheduleAppointment_UpdateStatus
    @AppointmentUid UNIQUEIDENTIFIER,
    @AppointmentStatus NVARCHAR(30),
    @ExpectedStatus NVARCHAR(30),
    @UpdatedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @AppointmentStatus=LTRIM(RTRIM(@AppointmentStatus));
    IF @AppointmentStatus IS NULL OR @AppointmentStatus NOT IN(N'Scheduled',N'Arrived',N'Roomed',N'Seen',N'Completed')
        THROW 51068,'Invalid appointment status.',1;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @PatientId BIGINT,@CurrentStatus NVARCHAR(30),@CanonicalCurrent NVARCHAR(30);
        SELECT @PatientId=p.PatientId,@CurrentStatus=a.AppointmentStatus
        FROM dbo.ScheduleAppointment a WITH(UPDLOCK,HOLDLOCK)
        INNER JOIN dbo.Patient p ON p.PatientUid=a.PatientUid
        WHERE a.AppointmentUid=@AppointmentUid AND a.IsDeleted=0;
        IF @CurrentStatus IS NULL BEGIN ROLLBACK; RETURN; END;
        IF @CurrentStatus=N'Cancelled' THROW 51067,'Cancelled appointments cannot be updated.',1;
        SET @CanonicalCurrent=CASE WHEN @CurrentStatus=N'Booked' THEN N'Scheduled' ELSE @CurrentStatus END;
        IF @ExpectedStatus IS NULL OR @CanonicalCurrent<>@ExpectedStatus OR @CanonicalCurrent=@AppointmentStatus
            THROW 51082,'The appointment status changed before it could be updated.',1;
        -- Successful clinical mutations require their existing audit record; fail closed if unavailable.
        IF OBJECT_ID(N'dbo.AuditLog',N'U') IS NULL THROW 51064,'Clinical audit storage is required.',1;
        UPDATE dbo.ScheduleAppointment SET AppointmentStatus=@AppointmentStatus,UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@UpdatedBy
        WHERE AppointmentUid=@AppointmentUid AND IsDeleted=0;
        INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
        VALUES(@UpdatedBy,@PatientId,N'UpdateStatus',N'ScheduleAppointment',CONVERT(NVARCHAR(100),@AppointmentUid),
            @CurrentStatus,@AppointmentStatus,SYSUTCDATETIME());
        EXEC dbo.AppointmentHistory_Create @AppointmentUid=@AppointmentUid,@ActionType=N'StatusChanged',
            @ActionDescription=N'Appointment status changed.',@OldStatus=@CurrentStatus,@NewStatus=@AppointmentStatus,
            @CreatedBy=@UpdatedBy,@ReturnResult=0;
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
    SELECT AppointmentUid,AppointmentStatus,UpdatedAt FROM dbo.ScheduleAppointment WHERE AppointmentUid=@AppointmentUid;
END;
GO
