/* Encounter lifecycle hardening. Existing Open rows are the Draft state. */
IF COL_LENGTH(N'dbo.PatientEncounterAddendum', N'ReasonForAmendment') IS NULL
    ALTER TABLE dbo.PatientEncounterAddendum ADD ReasonForAmendment NVARCHAR(500) NULL;
GO
IF COL_LENGTH(N'dbo.PatientEncounterAddendum', N'SignedBy') IS NULL
    ALTER TABLE dbo.PatientEncounterAddendum ADD SignedBy BIGINT NULL;
GO
IF COL_LENGTH(N'dbo.PatientEncounterAddendum', N'SignedAt') IS NULL
    ALTER TABLE dbo.PatientEncounterAddendum ADD SignedAt DATETIME2(0) NULL;
GO
IF COL_LENGTH(N'dbo.PatientEncounterAddendum', N'RowVersion') IS NULL
    ALTER TABLE dbo.PatientEncounterAddendum ADD RowVersion ROWVERSION;
GO

UPDATE dbo.PatientEncounterAddendum
SET ReasonForAmendment = COALESCE(NULLIF(ReasonForAmendment, N''), N'Legacy addendum'),
    SignedBy = COALESCE(SignedBy, CreatedBy),
    SignedAt = COALESCE(SignedAt, CreatedAt)
WHERE ReasonForAmendment IS NULL OR SignedAt IS NULL;
GO

CREATE OR ALTER PROCEDURE dbo.PatientEncounterAddendum_GetByEncounterUid
    @PatientUid UNIQUEIDENTIFIER, @EncounterUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.EncounterAddendumUid, a.EncounterUid, a.PatientUid, a.AddendumText,
           a.ReasonForAmendment, a.CreatedAt, a.CreatedBy,
           creator.DisplayName AS CreatedByDisplayName, a.SignedBy,
           signer.DisplayName AS SignedByDisplayName, a.SignedAt, a.RowVersion
    FROM dbo.PatientEncounterAddendum a
    LEFT JOIN dbo.ApplicationUser creator ON creator.UserId = a.CreatedBy
    LEFT JOIN dbo.ApplicationUser signer ON signer.UserId = a.SignedBy
    WHERE a.PatientUid=@PatientUid AND a.EncounterUid=@EncounterUid
    ORDER BY a.CreatedAt, a.EncounterAddendumId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientEncounterAddendum_Create
    @PatientUid UNIQUEIDENTIFIER, @EncounterUid UNIQUEIDENTIFIER,
    @AddendumText NVARCHAR(MAX), @ReasonForAmendment NVARCHAR(500), @CreatedBy BIGINT=NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF NULLIF(LTRIM(RTRIM(@AddendumText)),N'') IS NULL THROW 51074, 'Addendum text is required.', 1;
    IF NULLIF(LTRIM(RTRIM(@ReasonForAmendment)),N'') IS NULL THROW 51074, 'A reason for amendment is required.', 1;
    DECLARE @Status NVARCHAR(30), @Uid UNIQUEIDENTIFIER=NEWID(), @Now DATETIME2(0)=SYSUTCDATETIME();
    BEGIN TRANSACTION;
    SELECT @Status=EncounterStatus FROM dbo.PatientEncounter WITH(UPDLOCK,HOLDLOCK)
      WHERE PatientUid=@PatientUid AND EncounterUid=@EncounterUid;
    IF @Status IS NULL BEGIN ROLLBACK; RETURN; END;
    IF @Status<>N'Signed' BEGIN ROLLBACK; THROW 51075, 'Addendums can only be added to signed encounters.', 1; END;
    INSERT dbo.PatientEncounterAddendum
      (EncounterAddendumUid,EncounterUid,PatientUid,AddendumText,ReasonForAmendment,CreatedBy,SignedBy,SignedAt)
    VALUES (@Uid,@EncounterUid,@PatientUid,LTRIM(RTRIM(@AddendumText)),LTRIM(RTRIM(@ReasonForAmendment)),@CreatedBy,@CreatedBy,@Now);
    EXEC dbo.PatientEncounterHistory_Create @EncounterUid,@PatientUid,N'AmendmentCreated',N'Signed encounter amendment created.',N'Signed',N'Signed',@ReasonForAmendment,@CreatedBy,0;
    COMMIT;
    SELECT a.EncounterAddendumUid,a.EncounterUid,a.PatientUid,a.AddendumText,a.ReasonForAmendment,
           a.CreatedAt,a.CreatedBy,creator.DisplayName CreatedByDisplayName,a.SignedBy,
           signer.DisplayName SignedByDisplayName,a.SignedAt,a.RowVersion
    FROM dbo.PatientEncounterAddendum a
    LEFT JOIN dbo.ApplicationUser creator ON creator.UserId=a.CreatedBy
    LEFT JOIN dbo.ApplicationUser signer ON signer.UserId=a.SignedBy
    WHERE a.EncounterAddendumUid=@Uid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientEncounter_UpdateNote
 @PatientUid UNIQUEIDENTIFIER,@EncounterUid UNIQUEIDENTIFIER,@RowVersion BINARY(8),
 @EncounterNotes NVARCHAR(MAX)=NULL,@UpdatedBy BIGINT=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @PatientId BIGINT,@Status NVARCHAR(30),@Current BINARY(8);
 BEGIN TRANSACTION;
 SELECT @PatientId=PatientId,@Status=EncounterStatus,@Current=RowVersion FROM dbo.PatientEncounter WITH(UPDLOCK,HOLDLOCK)
 WHERE PatientUid=@PatientUid AND EncounterUid=@EncounterUid;
 IF @Status IS NULL BEGIN ROLLBACK; RETURN; END;
 IF @Status<>N'Open' BEGIN ROLLBACK; THROW 51071,'The encounter note cannot be edited in its current status.',1; END;
 IF @Current<>@RowVersion BEGIN ROLLBACK; THROW 51076,'The encounter was changed by another user.',1; END;
 UPDATE dbo.PatientEncounter SET EncounterNotes=NULLIF(@EncounterNotes,N''),UpdatedBy=@UpdatedBy,UpdatedAt=SYSUTCDATETIME()
 WHERE PatientUid=@PatientUid AND EncounterUid=@EncounterUid;
 IF OBJECT_ID(N'dbo.AuditLog',N'U') IS NOT NULL INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
 VALUES(@UpdatedBy,@PatientId,N'UpdateNote',N'PatientEncounter',CONVERT(NVARCHAR(100),@EncounterUid),NULL,N'Encounter note updated',SYSUTCDATETIME());
 EXEC dbo.PatientEncounterHistory_Create @EncounterUid,@PatientUid,N'NoteUpdated',N'Encounter note updated.',NULL,@Status,NULL,@UpdatedBy,0;
 COMMIT; EXEC dbo.PatientEncounter_GetByUid @EncounterUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientEncounter_GetByPatientUid @PatientUid UNIQUEIDENTIFIER
AS
BEGIN
 SET NOCOUNT ON;
 SELECT pe.EncounterUid,pe.PatientUid,COALESCE(pe.EncounterDateUtc,pe.EncounterDate,pe.CreatedAt) EncounterDateUtc,
 COALESCE(NULLIF(pe.EncounterType,N''),N'Office Visit') EncounterType,pe.ReasonForVisit,pe.LocationName,pe.ProviderName,
 pe.EncounterStatus,pe.CreatedBy,COALESCE(pe.CreatedByDisplayName,au.DisplayName) CreatedByDisplayName,
 pe.CreatedAt,pe.UpdatedAt,pe.SignedAt,
 CONVERT(BIT,CASE WHEN EXISTS(SELECT 1 FROM dbo.PatientEncounterAddendum a WHERE a.EncounterUid=pe.EncounterUid AND a.PatientUid=pe.PatientUid) THEN 1 ELSE 0 END) HasAmendments
 FROM dbo.PatientEncounter pe LEFT JOIN dbo.ApplicationUser au ON au.UserId=pe.CreatedBy
 WHERE pe.PatientUid=@PatientUid
 ORDER BY COALESCE(pe.EncounterDateUtc,pe.EncounterDate,pe.CreatedAt) DESC,pe.CreatedAt DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientEncounter_GetByUid @EncounterUid UNIQUEIDENTIFIER
AS
BEGIN
 SET NOCOUNT ON;
 SELECT pe.EncounterUid,pe.PatientUid,COALESCE(pe.EncounterDateUtc,pe.EncounterDate,pe.CreatedAt) EncounterDateUtc,
 COALESCE(NULLIF(pe.EncounterType,N''),N'Office Visit') EncounterType,pe.ReasonForVisit,pe.LocationName,pe.ProviderName,
 pe.EncounterStatus,pe.CreatedBy,COALESCE(pe.CreatedByDisplayName,au.DisplayName) CreatedByDisplayName,pe.CreatedAt,pe.UpdatedAt,
 pe.EncounterNotes,pe.SubjectiveNote,pe.ObjectiveNote,pe.AssessmentNote,pe.PlanNote,pe.SignedAt,pe.SignedBy,
 signedUser.DisplayName SignedByDisplayName,pe.AppointmentUid,appointment.StartDateTimeUtc AppointmentStartDateTime,
 appointment.EndDateTimeUtc AppointmentEndDateTime,appointment.Reason AppointmentReason,
 resource.DisplayName AppointmentProviderDisplayName,appointment.AppointmentStatus,pe.RowVersion,
 CONVERT(BIT,CASE WHEN EXISTS(SELECT 1 FROM dbo.PatientEncounterAddendum a WHERE a.EncounterUid=pe.EncounterUid AND a.PatientUid=pe.PatientUid) THEN 1 ELSE 0 END) HasAmendments
 FROM dbo.PatientEncounter pe
 LEFT JOIN dbo.ApplicationUser au ON au.UserId=pe.CreatedBy
 LEFT JOIN dbo.ApplicationUser signedUser ON signedUser.UserId=pe.SignedBy
 LEFT JOIN dbo.ScheduleAppointment appointment ON appointment.AppointmentUid=pe.AppointmentUid AND appointment.IsDeleted=0
 LEFT JOIN dbo.ScheduleResource resource ON resource.ResourceId=appointment.PrimaryResourceId
 WHERE pe.EncounterUid=@EncounterUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientEncounter_UpdateSoapNote
 @PatientUid UNIQUEIDENTIFIER,@EncounterUid UNIQUEIDENTIFIER,@RowVersion BINARY(8),@SubjectiveNote NVARCHAR(MAX)=NULL,
 @ObjectiveNote NVARCHAR(MAX)=NULL,@AssessmentNote NVARCHAR(MAX)=NULL,@PlanNote NVARCHAR(MAX)=NULL,
 @UpdatedBy BIGINT=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @PatientId BIGINT,@Status NVARCHAR(30),@Current BINARY(8);
 BEGIN TRANSACTION;
 SELECT @PatientId=PatientId,@Status=EncounterStatus,@Current=RowVersion FROM dbo.PatientEncounter WITH(UPDLOCK,HOLDLOCK)
 WHERE PatientUid=@PatientUid AND EncounterUid=@EncounterUid;
 IF @Status IS NULL BEGIN ROLLBACK; RETURN; END;
 IF @Status<>N'Open' BEGIN ROLLBACK; THROW 51071,'The encounter note cannot be edited in its current status.',1; END;
 IF @Current<>@RowVersion BEGIN ROLLBACK; THROW 51076,'The encounter was changed by another user.',1; END;
 UPDATE dbo.PatientEncounter SET SubjectiveNote=NULLIF(@SubjectiveNote,N''),ObjectiveNote=NULLIF(@ObjectiveNote,N''),
 AssessmentNote=NULLIF(@AssessmentNote,N''),PlanNote=NULLIF(@PlanNote,N''),UpdatedBy=@UpdatedBy,UpdatedAt=SYSUTCDATETIME()
 WHERE PatientUid=@PatientUid AND EncounterUid=@EncounterUid;
 IF OBJECT_ID(N'dbo.AuditLog',N'U') IS NOT NULL INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
 VALUES(@UpdatedBy,@PatientId,N'UpdateNote',N'PatientEncounter',CONVERT(NVARCHAR(100),@EncounterUid),NULL,N'Encounter SOAP note updated',SYSUTCDATETIME());
 EXEC dbo.PatientEncounterHistory_Create @EncounterUid,@PatientUid,N'NoteUpdated',N'Encounter SOAP note updated.',NULL,@Status,NULL,@UpdatedBy,0;
 COMMIT; EXEC dbo.PatientEncounter_GetByUid @EncounterUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientEncounter_Sign
 @PatientUid UNIQUEIDENTIFIER,@EncounterUid UNIQUEIDENTIFIER,@RowVersion BINARY(8),@SignedBy BIGINT=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @PatientId BIGINT,@Status NVARCHAR(30),@Current BINARY(8),@EncounterDate DATETIME2(0),
 @Type NVARCHAR(100),@Provider NVARCHAR(200),@Reason NVARCHAR(500),@HasNote BIT=0;
 BEGIN TRANSACTION;
 SELECT @PatientId=PatientId,@Status=EncounterStatus,@Current=RowVersion,@EncounterDate=COALESCE(EncounterDateUtc,EncounterDate),
 @Type=EncounterType,@Provider=ProviderName,@Reason=ReasonForVisit,
 @HasNote=CASE WHEN NULLIF(LTRIM(RTRIM(COALESCE(EncounterNotes,N'')+COALESCE(SubjectiveNote,N'')+COALESCE(ObjectiveNote,N'')+COALESCE(AssessmentNote,N'')+COALESCE(PlanNote,N''))),N'') IS NULL THEN 0 ELSE 1 END
 FROM dbo.PatientEncounter WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND EncounterUid=@EncounterUid;
 IF @Status IS NULL BEGIN ROLLBACK; RETURN; END;
 IF @Status<>N'Open' BEGIN ROLLBACK; THROW 51072,'Only a draft encounter can be signed.',1; END;
 IF @Current<>@RowVersion BEGIN ROLLBACK; THROW 51076,'The encounter was changed by another user.',1; END;
 IF @EncounterDate IS NULL OR NULLIF(LTRIM(RTRIM(@Type)),N'') IS NULL OR NULLIF(LTRIM(RTRIM(@Provider)),N'') IS NULL
    OR NULLIF(LTRIM(RTRIM(@Reason)),N'') IS NULL OR @HasNote=0
 BEGIN ROLLBACK; THROW 51077,'The encounter is missing information required for signing.',1; END;
 UPDATE dbo.PatientEncounter SET EncounterStatus=N'Signed',Status=N'Signed',SignedAt=SYSUTCDATETIME(),SignedBy=@SignedBy,
 UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@SignedBy WHERE PatientUid=@PatientUid AND EncounterUid=@EncounterUid;
 IF OBJECT_ID(N'dbo.AuditLog',N'U') IS NOT NULL INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
 VALUES(@SignedBy,@PatientId,N'Sign',N'PatientEncounter',CONVERT(NVARCHAR(100),@EncounterUid),N'Open',N'Signed',SYSUTCDATETIME());
 EXEC dbo.PatientEncounterHistory_Create @EncounterUid,@PatientUid,N'Signed',N'Encounter signed.',N'Open',N'Signed',NULL,@SignedBy,0;
 COMMIT; EXEC dbo.PatientEncounter_GetByUid @EncounterUid;
END;
GO
