-- Explicit allergy applicability; no legacy reclassification. Existing Medication DIN path remains compatible.
SET XACT_ABORT ON;
GO
ALTER TABLE dbo.PatientAllergy ADD DrugApplicability nvarchar(10) NULL
 CONSTRAINT CK_Allergy_DrugApplicability CHECK(DrugApplicability IS NULL OR DrugApplicability COLLATE Latin1_General_100_BIN2 IN(N'Drug',N'Non-drug',N'Unknown'));
GO
ALTER TABLE dbo.PatientAllergy DROP CONSTRAINT CK_Allergy_DIN;
ALTER TABLE dbo.PatientAllergy ADD CONSTRAINT CK_Allergy_DIN CHECK(DrugDIN IS NULL OR DrugApplicability=N'Drug' OR
 (DrugApplicability IS NULL AND AllergenType IS NOT NULL AND AllergenType COLLATE Latin1_General_100_BIN2=N'Medication'));
GO
CREATE OR ALTER PROCEDURE dbo.PatientAllergy_Create
 @PatientUid UNIQUEIDENTIFIER,@AllergenName NVARCHAR(200),@AllergenType NVARCHAR(100)=NULL,
 @Reaction NVARCHAR(500)=NULL,@Severity NVARCHAR(30)=NULL,@OnsetDate DATE=NULL,@Notes NVARCHAR(1000)=NULL,
 @ClinicalFields NVARCHAR(MAX)=NULL,@OnsetDateSpecified BIT=0,@RequestCorrelationId NVARCHAR(100)=NULL,
 @CreatedBy BIGINT=NULL,@CreatedByDisplayName NVARCHAR(200)=NULL,@ConfirmReplaceNoKnownAllergies BIT=0,
 @ExpectedAssertionUid UNIQUEIDENTIFIER=NULL,@ExpectedAssertionRowVersion BINARY(8)=NULL AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @PatientId BIGINT=(SELECT PatientId FROM dbo.Patient WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND IsDeleted=0);
 IF @PatientId IS NULL THROW 52401,'Patient was not found.',1;
 IF @CreatedBy IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@CreatedBy AND IsActive=1) THROW 52403,'An active clinical actor is required.',1;
 DECLARE @TenantUid UNIQUEIDENTIFIER=(SELECT TenantUid FROM dbo.TenantDatabaseIdentity);
 IF @TenantUid IS NULL THROW 52403,'Trusted tenant identity is required.',1;
 SET @RequestCorrelationId=COALESCE(NULLIF(LTRIM(RTRIM(@RequestCorrelationId)),N''),CONVERT(NVARCHAR(100),NEWID()));
 DECLARE @OldValue NVARCHAR(MAX)=NULL,@NewValue NVARCHAR(MAX);
 DECLARE @Replace BIT=CASE WHEN @ClinicalFields IS NULL THEN 0 ELSE 1 END,@Applicability nvarchar(10),@StoredApplicability nvarchar(10),@ApplicabilitySpecified bit=0,@ApplicabilityRaw nvarchar(max),@DIN NVARCHAR(MAX),@Stage NVARCHAR(MAX),@Y INT,@M INT,@D INT;
 IF NULLIF(LTRIM(RTRIM(@AllergenName)),N'') IS NULL THROW 52400,'Allergen name is required.',1;
 IF @Replace=1 AND (@OnsetDateSpecified=1 OR @OnsetDate IS NOT NULL) THROW 52400,'Supply clinical fields or legacy onset.',1;
 DECLARE @AllergyUid UNIQUEIDENTIFIER=NEWID(),@AssertionUid UNIQUEIDENTIFIER,@AssertionVersion BINARY(8);
 SELECT @AssertionUid=AssertionUid,@AssertionVersion=RowVersion FROM dbo.PatientAllergyAssertion WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND AssertionType=N'NoKnownAllergies' AND Status=N'Active';
 IF @ExpectedAssertionUid IS NOT NULL AND (@AssertionUid IS NULL OR @AssertionUid<>@ExpectedAssertionUid) THROW 52409,'The NKA assertion changed. Reload and review.',1;
 IF @AssertionUid IS NOT NULL
 BEGIN
  IF @ConfirmReplaceNoKnownAllergies<>1 THROW 52408,'Confirm replacement of No Known Allergies before adding an allergy.',1;
  IF @ExpectedAssertionUid IS NULL OR @ExpectedAssertionRowVersion IS NULL OR @AssertionVersion<>@ExpectedAssertionRowVersion THROW 52409,'The reviewed NKA assertion token is required and must be current.',1;
 SET @OldValue=(SELECT @TenantUid AS TenantUid,@RequestCorrelationId AS RequestCorrelationId,x.* FROM dbo.PatientAllergyAssertion x WHERE x.PatientUid=@PatientUid AND x.AssertionUid=@AssertionUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 UPDATE dbo.PatientAllergyAssertion SET Status=N'Revoked',RevokedBy=@CreatedBy,RevokedAtUtc=SYSUTCDATETIME(),RevocationReason=N'Replaced by active allergy'
 WHERE PatientUid=@PatientUid AND AssertionUid=@AssertionUid AND RowVersion=@ExpectedAssertionRowVersion;
 IF @@ROWCOUNT<>1 THROW 52409,'The NKA assertion changed.',1;
 SET @NewValue=(SELECT @TenantUid AS TenantUid,@RequestCorrelationId AS RequestCorrelationId,x.* FROM dbo.PatientAllergyAssertion x WHERE x.PatientUid=@PatientUid AND x.AssertionUid=@AssertionUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,PatientUid,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt,
 AuditEventUid,EventCategory,ResourceType,ResourceUid,Outcome,RequestCorrelationId,SourceApplication)
 VALUES(@CreatedBy,@PatientId,@PatientUid,N'NoKnownAllergiesRevoked',N'PatientAllergyAssertion',CONVERT(NVARCHAR(100),@AssertionUid),@OldValue,@NewValue,SYSUTCDATETIME(),
 NEWID(),N'ClinicalMutation',N'PatientAllergyAssertion',@AssertionUid,N'Succeeded',@RequestCorrelationId,N'MicroEMR.Api');
 SET @OldValue=NULL; SET @NewValue=NULL;
 END;
 IF @ClinicalFields IS NOT NULL AND (ISJSON(@ClinicalFields)<>1 OR LEFT(LTRIM(REPLACE(REPLACE(REPLACE(@ClinicalFields,NCHAR(9),N' '),NCHAR(10),N' '),NCHAR(13),N' ')),1)<>N'{') THROW 52400,'Clinical fields must be an object.',1;
 IF EXISTS(SELECT 1 FROM OPENJSON(COALESCE(@ClinicalFields,N'{}')) WHERE [key]=N'DrugApplicability' AND [type] NOT IN(0,1)) THROW 52400,'Drug applicability must be text or null.',1;
 SET @ApplicabilitySpecified=CASE WHEN EXISTS(SELECT 1 FROM OPENJSON(COALESCE(@ClinicalFields,N'{}')) WHERE [key]=N'DrugApplicability') THEN 1 ELSE 0 END;
 SET @ApplicabilityRaw=JSON_VALUE(@ClinicalFields,'$.DrugApplicability');
 IF @ApplicabilityRaw IS NOT NULL AND (DATALENGTH(@ApplicabilityRaw)>20 OR @ApplicabilityRaw COLLATE Latin1_General_100_BIN2 NOT IN(N'Drug',N'Non-drug',N'Unknown')) THROW 52400,'Invalid drug applicability.',1;
 SET @Applicability=CASE WHEN @ApplicabilitySpecified=1 THEN @ApplicabilityRaw ELSE @StoredApplicability END;
 SELECT @DIN=DrugDIN,@Stage=Stage,@Y=Y,@M=M,@D=D FROM OPENJSON(COALESCE(@ClinicalFields,N'{}')) WITH
 (DrugDIN NVARCHAR(MAX) '$.DrugDIN',Stage NVARCHAR(MAX) '$.LifeStageCode',Y INT '$.OnsetDate.Year',M INT '$.OnsetDate.Month',D INT '$.OnsetDate.Day');
 SET @DIN=NULLIF(LTRIM(RTRIM(@DIN)),N''); SET @Stage=NULLIF(LTRIM(RTRIM(@Stage)),N'');
 IF DATALENGTH(@DIN)>200 THROW 52400,'DIN exceeds 100 characters.',1;
 IF @DIN IS NOT NULL AND NOT (ISNULL(@Applicability,N'')=N'Drug' OR (@Applicability IS NULL AND @AllergenType IS NOT NULL AND @AllergenType COLLATE Latin1_General_100_BIN2=N'Medication')) THROW 52400,'DIN entry requires explicit Medication classification. Reconcile any existing DIN deliberately.',1;
 IF @Stage IS NOT NULL AND (@Stage COLLATE Latin1_General_100_BIN2 NOT IN(N'N',N'I',N'C',N'T',N'A') OR DATALENGTH(@Stage)<>2) THROW 52400,'Invalid CT-016 stage.',1;
 IF dbo.HistoryPartialDateValid(@Y,@M,@D)=0 THROW 52400,'Invalid onset date precision.',1;
 IF @Replace=1 SET @OnsetDate=CASE WHEN @D IS NOT NULL THEN DATEFROMPARTS(@Y,@M,@D) END;
 INSERT dbo.PatientAllergy(AllergyUid,PatientUid,AllergenName,AllergenType,Reaction,Severity,OnsetDate,Notes,AllergyStatus,CreatedBy,CreatedByDisplayName,CreatedAt,DrugApplicability,DrugDIN,LifeStageCode,OnsetYear,OnsetMonth,OnsetDay)
 SELECT @AllergyUid,@PatientUid,LTRIM(RTRIM(@AllergenName)),NULLIF(LTRIM(RTRIM(@AllergenType)),N''),NULLIF(LTRIM(RTRIM(@Reaction)),N''),NULLIF(LTRIM(RTRIM(@Severity)),N''),@OnsetDate,NULLIF(LTRIM(RTRIM(@Notes)),N''),N'Active',@CreatedBy,DisplayName,SYSUTCDATETIME(),@Applicability,@DIN,@Stage,@Y,@M,@D FROM dbo.ApplicationUser WHERE UserId=@CreatedBy;
 SET @NewValue=(SELECT @TenantUid AS TenantUid,@RequestCorrelationId AS RequestCorrelationId,x.* FROM dbo.PatientAllergy x WHERE x.PatientUid=@PatientUid AND x.AllergyUid=@AllergyUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,PatientUid,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt,
 AuditEventUid,EventCategory,ResourceType,ResourceUid,Outcome,RequestCorrelationId,SourceApplication)
 VALUES(@CreatedBy,@PatientId,@PatientUid,N'Create',N'PatientAllergy',CONVERT(NVARCHAR(100),@AllergyUid),@OldValue,@NewValue,SYSUTCDATETIME(),
 NEWID(),N'ClinicalMutation',N'PatientAllergy',@AllergyUid,N'Succeeded',@RequestCorrelationId,N'MicroEMR.Api');
 COMMIT;
 EXEC dbo.PatientAllergy_GetByUid @AllergyUid,@PatientUid;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientAllergy_Update
 @PatientUid UNIQUEIDENTIFIER,@AllergenName NVARCHAR(200),@AllergenType NVARCHAR(100)=NULL,
 @Reaction NVARCHAR(500)=NULL,@Severity NVARCHAR(30)=NULL,@OnsetDate DATE=NULL,@Notes NVARCHAR(1000)=NULL,
 @ClinicalFields NVARCHAR(MAX)=NULL,@OnsetDateSpecified BIT=0,@RequestCorrelationId NVARCHAR(100)=NULL,
 @AllergyUid UNIQUEIDENTIFIER,@AllergyStatus NVARCHAR(30),@UpdatedBy BIGINT=NULL,@RowVersion BINARY(8)=NULL AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @PatientId BIGINT=(SELECT PatientId FROM dbo.Patient WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND IsDeleted=0);
 IF @PatientId IS NULL THROW 52401,'Patient was not found.',1;
 IF @UpdatedBy IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@UpdatedBy AND IsActive=1) THROW 52403,'An active clinical actor is required.',1;
 DECLARE @TenantUid UNIQUEIDENTIFIER=(SELECT TenantUid FROM dbo.TenantDatabaseIdentity);
 IF @TenantUid IS NULL THROW 52403,'Trusted tenant identity is required.',1;
 SET @RequestCorrelationId=COALESCE(NULLIF(LTRIM(RTRIM(@RequestCorrelationId)),N''),CONVERT(NVARCHAR(100),NEWID()));
 DECLARE @OldValue NVARCHAR(MAX)=NULL,@NewValue NVARCHAR(MAX);
 DECLARE @Replace BIT=CASE WHEN @ClinicalFields IS NULL THEN 0 ELSE 1 END,@Applicability nvarchar(10),@StoredApplicability nvarchar(10),@ApplicabilitySpecified bit=0,@ApplicabilityRaw nvarchar(max),@DIN NVARCHAR(MAX),@Stage NVARCHAR(MAX),@Y INT,@M INT,@D INT;
 IF NULLIF(LTRIM(RTRIM(@AllergenName)),N'') IS NULL THROW 52400,'Allergen name is required.',1;
 IF @Replace=1 AND (@OnsetDateSpecified=1 OR @OnsetDate IS NOT NULL) THROW 52400,'Supply clinical fields or legacy onset.',1;
 IF @RowVersion IS NULL THROW 52400,'A current allergy RowVersion is required.',1;
 DECLARE @Current BINARY(8),@StoredDate DATE,@StoredY INT,@StoredD INT;
 SELECT @StoredApplicability=pa.DrugApplicability,@Current=pa.RowVersion,@StoredDate=pa.OnsetDate,@StoredY=pa.OnsetYear,@StoredD=pa.OnsetDay,
 @ClinicalFields=CASE WHEN @Replace=1 THEN @ClinicalFields ELSE (SELECT pa.DrugDIN,pa.LifeStageCode,pa.OnsetYear AS [OnsetDate.Year],pa.OnsetMonth AS [OnsetDate.Month],pa.OnsetDay AS [OnsetDate.Day] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END
 FROM dbo.PatientAllergy pa WITH(UPDLOCK,HOLDLOCK) WHERE pa.PatientUid=@PatientUid AND pa.AllergyUid=@AllergyUid;
 IF @Current IS NULL THROW 52401,'Allergy was not found in the patient context.',1;
 IF @Current<>@RowVersion THROW 52409,'Allergy changed. Reload and review.',1;
 IF @AllergyStatus IS NULL OR @AllergyStatus NOT IN(N'Active',N'Inactive',N'Resolved') THROW 52400,'Select a supported allergy status.',1;
 IF @AllergyStatus=N'Active' AND EXISTS(SELECT 1 FROM dbo.PatientAllergyAssertion WHERE PatientUid=@PatientUid AND Status=N'Active' AND AssertionType=N'NoKnownAllergies') THROW 52408,'Use the confirmed add workflow to replace NKA.',1;
 SET @OldValue=(SELECT @TenantUid AS TenantUid,@RequestCorrelationId AS RequestCorrelationId,x.* FROM dbo.PatientAllergy x WHERE x.PatientUid=@PatientUid AND x.AllergyUid=@AllergyUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 IF @ClinicalFields IS NOT NULL AND (ISJSON(@ClinicalFields)<>1 OR LEFT(LTRIM(REPLACE(REPLACE(REPLACE(@ClinicalFields,NCHAR(9),N' '),NCHAR(10),N' '),NCHAR(13),N' ')),1)<>N'{') THROW 52400,'Clinical fields must be an object.',1;
 IF EXISTS(SELECT 1 FROM OPENJSON(COALESCE(@ClinicalFields,N'{}')) WHERE [key]=N'DrugApplicability' AND [type] NOT IN(0,1)) THROW 52400,'Drug applicability must be text or null.',1;
 SET @ApplicabilitySpecified=CASE WHEN EXISTS(SELECT 1 FROM OPENJSON(COALESCE(@ClinicalFields,N'{}')) WHERE [key]=N'DrugApplicability') THEN 1 ELSE 0 END;
 SET @ApplicabilityRaw=JSON_VALUE(@ClinicalFields,'$.DrugApplicability');
 IF @ApplicabilityRaw IS NOT NULL AND (DATALENGTH(@ApplicabilityRaw)>20 OR @ApplicabilityRaw COLLATE Latin1_General_100_BIN2 NOT IN(N'Drug',N'Non-drug',N'Unknown')) THROW 52400,'Invalid drug applicability.',1;
 SET @Applicability=CASE WHEN @ApplicabilitySpecified=1 THEN @ApplicabilityRaw ELSE @StoredApplicability END;
 SELECT @DIN=DrugDIN,@Stage=Stage,@Y=Y,@M=M,@D=D FROM OPENJSON(COALESCE(@ClinicalFields,N'{}')) WITH
 (DrugDIN NVARCHAR(MAX) '$.DrugDIN',Stage NVARCHAR(MAX) '$.LifeStageCode',Y INT '$.OnsetDate.Year',M INT '$.OnsetDate.Month',D INT '$.OnsetDate.Day');
 SET @DIN=NULLIF(LTRIM(RTRIM(@DIN)),N''); SET @Stage=NULLIF(LTRIM(RTRIM(@Stage)),N'');
 IF DATALENGTH(@DIN)>200 THROW 52400,'DIN exceeds 100 characters.',1;
 IF @DIN IS NOT NULL AND NOT (ISNULL(@Applicability,N'')=N'Drug' OR (@Applicability IS NULL AND @AllergenType IS NOT NULL AND @AllergenType COLLATE Latin1_General_100_BIN2=N'Medication')) THROW 52400,'DIN entry requires explicit Medication classification. Reconcile any existing DIN deliberately.',1;
 IF @Stage IS NOT NULL AND (@Stage COLLATE Latin1_General_100_BIN2 NOT IN(N'N',N'I',N'C',N'T',N'A') OR DATALENGTH(@Stage)<>2) THROW 52400,'Invalid CT-016 stage.',1;
 IF dbo.HistoryPartialDateValid(@Y,@M,@D)=0 THROW 52400,'Invalid onset date precision.',1;
 IF @Replace=1 SET @OnsetDate=CASE WHEN @D IS NOT NULL THEN DATEFROMPARTS(@Y,@M,@D) END;
 ELSE IF @OnsetDateSpecified=1 OR @OnsetDate IS NOT NULL
 BEGIN
  IF @OnsetDate IS NULL AND @StoredY IS NOT NULL AND @StoredD IS NULL THROW 52400,'Use the updated editor to clear a partial onset.',1;
  SET @Y=NULL;SET @M=NULL;SET @D=NULL;
 END
 ELSE SET @OnsetDate=@StoredDate;
 UPDATE dbo.PatientAllergy SET AllergenName=LTRIM(RTRIM(@AllergenName)),AllergenType=NULLIF(LTRIM(RTRIM(@AllergenType)),N''),Reaction=NULLIF(LTRIM(RTRIM(@Reaction)),N''),Severity=NULLIF(LTRIM(RTRIM(@Severity)),N''),OnsetDate=@OnsetDate,Notes=NULLIF(LTRIM(RTRIM(@Notes)),N''),AllergyStatus=@AllergyStatus,
 UpdatedBy=@UpdatedBy,UpdatedAt=SYSUTCDATETIME(),DrugApplicability=@Applicability,DrugDIN=@DIN,LifeStageCode=@Stage,OnsetYear=@Y,OnsetMonth=@M,OnsetDay=@D
 WHERE PatientUid=@PatientUid AND AllergyUid=@AllergyUid AND RowVersion=@RowVersion;
 IF @@ROWCOUNT<>1 THROW 52409,'Allergy changed.',1;
 SET @NewValue=(SELECT @TenantUid AS TenantUid,@RequestCorrelationId AS RequestCorrelationId,x.* FROM dbo.PatientAllergy x WHERE x.PatientUid=@PatientUid AND x.AllergyUid=@AllergyUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,PatientUid,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt,
 AuditEventUid,EventCategory,ResourceType,ResourceUid,Outcome,RequestCorrelationId,SourceApplication)
 VALUES(@UpdatedBy,@PatientId,@PatientUid,N'Update',N'PatientAllergy',CONVERT(NVARCHAR(100),@AllergyUid),@OldValue,@NewValue,SYSUTCDATETIME(),
 NEWID(),N'ClinicalMutation',N'PatientAllergy',@AllergyUid,N'Succeeded',@RequestCorrelationId,N'MicroEMR.Api');
 COMMIT;
 EXEC dbo.PatientAllergy_GetByUid @AllergyUid,@PatientUid;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
