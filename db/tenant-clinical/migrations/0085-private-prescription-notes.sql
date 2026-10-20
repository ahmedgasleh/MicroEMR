-- Step 121: protected clinical notes. Never join these tables into public output.
SET XACT_ABORT ON;
ALTER TABLE dbo.PatientPrescription ADD CONSTRAINT UQ_PatientPrescription_PatientPair UNIQUE(PrescriptionUid,PatientUid);
CREATE TABLE dbo.PatientPrescriptionPrivateNote
(
 PrivateNoteId bigint IDENTITY PRIMARY KEY,
 PrivateNoteUid uniqueidentifier NOT NULL UNIQUE,
 PrescriptionUid uniqueidentifier NOT NULL UNIQUE,
 PatientUid uniqueidentifier NOT NULL,
 OriginalPrescriberUserId bigint NOT NULL,
 NoteText nvarchar(1000) NULL,
 CreatedBy bigint NOT NULL,CreatedAtUtc datetime2(7) NOT NULL,
 UpdatedBy bigint NULL,UpdatedAtUtc datetime2(7) NULL,
 RevisionNumber int NOT NULL,
 CONSTRAINT FK_PrivateNote_Prescription FOREIGN KEY(PrescriptionUid,PatientUid)
 REFERENCES dbo.PatientPrescription(PrescriptionUid,PatientUid)
);
CREATE TABLE dbo.PatientPrescriptionPrivateNoteRevision
(
 RevisionUid uniqueidentifier NOT NULL PRIMARY KEY,
 PrivateNoteUid uniqueidentifier NOT NULL,
 RevisionNumber int NOT NULL,
 Operation nvarchar(10) NOT NULL CHECK(Operation IN(N'Create',N'Update',N'Clear')),
 NoteText nvarchar(1000) NULL,
 ActorUserId bigint NOT NULL,CreatedAtUtc datetime2(7) NOT NULL,
 RequestCorrelationId nvarchar(100) NOT NULL,AuditEventUid uniqueidentifier NOT NULL UNIQUE,
 CONSTRAINT FK_PrivateNoteRevision_Note FOREIGN KEY(PrivateNoteUid)
 REFERENCES dbo.PatientPrescriptionPrivateNote(PrivateNoteUid),
 CONSTRAINT UQ_PrivateNoteRevision_Number UNIQUE(PrivateNoteUid,RevisionNumber)
);
-- Procedure ownership chains permit application operations without direct content access.
-- Explicit denials also prevent broad db_datareader/db_datawriter grants exposing text.
DENY SELECT,INSERT,UPDATE,DELETE ON dbo.PatientPrescriptionPrivateNote TO public;
DENY SELECT,INSERT,UPDATE,DELETE ON dbo.PatientPrescriptionPrivateNoteRevision TO public;
GO
CREATE OR ALTER TRIGGER dbo.PrivateNoteRevision_Retain ON dbo.PatientPrescriptionPrivateNoteRevision
INSTEAD OF UPDATE,DELETE AS
BEGIN THROW 53014,'Protected clinical revisions are immutable.',1; END;
GO
CREATE OR ALTER TRIGGER dbo.PrivateNote_Retain ON dbo.PatientPrescriptionPrivateNote
INSTEAD OF DELETE AS
BEGIN THROW 53014,'Protected clinical records must be retained.',1; END;
GO
-- Internal helper; caller must hold the parent lock and have advanced its token.
CREATE OR ALTER PROCEDURE dbo.PatientPrescriptionPrivateNote_Write
 @PatientUid uniqueidentifier,@PrescriptionUid uniqueidentifier,@ActorUserId bigint,
 @Note nvarchar(max),@Correlation nvarchar(100),@BeforeVersion varbinary(8)=NULL
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @@TRANCOUNT=0 THROW 53010,'An atomic draft transaction is required.',1;
 IF DATALENGTH(@Note)>2000 THROW 53000,'Private note exceeds the supported length.',1;
 IF NULLIF(LTRIM(RTRIM(@Correlation)),N'') IS NULL THROW 53010,'Audit correlation is required.',1;
 DECLARE @PatientId bigint,@AfterVersion varbinary(8);
 SELECT @PatientId=p.PatientId,@AfterVersion=x.RowVersion
 FROM dbo.PatientPrescription x WITH(UPDLOCK,HOLDLOCK)
 JOIN dbo.Patient p ON p.PatientUid=x.PatientUid AND p.IsDeleted=0
 JOIN dbo.ApplicationUser u ON u.UserId=@ActorUserId AND u.IsActive=1
 JOIN dbo.Provider pr ON pr.ProviderId=u.ProviderId AND pr.IsActive=1
 WHERE x.PatientUid=@PatientUid AND x.PrescriptionUid=@PrescriptionUid
 AND x.Status=N'Draft' AND x.PrescriberUserId=@ActorUserId;
 IF @PatientId IS NULL THROW 53011,'Private note write is not authorized.',1;
 SET @Note=NULLIF(LTRIM(RTRIM(@Note)),N'');
 DECLARE @NoteUid uniqueidentifier,@Previous uniqueidentifier,@Revision uniqueidentifier,
 @Number int,@OldText nvarchar(1000),@Operation nvarchar(10),@Event uniqueidentifier=NEWID(),@Now datetime2(7)=SYSUTCDATETIME();
 SELECT @NoteUid=PrivateNoteUid,@Number=RevisionNumber,@OldText=NoteText
 FROM dbo.PatientPrescriptionPrivateNote WITH(UPDLOCK,HOLDLOCK)
 WHERE PrescriptionUid=@PrescriptionUid AND PatientUid=@PatientUid AND OriginalPrescriberUserId=@ActorUserId;
 SELECT @Previous=RevisionUid FROM dbo.PatientPrescriptionPrivateNoteRevision
 WHERE PrivateNoteUid=@NoteUid AND RevisionNumber=@Number;
 SET @Operation=CASE WHEN @Note IS NULL THEN N'Clear' WHEN @NoteUid IS NULL THEN N'Create' ELSE N'Update' END;
 IF @NoteUid IS NOT NULL OR @Note IS NOT NULL
 BEGIN
  SET @Number=COALESCE(@Number,0)+1;SET @Revision=NEWID();
  IF @NoteUid IS NULL
  BEGIN
   SET @NoteUid=NEWID();
   INSERT dbo.PatientPrescriptionPrivateNote(PrivateNoteUid,PrescriptionUid,PatientUid,OriginalPrescriberUserId,NoteText,CreatedBy,CreatedAtUtc,RevisionNumber)
   VALUES(@NoteUid,@PrescriptionUid,@PatientUid,@ActorUserId,@Note,@ActorUserId,@Now,@Number);
  END
  ELSE UPDATE dbo.PatientPrescriptionPrivateNote SET NoteText=@Note,RevisionNumber=@Number,UpdatedBy=@ActorUserId,UpdatedAtUtc=@Now WHERE PrivateNoteUid=@NoteUid;
  INSERT dbo.PatientPrescriptionPrivateNoteRevision VALUES(@Revision,@NoteUid,@Number,@Operation,@Note,@ActorUserId,@Now,@Correlation,@Event);
 END;
 DECLARE @Metadata nvarchar(max)=(SELECT @NoteUid PrivateNoteUid,@Previous BeforeRevisionUid,@Revision AfterRevisionUid,
 @BeforeVersion BeforeRowVersion,@AfterVersion AfterRowVersion,
 CAST(CASE WHEN @OldText=@Note OR (@OldText IS NULL AND @Note IS NULL) THEN 0 ELSE 1 END AS bit) ContentChanged
 FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(AuditEventUid,UserId,PatientId,PatientUid,ActionName,EntityName,EntityId,NewValue,EventCategory,ResourceType,ResourceUid,Outcome,RequestCorrelationId,SourceApplication,CreatedAt)
 VALUES(@Event,@ActorUserId,@PatientId,@PatientUid,CASE @Operation WHEN N'Create' THEN N'PrescriptionPrivateNoteCreated' WHEN N'Clear' THEN N'PrescriptionPrivateNoteCleared' ELSE N'PrescriptionPrivateNoteUpdated' END,
 N'PatientPrescriptionPrivateNote',CONVERT(nvarchar(100),@PrescriptionUid),@Metadata,N'ClinicalChange',N'PatientPrescription',@PrescriptionUid,'Succeeded',@Correlation,N'MicroEMR.Api',@Now);
END;
GO
DENY EXECUTE ON dbo.PatientPrescriptionPrivateNote_Write TO public;
GO
CREATE OR ALTER PROCEDURE dbo.PatientPrescriptionPrivateNote_Read
 @PatientUid uniqueidentifier,@PrescriptionUid uniqueidentifier,@ActorUserId bigint,
 @Correlation nvarchar(100),@History bit=0,@CanPrescribe bit=0
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF NULLIF(LTRIM(RTRIM(@Correlation)),N'') IS NULL THROW 53010,'Audit correlation is required.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@ActorUserId AND IsActive=1)
 THROW 53011,'Active clinical access is required.',1;
 DECLARE @PatientId bigint,@Status nvarchar(20),@Version varbinary(8),@Owner bigint,@NoteUid uniqueidentifier,@Text nvarchar(1000),@Revision uniqueidentifier;
 DECLARE @Revisions TABLE(RevisionUid uniqueidentifier,RevisionNumber int,Operation nvarchar(10),NoteText nvarchar(1000),ActorUserId bigint,CreatedAtUtc datetime2(7));
 BEGIN TRY
 BEGIN TRANSACTION;
 SELECT @PatientId=p.PatientId,@Status=x.Status,@Version=x.RowVersion,@Owner=x.PrescriberUserId
 FROM dbo.PatientPrescription x WITH(HOLDLOCK)
 JOIN dbo.Patient p ON p.PatientUid=x.PatientUid AND p.IsDeleted=0
 WHERE x.PatientUid=@PatientUid AND x.PrescriptionUid=@PrescriptionUid;
 IF @PatientId IS NULL BEGIN COMMIT;RETURN;END;
 SELECT @NoteUid=PrivateNoteUid,@Text=NoteText FROM dbo.PatientPrescriptionPrivateNote WHERE PrescriptionUid=@PrescriptionUid AND PatientUid=@PatientUid;
 SELECT TOP(1) @Revision=RevisionUid FROM dbo.PatientPrescriptionPrivateNoteRevision WHERE PrivateNoteUid=@NoteUid ORDER BY RevisionNumber DESC;
 IF @History=1 INSERT @Revisions SELECT RevisionUid,RevisionNumber,Operation,NoteText,ActorUserId,CreatedAtUtc FROM dbo.PatientPrescriptionPrivateNoteRevision WHERE PrivateNoteUid=@NoteUid;
 DECLARE @Metadata nvarchar(max)=(SELECT @Revision RevisionUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(AuditEventUid,UserId,PatientId,PatientUid,ActionName,EntityName,EntityId,NewValue,EventCategory,ResourceType,ResourceUid,Outcome,RequestCorrelationId,SourceApplication,CreatedAt)
 VALUES(NEWID(),@ActorUserId,@PatientId,@PatientUid,CASE WHEN @History=1 THEN N'PrescriptionPrivateNoteHistoryViewed' ELSE N'PrescriptionPrivateNoteViewed' END,
 N'PatientPrescriptionPrivateNote',CONVERT(nvarchar(100),@PrescriptionUid),@Metadata,N'ClinicalRead',N'PatientPrescription',@PrescriptionUid,'Succeeded',@Correlation,N'MicroEMR.Api',SYSUTCDATETIME());
 COMMIT;
 END TRY
 BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
 END CATCH;
 -- Never stream protected content until the required audit has committed.
 SELECT @PatientUid PatientUid,@PrescriptionUid PrescriptionUid,
 CASE WHEN @History=0 THEN @Text ELSE NULL END NoteText,@Status Status,@Version RowVersion,
 CAST(CASE WHEN @CanPrescribe=1 AND @Owner=@ActorUserId AND @Status=N'Draft'
 AND EXISTS(SELECT 1 FROM dbo.ApplicationUser u JOIN dbo.Provider p ON p.ProviderId=u.ProviderId AND p.IsActive=1 WHERE u.UserId=@ActorUserId AND u.IsActive=1) THEN 1 ELSE 0 END AS bit) CanEdit;
 IF @History=1 SELECT * FROM @Revisions ORDER BY RevisionNumber;
END;
GO
-- Denied events contain attempted scope only; no resource lookup or note text.
CREATE OR ALTER PROCEDURE dbo.PatientPrescriptionPrivateNote_RecordDenied
 @Subject nvarchar(450),@ActorUserId bigint=NULL,@PatientUid uniqueidentifier=NULL,@PrescriptionUid uniqueidentifier=NULL,
 @Correlation nvarchar(100),@Status int
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @Status NOT IN(403,404,409) OR NULLIF(@Subject,N'') IS NULL OR NULLIF(@Correlation,N'') IS NULL
 THROW 53010,'Invalid denial audit metadata.',1;
 DECLARE @Metadata nvarchar(max)=(SELECT @Subject ActorSubject,@Status HttpStatus,@PatientUid RequestedPatientUid FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(AuditEventUid,UserId,ActionName,EntityName,EntityId,NewValue,EventCategory,ResourceType,ResourceUid,Outcome,RequestCorrelationId,SourceApplication,CreatedAt)
 VALUES(NEWID(),@ActorUserId,N'PrescriptionAccessDenied',N'PatientPrescription',CONVERT(nvarchar(100),@PrescriptionUid),@Metadata,
 N'Security',N'PatientPrescription',@PrescriptionUid,'Denied',@Correlation,N'MicroEMR.Api',SYSUTCDATETIME());
END;
GO

-- Effective draft contracts extended additively; printable/lifecycle procedures stay unchanged.
CREATE OR ALTER PROCEDURE dbo.PatientPrescription_CreateDraft
 @PatientUid UNIQUEIDENTIFIER,@ProductName NVARCHAR(200),@ProductIdentifierNamespace NVARCHAR(100)=NULL,@ProductIdentifierValue NVARCHAR(100)=NULL,
 @ProductDisplayText NVARCHAR(300),@StrengthValue DECIMAL(18,6)=NULL,@StrengthUnit NVARCHAR(50)=NULL,@DoseAmount DECIMAL(18,6)=NULL,
 @DoseUnit NVARCHAR(50)=NULL,@Route NVARCHAR(100),@FrequencyCode NVARCHAR(40),@FrequencyDisplay NVARCHAR(100),@Prn BIT,
 @Directions NVARCHAR(1000),@Quantity DECIMAL(18,3),@QuantityUnit NVARCHAR(50),@AuthorizedRepeats INT,@Indication NVARCHAR(500)=NULL,
 @PrescribedDate DATE=NULL,@StartDate DATE=NULL,@ActorUserId BIGINT,@SupersedesPrescriptionUid UNIQUEIDENTIFIER=NULL,
 @InitialDaysSupply INT=NULL,@RefillQuantity DECIMAL(18,3)=NULL,@RefillQuantityUnit NVARCHAR(50)=NULL,@RefillDaysSupply INT=NULL,@MedicationForm NVARCHAR(100)=NULL,@NotesToPharmacist NVARCHAR(1000)=NULL,@DateFields nvarchar(max)=NULL,@PrescribedDateSpecified bit=1,@StartDateSpecified bit=1,@GenericName nvarchar(max)=NULL,@GenericNameSpecified bit=0,@PrivateNoteSpecified bit=0,@PrivateNote nvarchar(max)=NULL,@PrivateNoteCorrelationId nvarchar(100)=NULL AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF DATALENGTH(@PrivateNote)>2000 THROW 53000,'Private note exceeds the supported length.',1;
 IF DATALENGTH(@GenericName)>600 THROW 53000,'Generic name exceeds 300 characters.',1;
 SET @GenericName=NULLIF(LTRIM(RTRIM(@GenericName)),N'');
 DECLARE @NewValue nvarchar(max); DECLARE @PatientId BIGINT,@ProviderUid UNIQUEIDENTIFIER,@Uid UNIQUEIDENTIFIER=NEWID();
 SELECT @PatientId=PatientId FROM dbo.Patient WHERE PatientUid=@PatientUid AND IsDeleted=0;
 SELECT @ProviderUid=p.ProviderUid FROM dbo.ApplicationUser u JOIN dbo.Provider p ON p.ProviderId=u.ProviderId AND p.IsActive=1 WHERE u.UserId=@ActorUserId AND u.IsActive=1;
 IF @PatientId IS NULL THROW 51501,'Patient not found.',1;
 IF @ProviderUid IS NULL THROW 51502,'An active mapped provider is required to prescribe.',1;
 IF @SupersedesPrescriptionUid IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@SupersedesPrescriptionUid AND Status=N'Finalized') THROW 51503,'Correction source must be a finalized prescription for this patient.',1;
 IF @DateFields IS NOT NULL
 BEGIN
  IF dbo.PrescriptionDatesValid(@DateFields)<>1 THROW 53000,'Invalid prescription date precision.',1;
  SET @PrescribedDate=NULL;
  SET @StartDate=NULL;
  -- Only full dates have a SQL DATE companion. Never invent a month or day.
  IF LEN(dbo.PrescriptionDateText(JSON_QUERY(@DateFields,'$.WrittenDate')))=10 SET @PrescribedDate=CONVERT(date,dbo.PrescriptionDateText(JSON_QUERY(@DateFields,'$.WrittenDate')),23);
  IF LEN(dbo.PrescriptionDateText(JSON_QUERY(@DateFields,'$.StartDate')))=10 SET @StartDate=CONVERT(date,dbo.PrescriptionDateText(JSON_QUERY(@DateFields,'$.StartDate')),23);
 END;
 IF @DateFields IS NULL AND @PrescribedDate IS NULL THROW 53000,'Written date is required.',1;
 BEGIN TRY
 BEGIN TRANSACTION;
 INSERT dbo.PatientPrescription(PrescriptionUid,PatientUid,GenericName,ProductName,ProductIdentifierNamespace,ProductIdentifierValue,ProductDisplayText,StrengthValue,StrengthUnit,DoseAmount,DoseUnit,Route,FrequencyCode,FrequencyDisplay,Prn,Directions,Quantity,QuantityUnit,MedicationForm,NotesToPharmacist,AuthorizedRepeats,InitialDaysSupply,RefillQuantity,RefillQuantityUnit,RefillDaysSupply,Indication,PrescribedDate,StartDate,DateFields,CreatedBy,PrescriberUserId,PrescriberProviderUid,SupersedesPrescriptionUid)
 VALUES(@Uid,@PatientUid,@GenericName,LTRIM(RTRIM(@ProductName)),NULLIF(LTRIM(RTRIM(@ProductIdentifierNamespace)),N''),NULLIF(LTRIM(RTRIM(@ProductIdentifierValue)),N''),LTRIM(RTRIM(@ProductDisplayText)),@StrengthValue,NULLIF(LTRIM(RTRIM(@StrengthUnit)),N''),@DoseAmount,NULLIF(LTRIM(RTRIM(@DoseUnit)),N''),LTRIM(RTRIM(@Route)),@FrequencyCode,@FrequencyDisplay,@Prn,LTRIM(RTRIM(@Directions)),@Quantity,LTRIM(RTRIM(@QuantityUnit)),@MedicationForm,@NotesToPharmacist,@AuthorizedRepeats,@InitialDaysSupply,@RefillQuantity,NULLIF(LTRIM(RTRIM(@RefillQuantityUnit)),N''),@RefillDaysSupply,NULLIF(LTRIM(RTRIM(@Indication)),N''),@PrescribedDate,@StartDate,@DateFields,@ActorUserId,@ActorUserId,@ProviderUid,@SupersedesPrescriptionUid);
 IF @PrivateNoteSpecified=1 EXEC dbo.PatientPrescriptionPrivateNote_Write @PatientUid,@Uid,@ActorUserId,@PrivateNote,@PrivateNoteCorrelationId;
 SELECT @NewValue=(SELECT * FROM dbo.PatientPrescription WHERE PrescriptionUid=@Uid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt,RequestCorrelationId,SourceApplication) VALUES(@ActorUserId,@PatientId,N'PrescriptionDraftCreated',N'PatientPrescription',CONVERT(NVARCHAR(100),@Uid),NULL,@NewValue,SYSUTCDATETIME(),@PrivateNoteCorrelationId,N'MicroEMR.Api');
 COMMIT;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK;THROW;END CATCH;
 EXEC dbo.PatientPrescription_GetByUid @PatientUid,@Uid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientPrescription_UpdateDraft
 @PatientUid UNIQUEIDENTIFIER,@PrescriptionUid UNIQUEIDENTIFIER,@ProductName NVARCHAR(200),@ProductIdentifierNamespace NVARCHAR(100)=NULL,@ProductIdentifierValue NVARCHAR(100)=NULL,
 @ProductDisplayText NVARCHAR(300),@StrengthValue DECIMAL(18,6)=NULL,@StrengthUnit NVARCHAR(50)=NULL,@DoseAmount DECIMAL(18,6)=NULL,@DoseUnit NVARCHAR(50)=NULL,
 @Route NVARCHAR(100),@FrequencyCode NVARCHAR(40),@FrequencyDisplay NVARCHAR(100),@Prn BIT,@Directions NVARCHAR(1000),@Quantity DECIMAL(18,3),@QuantityUnit NVARCHAR(50),
 @AuthorizedRepeats INT,@Indication NVARCHAR(500)=NULL,@PrescribedDate DATE=NULL,@StartDate DATE=NULL,@ActorUserId BIGINT,@RowVersion VARBINARY(MAX),
 @InitialDaysSupply INT=NULL,@RefillQuantity DECIMAL(18,3)=NULL,@RefillQuantityUnit NVARCHAR(50)=NULL,@RefillDaysSupply INT=NULL,@MedicationForm NVARCHAR(100)=NULL,@NotesToPharmacist NVARCHAR(1000)=NULL,@DateFields nvarchar(max)=NULL,@PrescribedDateSpecified bit=1,@StartDateSpecified bit=1,@GenericName nvarchar(max)=NULL,@GenericNameSpecified bit=0,@PrivateNoteSpecified bit=0,@PrivateNote nvarchar(max)=NULL,@PrivateNoteCorrelationId nvarchar(100)=NULL AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF DATALENGTH(@PrivateNote)>2000 THROW 53000,'Private note exceeds the supported length.',1;
 IF DATALENGTH(@GenericName)>600 THROW 53000,'Generic name exceeds 300 characters.',1;
 SET @GenericName=NULLIF(LTRIM(RTRIM(@GenericName)),N'');
 DECLARE @PatientId BIGINT,@OldValue nvarchar(max),@NewValue nvarchar(max),@ExistingFields nvarchar(max),@ExistingWritten date,@ExistingStart date;
 IF @RowVersion IS NULL OR DATALENGTH(@RowVersion)<>8 THROW 51504,'A reviewed eight-byte row version is required.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser u JOIN dbo.Provider p ON p.ProviderId=u.ProviderId AND p.IsActive=1 WHERE u.UserId=@ActorUserId AND u.IsActive=1) THROW 51502,'An active mapped provider is required to prescribe.',1;
 IF @DateFields IS NOT NULL
 BEGIN
  IF dbo.PrescriptionDatesValid(@DateFields)<>1 THROW 53000,'Invalid prescription date precision.',1;
  SET @PrescribedDate=NULL;
  SET @StartDate=NULL;
  -- Only full dates have a SQL DATE companion. Never invent a month or day.
  IF LEN(dbo.PrescriptionDateText(JSON_QUERY(@DateFields,'$.WrittenDate')))=10 SET @PrescribedDate=CONVERT(date,dbo.PrescriptionDateText(JSON_QUERY(@DateFields,'$.WrittenDate')),23);
  IF LEN(dbo.PrescriptionDateText(JSON_QUERY(@DateFields,'$.StartDate')))=10 SET @StartDate=CONVERT(date,dbo.PrescriptionDateText(JSON_QUERY(@DateFields,'$.StartDate')),23);
 END;
 BEGIN TRY
 BEGIN TRANSACTION;
 SELECT @PatientId=p.PatientId,@OldValue=(SELECT x.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER),@ExistingFields=x.DateFields,@ExistingWritten=x.PrescribedDate,@ExistingStart=x.StartDate FROM dbo.PatientPrescription x WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Patient p ON p.PatientUid=x.PatientUid AND p.IsDeleted=0 WHERE x.PatientUid=@PatientUid AND x.PrescriptionUid=@PrescriptionUid;
 IF @PatientId IS NULL BEGIN ROLLBACK;RETURN;END;
 IF @DateFields IS NULL
 BEGIN
  -- Old clients cannot erase structured precision during an unrelated edit.
  IF @ExistingFields IS NOT NULL BEGIN SET @DateFields=@ExistingFields; SET @PrescribedDate=@ExistingWritten; SET @StartDate=@ExistingStart; END;
  ELSE BEGIN
   IF @PrescribedDateSpecified=0 SET @PrescribedDate=@ExistingWritten;
   IF @StartDateSpecified=0 SET @StartDate=@ExistingStart;
  END;
 END;
 IF @PrivateNoteSpecified=1 AND EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid AND PrescriberUserId<>@ActorUserId) BEGIN ROLLBACK;THROW 53011,'Private note write is not authorized.',1;END;
 IF @DateFields IS NULL AND @PrescribedDate IS NULL BEGIN ROLLBACK; THROW 53000,'Written date is required.',1; END;
 UPDATE dbo.PatientPrescription SET GenericName=CASE WHEN @GenericNameSpecified=1 THEN @GenericName ELSE GenericName END,MedicationForm=NULLIF(LTRIM(RTRIM(@MedicationForm)),N''),NotesToPharmacist=NULLIF(LTRIM(RTRIM(@NotesToPharmacist)),N''),ProductName=LTRIM(RTRIM(@ProductName)),ProductIdentifierNamespace=NULLIF(LTRIM(RTRIM(@ProductIdentifierNamespace)),N''),ProductIdentifierValue=NULLIF(LTRIM(RTRIM(@ProductIdentifierValue)),N''),ProductDisplayText=LTRIM(RTRIM(@ProductDisplayText)),StrengthValue=@StrengthValue,StrengthUnit=NULLIF(LTRIM(RTRIM(@StrengthUnit)),N''),DoseAmount=@DoseAmount,DoseUnit=NULLIF(LTRIM(RTRIM(@DoseUnit)),N''),Route=LTRIM(RTRIM(@Route)),FrequencyCode=@FrequencyCode,FrequencyDisplay=@FrequencyDisplay,Prn=@Prn,Directions=LTRIM(RTRIM(@Directions)),Quantity=@Quantity,QuantityUnit=LTRIM(RTRIM(@QuantityUnit)),AuthorizedRepeats=@AuthorizedRepeats,InitialDaysSupply=@InitialDaysSupply,RefillQuantity=@RefillQuantity,RefillQuantityUnit=NULLIF(LTRIM(RTRIM(@RefillQuantityUnit)),N''),RefillDaysSupply=@RefillDaysSupply,Indication=NULLIF(LTRIM(RTRIM(@Indication)),N''),PrescribedDate=@PrescribedDate,StartDate=@StartDate,DateFields=@DateFields,UpdatedBy=@ActorUserId,UpdatedAtUtc=SYSUTCDATETIME()
 WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid AND Status=N'Draft' AND PrescriberUserId=@ActorUserId AND RowVersion=@RowVersion;
 IF @@ROWCOUNT=0 BEGIN ROLLBACK;THROW 51504,'Draft is stale, immutable, or belongs to another prescriber.',1;END;
 IF @PrivateNoteSpecified=1 EXEC dbo.PatientPrescriptionPrivateNote_Write @PatientUid,@PrescriptionUid,@ActorUserId,@PrivateNote,@PrivateNoteCorrelationId,@RowVersion;
 SELECT @NewValue=(SELECT * FROM dbo.PatientPrescription WHERE PrescriptionUid=@PrescriptionUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt,RequestCorrelationId,SourceApplication) VALUES(@ActorUserId,@PatientId,N'PrescriptionDraftUpdated',N'PatientPrescription',CONVERT(NVARCHAR(100),@PrescriptionUid),@OldValue,@NewValue,SYSUTCDATETIME(),@PrivateNoteCorrelationId,N'MicroEMR.Api');
 COMMIT;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK;THROW;END CATCH;
 EXEC dbo.PatientPrescription_GetByUid @PatientUid,@PrescriptionUid;
END;
GO
