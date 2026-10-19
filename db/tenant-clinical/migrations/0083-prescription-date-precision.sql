-- Step 116: scoped prescription date precision. No clinical backfill or artifact rewrites.
SET XACT_ABORT ON;
GO
CREATE OR ALTER FUNCTION dbo.PrescriptionDateText(@Value nvarchar(max)) RETURNS varchar(10) AS
BEGIN
 IF @Value IS NULL OR ISJSON(@Value)<>1 OR LEFT(LTRIM(@Value),1)<>'{' RETURN NULL;
 IF EXISTS(SELECT 1 FROM OPENJSON(@Value) GROUP BY [key] COLLATE Latin1_General_100_BIN2 HAVING COUNT(*)>1) RETURN NULL;
 IF EXISTS(SELECT 1 FROM OPENJSON(@Value) WHERE [key] COLLATE Latin1_General_100_BIN2 NOT IN('Precision','Year','Month','Day')) RETURN NULL;
 IF EXISTS(SELECT 1 FROM OPENJSON(@Value) WHERE ([key]='Precision' AND type<>1) OR ([key]<>'Precision' AND type NOT IN(0,2))) RETURN NULL;
 DECLARE @P nvarchar(max)=JSON_VALUE(@Value,'$.Precision'),@Y int=TRY_CONVERT(int,JSON_VALUE(@Value,'$.Year')),
 @M int=TRY_CONVERT(int,JSON_VALUE(@Value,'$.Month')),@D int=TRY_CONVERT(int,JSON_VALUE(@Value,'$.Day'));
 IF EXISTS(SELECT 1 FROM OPENJSON(@Value) WHERE [key]<>'Precision' AND type=2 AND (TRY_CONVERT(int,value) IS NULL OR value LIKE '%.%' OR value LIKE '%e%')) RETURN NULL;
 IF @P COLLATE Latin1_General_100_BIN2='Unknown' AND @Y IS NULL AND @M IS NULL AND @D IS NULL RETURN '';
 IF @P IS NULL RETURN NULL;
 IF @Y IS NULL OR @Y<1 OR @Y>9999 RETURN NULL;
 DECLARE @Text varchar(10)=RIGHT('0000'+CONVERT(varchar(4),@Y),4);
 IF @P COLLATE Latin1_General_100_BIN2='Year' AND @M IS NULL AND @D IS NULL RETURN @Text;
 IF @M IS NULL OR @M<1 OR @M>12 RETURN NULL;
 SET @Text=@Text+'-'+RIGHT('0'+CONVERT(varchar(2),@M),2);
 IF @P COLLATE Latin1_General_100_BIN2='Month' AND @D IS NULL RETURN @Text;
 IF @P COLLATE Latin1_General_100_BIN2<>'Day' OR @D IS NULL OR @D<1 OR @D>31 RETURN NULL;
 SET @Text=@Text+'-'+RIGHT('0'+CONVERT(varchar(2),@D),2);
 IF TRY_CONVERT(date,@Text,23) IS NULL RETURN NULL;
 RETURN @Text;
END;
GO
CREATE OR ALTER FUNCTION dbo.PrescriptionDatesValid(@Fields nvarchar(max)) RETURNS bit AS
BEGIN
 IF @Fields IS NULL OR ISJSON(@Fields)<>1 OR LEFT(LTRIM(@Fields),1)<>'{' RETURN 0;
 IF EXISTS(SELECT 1 FROM OPENJSON(@Fields) GROUP BY [key] COLLATE Latin1_General_100_BIN2 HAVING COUNT(*)>1) RETURN 0;
 IF EXISTS(SELECT 1 FROM OPENJSON(@Fields) WHERE [key] COLLATE Latin1_General_100_BIN2 NOT IN('WrittenDate','StartDate') OR type NOT IN(0,5)) RETURN 0;
 DECLARE @Written varchar(10)=dbo.PrescriptionDateText(JSON_QUERY(@Fields,'$.WrittenDate'));
 IF @Written IS NULL OR @Written='' RETURN 0;
 IF JSON_QUERY(@Fields,'$.StartDate') IS NOT NULL AND dbo.PrescriptionDateText(JSON_QUERY(@Fields,'$.StartDate')) IS NULL RETURN 0;
 RETURN 1;
END;
GO
ALTER TABLE dbo.PatientPrescription ALTER COLUMN PrescribedDate date NULL;
ALTER TABLE dbo.PatientPrescription ADD DateFields nvarchar(max) NULL;
GO
ALTER TABLE dbo.PatientPrescription ADD CONSTRAINT CK_PatientPrescription_DatePrecision CHECK
 ((DateFields IS NULL AND PrescribedDate IS NOT NULL) OR
 (DateFields IS NOT NULL AND dbo.PrescriptionDatesValid(DateFields)=1
 AND ISNULL(CONVERT(varchar(10),PrescribedDate,23),'')=CASE WHEN LEN(dbo.PrescriptionDateText(JSON_QUERY(DateFields,'$.WrittenDate')))=10 THEN dbo.PrescriptionDateText(JSON_QUERY(DateFields,'$.WrittenDate')) ELSE '' END
 AND ISNULL(CONVERT(varchar(10),StartDate,23),'')=CASE WHEN LEN(dbo.PrescriptionDateText(JSON_QUERY(DateFields,'$.StartDate')))=10 THEN dbo.PrescriptionDateText(JSON_QUERY(DateFields,'$.StartDate')) ELSE '' END));
GO
CREATE OR ALTER PROCEDURE dbo.PatientPrescription_GetByPatientUid @PatientUid UNIQUEIDENTIFIER AS
BEGIN
 SET NOCOUNT ON;
 SELECT PrescriptionUid,PatientUid,Status,ProductName,ProductIdentifierNamespace,ProductIdentifierValue,ProductDisplayText,
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
 SELECT PrescriptionUid,PatientUid,Status,ProductName,ProductIdentifierNamespace,ProductIdentifierValue,ProductDisplayText,
 StrengthValue,StrengthUnit,DoseAmount,DoseUnit,Route,FrequencyCode,FrequencyDisplay,Prn,Directions,Quantity,QuantityUnit,
 MedicationForm,NotesToPharmacist,AuthorizedRepeats,InitialDaysSupply,RefillQuantity,RefillQuantityUnit,RefillDaysSupply,Indication,PrescribedDate,StartDate,DateFields,CreatedBy,CreatedAtUtc,UpdatedBy,UpdatedAtUtc,PrescriberUserId,
 PrescriberProviderUid,PrescriberDisplayNameSnapshot,PrescriberCredentialSnapshot,ProductDisplaySnapshot,FinalizedBy,
 FinalizedAtUtc,CancelledBy,CancelledAtUtc,CancellationReason,SupersedesPrescriptionUid,SupersededByPrescriptionUid,ArtifactUid,RowVersion
 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientPrescription_CreateDraft
 @PatientUid UNIQUEIDENTIFIER,@ProductName NVARCHAR(200),@ProductIdentifierNamespace NVARCHAR(100)=NULL,@ProductIdentifierValue NVARCHAR(100)=NULL,
 @ProductDisplayText NVARCHAR(300),@StrengthValue DECIMAL(18,6)=NULL,@StrengthUnit NVARCHAR(50)=NULL,@DoseAmount DECIMAL(18,6)=NULL,
 @DoseUnit NVARCHAR(50)=NULL,@Route NVARCHAR(100),@FrequencyCode NVARCHAR(40),@FrequencyDisplay NVARCHAR(100),@Prn BIT,
 @Directions NVARCHAR(1000),@Quantity DECIMAL(18,3),@QuantityUnit NVARCHAR(50),@AuthorizedRepeats INT,@Indication NVARCHAR(500)=NULL,
 @PrescribedDate DATE=NULL,@StartDate DATE=NULL,@ActorUserId BIGINT,@SupersedesPrescriptionUid UNIQUEIDENTIFIER=NULL,
 @InitialDaysSupply INT=NULL,@RefillQuantity DECIMAL(18,3)=NULL,@RefillQuantityUnit NVARCHAR(50)=NULL,@RefillDaysSupply INT=NULL,@MedicationForm NVARCHAR(100)=NULL,@NotesToPharmacist NVARCHAR(1000)=NULL,@DateFields nvarchar(max)=NULL,@PrescribedDateSpecified bit=1,@StartDateSpecified bit=1 AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
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
 BEGIN TRANSACTION;
 INSERT dbo.PatientPrescription(PrescriptionUid,PatientUid,ProductName,ProductIdentifierNamespace,ProductIdentifierValue,ProductDisplayText,StrengthValue,StrengthUnit,DoseAmount,DoseUnit,Route,FrequencyCode,FrequencyDisplay,Prn,Directions,Quantity,QuantityUnit,MedicationForm,NotesToPharmacist,AuthorizedRepeats,InitialDaysSupply,RefillQuantity,RefillQuantityUnit,RefillDaysSupply,Indication,PrescribedDate,StartDate,DateFields,CreatedBy,PrescriberUserId,PrescriberProviderUid,SupersedesPrescriptionUid)
 VALUES(@Uid,@PatientUid,LTRIM(RTRIM(@ProductName)),NULLIF(LTRIM(RTRIM(@ProductIdentifierNamespace)),N''),NULLIF(LTRIM(RTRIM(@ProductIdentifierValue)),N''),LTRIM(RTRIM(@ProductDisplayText)),@StrengthValue,NULLIF(LTRIM(RTRIM(@StrengthUnit)),N''),@DoseAmount,NULLIF(LTRIM(RTRIM(@DoseUnit)),N''),LTRIM(RTRIM(@Route)),@FrequencyCode,@FrequencyDisplay,@Prn,LTRIM(RTRIM(@Directions)),@Quantity,LTRIM(RTRIM(@QuantityUnit)),@MedicationForm,@NotesToPharmacist,@AuthorizedRepeats,@InitialDaysSupply,@RefillQuantity,NULLIF(LTRIM(RTRIM(@RefillQuantityUnit)),N''),@RefillDaysSupply,NULLIF(LTRIM(RTRIM(@Indication)),N''),@PrescribedDate,@StartDate,@DateFields,@ActorUserId,@ActorUserId,@ProviderUid,@SupersedesPrescriptionUid);
 SELECT @NewValue=(SELECT * FROM dbo.PatientPrescription WHERE PrescriptionUid=@Uid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt) VALUES(@ActorUserId,@PatientId,N'PrescriptionDraftCreated',N'PatientPrescription',CONVERT(NVARCHAR(100),@Uid),NULL,@NewValue,SYSUTCDATETIME());
 COMMIT; EXEC dbo.PatientPrescription_GetByUid @PatientUid,@Uid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientPrescription_UpdateDraft
 @PatientUid UNIQUEIDENTIFIER,@PrescriptionUid UNIQUEIDENTIFIER,@ProductName NVARCHAR(200),@ProductIdentifierNamespace NVARCHAR(100)=NULL,@ProductIdentifierValue NVARCHAR(100)=NULL,
 @ProductDisplayText NVARCHAR(300),@StrengthValue DECIMAL(18,6)=NULL,@StrengthUnit NVARCHAR(50)=NULL,@DoseAmount DECIMAL(18,6)=NULL,@DoseUnit NVARCHAR(50)=NULL,
 @Route NVARCHAR(100),@FrequencyCode NVARCHAR(40),@FrequencyDisplay NVARCHAR(100),@Prn BIT,@Directions NVARCHAR(1000),@Quantity DECIMAL(18,3),@QuantityUnit NVARCHAR(50),
 @AuthorizedRepeats INT,@Indication NVARCHAR(500)=NULL,@PrescribedDate DATE=NULL,@StartDate DATE=NULL,@ActorUserId BIGINT,@RowVersion VARBINARY(MAX),
 @InitialDaysSupply INT=NULL,@RefillQuantity DECIMAL(18,3)=NULL,@RefillQuantityUnit NVARCHAR(50)=NULL,@RefillDaysSupply INT=NULL,@MedicationForm NVARCHAR(100)=NULL,@NotesToPharmacist NVARCHAR(1000)=NULL,@DateFields nvarchar(max)=NULL,@PrescribedDateSpecified bit=1,@StartDateSpecified bit=1 AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;DECLARE @PatientId BIGINT,@OldValue nvarchar(max),@NewValue nvarchar(max),@ExistingFields nvarchar(max),@ExistingWritten date,@ExistingStart date;
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
 IF @DateFields IS NULL AND @PrescribedDate IS NULL BEGIN ROLLBACK; THROW 53000,'Written date is required.',1; END;
 UPDATE dbo.PatientPrescription SET MedicationForm=NULLIF(LTRIM(RTRIM(@MedicationForm)),N''),NotesToPharmacist=NULLIF(LTRIM(RTRIM(@NotesToPharmacist)),N''),ProductName=LTRIM(RTRIM(@ProductName)),ProductIdentifierNamespace=NULLIF(LTRIM(RTRIM(@ProductIdentifierNamespace)),N''),ProductIdentifierValue=NULLIF(LTRIM(RTRIM(@ProductIdentifierValue)),N''),ProductDisplayText=LTRIM(RTRIM(@ProductDisplayText)),StrengthValue=@StrengthValue,StrengthUnit=NULLIF(LTRIM(RTRIM(@StrengthUnit)),N''),DoseAmount=@DoseAmount,DoseUnit=NULLIF(LTRIM(RTRIM(@DoseUnit)),N''),Route=LTRIM(RTRIM(@Route)),FrequencyCode=@FrequencyCode,FrequencyDisplay=@FrequencyDisplay,Prn=@Prn,Directions=LTRIM(RTRIM(@Directions)),Quantity=@Quantity,QuantityUnit=LTRIM(RTRIM(@QuantityUnit)),AuthorizedRepeats=@AuthorizedRepeats,InitialDaysSupply=@InitialDaysSupply,RefillQuantity=@RefillQuantity,RefillQuantityUnit=NULLIF(LTRIM(RTRIM(@RefillQuantityUnit)),N''),RefillDaysSupply=@RefillDaysSupply,Indication=NULLIF(LTRIM(RTRIM(@Indication)),N''),PrescribedDate=@PrescribedDate,StartDate=@StartDate,DateFields=@DateFields,UpdatedBy=@ActorUserId,UpdatedAtUtc=SYSUTCDATETIME()
 WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid AND Status=N'Draft' AND PrescriberUserId=@ActorUserId AND RowVersion=@RowVersion;
 IF @@ROWCOUNT=0 BEGIN ROLLBACK;THROW 51504,'Draft is stale, immutable, or belongs to another prescriber.',1;END;
 SELECT @NewValue=(SELECT * FROM dbo.PatientPrescription WHERE PrescriptionUid=@PrescriptionUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt) VALUES(@ActorUserId,@PatientId,N'PrescriptionDraftUpdated',N'PatientPrescription',CONVERT(NVARCHAR(100),@PrescriptionUid),@OldValue,@NewValue,SYSUTCDATETIME());
 COMMIT;EXEC dbo.PatientPrescription_GetByUid @PatientUid,@PrescriptionUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientPrescription_SignAndFinalize @PatientUid UNIQUEIDENTIFIER,@PrescriptionUid UNIQUEIDENTIFIER,@ActorUserId BIGINT,@RowVersion VARBINARY(MAX),@ConfirmSignature BIT AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @RowVersion IS NULL OR DATALENGTH(@RowVersion)<>8 THROW 51504,'A reviewed eight-byte row version is required.',1;
 IF ISNULL(@ConfirmSignature,0)<>1 THROW 51506,'Explicit signature confirmation is required.',1;
 DECLARE @SignedAt DATETIME2(0)=SYSUTCDATETIME();DECLARE @PatientId BIGINT,@ProviderUid UNIQUEIDENTIFIER,@ProviderName NVARCHAR(200),@Credential NVARCHAR(200),@ArtifactUid UNIQUEIDENTIFIER=NEWID(),@Original UNIQUEIDENTIFIER,@Html NVARCHAR(MAX);
 BEGIN TRANSACTION;
 SELECT @ProviderUid=p.ProviderUid,@ProviderName=p.DisplayName,@Credential=CONCAT(p.ProviderType,CASE WHEN p.BillingNumber IS NULL THEN N'' ELSE N' | '+p.BillingNumber END) FROM dbo.ApplicationUser u JOIN dbo.Provider p ON p.ProviderId=u.ProviderId AND p.IsActive=1 WHERE u.UserId=@ActorUserId AND u.IsActive=1;
 SELECT @PatientId=p.PatientId,@Original=x.SupersedesPrescriptionUid FROM dbo.PatientPrescription x WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Patient p ON p.PatientUid=x.PatientUid AND p.IsDeleted=0 WHERE x.PatientUid=@PatientUid AND x.PrescriptionUid=@PrescriptionUid AND x.Status=N'Draft' AND x.PrescriberUserId=@ActorUserId AND x.RowVersion=@RowVersion;
 IF @ProviderUid IS NULL BEGIN ROLLBACK;THROW 51502,'An active mapped provider is required to prescribe.',1;END;
 IF @PatientId IS NULL BEGIN ROLLBACK;THROW 51504,'Draft is stale, immutable, missing, or belongs to another prescriber.',1;END;
 SELECT @Html=(SELECT 2 PrescriptionOutputVersion,1 SignatureVersion,1 RefillInstructionsVersion,
 CONCAT(p.FirstName,N' ',p.LastName) PatientName,p.DateOfBirth,p.HealthCardNumber,p.PhoneNumber PatientPhone,
 p.AddressLine1 PatientAddressLine1,p.AddressLine2 PatientAddressLine2,p.City PatientCity,p.Province PatientProvince,p.PostalCode PatientPostalCode,
 c.LegalName ClinicName,c.Phone ClinicPhone,c.AddressLine1 ClinicAddressLine1,c.AddressLine2 ClinicAddressLine2,c.City ClinicCity,c.ProvinceState ClinicProvince,c.PostalCode ClinicPostalCode,
 COALESCE(dbo.PrescriptionDateText(JSON_QUERY(x.DateFields,'$.WrittenDate')),CONVERT(varchar(10),x.PrescribedDate,23)) PrescribedDate,COALESCE(NULLIF(dbo.PrescriptionDateText(JSON_QUERY(x.DateFields,'$.StartDate')),''),CONVERT(varchar(10),x.StartDate,23)) StartDate,1 DatePrecisionVersion,JSON_QUERY(x.DateFields) DateFields,x.ProductDisplayText,x.MedicationForm,x.NotesToPharmacist,x.StrengthValue,x.StrengthUnit,x.DoseAmount,x.DoseUnit,x.Route,x.FrequencyDisplay,x.Prn,x.Directions,
 x.Quantity,x.QuantityUnit,x.AuthorizedRepeats,x.InitialDaysSupply,x.RefillQuantity,x.RefillQuantityUnit,x.RefillDaysSupply,x.Indication,
 x.CreatedBy,x.CreatedAtUtc,creator.DisplayName CreatedByDisplayName,
 @ProviderName PrescriberDisplayName,@Credential PrescriberCredential,@ActorUserId SignerUserId,@ProviderUid SignerProviderUid,@SignedAt SignedAtUtc,x.PrescriptionUid
 FROM dbo.PatientPrescription x JOIN dbo.Patient p ON p.PatientUid=x.PatientUid
 LEFT JOIN dbo.ApplicationUser creator ON creator.UserId=x.CreatedBy
 LEFT JOIN dbo.ClinicProfile c ON c.ClinicProfileId=1
 WHERE x.PatientUid=@PatientUid AND x.PrescriptionUid=@PrescriptionUid FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
 UPDATE dbo.PatientPrescription SET Status=N'Finalized',PrescriberProviderUid=@ProviderUid,PrescriberDisplayNameSnapshot=@ProviderName,PrescriberCredentialSnapshot=@Credential,ProductDisplaySnapshot=ProductDisplayText,FinalizedBy=@ActorUserId,FinalizedAtUtc=@SignedAt,ArtifactUid=@ArtifactUid WHERE PrescriptionUid=@PrescriptionUid;
 INSERT dbo.PatientPrescriptionArtifact(ArtifactUid,PrescriptionUid,PatientUid,ArtifactJson,CreatedBy) VALUES(@ArtifactUid,@PrescriptionUid,@PatientUid,@Html,@ActorUserId);
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt) VALUES(@ActorUserId,@PatientId,N'PrescriptionFinalized',N'PatientPrescription',CONVERT(NVARCHAR(100),@PrescriptionUid),N'Prescription explicitly electronically signed and finalized',@SignedAt);
 IF @Original IS NOT NULL BEGIN
   UPDATE dbo.PatientPrescription SET Status=N'Superseded',SupersededByPrescriptionUid=@PrescriptionUid WHERE PatientUid=@PatientUid AND PrescriptionUid=@Original AND Status=N'Finalized';
   IF @@ROWCOUNT<>1 BEGIN ROLLBACK;THROW 51505,'The correction source is no longer eligible for supersession.',1;END;
   INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt) VALUES(@ActorUserId,@PatientId,N'PrescriptionSuperseded',N'PatientPrescription',CONVERT(NVARCHAR(100),@Original),N'Superseded by corrected prescription',SYSUTCDATETIME());
 END;
 COMMIT;EXEC dbo.PatientPrescription_GetByUid @PatientUid,@PrescriptionUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientPrescription_Finalize @PatientUid UNIQUEIDENTIFIER,@PrescriptionUid UNIQUEIDENTIFIER,@ActorUserId BIGINT,@RowVersion VARBINARY(MAX) AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;IF @RowVersion IS NULL OR DATALENGTH(@RowVersion)<>8 THROW 51504,'A reviewed eight-byte row version is required.',1; DECLARE @PatientId BIGINT,@ProviderUid UNIQUEIDENTIFIER,@ProviderName NVARCHAR(200),@Credential NVARCHAR(200),@ArtifactUid UNIQUEIDENTIFIER=NEWID(),@Original UNIQUEIDENTIFIER,@Html NVARCHAR(MAX);
 BEGIN TRANSACTION;
 SELECT @ProviderUid=p.ProviderUid,@ProviderName=p.DisplayName,@Credential=CONCAT(p.ProviderType,CASE WHEN p.BillingNumber IS NULL THEN N'' ELSE N' | '+p.BillingNumber END) FROM dbo.ApplicationUser u JOIN dbo.Provider p ON p.ProviderId=u.ProviderId AND p.IsActive=1 WHERE u.UserId=@ActorUserId AND u.IsActive=1;
 SELECT @PatientId=p.PatientId,@Original=x.SupersedesPrescriptionUid FROM dbo.PatientPrescription x WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Patient p ON p.PatientUid=x.PatientUid AND p.IsDeleted=0 WHERE x.PatientUid=@PatientUid AND x.PrescriptionUid=@PrescriptionUid AND x.Status=N'Draft' AND x.PrescriberUserId=@ActorUserId AND x.RowVersion=@RowVersion;
 IF @ProviderUid IS NULL BEGIN ROLLBACK;THROW 51502,'An active mapped provider is required to prescribe.',1;END;
 IF @PatientId IS NULL BEGIN ROLLBACK;THROW 51504,'Draft is stale, immutable, missing, or belongs to another prescriber.',1;END;
 SELECT @Html=(SELECT CONCAT(p.FirstName,N' ',p.LastName) PatientName,p.DateOfBirth,COALESCE(dbo.PrescriptionDateText(JSON_QUERY(x.DateFields,'$.WrittenDate')),CONVERT(varchar(10),x.PrescribedDate,23)) PrescribedDate,COALESCE(NULLIF(dbo.PrescriptionDateText(JSON_QUERY(x.DateFields,'$.StartDate')),''),CONVERT(varchar(10),x.StartDate,23)) StartDate,1 DatePrecisionVersion,JSON_QUERY(x.DateFields) DateFields,x.ProductDisplayText,x.StrengthValue,x.StrengthUnit,x.DoseAmount,x.DoseUnit,x.Route,x.FrequencyDisplay,x.Prn,x.Directions,x.Quantity,x.QuantityUnit,x.AuthorizedRepeats,x.InitialDaysSupply,x.RefillQuantity,x.RefillQuantityUnit,x.RefillDaysSupply,1 RefillInstructionsVersion,x.Indication,@ProviderName PrescriberDisplayName,@Credential PrescriberCredential,x.PrescriptionUid FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) FROM dbo.PatientPrescription x JOIN dbo.Patient p ON p.PatientUid=x.PatientUid WHERE x.PrescriptionUid=@PrescriptionUid;
 UPDATE dbo.PatientPrescription SET Status=N'Finalized',PrescriberProviderUid=@ProviderUid,PrescriberDisplayNameSnapshot=@ProviderName,PrescriberCredentialSnapshot=@Credential,ProductDisplaySnapshot=ProductDisplayText,FinalizedBy=@ActorUserId,FinalizedAtUtc=SYSUTCDATETIME(),ArtifactUid=@ArtifactUid WHERE PrescriptionUid=@PrescriptionUid;
 INSERT dbo.PatientPrescriptionArtifact(ArtifactUid,PrescriptionUid,PatientUid,ArtifactJson,CreatedBy) VALUES(@ArtifactUid,@PrescriptionUid,@PatientUid,@Html,@ActorUserId);
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt) VALUES(@ActorUserId,@PatientId,N'PrescriptionFinalized',N'PatientPrescription',CONVERT(NVARCHAR(100),@PrescriptionUid),N'Prescription finalized',SYSUTCDATETIME());
 IF @Original IS NOT NULL BEGIN
   UPDATE dbo.PatientPrescription SET Status=N'Superseded',SupersededByPrescriptionUid=@PrescriptionUid WHERE PatientUid=@PatientUid AND PrescriptionUid=@Original AND Status=N'Finalized';
   IF @@ROWCOUNT<>1 BEGIN ROLLBACK;THROW 51505,'The correction source is no longer eligible for supersession.',1;END;
   INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt) VALUES(@ActorUserId,@PatientId,N'PrescriptionSuperseded',N'PatientPrescription',CONVERT(NVARCHAR(100),@Original),N'Superseded by corrected prescription',SYSUTCDATETIME());
 END;
 COMMIT;EXEC dbo.PatientPrescription_GetByUid @PatientUid,@PrescriptionUid;
END;
GO
