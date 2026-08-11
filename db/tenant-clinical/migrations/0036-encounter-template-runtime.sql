SET XACT_ABORT ON;
GO

IF COL_LENGTH('dbo.PatientEncounter', 'TemplateUid') IS NULL
    ALTER TABLE dbo.PatientEncounter ADD TemplateUid UNIQUEIDENTIFIER NULL;
GO
IF COL_LENGTH('dbo.PatientEncounter', 'TemplateVersionUid') IS NULL
    ALTER TABLE dbo.PatientEncounter ADD TemplateVersionUid UNIQUEIDENTIFIER NULL;
GO
IF COL_LENGTH('dbo.PatientEncounter', 'StructuredDataJson') IS NULL
    ALTER TABLE dbo.PatientEncounter ADD StructuredDataJson NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PatientEncounter_DocumentTemplate')
    ALTER TABLE dbo.PatientEncounter ADD CONSTRAINT FK_PatientEncounter_DocumentTemplate
        FOREIGN KEY (TemplateUid) REFERENCES dbo.DocumentTemplate(TemplateUid);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PatientEncounter_DocumentTemplateVersion')
    ALTER TABLE dbo.PatientEncounter ADD CONSTRAINT FK_PatientEncounter_DocumentTemplateVersion
        FOREIGN KEY (TemplateVersionUid) REFERENCES dbo.DocumentTemplateVersion(TemplateVersionUid);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PatientEncounter') AND name=N'IX_PatientEncounter_TemplateVersionUid')
    CREATE INDEX IX_PatientEncounter_TemplateVersionUid ON dbo.PatientEncounter(TemplateVersionUid) WHERE TemplateVersionUid IS NOT NULL;
GO

CREATE OR ALTER PROCEDURE dbo.PatientEncounter_GetByUid @EncounterUid UNIQUEIDENTIFIER AS
BEGIN
 SET NOCOUNT ON;
 SELECT pe.EncounterUid,pe.PatientUid,COALESCE(pe.EncounterDateUtc,pe.EncounterDate,pe.CreatedAt) EncounterDateUtc,
  COALESCE(NULLIF(pe.EncounterType,N''),N'Office Visit') EncounterType,pe.ReasonForVisit,pe.LocationName,pe.ProviderName,
  pe.EncounterStatus,pe.CreatedBy,COALESCE(pe.CreatedByDisplayName,au.DisplayName) CreatedByDisplayName,pe.CreatedAt,pe.UpdatedAt,
  pe.EncounterNotes,pe.SubjectiveNote,pe.ObjectiveNote,pe.AssessmentNote,pe.PlanNote,pe.TemplateUid,pe.TemplateVersionUid,
  pe.StructuredDataJson,pe.SignedAt,pe.SignedBy,signedUser.DisplayName SignedByDisplayName,pe.AppointmentUid,
  appointment.StartDateTimeUtc AppointmentStartDateTime,appointment.EndDateTimeUtc AppointmentEndDateTime,
  appointment.Reason AppointmentReason,resource.DisplayName AppointmentProviderDisplayName,appointment.AppointmentStatus,pe.RowVersion
 FROM dbo.PatientEncounter pe
 LEFT JOIN dbo.ApplicationUser au ON au.UserId=pe.CreatedBy
 LEFT JOIN dbo.ApplicationUser signedUser ON signedUser.UserId=pe.SignedBy
 LEFT JOIN dbo.ScheduleAppointment appointment ON appointment.AppointmentUid=pe.AppointmentUid AND appointment.IsDeleted=0
 LEFT JOIN dbo.ScheduleResource resource ON resource.ResourceId=appointment.PrimaryResourceId
 WHERE pe.EncounterUid=@EncounterUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientEncounter_CreateStructured
 @PatientUid UNIQUEIDENTIFIER,@EncounterDateUtc DATETIME2(0),@EncounterType NVARCHAR(100),
 @ReasonForVisit NVARCHAR(500)=NULL,@LocationName NVARCHAR(200)=NULL,@ProviderName NVARCHAR(200)=NULL,
 @TemplateUid UNIQUEIDENTIFIER,@TemplateVersionUid UNIQUEIDENTIFIER,@StructuredDataJson NVARCHAR(MAX),
 @SubjectiveNote NVARCHAR(MAX)=NULL,@ObjectiveNote NVARCHAR(MAX)=NULL,@AssessmentNote NVARCHAR(MAX)=NULL,@PlanNote NVARCHAR(MAX)=NULL,
 @CreatedBy BIGINT=NULL,@CreatedByDisplayName NVARCHAR(200)=NULL AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @PatientId BIGINT,@EncounterUid UNIQUEIDENTIFIER=NEWID();
 SELECT @PatientId=PatientId FROM dbo.Patient WHERE PatientUid=@PatientUid AND IsDeleted=0;
 IF @PatientId IS NULL THROW 51041,'The requested patient was not found.',1;
 IF ISJSON(@StructuredDataJson)<>1 THROW 51110,'Structured encounter data is invalid.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.DocumentTemplate t JOIN dbo.DocumentTemplateVersion v ON v.TemplateUid=t.TemplateUid
  WHERE t.TemplateUid=@TemplateUid AND v.TemplateVersionUid=@TemplateVersionUid AND t.TemplateKind=N'Encounter'
   AND t.IsActive=1 AND v.VersionStatus=N'Published') THROW 51111,'The selected encounter template version is unavailable.',1;
 BEGIN TRANSACTION;
 INSERT dbo.PatientEncounter(EncounterUid,PatientId,PatientUid,EncounterDateUtc,EncounterType,ReasonForVisit,LocationName,ProviderName,
  EncounterStatus,Status,CreatedBy,CreatedByDisplayName,CreatedAt,TemplateUid,TemplateVersionUid,StructuredDataJson,
  SubjectiveNote,ObjectiveNote,AssessmentNote,PlanNote)
 VALUES(@EncounterUid,@PatientId,@PatientUid,@EncounterDateUtc,LTRIM(RTRIM(@EncounterType)),NULLIF(LTRIM(RTRIM(@ReasonForVisit)),N''),
  NULLIF(LTRIM(RTRIM(@LocationName)),N''),NULLIF(LTRIM(RTRIM(@ProviderName)),N''),N'Open',N'Open',@CreatedBy,
  NULLIF(LTRIM(RTRIM(@CreatedByDisplayName)),N''),SYSUTCDATETIME(),@TemplateUid,@TemplateVersionUid,@StructuredDataJson,
  NULLIF(@SubjectiveNote,N''),NULLIF(@ObjectiveNote,N''),NULLIF(@AssessmentNote,N''),NULLIF(@PlanNote,N''));
 IF OBJECT_ID(N'dbo.AuditLog',N'U') IS NOT NULL INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
  VALUES(@CreatedBy,@PatientId,N'Create',N'PatientEncounter',CONVERT(NVARCHAR(100),@EncounterUid),NULL,N'Schema encounter created',SYSUTCDATETIME());
 EXEC dbo.PatientEncounterHistory_Create @EncounterUid,@PatientUid,N'Created',N'Schema-driven encounter created.',NULL,N'Open',NULL,@CreatedBy,0;
 COMMIT; EXEC dbo.PatientEncounter_GetByUid @EncounterUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientEncounter_UpdateStructuredData
 @PatientUid UNIQUEIDENTIFIER,@EncounterUid UNIQUEIDENTIFIER,@StructuredDataJson NVARCHAR(MAX),
 @SubjectiveNote NVARCHAR(MAX)=NULL,@ObjectiveNote NVARCHAR(MAX)=NULL,@AssessmentNote NVARCHAR(MAX)=NULL,@PlanNote NVARCHAR(MAX)=NULL,
 @ExpectedRowVersion BINARY(8),@UpdatedBy BIGINT=NULL AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @PatientId BIGINT,@Status NVARCHAR(30),@CurrentRowVersion BINARY(8);
 IF ISJSON(@StructuredDataJson)<>1 THROW 51110,'Structured encounter data is invalid.',1;
 BEGIN TRANSACTION;
 SELECT @PatientId=PatientId,@Status=EncounterStatus,@CurrentRowVersion=RowVersion FROM dbo.PatientEncounter WITH(UPDLOCK,HOLDLOCK)
  WHERE PatientUid=@PatientUid AND EncounterUid=@EncounterUid AND TemplateVersionUid IS NOT NULL;
 IF @PatientId IS NULL BEGIN ROLLBACK; RETURN; END;
 IF @Status<>N'Open' BEGIN ROLLBACK; THROW 51071,'The encounter cannot be edited.',1; END;
 IF @CurrentRowVersion<>@ExpectedRowVersion BEGIN ROLLBACK; THROW 51073,'The encounter was changed by another user.',1; END;
 UPDATE dbo.PatientEncounter SET StructuredDataJson=@StructuredDataJson,SubjectiveNote=NULLIF(@SubjectiveNote,N''),
  ObjectiveNote=NULLIF(@ObjectiveNote,N''),AssessmentNote=NULLIF(@AssessmentNote,N''),PlanNote=NULLIF(@PlanNote,N''),
  UpdatedBy=@UpdatedBy,UpdatedAt=SYSUTCDATETIME() WHERE PatientUid=@PatientUid AND EncounterUid=@EncounterUid;
 IF OBJECT_ID(N'dbo.AuditLog',N'U') IS NOT NULL INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
  VALUES(@UpdatedBy,@PatientId,N'UpdateNote',N'PatientEncounter',CONVERT(NVARCHAR(100),@EncounterUid),NULL,N'Structured encounter updated',SYSUTCDATETIME());
 EXEC dbo.PatientEncounterHistory_Create @EncounterUid,@PatientUid,N'NoteUpdated',N'Structured encounter updated.',NULL,@Status,NULL,@UpdatedBy,0;
 COMMIT; EXEC dbo.PatientEncounter_GetByUid @EncounterUid;
END;
GO
