-- Step 204: atomic user-supplied reason coding; no terminology lookup, backfill or output changes.
SET XACT_ABORT ON;
GO
CREATE OR ALTER FUNCTION dbo.PrescriptionReasonTextNormalize(@name nvarchar(max)) RETURNS nvarchar(max) AS
BEGIN
 IF @name IS NULL RETURN NULL;
 DECLARE @start int=1,@finish int=DATALENGTH(@name)/2,@code int;
 WHILE @start<=@finish
 BEGIN
  SET @code=UNICODE(SUBSTRING(@name COLLATE Latin1_General_100_BIN2,@start,1));
  IF @code NOT IN(32,160,5760,8239,8287,12288) AND @code NOT BETWEEN 8192 AND 8202 BREAK;
  SET @start+=1;
 END;
 WHILE @finish>=@start
 BEGIN
  SET @code=UNICODE(SUBSTRING(@name COLLATE Latin1_General_100_BIN2,@finish,1));
  IF @code NOT IN(32,160,5760,8239,8287,12288) AND @code NOT BETWEEN 8192 AND 8202 BREAK;
  SET @finish-=1;
 END;
 IF @start>@finish RETURN NULL;
 RETURN SUBSTRING(@name COLLATE Latin1_General_100_BIN2,@start,@finish-@start+1);
END;
GO
CREATE OR ALTER FUNCTION dbo.PrescriptionReasonTextValid(@name nvarchar(max),@maximum int,@required bit) RETURNS bit AS
BEGIN
 IF @name IS NULL RETURN CASE WHEN @required=1 THEN 0 ELSE 1 END;
 DECLARE @i int=1,@length int=DATALENGTH(@name)/2,@code int,@next int;
 WHILE @i<=@length
 BEGIN
  SET @code=UNICODE(SUBSTRING(@name COLLATE Latin1_General_100_BIN2,@i,1));
  IF @code BETWEEN 0 AND 31 OR @code BETWEEN 127 AND 159 OR @code IN(8232,8233) RETURN 0;
  IF @code BETWEEN 55296 AND 56319
  BEGIN
   IF @i=@length RETURN 0;
   SET @next=UNICODE(SUBSTRING(@name COLLATE Latin1_General_100_BIN2,@i+1,1));
   IF @next NOT BETWEEN 56320 AND 57343 RETURN 0;
   SET @i+=1;
  END
  ELSE IF @code BETWEEN 56320 AND 57343 RETURN 0;
  SET @i+=1;
 END;
 IF @required=1 AND dbo.PrescriptionReasonTextNormalize(@name) IS NULL RETURN 0;
 IF DATALENGTH(dbo.PrescriptionReasonTextNormalize(@name))>@maximum*2 RETURN 0;
 RETURN 1;
END;
GO
CREATE OR ALTER FUNCTION dbo.PrescriptionReasonTextStoredValid(@text nvarchar(max),@maximum int) RETURNS bit AS
BEGIN
 IF @text IS NULL RETURN 1;
 IF dbo.PrescriptionReasonTextValid(@text,@maximum,1)<>1 RETURN 0;
 IF DATALENGTH(@text)<>DATALENGTH(dbo.PrescriptionReasonTextNormalize(@text)) RETURN 0;
 IF @text COLLATE Latin1_General_100_BIN2<>dbo.PrescriptionReasonTextNormalize(@text) COLLATE Latin1_General_100_BIN2 RETURN 0;
 RETURN 1;
END;
GO
CREATE OR ALTER FUNCTION dbo.PrescriptionReasonCodingValid(@coding nvarchar(max)) RETURNS bit AS
BEGIN
 IF @coding IS NULL RETURN 1;
 IF ISJSON(@coding)<>1 OR LEFT(LTRIM(@coding),1)<>N'{' RETURN 0;
 IF EXISTS(SELECT 1 FROM OPENJSON(@coding) WHERE [key] COLLATE Latin1_General_100_BIN2 NOT IN(N'system',N'code',N'version',N'display') OR [type] NOT IN(0,1)) RETURN 0;
 IF EXISTS(SELECT [key] FROM OPENJSON(@coding) GROUP BY [key] HAVING COUNT(*)>1) RETURN 0;
 DECLARE @system nvarchar(max),@code nvarchar(max),@version nvarchar(max),@display nvarchar(max);
 SELECT @system=[system],@code=code,@version=version,@display=display FROM OPENJSON(@coding)
 WITH([system] nvarchar(max) '$.system',code nvarchar(max) '$.code',version nvarchar(max) '$.version',display nvarchar(max) '$.display');
 IF dbo.PrescriptionReasonTextValid(@system,200,1)<>1 OR dbo.PrescriptionReasonTextValid(@code,100,1)<>1
 OR dbo.PrescriptionReasonTextValid(@version,100,0)<>1 OR dbo.PrescriptionReasonTextValid(@display,500,0)<>1 RETURN 0;
 RETURN 1;
END;
GO
ALTER TABLE dbo.PatientPrescription ADD ReasonCodeSystem nvarchar(200) NULL,ReasonCode nvarchar(100) NULL,
 ReasonCodeVersion nvarchar(100) NULL,ReasonCodeDisplay nvarchar(500) NULL,ReasonCodingReviewed bit NULL;
GO
ALTER TABLE dbo.PatientPrescription ADD CONSTRAINT CK_PatientPrescription_ReasonCoding CHECK(
 (ReasonCodeSystem IS NULL AND ReasonCode IS NULL AND ReasonCodeVersion IS NULL AND ReasonCodeDisplay IS NULL) OR
 (ReasonCodeSystem IS NOT NULL AND ReasonCode IS NOT NULL AND dbo.PrescriptionReasonTextStoredValid(ReasonCodeSystem,200)=1 AND
 dbo.PrescriptionReasonTextStoredValid(ReasonCode,100)=1 AND dbo.PrescriptionReasonTextStoredValid(ReasonCodeVersion,100)=1 AND
 dbo.PrescriptionReasonTextStoredValid(ReasonCodeDisplay,500)=1));
GO
CREATE OR ALTER PROCEDURE dbo.PatientPrescription_GetByPatientUid @PatientUid UNIQUEIDENTIFIER AS
BEGIN
 SET NOCOUNT ON;
 SELECT CASE WHEN ReasonCodeSystem IS NULL THEN NULL ELSE (SELECT ReasonCodeSystem [system],ReasonCode code,ReasonCodeVersion version,ReasonCodeDisplay display FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END ReasonCoding,ReasonCodingReviewed,CAST(CASE WHEN SupersedesPrescriptionUid IS NOT NULL AND (ReasonCodeSystem IS NOT NULL OR EXISTS(SELECT 1 FROM dbo.PatientPrescription original WHERE original.PrescriptionUid=dbo.PatientPrescription.SupersedesPrescriptionUid AND original.PatientUid=dbo.PatientPrescription.PatientUid AND original.ReasonCodeSystem IS NOT NULL)) THEN 1 ELSE 0 END AS bit) ReasonCodingReviewRequired,TargetDispensingFacilityAddress,FacilityAddressReviewed,CAST(CASE WHEN SupersedesPrescriptionUid IS NOT NULL AND (TargetDispensingFacilityAddress IS NOT NULL OR EXISTS(SELECT 1 FROM dbo.PatientPrescription original WHERE original.PrescriptionUid=dbo.PatientPrescription.SupersedesPrescriptionUid AND original.PatientUid=dbo.PatientPrescription.PatientUid AND original.TargetDispensingFacilityAddress IS NOT NULL)) THEN 1 ELSE 0 END AS bit) FacilityAddressReviewRequired,TargetDispensingFacilityName,FacilityNameReviewed,CAST(CASE WHEN SupersedesPrescriptionUid IS NOT NULL AND (TargetDispensingFacilityName IS NOT NULL OR EXISTS(SELECT 1 FROM dbo.PatientPrescription original WHERE original.PrescriptionUid=dbo.PatientPrescription.SupersedesPrescriptionUid AND original.PatientUid=dbo.PatientPrescription.PatientUid AND original.TargetDispensingFacilityName IS NOT NULL)) THEN 1 ELSE 0 END AS bit) FacilityNameReviewRequired,EarliestPickup,CAST(CASE WHEN SupersedesPrescriptionUid IS NOT NULL AND (EarliestPickup IS NOT NULL OR EXISTS(SELECT 1 FROM dbo.PatientPrescription original WHERE original.PrescriptionUid=dbo.PatientPrescription.SupersedesPrescriptionUid AND original.PatientUid=dbo.PatientPrescription.PatientUid AND original.EarliestPickup IS NOT NULL)) THEN 1 ELSE 0 END AS bit) PickupReviewRequired,PaperCopyRequiredBeforeDispense,MinimumDispenseIntervalDays,SubstitutionProhibited,TreatmentType,PrescriberFirstNameSnapshot,PrescriberLastNameSnapshot,PrescriberOhipSnapshot,PrescriptionUid,PatientUid,Status,ProductName,GenericName,ProductIdentifierNamespace,ProductIdentifierValue,ProductDisplayText,
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
 SELECT CASE WHEN ReasonCodeSystem IS NULL THEN NULL ELSE (SELECT ReasonCodeSystem [system],ReasonCode code,ReasonCodeVersion version,ReasonCodeDisplay display FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END ReasonCoding,ReasonCodingReviewed,CAST(CASE WHEN SupersedesPrescriptionUid IS NOT NULL AND (ReasonCodeSystem IS NOT NULL OR EXISTS(SELECT 1 FROM dbo.PatientPrescription original WHERE original.PrescriptionUid=dbo.PatientPrescription.SupersedesPrescriptionUid AND original.PatientUid=dbo.PatientPrescription.PatientUid AND original.ReasonCodeSystem IS NOT NULL)) THEN 1 ELSE 0 END AS bit) ReasonCodingReviewRequired,TargetDispensingFacilityAddress,FacilityAddressReviewed,CAST(CASE WHEN SupersedesPrescriptionUid IS NOT NULL AND (TargetDispensingFacilityAddress IS NOT NULL OR EXISTS(SELECT 1 FROM dbo.PatientPrescription original WHERE original.PrescriptionUid=dbo.PatientPrescription.SupersedesPrescriptionUid AND original.PatientUid=dbo.PatientPrescription.PatientUid AND original.TargetDispensingFacilityAddress IS NOT NULL)) THEN 1 ELSE 0 END AS bit) FacilityAddressReviewRequired,TargetDispensingFacilityName,FacilityNameReviewed,CAST(CASE WHEN SupersedesPrescriptionUid IS NOT NULL AND (TargetDispensingFacilityName IS NOT NULL OR EXISTS(SELECT 1 FROM dbo.PatientPrescription original WHERE original.PrescriptionUid=dbo.PatientPrescription.SupersedesPrescriptionUid AND original.PatientUid=dbo.PatientPrescription.PatientUid AND original.TargetDispensingFacilityName IS NOT NULL)) THEN 1 ELSE 0 END AS bit) FacilityNameReviewRequired,EarliestPickup,CAST(CASE WHEN SupersedesPrescriptionUid IS NOT NULL AND (EarliestPickup IS NOT NULL OR EXISTS(SELECT 1 FROM dbo.PatientPrescription original WHERE original.PrescriptionUid=dbo.PatientPrescription.SupersedesPrescriptionUid AND original.PatientUid=dbo.PatientPrescription.PatientUid AND original.EarliestPickup IS NOT NULL)) THEN 1 ELSE 0 END AS bit) PickupReviewRequired,PaperCopyRequiredBeforeDispense,MinimumDispenseIntervalDays,SubstitutionProhibited,TreatmentType,PrescriberFirstNameSnapshot,PrescriberLastNameSnapshot,PrescriberOhipSnapshot,PrescriptionUid,PatientUid,Status,ProductName,GenericName,ProductIdentifierNamespace,ProductIdentifierValue,ProductDisplayText,
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
 @InitialDaysSupply INT=NULL,@RefillQuantity DECIMAL(18,3)=NULL,@RefillQuantityUnit NVARCHAR(50)=NULL,@RefillDaysSupply INT=NULL,@MedicationForm NVARCHAR(100)=NULL,@NotesToPharmacist NVARCHAR(1000)=NULL,@DateFields nvarchar(max)=NULL,@PrescribedDateSpecified bit=1,@StartDateSpecified bit=1,@GenericName nvarchar(max)=NULL,@GenericNameSpecified bit=0,@PrivateNoteSpecified bit=0,@PrivateNote nvarchar(max)=NULL,@PrivateNoteCorrelationId nvarchar(100)=NULL,@OrderFields nvarchar(max)=NULL,@EarliestPickupSpecified bit=0,@EarliestPickup nvarchar(max)=NULL,@TargetDispensingFacilityNameSpecified bit=0,@TargetDispensingFacilityName nvarchar(max)=NULL,@FacilityNameReviewed bit=0,@TargetDispensingFacilityAddressSpecified bit=0,@TargetDispensingFacilityAddress nvarchar(max)=NULL,@FacilityAddressReviewed bit=0,@ReasonCodingSpecified bit=0,@ReasonCoding nvarchar(max)=NULL,@ReasonCodingReviewed bit=0 AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF dbo.PrescriptionReasonCodingValid(@ReasonCoding)<>1 THROW 53024,'Invalid coded reason group.',1;
 DECLARE @ReasonSystem nvarchar(max),@ReasonCode nvarchar(max),@ReasonVersion nvarchar(max),@ReasonDisplay nvarchar(max);
 SELECT @ReasonSystem=dbo.PrescriptionReasonTextNormalize([system]),@ReasonCode=dbo.PrescriptionReasonTextNormalize(code),
 @ReasonVersion=dbo.PrescriptionReasonTextNormalize(version),@ReasonDisplay=dbo.PrescriptionReasonTextNormalize(display)
 FROM OPENJSON(COALESCE(@ReasonCoding,N'{}')) WITH([system] nvarchar(max) '$.system',code nvarchar(max) '$.code',version nvarchar(max) '$.version',display nvarchar(max) '$.display');
 IF dbo.PrescriptionFacilityAddressValid(@TargetDispensingFacilityAddress)<>1 THROW 53022,'Invalid target dispensing facility address.',1;
 SET @TargetDispensingFacilityAddress=dbo.PrescriptionFacilityAddressNormalize(@TargetDispensingFacilityAddress);
 IF dbo.PrescriptionFacilityNameValid(@TargetDispensingFacilityName)<>1 THROW 53020,'Invalid target dispensing facility name.',1;
 SET @TargetDispensingFacilityName=dbo.PrescriptionFacilityNameNormalize(@TargetDispensingFacilityName);
 IF dbo.PrescriptionPickupValid(@EarliestPickup)<>1 THROW 53000,'Invalid earliest pickup representation.',1;
 IF NULLIF(LTRIM(RTRIM(@ProductDisplayText)),N'') IS NULL OR DATALENGTH(@ProductDisplayText)>600
 OR CHARINDEX(NCHAR(10),@ProductDisplayText)>0 OR CHARINDEX(NCHAR(13),@ProductDisplayText)>0 OR CHARINDEX(NCHAR(11),@ProductDisplayText)>0
 OR CHARINDEX(NCHAR(12),@ProductDisplayText)>0 OR CHARINDEX(NCHAR(133),@ProductDisplayText)>0 OR CHARINDEX(NCHAR(8232),@ProductDisplayText)>0 OR CHARINDEX(NCHAR(8233),@ProductDisplayText)>0
 THROW 53000,'Drug Description must be a nonblank single line of at most 300 UTF-16 code units.',1;
 IF @OrderFields IS NOT NULL AND (ISJSON(@OrderFields)<>1 OR LEFT(LTRIM(@OrderFields),1)<>N'{') THROW 53000,'Order fields must be an object.',1;
 IF EXISTS(SELECT 1 FROM OPENJSON(COALESCE(@OrderFields,N'{}')) WHERE [key]=N'TreatmentType' AND [type] NOT IN(0,1)) THROW 53000,'Treatment type must be text or null.',1;
 IF EXISTS(SELECT 1 FROM OPENJSON(COALESCE(@OrderFields,N'{}')) WHERE [key]=N'SubstitutionProhibited' AND [type] NOT IN(0,3)) THROW 53000,'Substitution prohibition must be a boolean or null.',1;

 IF EXISTS(SELECT [key] FROM OPENJSON(COALESCE(@OrderFields,N'{}')) WHERE [key] IN(N'PaperCopyRequiredBeforeDispense',N'MinimumDispenseIntervalDays') GROUP BY [key] HAVING COUNT(*)>1) THROW 53000,'Duplicate order clinical fields.',1;
 IF EXISTS(SELECT 1 FROM OPENJSON(COALESCE(@OrderFields,N'{}')) WHERE [key]=N'PaperCopyRequiredBeforeDispense' AND NOT([type]=0 OR ([type]=3 AND [value]=N'true'))) THROW 53000,'Paper copy requirement permits Yes or Not recorded only.',1;
 IF EXISTS(SELECT 1 FROM OPENJSON(COALESCE(@OrderFields,N'{}')) WHERE [key]=N'MinimumDispenseIntervalDays' AND [type]<>0 AND ([type]<>2 OR [value] COLLATE Latin1_General_100_BIN2 LIKE N'%[^0-9]%' OR TRY_CONVERT(int,[value]) IS NULL OR TRY_CONVERT(int,[value])<=0)) THROW 53000,'Minimum dispense interval must be positive whole days within Int32 storage.',1;
 DECLARE @PaperSpecified bit=CASE WHEN EXISTS(SELECT 1 FROM OPENJSON(COALESCE(@OrderFields,N'{}')) WHERE [key]=N'PaperCopyRequiredBeforeDispense') THEN 1 ELSE 0 END,
 @IntervalSpecified bit=CASE WHEN EXISTS(SELECT 1 FROM OPENJSON(COALESCE(@OrderFields,N'{}')) WHERE [key]=N'MinimumDispenseIntervalDays') THEN 1 ELSE 0 END;
 DECLARE @PaperCopyRequiredBeforeDispense bit=CASE JSON_VALUE(@OrderFields,'$.PaperCopyRequiredBeforeDispense') WHEN N'true' THEN 1 END,
 @MinimumDispenseIntervalDays int=TRY_CONVERT(int,JSON_VALUE(@OrderFields,'$.MinimumDispenseIntervalDays'));
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
 -- Corrections inherit the exact original fact even if the tenant zone has since changed.
 IF @SupersedesPrescriptionUid IS NOT NULL
 BEGIN
  SELECT @EarliestPickup=EarliestPickup,@TargetDispensingFacilityName=TargetDispensingFacilityName,@TargetDispensingFacilityAddress=TargetDispensingFacilityAddress,@ReasonSystem=ReasonCodeSystem,@ReasonCode=ReasonCode,@ReasonVersion=ReasonCodeVersion,@ReasonDisplay=ReasonCodeDisplay FROM dbo.PatientPrescription WITH(UPDLOCK,HOLDLOCK)
  WHERE PatientUid=@PatientUid AND PrescriptionUid=@SupersedesPrescriptionUid AND Status=N'Finalized';
  IF @@ROWCOUNT<>1 THROW 51505,'The correction source is no longer eligible.',1;
 END;
 INSERT dbo.PatientPrescription(ReasonCodeSystem,ReasonCode,ReasonCodeVersion,ReasonCodeDisplay,ReasonCodingReviewed,TargetDispensingFacilityAddress,FacilityAddressReviewed,TargetDispensingFacilityName,FacilityNameReviewed,EarliestPickup,PaperCopyRequiredBeforeDispense,MinimumDispenseIntervalDays,SubstitutionProhibited,TreatmentType,PrescriberFirstNameSnapshot,PrescriberLastNameSnapshot,PrescriberOhipSnapshot,PrescriptionUid,PatientUid,GenericName,ProductName,ProductIdentifierNamespace,ProductIdentifierValue,ProductDisplayText,StrengthValue,StrengthUnit,DoseAmount,DoseUnit,Route,FrequencyCode,FrequencyDisplay,Prn,Directions,Quantity,QuantityUnit,MedicationForm,NotesToPharmacist,AuthorizedRepeats,InitialDaysSupply,RefillQuantity,RefillQuantityUnit,RefillDaysSupply,Indication,PrescribedDate,StartDate,DateFields,CreatedBy,PrescriberUserId,PrescriberProviderUid,SupersedesPrescriptionUid)
 VALUES(@ReasonSystem,@ReasonCode,@ReasonVersion,@ReasonDisplay,CASE WHEN @SupersedesPrescriptionUid IS NOT NULL AND @ReasonSystem IS NOT NULL THEN 0 END,@TargetDispensingFacilityAddress,CASE WHEN @SupersedesPrescriptionUid IS NOT NULL AND @TargetDispensingFacilityAddress IS NOT NULL THEN 0 END,@TargetDispensingFacilityName,CASE WHEN @SupersedesPrescriptionUid IS NOT NULL AND @TargetDispensingFacilityName IS NOT NULL THEN 0 END,@EarliestPickup,@PaperCopyRequiredBeforeDispense,@MinimumDispenseIntervalDays,@SubstitutionProhibited,@TreatmentType,@FirstName,@LastName,@Ohip,@Uid,@PatientUid,@GenericName,LTRIM(RTRIM(@ProductName)),NULLIF(LTRIM(RTRIM(@ProductIdentifierNamespace)),N''),NULLIF(LTRIM(RTRIM(@ProductIdentifierValue)),N''),LTRIM(RTRIM(@ProductDisplayText)),@StrengthValue,NULLIF(LTRIM(RTRIM(@StrengthUnit)),N''),@DoseAmount,NULLIF(LTRIM(RTRIM(@DoseUnit)),N''),LTRIM(RTRIM(@Route)),@FrequencyCode,@FrequencyDisplay,@Prn,LTRIM(RTRIM(@Directions)),@Quantity,LTRIM(RTRIM(@QuantityUnit)),@MedicationForm,@NotesToPharmacist,@AuthorizedRepeats,@InitialDaysSupply,@RefillQuantity,NULLIF(LTRIM(RTRIM(@RefillQuantityUnit)),N''),@RefillDaysSupply,NULLIF(LTRIM(RTRIM(@Indication)),N''),@PrescribedDate,@StartDate,@DateFields,@ActorUserId,@ActorUserId,@ProviderUid,@SupersedesPrescriptionUid);
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
 @InitialDaysSupply INT=NULL,@RefillQuantity DECIMAL(18,3)=NULL,@RefillQuantityUnit NVARCHAR(50)=NULL,@RefillDaysSupply INT=NULL,@MedicationForm NVARCHAR(100)=NULL,@NotesToPharmacist NVARCHAR(1000)=NULL,@DateFields nvarchar(max)=NULL,@PrescribedDateSpecified bit=1,@StartDateSpecified bit=1,@GenericName nvarchar(max)=NULL,@GenericNameSpecified bit=0,@PrivateNoteSpecified bit=0,@PrivateNote nvarchar(max)=NULL,@PrivateNoteCorrelationId nvarchar(100)=NULL,@OrderFields nvarchar(max)=NULL,@EarliestPickupSpecified bit=0,@EarliestPickup nvarchar(max)=NULL,@TargetDispensingFacilityNameSpecified bit=0,@TargetDispensingFacilityName nvarchar(max)=NULL,@FacilityNameReviewed bit=0,@TargetDispensingFacilityAddressSpecified bit=0,@TargetDispensingFacilityAddress nvarchar(max)=NULL,@FacilityAddressReviewed bit=0,@ReasonCodingSpecified bit=0,@ReasonCoding nvarchar(max)=NULL,@ReasonCodingReviewed bit=0 AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF dbo.PrescriptionReasonCodingValid(@ReasonCoding)<>1 THROW 53024,'Invalid coded reason group.',1;
 DECLARE @ReasonSystem nvarchar(max),@ReasonCode nvarchar(max),@ReasonVersion nvarchar(max),@ReasonDisplay nvarchar(max);
 SELECT @ReasonSystem=dbo.PrescriptionReasonTextNormalize([system]),@ReasonCode=dbo.PrescriptionReasonTextNormalize(code),
 @ReasonVersion=dbo.PrescriptionReasonTextNormalize(version),@ReasonDisplay=dbo.PrescriptionReasonTextNormalize(display)
 FROM OPENJSON(COALESCE(@ReasonCoding,N'{}')) WITH([system] nvarchar(max) '$.system',code nvarchar(max) '$.code',version nvarchar(max) '$.version',display nvarchar(max) '$.display');
 IF dbo.PrescriptionFacilityAddressValid(@TargetDispensingFacilityAddress)<>1 THROW 53022,'Invalid target dispensing facility address.',1;
 SET @TargetDispensingFacilityAddress=dbo.PrescriptionFacilityAddressNormalize(@TargetDispensingFacilityAddress);
 IF dbo.PrescriptionFacilityNameValid(@TargetDispensingFacilityName)<>1 THROW 53020,'Invalid target dispensing facility name.',1;
 SET @TargetDispensingFacilityName=dbo.PrescriptionFacilityNameNormalize(@TargetDispensingFacilityName);
 IF dbo.PrescriptionPickupValid(@EarliestPickup)<>1 THROW 53000,'Invalid earliest pickup representation.',1;
 IF NULLIF(LTRIM(RTRIM(@ProductDisplayText)),N'') IS NULL OR DATALENGTH(@ProductDisplayText)>600
 OR CHARINDEX(NCHAR(10),@ProductDisplayText)>0 OR CHARINDEX(NCHAR(13),@ProductDisplayText)>0 OR CHARINDEX(NCHAR(11),@ProductDisplayText)>0
 OR CHARINDEX(NCHAR(12),@ProductDisplayText)>0 OR CHARINDEX(NCHAR(133),@ProductDisplayText)>0 OR CHARINDEX(NCHAR(8232),@ProductDisplayText)>0 OR CHARINDEX(NCHAR(8233),@ProductDisplayText)>0
 THROW 53000,'Drug Description must be a nonblank single line of at most 300 UTF-16 code units.',1;
 IF @OrderFields IS NOT NULL AND (ISJSON(@OrderFields)<>1 OR LEFT(LTRIM(@OrderFields),1)<>N'{') THROW 53000,'Order fields must be an object.',1;
 IF EXISTS(SELECT 1 FROM OPENJSON(COALESCE(@OrderFields,N'{}')) WHERE [key]=N'TreatmentType' AND [type] NOT IN(0,1)) THROW 53000,'Treatment type must be text or null.',1;
 IF EXISTS(SELECT 1 FROM OPENJSON(COALESCE(@OrderFields,N'{}')) WHERE [key]=N'SubstitutionProhibited' AND [type] NOT IN(0,3)) THROW 53000,'Substitution prohibition must be a boolean or null.',1;

 IF EXISTS(SELECT [key] FROM OPENJSON(COALESCE(@OrderFields,N'{}')) WHERE [key] IN(N'PaperCopyRequiredBeforeDispense',N'MinimumDispenseIntervalDays') GROUP BY [key] HAVING COUNT(*)>1) THROW 53000,'Duplicate order clinical fields.',1;
 IF EXISTS(SELECT 1 FROM OPENJSON(COALESCE(@OrderFields,N'{}')) WHERE [key]=N'PaperCopyRequiredBeforeDispense' AND NOT([type]=0 OR ([type]=3 AND [value]=N'true'))) THROW 53000,'Paper copy requirement permits Yes or Not recorded only.',1;
 IF EXISTS(SELECT 1 FROM OPENJSON(COALESCE(@OrderFields,N'{}')) WHERE [key]=N'MinimumDispenseIntervalDays' AND [type]<>0 AND ([type]<>2 OR [value] COLLATE Latin1_General_100_BIN2 LIKE N'%[^0-9]%' OR TRY_CONVERT(int,[value]) IS NULL OR TRY_CONVERT(int,[value])<=0)) THROW 53000,'Minimum dispense interval must be positive whole days within Int32 storage.',1;
 DECLARE @PaperSpecified bit=CASE WHEN EXISTS(SELECT 1 FROM OPENJSON(COALESCE(@OrderFields,N'{}')) WHERE [key]=N'PaperCopyRequiredBeforeDispense') THEN 1 ELSE 0 END,
 @IntervalSpecified bit=CASE WHEN EXISTS(SELECT 1 FROM OPENJSON(COALESCE(@OrderFields,N'{}')) WHERE [key]=N'MinimumDispenseIntervalDays') THEN 1 ELSE 0 END;
 DECLARE @PaperCopyRequiredBeforeDispense bit=CASE JSON_VALUE(@OrderFields,'$.PaperCopyRequiredBeforeDispense') WHEN N'true' THEN 1 END,
 @MinimumDispenseIntervalDays int=TRY_CONVERT(int,JSON_VALUE(@OrderFields,'$.MinimumDispenseIntervalDays'));
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
 DECLARE @OldReasonSystem nvarchar(200),@OldReasonCode nvarchar(100),@OldReasonVersion nvarchar(100),@OldReasonDisplay nvarchar(500),@OldReasonReviewed bit; DECLARE @OldAddress nvarchar(1000),@OldAddressReviewed bit; DECLARE @OldFacilityName nvarchar(200),@OldFacilityReviewed bit; DECLARE @PatientId BIGINT,@OldValue nvarchar(max),@NewValue nvarchar(max),@ExistingFields nvarchar(max),@ExistingWritten date,@ExistingStart date;
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
 SELECT @OldReasonSystem=x.ReasonCodeSystem,@OldReasonCode=x.ReasonCode,@OldReasonVersion=x.ReasonCodeVersion,@OldReasonDisplay=x.ReasonCodeDisplay,@OldReasonReviewed=x.ReasonCodingReviewed,@OldAddress=x.TargetDispensingFacilityAddress,@OldAddressReviewed=x.FacilityAddressReviewed,@OldFacilityName=x.TargetDispensingFacilityName,@OldFacilityReviewed=x.FacilityNameReviewed,@PatientId=p.PatientId,@OldValue=(SELECT x.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER),@ExistingFields=x.DateFields,@ExistingWritten=x.PrescribedDate,@ExistingStart=x.StartDate FROM dbo.PatientPrescription x WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Patient p ON p.PatientUid=x.PatientUid AND p.IsDeleted=0 WHERE x.PatientUid=@PatientUid AND x.PrescriptionUid=@PrescriptionUid;
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
 -- A name replacement/clear invalidates earlier review unless this version's save explicitly reviews it.
 DECLARE @NameChanged bit=CASE WHEN @TargetDispensingFacilityNameSpecified=1 AND
 (ISNULL(DATALENGTH(@TargetDispensingFacilityName),-1)<>ISNULL(DATALENGTH(@OldFacilityName),-1) OR
 COALESCE(@TargetDispensingFacilityName,N'') COLLATE Latin1_General_100_BIN2<>COALESCE(@OldFacilityName,N'') COLLATE Latin1_General_100_BIN2) THEN 1 ELSE 0 END;
 -- Address review is independent of name/pickup review and belongs to the saved RowVersion.
 DECLARE @AddressChanged bit=CASE WHEN @TargetDispensingFacilityAddressSpecified=1 AND
 (ISNULL(DATALENGTH(@TargetDispensingFacilityAddress),-1)<>ISNULL(DATALENGTH(@OldAddress),-1) OR
 COALESCE(@TargetDispensingFacilityAddress,N'') COLLATE Latin1_General_100_BIN2<>COALESCE(@OldAddress,N'') COLLATE Latin1_General_100_BIN2) THEN 1 ELSE 0 END;
 DECLARE @ReasonChanged bit=CASE WHEN @ReasonCodingSpecified=1 AND ((ISNULL(DATALENGTH(@ReasonSystem),-1)<>ISNULL(DATALENGTH(@OldReasonSystem),-1) OR COALESCE(@ReasonSystem,N'') COLLATE Latin1_General_100_BIN2<>COALESCE(@OldReasonSystem,N'') COLLATE Latin1_General_100_BIN2) OR (ISNULL(DATALENGTH(@ReasonCode),-1)<>ISNULL(DATALENGTH(@OldReasonCode),-1) OR COALESCE(@ReasonCode,N'') COLLATE Latin1_General_100_BIN2<>COALESCE(@OldReasonCode,N'') COLLATE Latin1_General_100_BIN2) OR (ISNULL(DATALENGTH(@ReasonVersion),-1)<>ISNULL(DATALENGTH(@OldReasonVersion),-1) OR COALESCE(@ReasonVersion,N'') COLLATE Latin1_General_100_BIN2<>COALESCE(@OldReasonVersion,N'') COLLATE Latin1_General_100_BIN2) OR (ISNULL(DATALENGTH(@ReasonDisplay),-1)<>ISNULL(DATALENGTH(@OldReasonDisplay),-1) OR COALESCE(@ReasonDisplay,N'') COLLATE Latin1_General_100_BIN2<>COALESCE(@OldReasonDisplay,N'') COLLATE Latin1_General_100_BIN2)) THEN 1 ELSE 0 END;
 UPDATE dbo.PatientPrescription SET ReasonCodeSystem=CASE WHEN @ReasonCodingSpecified=1 THEN @ReasonSystem ELSE ReasonCodeSystem END,ReasonCode=CASE WHEN @ReasonCodingSpecified=1 THEN @ReasonCode ELSE ReasonCode END,ReasonCodeVersion=CASE WHEN @ReasonCodingSpecified=1 THEN @ReasonVersion ELSE ReasonCodeVersion END,ReasonCodeDisplay=CASE WHEN @ReasonCodingSpecified=1 THEN @ReasonDisplay ELSE ReasonCodeDisplay END,ReasonCodingReviewed=CASE WHEN SupersedesPrescriptionUid IS NULL THEN NULL WHEN @ReasonCodingReviewed=1 THEN 1 WHEN @ReasonChanged=1 THEN 0 ELSE @OldReasonReviewed END,TargetDispensingFacilityAddress=CASE WHEN @TargetDispensingFacilityAddressSpecified=1 THEN @TargetDispensingFacilityAddress ELSE TargetDispensingFacilityAddress END,
 FacilityAddressReviewed=CASE WHEN SupersedesPrescriptionUid IS NULL THEN NULL WHEN @FacilityAddressReviewed=1 THEN 1 WHEN @AddressChanged=1 THEN 0 ELSE @OldAddressReviewed END,TargetDispensingFacilityName=CASE WHEN @TargetDispensingFacilityNameSpecified=1 THEN @TargetDispensingFacilityName ELSE TargetDispensingFacilityName END,
 FacilityNameReviewed=CASE WHEN SupersedesPrescriptionUid IS NULL THEN NULL WHEN @FacilityNameReviewed=1 THEN 1 WHEN @NameChanged=1 THEN 0 ELSE @OldFacilityReviewed END,EarliestPickup=CASE WHEN @EarliestPickupSpecified=1 THEN @EarliestPickup ELSE EarliestPickup END,PaperCopyRequiredBeforeDispense=CASE WHEN @PaperSpecified=1 THEN @PaperCopyRequiredBeforeDispense ELSE PaperCopyRequiredBeforeDispense END,MinimumDispenseIntervalDays=CASE WHEN @IntervalSpecified=1 THEN @MinimumDispenseIntervalDays ELSE MinimumDispenseIntervalDays END,SubstitutionProhibited=CASE WHEN @SubstitutionSpecified=1 THEN @SubstitutionProhibited ELSE SubstitutionProhibited END,TreatmentType=CASE WHEN @OrderFields IS NULL THEN TreatmentType ELSE @TreatmentType END,GenericName=CASE WHEN @GenericNameSpecified=1 THEN @GenericName ELSE GenericName END,MedicationForm=NULLIF(LTRIM(RTRIM(@MedicationForm)),N''),NotesToPharmacist=NULLIF(LTRIM(RTRIM(@NotesToPharmacist)),N''),ProductName=LTRIM(RTRIM(@ProductName)),ProductIdentifierNamespace=NULLIF(LTRIM(RTRIM(@ProductIdentifierNamespace)),N''),ProductIdentifierValue=NULLIF(LTRIM(RTRIM(@ProductIdentifierValue)),N''),ProductDisplayText=LTRIM(RTRIM(@ProductDisplayText)),StrengthValue=@StrengthValue,StrengthUnit=NULLIF(LTRIM(RTRIM(@StrengthUnit)),N''),DoseAmount=@DoseAmount,DoseUnit=NULLIF(LTRIM(RTRIM(@DoseUnit)),N''),Route=LTRIM(RTRIM(@Route)),FrequencyCode=@FrequencyCode,FrequencyDisplay=@FrequencyDisplay,Prn=@Prn,Directions=LTRIM(RTRIM(@Directions)),Quantity=@Quantity,QuantityUnit=LTRIM(RTRIM(@QuantityUnit)),AuthorizedRepeats=@AuthorizedRepeats,InitialDaysSupply=@InitialDaysSupply,RefillQuantity=@RefillQuantity,RefillQuantityUnit=NULLIF(LTRIM(RTRIM(@RefillQuantityUnit)),N''),RefillDaysSupply=@RefillDaysSupply,Indication=NULLIF(LTRIM(RTRIM(@Indication)),N''),PrescribedDate=@PrescribedDate,StartDate=@StartDate,DateFields=@DateFields,UpdatedBy=@ActorUserId,UpdatedAtUtc=SYSUTCDATETIME()
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
CREATE OR ALTER PROCEDURE dbo.PatientPrescription_SignAndFinalize @PatientUid UNIQUEIDENTIFIER,@PrescriptionUid UNIQUEIDENTIFIER,@ActorUserId BIGINT,@RowVersion VARBINARY(MAX),@ConfirmSignature BIT,@PickupReviewed bit=0 AS
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
 IF @Original IS NOT NULL AND EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid IN(@PrescriptionUid,@Original) AND ReasonCodeSystem IS NOT NULL)
 AND NOT EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid AND ReasonCodingReviewed=1)
 BEGIN ROLLBACK;THROW 53025,'Review coded reason in Edit and save before finalizing this correction.',1;END;
 IF @Original IS NOT NULL AND EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid IN(@PrescriptionUid,@Original) AND TargetDispensingFacilityAddress IS NOT NULL)
 AND NOT EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid AND FacilityAddressReviewed=1)
 BEGIN ROLLBACK;THROW 53023,'Review target dispensing facility address in Edit and save before finalizing this correction.',1;END;
 IF @Original IS NOT NULL AND EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid IN(@PrescriptionUid,@Original) AND TargetDispensingFacilityName IS NOT NULL)
 AND NOT EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid AND FacilityNameReviewed=1)
 BEGIN ROLLBACK;THROW 53021,'Review target dispensing facility name in Edit and save before finalizing this correction.',1;END;
 IF @Original IS NOT NULL AND ISNULL(@PickupReviewed,0)<>1 AND EXISTS(
  SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid IN(@PrescriptionUid,@Original) AND EarliestPickup IS NOT NULL)
 BEGIN ROLLBACK;THROW 51504,'Review earliest pickup before finalizing this correction.',1;END;
 SELECT @OldValue=(SELECT * FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 SELECT @Html=(SELECT 2 PrescriptionOutputVersion,1 SignatureVersion,1 RefillInstructionsVersion,
 CONCAT(p.FirstName,N' ',p.LastName) PatientName,p.DateOfBirth,p.HealthCardNumber,p.PhoneNumber PatientPhone,
 p.AddressLine1 PatientAddressLine1,p.AddressLine2 PatientAddressLine2,p.City PatientCity,p.Province PatientProvince,p.PostalCode PatientPostalCode,
 c.LegalName ClinicName,c.Phone ClinicPhone,c.AddressLine1 ClinicAddressLine1,c.AddressLine2 ClinicAddressLine2,c.City ClinicCity,c.ProvinceState ClinicProvince,c.PostalCode ClinicPostalCode,
 COALESCE(dbo.PrescriptionDateText(JSON_QUERY(x.DateFields,'$.WrittenDate')),CONVERT(varchar(10),x.PrescribedDate,23)) PrescribedDate,COALESCE(NULLIF(dbo.PrescriptionDateText(JSON_QUERY(x.DateFields,'$.StartDate')),''),CONVERT(varchar(10),x.StartDate,23)) StartDate,1 DatePrecisionVersion,JSON_QUERY(x.DateFields) DateFields,1 GenericNameVersion,1 ReasonCodingVersion,JSON_QUERY(CASE WHEN x.ReasonCodeSystem IS NULL THEN NULL ELSE (SELECT x.ReasonCodeSystem [system],x.ReasonCode code,x.ReasonCodeVersion version,x.ReasonCodeDisplay display FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END) ReasonCoding,x.ReasonCodingReviewed,1 TargetDispensingFacilityAddressVersion,x.TargetDispensingFacilityAddress,x.FacilityAddressReviewed,1 TargetDispensingFacilityNameVersion,x.TargetDispensingFacilityName,x.FacilityNameReviewed,1 EarliestPickupVersion,JSON_QUERY(x.EarliestPickup) EarliestPickup,@PickupReviewed EarliestPickupReviewed,x.GenericName,x.ProductDisplayText,x.PaperCopyRequiredBeforeDispense,x.MinimumDispenseIntervalDays,x.SubstitutionProhibited,x.TreatmentType,@FirstName PrescriberFirstName,@LastName PrescriberLastName,@Ohip PrescriberOhip,x.MedicationForm,x.NotesToPharmacist,x.StrengthValue,x.StrengthUnit,x.DoseAmount,x.DoseUnit,x.Route,x.FrequencyDisplay,x.Prn,x.Directions,
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
 SET @NewValue=JSON_MODIFY(@NewValue,'$.EarliestPickupReviewed',ISNULL(@PickupReviewed,0));
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
CREATE OR ALTER PROCEDURE dbo.PatientPrescription_Finalize @PatientUid UNIQUEIDENTIFIER,@PrescriptionUid UNIQUEIDENTIFIER,@ActorUserId BIGINT,@RowVersion VARBINARY(MAX),@PickupReviewed bit=0 AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;IF @RowVersion IS NULL OR DATALENGTH(@RowVersion)<>8 THROW 51504,'A reviewed eight-byte row version is required.',1; DECLARE @FirstName nvarchar(100),@LastName nvarchar(100),@Ohip nvarchar(50); DECLARE @PatientId BIGINT,@ProviderUid UNIQUEIDENTIFIER,@ProviderName NVARCHAR(200),@Credential NVARCHAR(200),@ArtifactUid UNIQUEIDENTIFIER=NEWID(),@Original UNIQUEIDENTIFIER,@Html NVARCHAR(MAX),@OldValue nvarchar(max),@NewValue nvarchar(max),@OriginalOld nvarchar(max),@OriginalNew nvarchar(max);
 BEGIN TRANSACTION;
 SELECT @FirstName=p.FirstName,@LastName=p.LastName,@Ohip=p.BillingNumber,@ProviderUid=p.ProviderUid,@ProviderName=p.DisplayName,@Credential=CONCAT(p.ProviderType,CASE WHEN p.BillingNumber IS NULL THEN N'' ELSE N' | '+p.BillingNumber END) FROM dbo.ApplicationUser u JOIN dbo.Provider p ON p.ProviderId=u.ProviderId AND p.IsActive=1 WHERE u.UserId=@ActorUserId AND u.IsActive=1;
 SELECT @PatientId=p.PatientId,@Original=x.SupersedesPrescriptionUid FROM dbo.PatientPrescription x WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Patient p ON p.PatientUid=x.PatientUid AND p.IsDeleted=0 WHERE x.PatientUid=@PatientUid AND x.PrescriptionUid=@PrescriptionUid AND x.Status=N'Draft' AND x.PrescriberUserId=@ActorUserId AND x.RowVersion=@RowVersion;
 IF @ProviderUid IS NULL BEGIN ROLLBACK;THROW 51502,'An active mapped provider is required to prescribe.',1;END;
 IF @PatientId IS NULL BEGIN ROLLBACK;THROW 51504,'Draft is stale, immutable, missing, or belongs to another prescriber.',1;END;
 IF @Original IS NOT NULL AND EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid IN(@PrescriptionUid,@Original) AND ReasonCodeSystem IS NOT NULL)
 AND NOT EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid AND ReasonCodingReviewed=1)
 BEGIN ROLLBACK;THROW 53025,'Review coded reason in Edit and save before finalizing this correction.',1;END;
 IF @Original IS NOT NULL AND EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid IN(@PrescriptionUid,@Original) AND TargetDispensingFacilityAddress IS NOT NULL)
 AND NOT EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid AND FacilityAddressReviewed=1)
 BEGIN ROLLBACK;THROW 53023,'Review target dispensing facility address in Edit and save before finalizing this correction.',1;END;
 IF @Original IS NOT NULL AND EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid IN(@PrescriptionUid,@Original) AND TargetDispensingFacilityName IS NOT NULL)
 AND NOT EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid AND FacilityNameReviewed=1)
 BEGIN ROLLBACK;THROW 53021,'Review target dispensing facility name in Edit and save before finalizing this correction.',1;END;
 IF @Original IS NOT NULL AND ISNULL(@PickupReviewed,0)<>1 AND EXISTS(
  SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid IN(@PrescriptionUid,@Original) AND EarliestPickup IS NOT NULL)
 BEGIN ROLLBACK;THROW 51504,'Review earliest pickup before finalizing this correction.',1;END;
 SELECT @OldValue=(SELECT * FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 SELECT @Html=(SELECT CONCAT(p.FirstName,N' ',p.LastName) PatientName,p.DateOfBirth,COALESCE(dbo.PrescriptionDateText(JSON_QUERY(x.DateFields,'$.WrittenDate')),CONVERT(varchar(10),x.PrescribedDate,23)) PrescribedDate,COALESCE(NULLIF(dbo.PrescriptionDateText(JSON_QUERY(x.DateFields,'$.StartDate')),''),CONVERT(varchar(10),x.StartDate,23)) StartDate,1 DatePrecisionVersion,JSON_QUERY(x.DateFields) DateFields,1 GenericNameVersion,1 ReasonCodingVersion,JSON_QUERY(CASE WHEN x.ReasonCodeSystem IS NULL THEN NULL ELSE (SELECT x.ReasonCodeSystem [system],x.ReasonCode code,x.ReasonCodeVersion version,x.ReasonCodeDisplay display FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END) ReasonCoding,x.ReasonCodingReviewed,1 TargetDispensingFacilityAddressVersion,x.TargetDispensingFacilityAddress,x.FacilityAddressReviewed,1 TargetDispensingFacilityNameVersion,x.TargetDispensingFacilityName,x.FacilityNameReviewed,1 EarliestPickupVersion,JSON_QUERY(x.EarliestPickup) EarliestPickup,@PickupReviewed EarliestPickupReviewed,x.GenericName,x.ProductDisplayText,x.PaperCopyRequiredBeforeDispense,x.MinimumDispenseIntervalDays,x.SubstitutionProhibited,x.TreatmentType,@FirstName PrescriberFirstName,@LastName PrescriberLastName,@Ohip PrescriberOhip,x.StrengthValue,x.StrengthUnit,x.DoseAmount,x.DoseUnit,x.Route,x.FrequencyDisplay,x.Prn,x.Directions,x.Quantity,x.QuantityUnit,x.AuthorizedRepeats,x.InitialDaysSupply,x.RefillQuantity,x.RefillQuantityUnit,x.RefillDaysSupply,1 RefillInstructionsVersion,x.Indication,@ProviderName PrescriberDisplayName,@Credential PrescriberCredential,x.PrescriptionUid FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) FROM dbo.PatientPrescription x JOIN dbo.Patient p ON p.PatientUid=x.PatientUid WHERE x.PrescriptionUid=@PrescriptionUid;
 UPDATE dbo.PatientPrescription SET PrescriberFirstNameSnapshot=@FirstName,PrescriberLastNameSnapshot=@LastName,PrescriberOhipSnapshot=@Ohip,Status=N'Finalized',PrescriberProviderUid=@ProviderUid,PrescriberDisplayNameSnapshot=@ProviderName,PrescriberCredentialSnapshot=@Credential,ProductDisplaySnapshot=ProductDisplayText,FinalizedBy=@ActorUserId,FinalizedAtUtc=SYSUTCDATETIME(),ArtifactUid=@ArtifactUid WHERE PrescriptionUid=@PrescriptionUid;
 INSERT dbo.PatientPrescriptionArtifact(ArtifactUid,PrescriptionUid,PatientUid,ArtifactJson,CreatedBy) VALUES(@ArtifactUid,@PrescriptionUid,@PatientUid,@Html,@ActorUserId);
 SELECT @NewValue=(SELECT * FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 SET @NewValue=JSON_MODIFY(@NewValue,'$.EarliestPickupReviewed',ISNULL(@PickupReviewed,0));
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
