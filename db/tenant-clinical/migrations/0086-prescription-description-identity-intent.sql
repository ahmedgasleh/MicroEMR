-- Approved independent prescription facts and prospective native identity. No clinical backfill.
SET XACT_ABORT ON;
GO
ALTER TABLE dbo.PatientPrescription ADD TreatmentType nvarchar(300) NULL,SubstitutionProhibited bit NULL,
 PrescriberFirstNameSnapshot nvarchar(100) NULL,PrescriberLastNameSnapshot nvarchar(100) NULL,PrescriberOhipSnapshot nvarchar(50) NULL;
GO
CREATE OR ALTER PROCEDURE dbo.PatientPrescription_GetByPatientUid @PatientUid UNIQUEIDENTIFIER AS
BEGIN
 SET NOCOUNT ON;
 SELECT SubstitutionProhibited,TreatmentType,PrescriberFirstNameSnapshot,PrescriberLastNameSnapshot,PrescriberOhipSnapshot,PrescriptionUid,PatientUid,Status,ProductName,GenericName,ProductIdentifierNamespace,ProductIdentifierValue,ProductDisplayText,
 StrengthValue,StrengthUnit,DoseAmount,DoseUnit,Route,FrequencyCode,FrequencyDisplay,Prn,Directions,Quantity,QuantityUnit,
 MedicationForm,NotesToPharmacist,AuthorizedRepeats,InitialDaysSupply,RefillQuantity,RefillQuantityUnit,RefillDaysSupply,Indication,PrescribedDate,StartDate,DateFields,CreatedBy,CreatedAtUtc,UpdatedBy,UpdatedAtUtc,PrescriberUserId,
 PrescriberProviderUid,PrescriberDisplayNameSnapshot,PrescriberCredentialSnapshot,ProductDisplaySnapshot,FinalizedBy,
 FinalizedAtUtc,CancelledBy,CancelledAtUtc,CancellationReason,SupersedesPrescriptionUid,SupersededByPrescriptionUid,ArtifactUid,RowVersion
 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid ORDER BY PrescribedDate DESC,CreatedAtUtc DESC;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientPrescription_GetByUid @PatientUid UNIQUEIDENTIFIER,@PrescriptionUid UNIQUEIDENTIFIER AS
BEGIN
 SET NOCOUNT ON;
 SELECT SubstitutionProhibited,TreatmentType,PrescriberFirstNameSnapshot,PrescriberLastNameSnapshot,PrescriberOhipSnapshot,PrescriptionUid,PatientUid,Status,ProductName,GenericName,ProductIdentifierNamespace,ProductIdentifierValue,ProductDisplayText,
 StrengthValue,StrengthUnit,DoseAmount,DoseUnit,Route,FrequencyCode,FrequencyDisplay,Prn,Directions,Quantity,QuantityUnit,
 MedicationForm,NotesToPharmacist,AuthorizedRepeats,InitialDaysSupply,RefillQuantity,RefillQuantityUnit,RefillDaysSupply,Indication,PrescribedDate,StartDate,DateFields,CreatedBy,CreatedAtUtc,UpdatedBy,UpdatedAtUtc,PrescriberUserId,
 PrescriberProviderUid,PrescriberDisplayNameSnapshot,PrescriberCredentialSnapshot,ProductDisplaySnapshot,FinalizedBy,
 FinalizedAtUtc,CancelledBy,CancelledAtUtc,CancellationReason,SupersedesPrescriptionUid,SupersededByPrescriptionUid,ArtifactUid,RowVersion
 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientPrescription_CreateDraft
 @PatientUid UNIQUEIDENTIFIER,@ProductName NVARCHAR(200),@ProductIdentifierNamespace NVARCHAR(100)=NULL,@ProductIdentifierValue NVARCHAR(100)=NULL,
 @ProductDisplayText NVARCHAR(MAX),@StrengthValue DECIMAL(18,6)=NULL,@StrengthUnit NVARCHAR(50)=NULL,@DoseAmount DECIMAL(18,6)=NULL,
 @DoseUnit NVARCHAR(50)=NULL,@Route NVARCHAR(100),@FrequencyCode NVARCHAR(40),@FrequencyDisplay NVARCHAR(100),@Prn BIT,
 @Directions NVARCHAR(1000),@Quantity DECIMAL(18,3),@QuantityUnit NVARCHAR(50),@AuthorizedRepeats INT,@Indication NVARCHAR(500)=NULL,
 @PrescribedDate DATE=NULL,@StartDate DATE=NULL,@ActorUserId BIGINT,@SupersedesPrescriptionUid UNIQUEIDENTIFIER=NULL,
 @InitialDaysSupply INT=NULL,@RefillQuantity DECIMAL(18,3)=NULL,@RefillQuantityUnit NVARCHAR(50)=NULL,@RefillDaysSupply INT=NULL,@MedicationForm NVARCHAR(100)=NULL,@NotesToPharmacist NVARCHAR(1000)=NULL,@DateFields nvarchar(max)=NULL,@PrescribedDateSpecified bit=1,@StartDateSpecified bit=1,@GenericName nvarchar(max)=NULL,@GenericNameSpecified bit=0,@PrivateNoteSpecified bit=0,@PrivateNote nvarchar(max)=NULL,@PrivateNoteCorrelationId nvarchar(100)=NULL,@OrderFields nvarchar(max)=NULL AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF NULLIF(LTRIM(RTRIM(@ProductDisplayText)),N'') IS NULL OR DATALENGTH(@ProductDisplayText)>600
 OR CHARINDEX(NCHAR(10),@ProductDisplayText)>0 OR CHARINDEX(NCHAR(13),@ProductDisplayText)>0 OR CHARINDEX(NCHAR(11),@ProductDisplayText)>0
 OR CHARINDEX(NCHAR(12),@ProductDisplayText)>0 OR CHARINDEX(NCHAR(133),@ProductDisplayText)>0 OR CHARINDEX(NCHAR(8232),@ProductDisplayText)>0 OR CHARINDEX(NCHAR(8233),@ProductDisplayText)>0
 THROW 53000,'Drug Description must be a nonblank single line of at most 300 UTF-16 code units.',1;
 IF @OrderFields IS NOT NULL AND (ISJSON(@OrderFields)<>1 OR LEFT(LTRIM(@OrderFields),1)<>N'{') THROW 53000,'Order fields must be an object.',1;
 IF EXISTS(SELECT 1 FROM OPENJSON(COALESCE(@OrderFields,N'{}')) WHERE [key]=N'TreatmentType' AND [type] NOT IN(0,1)) THROW 53000,'Treatment type must be text or null.',1;
 IF EXISTS(SELECT 1 FROM OPENJSON(COALESCE(@OrderFields,N'{}')) WHERE [key]=N'SubstitutionProhibited' AND [type] NOT IN(0,3)) THROW 53000,'Substitution prohibition must be a boolean or null.',1;
 DECLARE @SubstitutionSpecified bit=CASE WHEN EXISTS(SELECT 1 FROM OPENJSON(COALESCE(@OrderFields,N'{}')) WHERE [key]=N'SubstitutionProhibited') THEN 1 ELSE 0 END;
 DECLARE @SubstitutionProhibited bit=CASE JSON_VALUE(@OrderFields,'$.SubstitutionProhibited') WHEN N'true' THEN 1 WHEN N'false' THEN 0 END;
 DECLARE @TreatmentType nvarchar(max); SELECT @TreatmentType=TreatmentType FROM OPENJSON(COALESCE(@OrderFields,N'{}')) WITH(TreatmentType nvarchar(max) '$.TreatmentType');
 IF DATALENGTH(@TreatmentType)>600 OR CHARINDEX(NCHAR(10),@TreatmentType)>0 OR CHARINDEX(NCHAR(13),@TreatmentType)>0
 OR CHARINDEX(NCHAR(11),@TreatmentType)>0 OR CHARINDEX(NCHAR(12),@TreatmentType)>0 OR CHARINDEX(NCHAR(133),@TreatmentType)>0
 OR CHARINDEX(NCHAR(8232),@TreatmentType)>0 OR CHARINDEX(NCHAR(8233),@TreatmentType)>0 THROW 53000,'Treatment type must be one line of at most 300 UTF-16 code units.',1;
 SET @TreatmentType=NULLIF(LTRIM(RTRIM(@TreatmentType)),N'');
 IF DATALENGTH(@PrivateNote)>2000 THROW 53000,'Private note exceeds the supported length.',1;
 IF DATALENGTH(@GenericName)>600 THROW 53000,'Generic name exceeds 300 characters.',1;
 SET @GenericName=NULLIF(LTRIM(RTRIM(@GenericName)),N'');
 DECLARE @FirstName nvarchar(100),@LastName nvarchar(100),@Ohip nvarchar(50); DECLARE @NewValue nvarchar(max); DECLARE @PatientId BIGINT,@ProviderUid UNIQUEIDENTIFIER,@Uid UNIQUEIDENTIFIER=NEWID();
 SELECT @PatientId=PatientId FROM dbo.Patient WHERE PatientUid=@PatientUid AND IsDeleted=0;
 SELECT @FirstName=p.FirstName,@LastName=p.LastName,@Ohip=p.BillingNumber,@ProviderUid=p.ProviderUid FROM dbo.ApplicationUser u JOIN dbo.Provider p ON p.ProviderId=u.ProviderId AND p.IsActive=1 WHERE u.UserId=@ActorUserId AND u.IsActive=1;
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
 INSERT dbo.PatientPrescription(SubstitutionProhibited,TreatmentType,PrescriberFirstNameSnapshot,PrescriberLastNameSnapshot,PrescriberOhipSnapshot,PrescriptionUid,PatientUid,GenericName,ProductName,ProductIdentifierNamespace,ProductIdentifierValue,ProductDisplayText,StrengthValue,StrengthUnit,DoseAmount,DoseUnit,Route,FrequencyCode,FrequencyDisplay,Prn,Directions,Quantity,QuantityUnit,MedicationForm,NotesToPharmacist,AuthorizedRepeats,InitialDaysSupply,RefillQuantity,RefillQuantityUnit,RefillDaysSupply,Indication,PrescribedDate,StartDate,DateFields,CreatedBy,PrescriberUserId,PrescriberProviderUid,SupersedesPrescriptionUid)
 VALUES(@SubstitutionProhibited,@TreatmentType,@FirstName,@LastName,@Ohip,@Uid,@PatientUid,@GenericName,LTRIM(RTRIM(@ProductName)),NULLIF(LTRIM(RTRIM(@ProductIdentifierNamespace)),N''),NULLIF(LTRIM(RTRIM(@ProductIdentifierValue)),N''),LTRIM(RTRIM(@ProductDisplayText)),@StrengthValue,NULLIF(LTRIM(RTRIM(@StrengthUnit)),N''),@DoseAmount,NULLIF(LTRIM(RTRIM(@DoseUnit)),N''),LTRIM(RTRIM(@Route)),@FrequencyCode,@FrequencyDisplay,@Prn,LTRIM(RTRIM(@Directions)),@Quantity,LTRIM(RTRIM(@QuantityUnit)),@MedicationForm,@NotesToPharmacist,@AuthorizedRepeats,@InitialDaysSupply,@RefillQuantity,NULLIF(LTRIM(RTRIM(@RefillQuantityUnit)),N''),@RefillDaysSupply,NULLIF(LTRIM(RTRIM(@Indication)),N''),@PrescribedDate,@StartDate,@DateFields,@ActorUserId,@ActorUserId,@ProviderUid,@SupersedesPrescriptionUid);
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
 @ProductDisplayText NVARCHAR(MAX),@StrengthValue DECIMAL(18,6)=NULL,@StrengthUnit NVARCHAR(50)=NULL,@DoseAmount DECIMAL(18,6)=NULL,@DoseUnit NVARCHAR(50)=NULL,
 @Route NVARCHAR(100),@FrequencyCode NVARCHAR(40),@FrequencyDisplay NVARCHAR(100),@Prn BIT,@Directions NVARCHAR(1000),@Quantity DECIMAL(18,3),@QuantityUnit NVARCHAR(50),
 @AuthorizedRepeats INT,@Indication NVARCHAR(500)=NULL,@PrescribedDate DATE=NULL,@StartDate DATE=NULL,@ActorUserId BIGINT,@RowVersion VARBINARY(MAX),
 @InitialDaysSupply INT=NULL,@RefillQuantity DECIMAL(18,3)=NULL,@RefillQuantityUnit NVARCHAR(50)=NULL,@RefillDaysSupply INT=NULL,@MedicationForm NVARCHAR(100)=NULL,@NotesToPharmacist NVARCHAR(1000)=NULL,@DateFields nvarchar(max)=NULL,@PrescribedDateSpecified bit=1,@StartDateSpecified bit=1,@GenericName nvarchar(max)=NULL,@GenericNameSpecified bit=0,@PrivateNoteSpecified bit=0,@PrivateNote nvarchar(max)=NULL,@PrivateNoteCorrelationId nvarchar(100)=NULL,@OrderFields nvarchar(max)=NULL AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF NULLIF(LTRIM(RTRIM(@ProductDisplayText)),N'') IS NULL OR DATALENGTH(@ProductDisplayText)>600
 OR CHARINDEX(NCHAR(10),@ProductDisplayText)>0 OR CHARINDEX(NCHAR(13),@ProductDisplayText)>0 OR CHARINDEX(NCHAR(11),@ProductDisplayText)>0
 OR CHARINDEX(NCHAR(12),@ProductDisplayText)>0 OR CHARINDEX(NCHAR(133),@ProductDisplayText)>0 OR CHARINDEX(NCHAR(8232),@ProductDisplayText)>0 OR CHARINDEX(NCHAR(8233),@ProductDisplayText)>0
 THROW 53000,'Drug Description must be a nonblank single line of at most 300 UTF-16 code units.',1;
 IF @OrderFields IS NOT NULL AND (ISJSON(@OrderFields)<>1 OR LEFT(LTRIM(@OrderFields),1)<>N'{') THROW 53000,'Order fields must be an object.',1;
 IF EXISTS(SELECT 1 FROM OPENJSON(COALESCE(@OrderFields,N'{}')) WHERE [key]=N'TreatmentType' AND [type] NOT IN(0,1)) THROW 53000,'Treatment type must be text or null.',1;
 IF EXISTS(SELECT 1 FROM OPENJSON(COALESCE(@OrderFields,N'{}')) WHERE [key]=N'SubstitutionProhibited' AND [type] NOT IN(0,3)) THROW 53000,'Substitution prohibition must be a boolean or null.',1;
 DECLARE @SubstitutionSpecified bit=CASE WHEN EXISTS(SELECT 1 FROM OPENJSON(COALESCE(@OrderFields,N'{}')) WHERE [key]=N'SubstitutionProhibited') THEN 1 ELSE 0 END;
 DECLARE @SubstitutionProhibited bit=CASE JSON_VALUE(@OrderFields,'$.SubstitutionProhibited') WHEN N'true' THEN 1 WHEN N'false' THEN 0 END;
 DECLARE @TreatmentType nvarchar(max); SELECT @TreatmentType=TreatmentType FROM OPENJSON(COALESCE(@OrderFields,N'{}')) WITH(TreatmentType nvarchar(max) '$.TreatmentType');
 IF DATALENGTH(@TreatmentType)>600 OR CHARINDEX(NCHAR(10),@TreatmentType)>0 OR CHARINDEX(NCHAR(13),@TreatmentType)>0
 OR CHARINDEX(NCHAR(11),@TreatmentType)>0 OR CHARINDEX(NCHAR(12),@TreatmentType)>0 OR CHARINDEX(NCHAR(133),@TreatmentType)>0
 OR CHARINDEX(NCHAR(8232),@TreatmentType)>0 OR CHARINDEX(NCHAR(8233),@TreatmentType)>0 THROW 53000,'Treatment type must be one line of at most 300 UTF-16 code units.',1;
 SET @TreatmentType=NULLIF(LTRIM(RTRIM(@TreatmentType)),N'');
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
 UPDATE dbo.PatientPrescription SET SubstitutionProhibited=CASE WHEN @SubstitutionSpecified=1 THEN @SubstitutionProhibited ELSE SubstitutionProhibited END,TreatmentType=CASE WHEN @OrderFields IS NULL THEN TreatmentType ELSE @TreatmentType END,GenericName=CASE WHEN @GenericNameSpecified=1 THEN @GenericName ELSE GenericName END,MedicationForm=NULLIF(LTRIM(RTRIM(@MedicationForm)),N''),NotesToPharmacist=NULLIF(LTRIM(RTRIM(@NotesToPharmacist)),N''),ProductName=LTRIM(RTRIM(@ProductName)),ProductIdentifierNamespace=NULLIF(LTRIM(RTRIM(@ProductIdentifierNamespace)),N''),ProductIdentifierValue=NULLIF(LTRIM(RTRIM(@ProductIdentifierValue)),N''),ProductDisplayText=LTRIM(RTRIM(@ProductDisplayText)),StrengthValue=@StrengthValue,StrengthUnit=NULLIF(LTRIM(RTRIM(@StrengthUnit)),N''),DoseAmount=@DoseAmount,DoseUnit=NULLIF(LTRIM(RTRIM(@DoseUnit)),N''),Route=LTRIM(RTRIM(@Route)),FrequencyCode=@FrequencyCode,FrequencyDisplay=@FrequencyDisplay,Prn=@Prn,Directions=LTRIM(RTRIM(@Directions)),Quantity=@Quantity,QuantityUnit=LTRIM(RTRIM(@QuantityUnit)),AuthorizedRepeats=@AuthorizedRepeats,InitialDaysSupply=@InitialDaysSupply,RefillQuantity=@RefillQuantity,RefillQuantityUnit=NULLIF(LTRIM(RTRIM(@RefillQuantityUnit)),N''),RefillDaysSupply=@RefillDaysSupply,Indication=NULLIF(LTRIM(RTRIM(@Indication)),N''),PrescribedDate=@PrescribedDate,StartDate=@StartDate,DateFields=@DateFields,UpdatedBy=@ActorUserId,UpdatedAtUtc=SYSUTCDATETIME()
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
CREATE OR ALTER PROCEDURE dbo.PatientPrescription_SignAndFinalize @PatientUid UNIQUEIDENTIFIER,@PrescriptionUid UNIQUEIDENTIFIER,@ActorUserId BIGINT,@RowVersion VARBINARY(MAX),@ConfirmSignature BIT AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @RowVersion IS NULL OR DATALENGTH(@RowVersion)<>8 THROW 51504,'A reviewed eight-byte row version is required.',1;
 IF ISNULL(@ConfirmSignature,0)<>1 THROW 51506,'Explicit signature confirmation is required.',1;
 DECLARE @SignedAt DATETIME2(0)=SYSUTCDATETIME();DECLARE @FirstName nvarchar(100),@LastName nvarchar(100),@Ohip nvarchar(50); DECLARE @PatientId BIGINT,@ProviderUid UNIQUEIDENTIFIER,@ProviderName NVARCHAR(200),@Credential NVARCHAR(200),@ArtifactUid UNIQUEIDENTIFIER=NEWID(),@Original UNIQUEIDENTIFIER,@Html NVARCHAR(MAX),@OldValue nvarchar(max),@NewValue nvarchar(max),@OriginalOld nvarchar(max),@OriginalNew nvarchar(max);
 BEGIN TRANSACTION;
 SELECT @FirstName=p.FirstName,@LastName=p.LastName,@Ohip=p.BillingNumber,@ProviderUid=p.ProviderUid,@ProviderName=p.DisplayName,@Credential=CONCAT(p.ProviderType,CASE WHEN p.BillingNumber IS NULL THEN N'' ELSE N' | '+p.BillingNumber END) FROM dbo.ApplicationUser u JOIN dbo.Provider p ON p.ProviderId=u.ProviderId AND p.IsActive=1 WHERE u.UserId=@ActorUserId AND u.IsActive=1;
 SELECT @PatientId=p.PatientId,@Original=x.SupersedesPrescriptionUid FROM dbo.PatientPrescription x WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Patient p ON p.PatientUid=x.PatientUid AND p.IsDeleted=0 WHERE x.PatientUid=@PatientUid AND x.PrescriptionUid=@PrescriptionUid AND x.Status=N'Draft' AND x.PrescriberUserId=@ActorUserId AND x.RowVersion=@RowVersion;
 IF @ProviderUid IS NULL BEGIN ROLLBACK;THROW 51502,'An active mapped provider is required to prescribe.',1;END;
 IF @PatientId IS NULL BEGIN ROLLBACK;THROW 51504,'Draft is stale, immutable, missing, or belongs to another prescriber.',1;END;
 SELECT @OldValue=(SELECT * FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 SELECT @Html=(SELECT 2 PrescriptionOutputVersion,1 SignatureVersion,1 RefillInstructionsVersion,
 CONCAT(p.FirstName,N' ',p.LastName) PatientName,p.DateOfBirth,p.HealthCardNumber,p.PhoneNumber PatientPhone,
 p.AddressLine1 PatientAddressLine1,p.AddressLine2 PatientAddressLine2,p.City PatientCity,p.Province PatientProvince,p.PostalCode PatientPostalCode,
 c.LegalName ClinicName,c.Phone ClinicPhone,c.AddressLine1 ClinicAddressLine1,c.AddressLine2 ClinicAddressLine2,c.City ClinicCity,c.ProvinceState ClinicProvince,c.PostalCode ClinicPostalCode,
 COALESCE(dbo.PrescriptionDateText(JSON_QUERY(x.DateFields,'$.WrittenDate')),CONVERT(varchar(10),x.PrescribedDate,23)) PrescribedDate,COALESCE(NULLIF(dbo.PrescriptionDateText(JSON_QUERY(x.DateFields,'$.StartDate')),''),CONVERT(varchar(10),x.StartDate,23)) StartDate,1 DatePrecisionVersion,JSON_QUERY(x.DateFields) DateFields,1 GenericNameVersion,x.GenericName,x.ProductDisplayText,x.SubstitutionProhibited,x.TreatmentType,@FirstName PrescriberFirstName,@LastName PrescriberLastName,@Ohip PrescriberOhip,x.MedicationForm,x.NotesToPharmacist,x.StrengthValue,x.StrengthUnit,x.DoseAmount,x.DoseUnit,x.Route,x.FrequencyDisplay,x.Prn,x.Directions,
 x.Quantity,x.QuantityUnit,x.AuthorizedRepeats,x.InitialDaysSupply,x.RefillQuantity,x.RefillQuantityUnit,x.RefillDaysSupply,x.Indication,
 x.CreatedBy,x.CreatedAtUtc,creator.DisplayName CreatedByDisplayName,
 @ProviderName PrescriberDisplayName,@Credential PrescriberCredential,@ActorUserId SignerUserId,@ProviderUid SignerProviderUid,@SignedAt SignedAtUtc,x.PrescriptionUid
 FROM dbo.PatientPrescription x JOIN dbo.Patient p ON p.PatientUid=x.PatientUid
 LEFT JOIN dbo.ApplicationUser creator ON creator.UserId=x.CreatedBy
 LEFT JOIN dbo.ClinicProfile c ON c.ClinicProfileId=1
 WHERE x.PatientUid=@PatientUid AND x.PrescriptionUid=@PrescriptionUid FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 UPDATE dbo.PatientPrescription SET PrescriberFirstNameSnapshot=@FirstName,PrescriberLastNameSnapshot=@LastName,PrescriberOhipSnapshot=@Ohip,Status=N'Finalized',PrescriberProviderUid=@ProviderUid,PrescriberDisplayNameSnapshot=@ProviderName,PrescriberCredentialSnapshot=@Credential,ProductDisplaySnapshot=ProductDisplayText,FinalizedBy=@ActorUserId,FinalizedAtUtc=@SignedAt,ArtifactUid=@ArtifactUid WHERE PrescriptionUid=@PrescriptionUid;
 INSERT dbo.PatientPrescriptionArtifact(ArtifactUid,PrescriptionUid,PatientUid,ArtifactJson,CreatedBy) VALUES(@ArtifactUid,@PrescriptionUid,@PatientUid,@Html,@ActorUserId);
 SELECT @NewValue=(SELECT * FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt) VALUES(@ActorUserId,@PatientId,N'PrescriptionFinalized',N'PatientPrescription',CONVERT(nvarchar(100),@PrescriptionUid),@OldValue,@NewValue,SYSUTCDATETIME());
 IF @Original IS NOT NULL BEGIN
   SELECT @OriginalOld=(SELECT * FROM dbo.PatientPrescription WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND PrescriptionUid=@Original FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
   UPDATE dbo.PatientPrescription SET Status=N'Superseded',SupersededByPrescriptionUid=@PrescriptionUid WHERE PatientUid=@PatientUid AND PrescriptionUid=@Original AND Status=N'Finalized';
   IF @@ROWCOUNT<>1 BEGIN ROLLBACK;THROW 51505,'The correction source is no longer eligible for supersession.',1;END;
   SELECT @OriginalNew=(SELECT * FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@Original FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
   INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt) VALUES(@ActorUserId,@PatientId,N'PrescriptionSuperseded',N'PatientPrescription',CONVERT(nvarchar(100),@Original),@OriginalOld,@OriginalNew,SYSUTCDATETIME());
 END;
 COMMIT;EXEC dbo.PatientPrescription_GetByUid @PatientUid,@PrescriptionUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientPrescription_Finalize @PatientUid UNIQUEIDENTIFIER,@PrescriptionUid UNIQUEIDENTIFIER,@ActorUserId BIGINT,@RowVersion VARBINARY(MAX) AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;IF @RowVersion IS NULL OR DATALENGTH(@RowVersion)<>8 THROW 51504,'A reviewed eight-byte row version is required.',1; DECLARE @FirstName nvarchar(100),@LastName nvarchar(100),@Ohip nvarchar(50); DECLARE @PatientId BIGINT,@ProviderUid UNIQUEIDENTIFIER,@ProviderName NVARCHAR(200),@Credential NVARCHAR(200),@ArtifactUid UNIQUEIDENTIFIER=NEWID(),@Original UNIQUEIDENTIFIER,@Html NVARCHAR(MAX),@OldValue nvarchar(max),@NewValue nvarchar(max),@OriginalOld nvarchar(max),@OriginalNew nvarchar(max);
 BEGIN TRANSACTION;
 SELECT @FirstName=p.FirstName,@LastName=p.LastName,@Ohip=p.BillingNumber,@ProviderUid=p.ProviderUid,@ProviderName=p.DisplayName,@Credential=CONCAT(p.ProviderType,CASE WHEN p.BillingNumber IS NULL THEN N'' ELSE N' | '+p.BillingNumber END) FROM dbo.ApplicationUser u JOIN dbo.Provider p ON p.ProviderId=u.ProviderId AND p.IsActive=1 WHERE u.UserId=@ActorUserId AND u.IsActive=1;
 SELECT @PatientId=p.PatientId,@Original=x.SupersedesPrescriptionUid FROM dbo.PatientPrescription x WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Patient p ON p.PatientUid=x.PatientUid AND p.IsDeleted=0 WHERE x.PatientUid=@PatientUid AND x.PrescriptionUid=@PrescriptionUid AND x.Status=N'Draft' AND x.PrescriberUserId=@ActorUserId AND x.RowVersion=@RowVersion;
 IF @ProviderUid IS NULL BEGIN ROLLBACK;THROW 51502,'An active mapped provider is required to prescribe.',1;END;
 IF @PatientId IS NULL BEGIN ROLLBACK;THROW 51504,'Draft is stale, immutable, missing, or belongs to another prescriber.',1;END;
 SELECT @OldValue=(SELECT * FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 SELECT @Html=(SELECT CONCAT(p.FirstName,N' ',p.LastName) PatientName,p.DateOfBirth,COALESCE(dbo.PrescriptionDateText(JSON_QUERY(x.DateFields,'$.WrittenDate')),CONVERT(varchar(10),x.PrescribedDate,23)) PrescribedDate,COALESCE(NULLIF(dbo.PrescriptionDateText(JSON_QUERY(x.DateFields,'$.StartDate')),''),CONVERT(varchar(10),x.StartDate,23)) StartDate,1 DatePrecisionVersion,JSON_QUERY(x.DateFields) DateFields,1 GenericNameVersion,x.GenericName,x.ProductDisplayText,x.SubstitutionProhibited,x.TreatmentType,@FirstName PrescriberFirstName,@LastName PrescriberLastName,@Ohip PrescriberOhip,x.StrengthValue,x.StrengthUnit,x.DoseAmount,x.DoseUnit,x.Route,x.FrequencyDisplay,x.Prn,x.Directions,x.Quantity,x.QuantityUnit,x.AuthorizedRepeats,x.InitialDaysSupply,x.RefillQuantity,x.RefillQuantityUnit,x.RefillDaysSupply,1 RefillInstructionsVersion,x.Indication,@ProviderName PrescriberDisplayName,@Credential PrescriberCredential,x.PrescriptionUid FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) FROM dbo.PatientPrescription x JOIN dbo.Patient p ON p.PatientUid=x.PatientUid WHERE x.PrescriptionUid=@PrescriptionUid;
 UPDATE dbo.PatientPrescription SET PrescriberFirstNameSnapshot=@FirstName,PrescriberLastNameSnapshot=@LastName,PrescriberOhipSnapshot=@Ohip,Status=N'Finalized',PrescriberProviderUid=@ProviderUid,PrescriberDisplayNameSnapshot=@ProviderName,PrescriberCredentialSnapshot=@Credential,ProductDisplaySnapshot=ProductDisplayText,FinalizedBy=@ActorUserId,FinalizedAtUtc=SYSUTCDATETIME(),ArtifactUid=@ArtifactUid WHERE PrescriptionUid=@PrescriptionUid;
 INSERT dbo.PatientPrescriptionArtifact(ArtifactUid,PrescriptionUid,PatientUid,ArtifactJson,CreatedBy) VALUES(@ArtifactUid,@PrescriptionUid,@PatientUid,@Html,@ActorUserId);
 SELECT @NewValue=(SELECT * FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt) VALUES(@ActorUserId,@PatientId,N'PrescriptionFinalized',N'PatientPrescription',CONVERT(nvarchar(100),@PrescriptionUid),@OldValue,@NewValue,SYSUTCDATETIME());
 IF @Original IS NOT NULL BEGIN
   SELECT @OriginalOld=(SELECT * FROM dbo.PatientPrescription WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND PrescriptionUid=@Original FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
   UPDATE dbo.PatientPrescription SET Status=N'Superseded',SupersededByPrescriptionUid=@PrescriptionUid WHERE PatientUid=@PatientUid AND PrescriptionUid=@Original AND Status=N'Finalized';
   IF @@ROWCOUNT<>1 BEGIN ROLLBACK;THROW 51505,'The correction source is no longer eligible for supersession.',1;END;
   SELECT @OriginalNew=(SELECT * FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@Original FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
   INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt) VALUES(@ActorUserId,@PatientId,N'PrescriptionSuperseded',N'PatientPrescription',CONVERT(nvarchar(100),@Original),@OriginalOld,@OriginalNew,SYSUTCDATETIME());
 END;
 COMMIT;EXEC dbo.PatientPrescription_GetByUid @PatientUid,@PrescriptionUid;
END;
GO
