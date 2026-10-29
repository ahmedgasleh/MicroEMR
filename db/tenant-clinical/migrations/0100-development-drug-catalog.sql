-- Development DPD-format catalogue. No commercial COTS eligibility is asserted.
-- Database-per-tenant: no redundant tenant key in catalogue identities.
CREATE TABLE dbo.DrugCatalogProduct(
 ProductUid uniqueidentifier NOT NULL CONSTRAINT PK_DrugCatalogProduct PRIMARY KEY DEFAULT NEWID(),
 SourceSystem nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
 SourceProductId nvarchar(50) COLLATE Latin1_General_100_BIN2 NOT NULL,
 Facts nvarchar(max) NOT NULL CONSTRAINT CK_DrugCatalogProduct_Json CHECK(ISJSON(Facts)=1),
 ImportedAtUtc datetime2(7) NOT NULL,ImportSha256 varchar(64) NOT NULL,RowVersion rowversion NOT NULL,
 CONSTRAINT UQ_DrugCatalogProduct_Source UNIQUE(SourceSystem,SourceProductId)
);
GO
CREATE OR ALTER PROCEDURE dbo.DrugCatalog_Get @ProductUid uniqueidentifier AS
BEGIN
 SET NOCOUNT ON;
 SELECT ProductUid,Facts,ImportedAtUtc,ImportSha256,RowVersion FROM dbo.DrugCatalogProduct WHERE ProductUid=@ProductUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.DrugCatalog_Search @Query nvarchar(200)=N'',@Offset int=0,@Take int=21 AS
BEGIN
 SET NOCOUNT ON;
 IF @Offset<0 OR @Take<1 OR @Take>51 THROW 53200,'Invalid catalogue page.',1;
 DECLARE @Like nvarchar(1000)=N'%'+REPLACE(REPLACE(REPLACE(REPLACE(@Query,N'\',N'\\'),N'%',N'\%'),N'_',N'\_'),N'[',N'\[')+N'%';
 SELECT ProductUid,Facts,ImportedAtUtc,ImportSha256,RowVersion FROM dbo.DrugCatalogProduct d
 WHERE JSON_VALUE(Facts,'$.Name') COLLATE Latin1_General_100_CI_AS_SC LIKE @Like ESCAPE N'\'
 OR JSON_VALUE(Facts,'$.Din') LIKE @Like ESCAPE N'\'
 OR EXISTS(SELECT 1 FROM OPENJSON(Facts,'$.Ingredients') WITH(Name nvarchar(240) '$.Name') i WHERE i.Name COLLATE Latin1_General_100_CI_AS_SC LIKE @Like ESCAPE N'\')
 ORDER BY JSON_VALUE(Facts,'$.Name'),SourceSystem,SourceProductId OFFSET @Offset ROWS FETCH NEXT @Take ROWS ONLY;
END;
GO
CREATE OR ALTER PROCEDURE dbo.DrugCatalog_Import @Products nvarchar(max),@ImportSha256 varchar(64),@ActorUserId bigint AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@ActorUserId AND IsActive=1) THROW 53200,'Active actor required.',1;
 IF ISJSON(@Products)<>1 OR LEFT(LTRIM(@Products),1)<>N'[' OR LEN(@ImportSha256)<>64 OR @ImportSha256 LIKE '%[^0-9A-F]%' COLLATE Latin1_General_100_BIN2 THROW 53200,'Invalid catalogue batch.',1;
 IF EXISTS(SELECT 1 FROM OPENJSON(@Products) j WHERE j.type<>5 OR
 NULLIF(JSON_VALUE(j.value,'$.SourceSystem'),N'') IS NULL OR DATALENGTH(JSON_VALUE(j.value,'$.SourceSystem'))>400 OR
 NULLIF(JSON_VALUE(j.value,'$.SourceProductId'),N'') IS NULL OR DATALENGTH(JSON_VALUE(j.value,'$.SourceProductId'))>100 OR
 JSON_VALUE(j.value,'$.Din') IS NULL OR DATALENGTH(JSON_VALUE(j.value,'$.Din'))>58 OR
 JSON_VALUE(j.value,'$.Name') IS NULL OR DATALENGTH(JSON_VALUE(j.value,'$.Name'))>400 OR
 JSON_QUERY(j.value,'$.Ingredients') IS NULL OR JSON_QUERY(j.value,'$.Forms') IS NULL OR JSON_QUERY(j.value,'$.Routes') IS NULL)
 THROW 53200,'Invalid catalogue product.',1;
 DECLARE @Input TABLE(SourceSystem nvarchar(200) COLLATE Latin1_General_100_BIN2,SourceProductId nvarchar(50) COLLATE Latin1_General_100_BIN2,Facts nvarchar(max));
 INSERT @Input SELECT JSON_VALUE(value,'$.SourceSystem'),JSON_VALUE(value,'$.SourceProductId'),value FROM OPENJSON(@Products);
 IF NOT EXISTS(SELECT 1 FROM @Input) OR (SELECT COUNT(*) FROM @Input)>100000 OR EXISTS(SELECT 1 FROM @Input GROUP BY SourceSystem,SourceProductId HAVING COUNT(*)<>1) THROW 53200,'Empty, oversized or ambiguous catalogue batch.',1;
 DECLARE @Changed TABLE(ProductUid uniqueidentifier,OldFacts nvarchar(max));
 DECLARE @Inserted int=0,@Updated int=0,@Now datetime2(7)=SYSUTCDATETIME(),@TenantUid uniqueidentifier=(SELECT TOP(1) TenantUid FROM dbo.TenantDatabaseIdentity);
 BEGIN TRY
 BEGIN TRANSACTION;
 UPDATE d WITH(UPDLOCK,HOLDLOCK) SET Facts=i.Facts,ImportedAtUtc=@Now,ImportSha256=@ImportSha256
 OUTPUT inserted.ProductUid,deleted.Facts INTO @Changed
 FROM dbo.DrugCatalogProduct d JOIN @Input i ON i.SourceSystem=d.SourceSystem AND i.SourceProductId=d.SourceProductId
 WHERE DATALENGTH(d.Facts)<>DATALENGTH(i.Facts) OR d.Facts COLLATE Latin1_General_100_BIN2<>i.Facts COLLATE Latin1_General_100_BIN2;
 SET @Updated=@@ROWCOUNT;
 INSERT dbo.DrugCatalogProduct(SourceSystem,SourceProductId,Facts,ImportedAtUtc,ImportSha256)
 OUTPUT inserted.ProductUid,NULL INTO @Changed
 SELECT i.SourceSystem,i.SourceProductId,i.Facts,@Now,@ImportSha256 FROM @Input i
 WHERE NOT EXISTS(SELECT 1 FROM dbo.DrugCatalogProduct d WITH(UPDLOCK,HOLDLOCK) WHERE d.SourceSystem=i.SourceSystem AND d.SourceProductId=i.SourceProductId);
 SET @Inserted=@@ROWCOUNT;
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt,SourceApplication)
 SELECT @ActorUserId,NULL,N'DrugCatalogImported',N'DrugCatalogProduct',CONVERT(nvarchar(100),d.ProductUid),c.OldFacts,
 (SELECT d.*,@TenantUid TenantUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER),@Now,N'MicroEMR.Api'
 FROM @Changed c JOIN dbo.DrugCatalogProduct d ON d.ProductUid=c.ProductUid;
 COMMIT;
 SELECT @Inserted Inserted,@Updated Updated,(SELECT COUNT(*) FROM @Input)-@Inserted-@Updated Unchanged;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK;THROW;END CATCH;
END;
GO
ALTER TABLE dbo.PatientPrescription ADD DrugCatalogSnapshot nvarchar(max) NULL;
GO
ALTER TABLE dbo.PatientPrescription ADD CONSTRAINT CK_PatientPrescription_DrugCatalogSnapshot CHECK(DrugCatalogSnapshot IS NULL OR ISJSON(DrugCatalogSnapshot)=1);
GO
CREATE OR ALTER FUNCTION dbo.DrugCatalogPrescriptionMatches(@Snapshot nvarchar(max),@Name nvarchar(200),@Display nvarchar(max),@System nvarchar(100),@Identifier nvarchar(100),@Form nvarchar(100),@Strength decimal(18,6),@Unit nvarchar(50)) RETURNS bit AS
BEGIN
 IF @Snapshot IS NULL RETURN 0;
 IF JSON_VALUE(@Snapshot,'$.Facts.Name') COLLATE Latin1_General_100_BIN2<>LTRIM(RTRIM(@Name)) COLLATE Latin1_General_100_BIN2 RETURN 0;
 IF JSON_VALUE(@Snapshot,'$.Facts.Name') COLLATE Latin1_General_100_BIN2<>LTRIM(RTRIM(@Display)) COLLATE Latin1_General_100_BIN2 RETURN 0;
 IF ISNULL(JSON_VALUE(@Snapshot,'$.Facts.SourceSystem'),N'') COLLATE Latin1_General_100_BIN2<>ISNULL(LTRIM(RTRIM(@System)),N'') COLLATE Latin1_General_100_BIN2 RETURN 0;
 IF ISNULL(JSON_VALUE(@Snapshot,'$.Facts.Din'),N'') COLLATE Latin1_General_100_BIN2<>ISNULL(LTRIM(RTRIM(@Identifier)),N'') COLLATE Latin1_General_100_BIN2 RETURN 0;
 IF ISNULL(JSON_VALUE(@Snapshot,'$.Facts.MedicationForm'),N'') COLLATE Latin1_General_100_BIN2<>ISNULL(LTRIM(RTRIM(@Form)),N'') COLLATE Latin1_General_100_BIN2 RETURN 0;
 IF ISNULL(TRY_CONVERT(decimal(18,6),JSON_VALUE(@Snapshot,'$.Facts.StrengthValue')),-1)<>ISNULL(@Strength,-1) RETURN 0;
 IF ISNULL(JSON_VALUE(@Snapshot,'$.Facts.StrengthUnit'),N'') COLLATE Latin1_General_100_BIN2<>ISNULL(LTRIM(RTRIM(@Unit)),N'') COLLATE Latin1_General_100_BIN2 RETURN 0;
 RETURN 1;
END;
GO

-- Preserve the existing clinical contracts; add transactional selection validation and snapshot only.
CREATE OR ALTER PROCEDURE dbo.PatientPrescription_CreateDraft
 @PatientUid UNIQUEIDENTIFIER,@ProductName NVARCHAR(200),@ProductIdentifierNamespace NVARCHAR(100)=NULL,@ProductIdentifierValue NVARCHAR(100)=NULL,
 @ProductDisplayText NVARCHAR(MAX),@StrengthValue DECIMAL(18,6)=NULL,@StrengthUnit NVARCHAR(50)=NULL,@DoseAmount DECIMAL(18,6)=NULL,
 @DoseUnit NVARCHAR(50)=NULL,@Route NVARCHAR(100),@FrequencyCode NVARCHAR(40),@FrequencyDisplay NVARCHAR(100),@Prn BIT,
 @Directions NVARCHAR(1000),@Quantity DECIMAL(18,3),@QuantityUnit NVARCHAR(50),@AuthorizedRepeats INT,@Indication NVARCHAR(500)=NULL,
 @PrescribedDate DATE=NULL,@StartDate DATE=NULL,@ActorUserId BIGINT,@SupersedesPrescriptionUid UNIQUEIDENTIFIER=NULL,
 @InitialDaysSupply INT=NULL,@RefillQuantity DECIMAL(18,3)=NULL,@RefillQuantityUnit NVARCHAR(50)=NULL,@RefillDaysSupply INT=NULL,@MedicationForm NVARCHAR(100)=NULL,@NotesToPharmacist NVARCHAR(1000)=NULL,@DateFields nvarchar(max)=NULL,@PrescribedDateSpecified bit=1,@StartDateSpecified bit=1,@GenericName nvarchar(max)=NULL,@GenericNameSpecified bit=0,@PrivateNoteSpecified bit=0,@PrivateNote nvarchar(max)=NULL,@PrivateNoteCorrelationId nvarchar(100)=NULL,@OrderFields nvarchar(max)=NULL,@EarliestPickupSpecified bit=0,@EarliestPickup nvarchar(max)=NULL,@TargetDispensingFacilityNameSpecified bit=0,@TargetDispensingFacilityName nvarchar(max)=NULL,@FacilityNameReviewed bit=0,@TargetDispensingFacilityAddressSpecified bit=0,@TargetDispensingFacilityAddress nvarchar(max)=NULL,@FacilityAddressReviewed bit=0,@ReasonCodingSpecified bit=0,@ReasonCoding nvarchar(max)=NULL,@ReasonCodingReviewed bit=0,@ProtocolReferenceSpecified bit=0,@ProtocolReference nvarchar(max)=NULL,@ProtocolReferenceReviewed bit=0,@DispensingLocationReferenceSpecified bit=0,@DispensingLocationReference nvarchar(max)=NULL,@DispensingLocationReferenceReviewed bit=0,@DispensingLocationDirectoryUid uniqueidentifier=NULL,@DispensingLocationDirectoryRowVersion varbinary(max)=NULL,@DrugCatalogUid uniqueidentifier=NULL,@DrugCatalogRowVersion varbinary(max)=NULL AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF dbo.PrescriptionDispensingLocationReferenceValid(@DispensingLocationReference)<>1 THROW 53028,'Invalid dispensing location reference group.',1;
 DECLARE @DispensingLocationSystem nvarchar(max),@DispensingLocationIdentifier nvarchar(max);
 SELECT @DispensingLocationSystem=dbo.PrescriptionReasonTextNormalize([system]),@DispensingLocationIdentifier=dbo.PrescriptionReasonTextNormalize(identifier)
 FROM OPENJSON(COALESCE(@DispensingLocationReference,N'{}')) WITH([system] nvarchar(max) '$.system',identifier nvarchar(max) '$.identifier');
 IF dbo.PrescriptionProtocolReferenceValid(@ProtocolReference)<>1 THROW 53026,'Invalid protocol reference group.',1;
 DECLARE @ProtocolSystem nvarchar(max),@ProtocolIdentifier nvarchar(max);
 SELECT @ProtocolSystem=dbo.PrescriptionReasonTextNormalize([system]),@ProtocolIdentifier=dbo.PrescriptionReasonTextNormalize(identifier)
 FROM OPENJSON(COALESCE(@ProtocolReference,N'{}')) WITH([system] nvarchar(max) '$.system',identifier nvarchar(max) '$.identifier');
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
 DECLARE @DrugSnapshot nvarchar(max);
 IF (@DrugCatalogUid IS NULL AND @DrugCatalogRowVersion IS NOT NULL) OR (@DrugCatalogUid IS NOT NULL AND (@DrugCatalogRowVersion IS NULL OR DATALENGTH(@DrugCatalogRowVersion)<>8)) THROW 53201,'Incomplete drug catalogue selection.',1;
 IF @DrugCatalogUid IS NOT NULL
 BEGIN
  SELECT @DrugSnapshot=(SELECT c.ProductUid,JSON_QUERY(c.Facts) Facts,c.ImportedAtUtc,c.ImportSha256,c.RowVersion FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)
  FROM dbo.DrugCatalogProduct c WITH(UPDLOCK,HOLDLOCK) WHERE ProductUid=@DrugCatalogUid AND RowVersion=@DrugCatalogRowVersion
  AND JSON_VALUE(Facts,'$.Selectable')=N'true';
  IF @DrugSnapshot IS NULL OR dbo.DrugCatalogPrescriptionMatches(@DrugSnapshot,@ProductName,@ProductDisplayText,@ProductIdentifierNamespace,@ProductIdentifierValue,@MedicationForm,@StrengthValue,@StrengthUnit)<>1 THROW 53201,'Drug catalogue selection is stale, unavailable or mismatched.',1;
 END;
 -- Lock selection until the clinical write and its audit commit; exact identity comparisons use approved BIN2.
 IF (@DispensingLocationDirectoryUid IS NULL AND @DispensingLocationDirectoryRowVersion IS NOT NULL)
 OR (@DispensingLocationDirectoryUid IS NOT NULL AND (@DispensingLocationDirectoryRowVersion IS NULL OR DATALENGTH(@DispensingLocationDirectoryRowVersion)<>8 OR @DispensingLocationReferenceSpecified<>1 OR @DispensingLocationSystem IS NULL))
 THROW 53102,'Incomplete directory selection.',1;
 IF @DispensingLocationDirectoryUid IS NOT NULL AND NOT EXISTS(
 SELECT 1 FROM dbo.DispensingLocation WITH(UPDLOCK,HOLDLOCK) WHERE LocationUid=@DispensingLocationDirectoryUid AND IsActive=1 AND RowVersion=@DispensingLocationDirectoryRowVersion
 AND [System]=@DispensingLocationSystem COLLATE Latin1_General_100_BIN2 AND Identifier=@DispensingLocationIdentifier COLLATE Latin1_General_100_BIN2)
 THROW 53102,'Directory selection is invalid, inactive or stale.',1;

 IF @SupersedesPrescriptionUid IS NOT NULL SET @DispensingLocationDirectoryUid=NULL;
 -- Corrections inherit the exact original fact even if the tenant zone has since changed.
 IF @SupersedesPrescriptionUid IS NOT NULL
 BEGIN
  SELECT @EarliestPickup=EarliestPickup,@TargetDispensingFacilityName=TargetDispensingFacilityName,@TargetDispensingFacilityAddress=TargetDispensingFacilityAddress,@ReasonSystem=ReasonCodeSystem,@ReasonCode=ReasonCode,@ReasonVersion=ReasonCodeVersion,@ReasonDisplay=ReasonCodeDisplay,@ProtocolSystem=ProtocolSystem,@ProtocolIdentifier=ProtocolIdentifier,@DispensingLocationSystem=DispensingLocationSystem,@DispensingLocationIdentifier=DispensingLocationIdentifier FROM dbo.PatientPrescription WITH(UPDLOCK,HOLDLOCK)
  WHERE PatientUid=@PatientUid AND PrescriptionUid=@SupersedesPrescriptionUid AND Status=N'Finalized';
  IF @@ROWCOUNT<>1 THROW 51505,'The correction source is no longer eligible.',1;
 END;
 INSERT dbo.PatientPrescription(DrugCatalogSnapshot,DispensingLocationDirectoryUid,DispensingLocationSystem,DispensingLocationIdentifier,DispensingLocationReferenceReviewed,ProtocolSystem,ProtocolIdentifier,ProtocolReferenceReviewed,ReasonCodeSystem,ReasonCode,ReasonCodeVersion,ReasonCodeDisplay,ReasonCodingReviewed,TargetDispensingFacilityAddress,FacilityAddressReviewed,TargetDispensingFacilityName,FacilityNameReviewed,EarliestPickup,PaperCopyRequiredBeforeDispense,MinimumDispenseIntervalDays,SubstitutionProhibited,TreatmentType,PrescriberFirstNameSnapshot,PrescriberLastNameSnapshot,PrescriberOhipSnapshot,PrescriptionUid,PatientUid,GenericName,ProductName,ProductIdentifierNamespace,ProductIdentifierValue,ProductDisplayText,StrengthValue,StrengthUnit,DoseAmount,DoseUnit,Route,FrequencyCode,FrequencyDisplay,Prn,Directions,Quantity,QuantityUnit,MedicationForm,NotesToPharmacist,AuthorizedRepeats,InitialDaysSupply,RefillQuantity,RefillQuantityUnit,RefillDaysSupply,Indication,PrescribedDate,StartDate,DateFields,CreatedBy,PrescriberUserId,PrescriberProviderUid,SupersedesPrescriptionUid)
 VALUES(@DrugSnapshot,@DispensingLocationDirectoryUid,@DispensingLocationSystem,@DispensingLocationIdentifier,CASE WHEN @SupersedesPrescriptionUid IS NOT NULL AND @DispensingLocationSystem IS NOT NULL THEN 0 END,@ProtocolSystem,@ProtocolIdentifier,CASE WHEN @SupersedesPrescriptionUid IS NOT NULL AND @ProtocolSystem IS NOT NULL THEN 0 END,@ReasonSystem,@ReasonCode,@ReasonVersion,@ReasonDisplay,CASE WHEN @SupersedesPrescriptionUid IS NOT NULL AND @ReasonSystem IS NOT NULL THEN 0 END,@TargetDispensingFacilityAddress,CASE WHEN @SupersedesPrescriptionUid IS NOT NULL AND @TargetDispensingFacilityAddress IS NOT NULL THEN 0 END,@TargetDispensingFacilityName,CASE WHEN @SupersedesPrescriptionUid IS NOT NULL AND @TargetDispensingFacilityName IS NOT NULL THEN 0 END,@EarliestPickup,@PaperCopyRequiredBeforeDispense,@MinimumDispenseIntervalDays,@SubstitutionProhibited,@TreatmentType,@FirstName,@LastName,@Ohip,@Uid,@PatientUid,@GenericName,LTRIM(RTRIM(@ProductName)),NULLIF(LTRIM(RTRIM(@ProductIdentifierNamespace)),N''),NULLIF(LTRIM(RTRIM(@ProductIdentifierValue)),N''),LTRIM(RTRIM(@ProductDisplayText)),@StrengthValue,NULLIF(LTRIM(RTRIM(@StrengthUnit)),N''),@DoseAmount,NULLIF(LTRIM(RTRIM(@DoseUnit)),N''),LTRIM(RTRIM(@Route)),@FrequencyCode,@FrequencyDisplay,@Prn,LTRIM(RTRIM(@Directions)),@Quantity,LTRIM(RTRIM(@QuantityUnit)),@MedicationForm,@NotesToPharmacist,@AuthorizedRepeats,@InitialDaysSupply,@RefillQuantity,NULLIF(LTRIM(RTRIM(@RefillQuantityUnit)),N''),@RefillDaysSupply,NULLIF(LTRIM(RTRIM(@Indication)),N''),@PrescribedDate,@StartDate,@DateFields,@ActorUserId,@ActorUserId,@ProviderUid,@SupersedesPrescriptionUid);
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
 @InitialDaysSupply INT=NULL,@RefillQuantity DECIMAL(18,3)=NULL,@RefillQuantityUnit NVARCHAR(50)=NULL,@RefillDaysSupply INT=NULL,@MedicationForm NVARCHAR(100)=NULL,@NotesToPharmacist NVARCHAR(1000)=NULL,@DateFields nvarchar(max)=NULL,@PrescribedDateSpecified bit=1,@StartDateSpecified bit=1,@GenericName nvarchar(max)=NULL,@GenericNameSpecified bit=0,@PrivateNoteSpecified bit=0,@PrivateNote nvarchar(max)=NULL,@PrivateNoteCorrelationId nvarchar(100)=NULL,@OrderFields nvarchar(max)=NULL,@EarliestPickupSpecified bit=0,@EarliestPickup nvarchar(max)=NULL,@TargetDispensingFacilityNameSpecified bit=0,@TargetDispensingFacilityName nvarchar(max)=NULL,@FacilityNameReviewed bit=0,@TargetDispensingFacilityAddressSpecified bit=0,@TargetDispensingFacilityAddress nvarchar(max)=NULL,@FacilityAddressReviewed bit=0,@ReasonCodingSpecified bit=0,@ReasonCoding nvarchar(max)=NULL,@ReasonCodingReviewed bit=0,@ProtocolReferenceSpecified bit=0,@ProtocolReference nvarchar(max)=NULL,@ProtocolReferenceReviewed bit=0,@DispensingLocationReferenceSpecified bit=0,@DispensingLocationReference nvarchar(max)=NULL,@DispensingLocationReferenceReviewed bit=0,@DispensingLocationDirectoryUid uniqueidentifier=NULL,@DispensingLocationDirectoryRowVersion varbinary(max)=NULL,@DrugCatalogUid uniqueidentifier=NULL,@DrugCatalogRowVersion varbinary(max)=NULL AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF dbo.PrescriptionDispensingLocationReferenceValid(@DispensingLocationReference)<>1 THROW 53028,'Invalid dispensing location reference group.',1;
 DECLARE @DispensingLocationSystem nvarchar(max),@DispensingLocationIdentifier nvarchar(max);
 SELECT @DispensingLocationSystem=dbo.PrescriptionReasonTextNormalize([system]),@DispensingLocationIdentifier=dbo.PrescriptionReasonTextNormalize(identifier)
 FROM OPENJSON(COALESCE(@DispensingLocationReference,N'{}')) WITH([system] nvarchar(max) '$.system',identifier nvarchar(max) '$.identifier');
 IF dbo.PrescriptionProtocolReferenceValid(@ProtocolReference)<>1 THROW 53026,'Invalid protocol reference group.',1;
 DECLARE @ProtocolSystem nvarchar(max),@ProtocolIdentifier nvarchar(max);
 SELECT @ProtocolSystem=dbo.PrescriptionReasonTextNormalize([system]),@ProtocolIdentifier=dbo.PrescriptionReasonTextNormalize(identifier)
 FROM OPENJSON(COALESCE(@ProtocolReference,N'{}')) WITH([system] nvarchar(max) '$.system',identifier nvarchar(max) '$.identifier');
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
 DECLARE @OldProtocolSystem nvarchar(200),@OldProtocolIdentifier nvarchar(200),@OldProtocolReviewed bit; DECLARE @OldDispensingLocationSystem nvarchar(200),@OldDispensingLocationIdentifier nvarchar(200),@OldDispensingLocationReviewed bit; DECLARE @OldReasonSystem nvarchar(200),@OldReasonCode nvarchar(100),@OldReasonVersion nvarchar(100),@OldReasonDisplay nvarchar(500),@OldReasonReviewed bit; DECLARE @OldAddress nvarchar(1000),@OldAddressReviewed bit; DECLARE @OldFacilityName nvarchar(200),@OldFacilityReviewed bit; DECLARE @PatientId BIGINT,@OldValue nvarchar(max),@NewValue nvarchar(max),@ExistingFields nvarchar(max),@ExistingWritten date,@ExistingStart date;
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
 DECLARE @DrugSnapshot nvarchar(max);
 IF (@DrugCatalogUid IS NULL AND @DrugCatalogRowVersion IS NOT NULL) OR (@DrugCatalogUid IS NOT NULL AND (@DrugCatalogRowVersion IS NULL OR DATALENGTH(@DrugCatalogRowVersion)<>8)) THROW 53201,'Incomplete drug catalogue selection.',1;
 IF @DrugCatalogUid IS NOT NULL
 BEGIN
  SELECT @DrugSnapshot=(SELECT c.ProductUid,JSON_QUERY(c.Facts) Facts,c.ImportedAtUtc,c.ImportSha256,c.RowVersion FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)
  FROM dbo.DrugCatalogProduct c WITH(UPDLOCK,HOLDLOCK) WHERE ProductUid=@DrugCatalogUid AND RowVersion=@DrugCatalogRowVersion
  AND JSON_VALUE(Facts,'$.Selectable')=N'true';
  IF @DrugSnapshot IS NULL OR dbo.DrugCatalogPrescriptionMatches(@DrugSnapshot,@ProductName,@ProductDisplayText,@ProductIdentifierNamespace,@ProductIdentifierValue,@MedicationForm,@StrengthValue,@StrengthUnit)<>1 THROW 53201,'Drug catalogue selection is stale, unavailable or mismatched.',1;
 END;
 -- Lock selection until the clinical write and its audit commit; exact identity comparisons use approved BIN2.
 IF (@DispensingLocationDirectoryUid IS NULL AND @DispensingLocationDirectoryRowVersion IS NOT NULL)
 OR (@DispensingLocationDirectoryUid IS NOT NULL AND (@DispensingLocationDirectoryRowVersion IS NULL OR DATALENGTH(@DispensingLocationDirectoryRowVersion)<>8 OR @DispensingLocationReferenceSpecified<>1 OR @DispensingLocationSystem IS NULL))
 THROW 53102,'Incomplete directory selection.',1;
 IF @DispensingLocationDirectoryUid IS NOT NULL AND NOT EXISTS(
 SELECT 1 FROM dbo.DispensingLocation WITH(UPDLOCK,HOLDLOCK) WHERE LocationUid=@DispensingLocationDirectoryUid AND IsActive=1 AND RowVersion=@DispensingLocationDirectoryRowVersion
 AND [System]=@DispensingLocationSystem COLLATE Latin1_General_100_BIN2 AND Identifier=@DispensingLocationIdentifier COLLATE Latin1_General_100_BIN2)
 THROW 53102,'Directory selection is invalid, inactive or stale.',1;

 SELECT @OldDispensingLocationSystem=x.DispensingLocationSystem,@OldDispensingLocationIdentifier=x.DispensingLocationIdentifier,@OldDispensingLocationReviewed=x.DispensingLocationReferenceReviewed,@OldProtocolSystem=x.ProtocolSystem,@OldProtocolIdentifier=x.ProtocolIdentifier,@OldProtocolReviewed=x.ProtocolReferenceReviewed,@OldReasonSystem=x.ReasonCodeSystem,@OldReasonCode=x.ReasonCode,@OldReasonVersion=x.ReasonCodeVersion,@OldReasonDisplay=x.ReasonCodeDisplay,@OldReasonReviewed=x.ReasonCodingReviewed,@OldAddress=x.TargetDispensingFacilityAddress,@OldAddressReviewed=x.FacilityAddressReviewed,@OldFacilityName=x.TargetDispensingFacilityName,@OldFacilityReviewed=x.FacilityNameReviewed,@PatientId=p.PatientId,@OldValue=(SELECT x.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER),@ExistingFields=x.DateFields,@ExistingWritten=x.PrescribedDate,@ExistingStart=x.StartDate FROM dbo.PatientPrescription x WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Patient p ON p.PatientUid=x.PatientUid AND p.IsDeleted=0 WHERE x.PatientUid=@PatientUid AND x.PrescriptionUid=@PrescriptionUid;
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
 DECLARE @DispensingLocationChanged bit=CASE WHEN @DispensingLocationReferenceSpecified=1 AND
 ((ISNULL(DATALENGTH(@DispensingLocationSystem),-1)<>ISNULL(DATALENGTH(@OldDispensingLocationSystem),-1) OR COALESCE(@DispensingLocationSystem,N'') COLLATE Latin1_General_100_BIN2<>COALESCE(@OldDispensingLocationSystem,N'') COLLATE Latin1_General_100_BIN2)
 OR (ISNULL(DATALENGTH(@DispensingLocationIdentifier),-1)<>ISNULL(DATALENGTH(@OldDispensingLocationIdentifier),-1) OR COALESCE(@DispensingLocationIdentifier,N'') COLLATE Latin1_General_100_BIN2<>COALESCE(@OldDispensingLocationIdentifier,N'') COLLATE Latin1_General_100_BIN2)) THEN 1 ELSE 0 END;
 DECLARE @ProtocolChanged bit=CASE WHEN @ProtocolReferenceSpecified=1 AND
 ((ISNULL(DATALENGTH(@ProtocolSystem),-1)<>ISNULL(DATALENGTH(@OldProtocolSystem),-1) OR COALESCE(@ProtocolSystem,N'') COLLATE Latin1_General_100_BIN2<>COALESCE(@OldProtocolSystem,N'') COLLATE Latin1_General_100_BIN2)
 OR (ISNULL(DATALENGTH(@ProtocolIdentifier),-1)<>ISNULL(DATALENGTH(@OldProtocolIdentifier),-1) OR COALESCE(@ProtocolIdentifier,N'') COLLATE Latin1_General_100_BIN2<>COALESCE(@OldProtocolIdentifier,N'') COLLATE Latin1_General_100_BIN2)) THEN 1 ELSE 0 END;
 UPDATE dbo.PatientPrescription SET DrugCatalogSnapshot=CASE WHEN @DrugCatalogUid IS NOT NULL THEN @DrugSnapshot WHEN dbo.DrugCatalogPrescriptionMatches(DrugCatalogSnapshot,@ProductName,@ProductDisplayText,@ProductIdentifierNamespace,@ProductIdentifierValue,@MedicationForm,@StrengthValue,@StrengthUnit)=1 THEN DrugCatalogSnapshot ELSE NULL END,DispensingLocationDirectoryUid=CASE WHEN @DispensingLocationReferenceSpecified=1 THEN @DispensingLocationDirectoryUid ELSE DispensingLocationDirectoryUid END,DispensingLocationSystem=CASE WHEN @DispensingLocationReferenceSpecified=1 THEN @DispensingLocationSystem ELSE DispensingLocationSystem END,
 DispensingLocationIdentifier=CASE WHEN @DispensingLocationReferenceSpecified=1 THEN @DispensingLocationIdentifier ELSE DispensingLocationIdentifier END,
 DispensingLocationReferenceReviewed=CASE WHEN SupersedesPrescriptionUid IS NULL THEN NULL WHEN @DispensingLocationReferenceReviewed=1 THEN 1 WHEN @DispensingLocationChanged=1 THEN 0 ELSE @OldDispensingLocationReviewed END,ProtocolSystem=CASE WHEN @ProtocolReferenceSpecified=1 THEN @ProtocolSystem ELSE ProtocolSystem END,
 ProtocolIdentifier=CASE WHEN @ProtocolReferenceSpecified=1 THEN @ProtocolIdentifier ELSE ProtocolIdentifier END,
 ProtocolReferenceReviewed=CASE WHEN SupersedesPrescriptionUid IS NULL THEN NULL WHEN @ProtocolReferenceReviewed=1 THEN 1 WHEN @ProtocolChanged=1 THEN 0 ELSE @OldProtocolReviewed END,ReasonCodeSystem=CASE WHEN @ReasonCodingSpecified=1 THEN @ReasonSystem ELSE ReasonCodeSystem END,ReasonCode=CASE WHEN @ReasonCodingSpecified=1 THEN @ReasonCode ELSE ReasonCode END,ReasonCodeVersion=CASE WHEN @ReasonCodingSpecified=1 THEN @ReasonVersion ELSE ReasonCodeVersion END,ReasonCodeDisplay=CASE WHEN @ReasonCodingSpecified=1 THEN @ReasonDisplay ELSE ReasonCodeDisplay END,ReasonCodingReviewed=CASE WHEN SupersedesPrescriptionUid IS NULL THEN NULL WHEN @ReasonCodingReviewed=1 THEN 1 WHEN @ReasonChanged=1 THEN 0 ELSE @OldReasonReviewed END,TargetDispensingFacilityAddress=CASE WHEN @TargetDispensingFacilityAddressSpecified=1 THEN @TargetDispensingFacilityAddress ELSE TargetDispensingFacilityAddress END,
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
CREATE OR ALTER PROCEDURE dbo.PatientPrescription_GetByPatientUid @PatientUid UNIQUEIDENTIFIER AS
BEGIN
 SET NOCOUNT ON;
 SELECT DrugCatalogSnapshot,DispensingLocationDirectoryUid,CASE WHEN DispensingLocationSystem IS NULL THEN NULL ELSE (SELECT DispensingLocationSystem [system],DispensingLocationIdentifier identifier FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) END DispensingLocationReference,DispensingLocationReferenceReviewed,CAST(CASE WHEN SupersedesPrescriptionUid IS NOT NULL AND (DispensingLocationSystem IS NOT NULL OR EXISTS(SELECT 1 FROM dbo.PatientPrescription original WHERE original.PrescriptionUid=dbo.PatientPrescription.SupersedesPrescriptionUid AND original.PatientUid=dbo.PatientPrescription.PatientUid AND original.DispensingLocationSystem IS NOT NULL)) THEN 1 ELSE 0 END AS bit) DispensingLocationReferenceReviewRequired,CASE WHEN ProtocolSystem IS NULL THEN NULL ELSE (SELECT ProtocolSystem [system],ProtocolIdentifier identifier FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) END ProtocolReference,ProtocolReferenceReviewed,CAST(CASE WHEN SupersedesPrescriptionUid IS NOT NULL AND (ProtocolSystem IS NOT NULL OR EXISTS(SELECT 1 FROM dbo.PatientPrescription original WHERE original.PrescriptionUid=dbo.PatientPrescription.SupersedesPrescriptionUid AND original.PatientUid=dbo.PatientPrescription.PatientUid AND original.ProtocolSystem IS NOT NULL)) THEN 1 ELSE 0 END AS bit) ProtocolReferenceReviewRequired,CASE WHEN ReasonCodeSystem IS NULL THEN NULL ELSE (SELECT ReasonCodeSystem [system],ReasonCode code,ReasonCodeVersion version,ReasonCodeDisplay display FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END ReasonCoding,ReasonCodingReviewed,CAST(CASE WHEN SupersedesPrescriptionUid IS NOT NULL AND (ReasonCodeSystem IS NOT NULL OR EXISTS(SELECT 1 FROM dbo.PatientPrescription original WHERE original.PrescriptionUid=dbo.PatientPrescription.SupersedesPrescriptionUid AND original.PatientUid=dbo.PatientPrescription.PatientUid AND original.ReasonCodeSystem IS NOT NULL)) THEN 1 ELSE 0 END AS bit) ReasonCodingReviewRequired,TargetDispensingFacilityAddress,FacilityAddressReviewed,CAST(CASE WHEN SupersedesPrescriptionUid IS NOT NULL AND (TargetDispensingFacilityAddress IS NOT NULL OR EXISTS(SELECT 1 FROM dbo.PatientPrescription original WHERE original.PrescriptionUid=dbo.PatientPrescription.SupersedesPrescriptionUid AND original.PatientUid=dbo.PatientPrescription.PatientUid AND original.TargetDispensingFacilityAddress IS NOT NULL)) THEN 1 ELSE 0 END AS bit) FacilityAddressReviewRequired,TargetDispensingFacilityName,FacilityNameReviewed,CAST(CASE WHEN SupersedesPrescriptionUid IS NOT NULL AND (TargetDispensingFacilityName IS NOT NULL OR EXISTS(SELECT 1 FROM dbo.PatientPrescription original WHERE original.PrescriptionUid=dbo.PatientPrescription.SupersedesPrescriptionUid AND original.PatientUid=dbo.PatientPrescription.PatientUid AND original.TargetDispensingFacilityName IS NOT NULL)) THEN 1 ELSE 0 END AS bit) FacilityNameReviewRequired,EarliestPickup,CAST(CASE WHEN SupersedesPrescriptionUid IS NOT NULL AND (EarliestPickup IS NOT NULL OR EXISTS(SELECT 1 FROM dbo.PatientPrescription original WHERE original.PrescriptionUid=dbo.PatientPrescription.SupersedesPrescriptionUid AND original.PatientUid=dbo.PatientPrescription.PatientUid AND original.EarliestPickup IS NOT NULL)) THEN 1 ELSE 0 END AS bit) PickupReviewRequired,PaperCopyRequiredBeforeDispense,MinimumDispenseIntervalDays,SubstitutionProhibited,TreatmentType,PrescriberFirstNameSnapshot,PrescriberLastNameSnapshot,PrescriberOhipSnapshot,PrescriptionUid,PatientUid,Status,ProductName,GenericName,ProductIdentifierNamespace,ProductIdentifierValue,ProductDisplayText,
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
 SELECT DrugCatalogSnapshot,DispensingLocationDirectoryUid,CASE WHEN DispensingLocationSystem IS NULL THEN NULL ELSE (SELECT DispensingLocationSystem [system],DispensingLocationIdentifier identifier FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) END DispensingLocationReference,DispensingLocationReferenceReviewed,CAST(CASE WHEN SupersedesPrescriptionUid IS NOT NULL AND (DispensingLocationSystem IS NOT NULL OR EXISTS(SELECT 1 FROM dbo.PatientPrescription original WHERE original.PrescriptionUid=dbo.PatientPrescription.SupersedesPrescriptionUid AND original.PatientUid=dbo.PatientPrescription.PatientUid AND original.DispensingLocationSystem IS NOT NULL)) THEN 1 ELSE 0 END AS bit) DispensingLocationReferenceReviewRequired,CASE WHEN ProtocolSystem IS NULL THEN NULL ELSE (SELECT ProtocolSystem [system],ProtocolIdentifier identifier FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) END ProtocolReference,ProtocolReferenceReviewed,CAST(CASE WHEN SupersedesPrescriptionUid IS NOT NULL AND (ProtocolSystem IS NOT NULL OR EXISTS(SELECT 1 FROM dbo.PatientPrescription original WHERE original.PrescriptionUid=dbo.PatientPrescription.SupersedesPrescriptionUid AND original.PatientUid=dbo.PatientPrescription.PatientUid AND original.ProtocolSystem IS NOT NULL)) THEN 1 ELSE 0 END AS bit) ProtocolReferenceReviewRequired,CASE WHEN ReasonCodeSystem IS NULL THEN NULL ELSE (SELECT ReasonCodeSystem [system],ReasonCode code,ReasonCodeVersion version,ReasonCodeDisplay display FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END ReasonCoding,ReasonCodingReviewed,CAST(CASE WHEN SupersedesPrescriptionUid IS NOT NULL AND (ReasonCodeSystem IS NOT NULL OR EXISTS(SELECT 1 FROM dbo.PatientPrescription original WHERE original.PrescriptionUid=dbo.PatientPrescription.SupersedesPrescriptionUid AND original.PatientUid=dbo.PatientPrescription.PatientUid AND original.ReasonCodeSystem IS NOT NULL)) THEN 1 ELSE 0 END AS bit) ReasonCodingReviewRequired,TargetDispensingFacilityAddress,FacilityAddressReviewed,CAST(CASE WHEN SupersedesPrescriptionUid IS NOT NULL AND (TargetDispensingFacilityAddress IS NOT NULL OR EXISTS(SELECT 1 FROM dbo.PatientPrescription original WHERE original.PrescriptionUid=dbo.PatientPrescription.SupersedesPrescriptionUid AND original.PatientUid=dbo.PatientPrescription.PatientUid AND original.TargetDispensingFacilityAddress IS NOT NULL)) THEN 1 ELSE 0 END AS bit) FacilityAddressReviewRequired,TargetDispensingFacilityName,FacilityNameReviewed,CAST(CASE WHEN SupersedesPrescriptionUid IS NOT NULL AND (TargetDispensingFacilityName IS NOT NULL OR EXISTS(SELECT 1 FROM dbo.PatientPrescription original WHERE original.PrescriptionUid=dbo.PatientPrescription.SupersedesPrescriptionUid AND original.PatientUid=dbo.PatientPrescription.PatientUid AND original.TargetDispensingFacilityName IS NOT NULL)) THEN 1 ELSE 0 END AS bit) FacilityNameReviewRequired,EarliestPickup,CAST(CASE WHEN SupersedesPrescriptionUid IS NOT NULL AND (EarliestPickup IS NOT NULL OR EXISTS(SELECT 1 FROM dbo.PatientPrescription original WHERE original.PrescriptionUid=dbo.PatientPrescription.SupersedesPrescriptionUid AND original.PatientUid=dbo.PatientPrescription.PatientUid AND original.EarliestPickup IS NOT NULL)) THEN 1 ELSE 0 END AS bit) PickupReviewRequired,PaperCopyRequiredBeforeDispense,MinimumDispenseIntervalDays,SubstitutionProhibited,TreatmentType,PrescriberFirstNameSnapshot,PrescriberLastNameSnapshot,PrescriberOhipSnapshot,PrescriptionUid,PatientUid,Status,ProductName,GenericName,ProductIdentifierNamespace,ProductIdentifierValue,ProductDisplayText,
 StrengthValue,StrengthUnit,DoseAmount,DoseUnit,Route,FrequencyCode,FrequencyDisplay,Prn,Directions,Quantity,QuantityUnit,
 MedicationForm,NotesToPharmacist,AuthorizedRepeats,InitialDaysSupply,RefillQuantity,RefillQuantityUnit,RefillDaysSupply,Indication,PrescribedDate,StartDate,DateFields,CreatedBy,CreatedAtUtc,UpdatedBy,UpdatedAtUtc,PrescriberUserId,
 PrescriberProviderUid,PrescriberDisplayNameSnapshot,PrescriberCredentialSnapshot,ProductDisplaySnapshot,FinalizedBy,
 FinalizedAtUtc,CancelledBy,CancelledAtUtc,CancellationReason,SupersedesPrescriptionUid,SupersededByPrescriptionUid,ArtifactUid,RowVersion
 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid;
END;
GO
