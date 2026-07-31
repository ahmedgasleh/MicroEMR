/* Practical clinic scheduling status workflow and optimistic concurrency. */
IF COL_LENGTH(N'dbo.ScheduleAppointment', N'RowVersion') IS NULL
    ALTER TABLE dbo.ScheduleAppointment ADD RowVersion ROWVERSION;
GO
UPDATE dbo.ScheduleAppointment SET AppointmentStatus=N'Scheduled' WHERE AppointmentStatus=N'Booked';
GO
CREATE OR ALTER PROCEDURE dbo.ScheduleAppointment_UpdateStatus
 @AppointmentUid UNIQUEIDENTIFIER,@ExpectedCurrentStatus NVARCHAR(30),@AppointmentStatus NVARCHAR(30),
 @RowVersion BINARY(8),@Reason NVARCHAR(500)=NULL,@UpdatedBy BIGINT=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 SET @ExpectedCurrentStatus=LTRIM(RTRIM(@ExpectedCurrentStatus)); SET @AppointmentStatus=LTRIM(RTRIM(@AppointmentStatus));
 IF @ExpectedCurrentStatus NOT IN(N'Scheduled',N'Confirmed',N'Arrived',N'CheckedIn',N'Roomed',N'Seen',N'Completed',N'Cancelled',N'NoShow')
    OR @AppointmentStatus NOT IN(N'Confirmed',N'Arrived',N'CheckedIn',N'Roomed',N'Seen',N'Completed',N'NoShow')
    THROW 51068,'Invalid appointment status.',1;
 DECLARE @Current NVARCHAR(30),@CurrentVersion BINARY(8),@PatientId BIGINT;
 BEGIN TRANSACTION;
 SELECT @Current=a.AppointmentStatus,@CurrentVersion=a.RowVersion,@PatientId=p.PatientId
 FROM dbo.ScheduleAppointment a WITH(UPDLOCK,HOLDLOCK) INNER JOIN dbo.Patient p ON p.PatientUid=a.PatientUid
 WHERE a.AppointmentUid=@AppointmentUid AND a.IsDeleted=0;
 IF @Current IS NULL BEGIN ROLLBACK; RETURN; END;
 IF @Current<>@ExpectedCurrentStatus OR @CurrentVersion<>@RowVersion BEGIN ROLLBACK; THROW 51079,'Appointment concurrency conflict.',1; END;
 IF NOT ((@Current=N'Scheduled' AND @AppointmentStatus IN(N'Confirmed',N'Arrived',N'NoShow')) OR
   (@Current=N'Confirmed' AND @AppointmentStatus IN(N'Arrived',N'CheckedIn',N'NoShow')) OR
   (@Current=N'Arrived' AND @AppointmentStatus IN(N'CheckedIn',N'Roomed')) OR
   (@Current=N'CheckedIn' AND @AppointmentStatus IN(N'Roomed',N'Seen')) OR
   (@Current=N'Roomed' AND @AppointmentStatus=N'Seen') OR (@Current=N'Seen' AND @AppointmentStatus=N'Completed'))
 BEGIN ROLLBACK; THROW 51078,'Invalid appointment status transition.',1; END;
 UPDATE dbo.ScheduleAppointment SET AppointmentStatus=@AppointmentStatus,UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@UpdatedBy WHERE AppointmentUid=@AppointmentUid;
 IF OBJECT_ID(N'dbo.AuditLog',N'U') IS NOT NULL INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
 VALUES(@UpdatedBy,@PatientId,N'UpdateStatus',N'ScheduleAppointment',CONVERT(NVARCHAR(100),@AppointmentUid),@Current,@AppointmentStatus,SYSUTCDATETIME());
 EXEC dbo.AppointmentHistory_Create @AppointmentUid=@AppointmentUid,@ActionType=N'StatusChanged',@ActionDescription=N'Appointment status changed.',@OldStatus=@Current,@NewStatus=@AppointmentStatus,@Reason=@Reason,@CreatedBy=@UpdatedBy,@ReturnResult=0;
 COMMIT; SELECT AppointmentUid,AppointmentStatus,UpdatedAt,RowVersion FROM dbo.ScheduleAppointment WHERE AppointmentUid=@AppointmentUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.ScheduleAppointment_Cancel @AppointmentUid UNIQUEIDENTIFIER,@CancelReason NVARCHAR(500)=NULL,@CancelledBy BIGINT=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @PatientId BIGINT,@Current NVARCHAR(30);
 BEGIN TRANSACTION;
 SELECT @PatientId=p.PatientId,@Current=a.AppointmentStatus FROM dbo.ScheduleAppointment a WITH(UPDLOCK,HOLDLOCK)
 INNER JOIN dbo.Patient p ON p.PatientUid=a.PatientUid WHERE a.AppointmentUid=@AppointmentUid AND a.IsDeleted=0;
 IF @Current IS NULL BEGIN ROLLBACK; RETURN; END;
 IF @Current=N'Cancelled' BEGIN COMMIT; SELECT AppointmentUid,AppointmentStatus,CancelledAt,CancelReason FROM dbo.ScheduleAppointment WHERE AppointmentUid=@AppointmentUid; RETURN; END;
 IF @Current IN(N'Completed',N'NoShow') BEGIN ROLLBACK; THROW 51081,'Terminal appointments cannot be cancelled.',1; END;
 UPDATE dbo.ScheduleAppointment SET AppointmentStatus=N'Cancelled',CancelledAt=SYSUTCDATETIME(),CancelledBy=@CancelledBy,
 CancelReason=NULLIF(LTRIM(RTRIM(@CancelReason)),N''),UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@CancelledBy WHERE AppointmentUid=@AppointmentUid;
 IF OBJECT_ID(N'dbo.AuditLog',N'U') IS NOT NULL INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
 VALUES(@CancelledBy,@PatientId,N'Cancel',N'ScheduleAppointment',CONVERT(NVARCHAR(100),@AppointmentUid),@Current,N'Appointment cancelled',SYSUTCDATETIME());
 EXEC dbo.AppointmentHistory_Create @AppointmentUid=@AppointmentUid,@ActionType=N'Cancelled',@ActionDescription=N'Appointment cancelled.',
 @OldStatus=@Current,@NewStatus=N'Cancelled',@Reason=@CancelReason,@CreatedBy=@CancelledBy,@ReturnResult=0;
 COMMIT; SELECT AppointmentUid,AppointmentStatus,CancelledAt,CancelReason FROM dbo.ScheduleAppointment WHERE AppointmentUid=@AppointmentUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.ScheduleAppointment_GetByUid @AppointmentUid UNIQUEIDENTIFIER
AS
BEGIN
 SET NOCOUNT ON;
 SELECT a.AppointmentUid,a.PatientUid,pr.ResourceUid PrimaryResourceUid,rr.ResourceUid RoomResourceUid,a.StartDateTimeUtc,a.EndDateTimeUtc,
 a.AppointmentType,a.Reason,a.Notes,a.AppointmentStatus Status,NULLIF(LTRIM(RTRIM(CONCAT(p.LastName,N', ',p.FirstName))),N',') PatientDisplayName,
 p.ChartNumber,pr.DisplayName PrimaryResourceName,rr.DisplayName RoomResourceName,a.CreatedBy,u.DisplayName CreatedByDisplayName,a.CreatedAt,a.UpdatedAt,
 e.EncounterUid LinkedEncounterUid,e.EncounterStatus LinkedEncounterStatus,a.RowVersion
 FROM dbo.ScheduleAppointment a INNER JOIN dbo.Patient p ON p.PatientUid=a.PatientUid INNER JOIN dbo.ScheduleResource pr ON pr.ResourceId=a.PrimaryResourceId
 LEFT JOIN dbo.ScheduleResource rr ON rr.ResourceId=a.RoomResourceId LEFT JOIN dbo.ApplicationUser u ON u.UserId=a.CreatedBy
 LEFT JOIN dbo.PatientEncounter e ON e.AppointmentUid=a.AppointmentUid WHERE a.AppointmentUid=@AppointmentUid AND a.IsDeleted=0;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientEncounter_StartFromAppointment @AppointmentUid UNIQUEIDENTIFIER,@CreatedBy BIGINT=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @PatientUid UNIQUEIDENTIFIER,@PatientId BIGINT,@Status NVARCHAR(30),@Date DATETIME2(0),@Type NVARCHAR(100),@Reason NVARCHAR(500),
 @Provider NVARCHAR(200),@EncounterUid UNIQUEIDENTIFIER,@WasCreated BIT;
 SET @WasCreated=CONVERT(BIT,@@ROWCOUNT-@@ROWCOUNT);
 BEGIN TRANSACTION;
 SELECT @PatientUid=a.PatientUid,@PatientId=p.PatientId,@Status=a.AppointmentStatus,@Date=a.StartDateTimeUtc,@Type=a.AppointmentType,@Reason=a.Reason,@Provider=r.DisplayName
 FROM dbo.ScheduleAppointment a WITH(UPDLOCK,HOLDLOCK) INNER JOIN dbo.Patient p ON p.PatientUid=a.PatientUid INNER JOIN dbo.ScheduleResource r ON r.ResourceId=a.PrimaryResourceId
 WHERE a.AppointmentUid=@AppointmentUid AND a.IsDeleted=0 AND p.IsDeleted=0;
 IF @PatientUid IS NULL BEGIN ROLLBACK; RETURN; END;
 IF @Status=N'Cancelled' BEGIN ROLLBACK; THROW 51069,'Cancelled appointments cannot start encounters.',1; END;
 IF @Status=N'Completed' BEGIN ROLLBACK; THROW 51070,'Completed appointments cannot start new encounters.',1; END;
 IF @Status=N'NoShow' BEGIN ROLLBACK; THROW 51080,'No-show appointments cannot start encounters.',1; END;
 SELECT @EncounterUid=EncounterUid FROM dbo.PatientEncounter WITH(UPDLOCK,HOLDLOCK) WHERE AppointmentUid=@AppointmentUid;
 IF @EncounterUid IS NULL BEGIN
   SET @EncounterUid=NEWID(); SET @WasCreated=CONVERT(BIT,SIGN(@@TRANCOUNT));
   INSERT dbo.PatientEncounter(EncounterUid,AppointmentUid,PatientId,PatientUid,EncounterDateUtc,EncounterType,ReasonForVisit,ProviderName,EncounterStatus,Status,CreatedBy,CreatedAt)
   VALUES(@EncounterUid,@AppointmentUid,@PatientId,@PatientUid,@Date,COALESCE(NULLIF(LTRIM(RTRIM(@Type)),N''),N'Scheduled Visit'),NULLIF(LTRIM(RTRIM(@Reason)),N''),@Provider,N'Open',N'Open',@CreatedBy,SYSUTCDATETIME());
   IF OBJECT_ID(N'dbo.AuditLog',N'U') IS NOT NULL INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
   VALUES(@CreatedBy,@PatientId,N'Create',N'PatientEncounter',CONVERT(NVARCHAR(100),@EncounterUid),NULL,N'Encounter started from appointment',SYSUTCDATETIME());
   EXEC dbo.PatientEncounterHistory_Create @EncounterUid,@PatientUid,N'Created',N'Encounter created.',NULL,N'Open',NULL,@CreatedBy,0;
 END;
 IF @Status<>N'Seen' BEGIN
   UPDATE dbo.ScheduleAppointment SET AppointmentStatus=N'Seen',UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@CreatedBy WHERE AppointmentUid=@AppointmentUid;
   IF OBJECT_ID(N'dbo.AuditLog',N'U') IS NOT NULL INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
   VALUES(@CreatedBy,@PatientId,N'UpdateStatus',N'ScheduleAppointment',CONVERT(NVARCHAR(100),@AppointmentUid),@Status,N'Seen',SYSUTCDATETIME());
   EXEC dbo.AppointmentHistory_Create @AppointmentUid=@AppointmentUid,@ActionType=N'EncounterStarted',@ActionDescription=N'Encounter started from appointment.',@OldStatus=@Status,@NewStatus=N'Seen',@CreatedBy=@CreatedBy,@ReturnResult=0;
 END;
 COMMIT;
 SELECT EncounterUid,PatientUid,AppointmentUid,EncounterDateUtc EncounterDate,EncounterType,ReasonForVisit,EncounterStatus Status,@WasCreated WasCreated
 FROM dbo.PatientEncounter WHERE EncounterUid=@EncounterUid;
END;
GO
