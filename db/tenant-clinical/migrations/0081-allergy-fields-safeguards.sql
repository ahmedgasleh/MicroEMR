-- Additive Allergy fields and required positive/NKA safeguards. No clinical backfill.
SET XACT_ABORT ON;
GO
ALTER TABLE dbo.PatientAllergy ADD DrugDIN NVARCHAR(100) NULL,LifeStageCode NVARCHAR(1) NULL,
 OnsetYear SMALLINT NULL,OnsetMonth TINYINT NULL,OnsetDay TINYINT NULL;
GO
ALTER TABLE dbo.PatientAllergy ADD
 CONSTRAINT CK_Allergy_Parts CHECK(dbo.HistoryPartialDateValid(OnsetYear,OnsetMonth,OnsetDay)=1),
 CONSTRAINT CK_Allergy_Stage CHECK(LifeStageCode IS NULL OR LifeStageCode COLLATE Latin1_General_100_BIN2 IN(N'N',N'I',N'C',N'T',N'A')),
 CONSTRAINT CK_Allergy_DIN CHECK(DrugDIN IS NULL OR (AllergenType IS NOT NULL AND AllergenType COLLATE Latin1_General_100_BIN2=N'Medication')),
 CONSTRAINT CK_Allergy_OnsetCompatibility CHECK(OnsetYear IS NULL OR (OnsetDay IS NULL AND OnsetDate IS NULL) OR
 (OnsetDay IS NOT NULL AND OnsetDate IS NOT NULL AND YEAR(OnsetDate)=OnsetYear AND MONTH(OnsetDate)=OnsetMonth AND DAY(OnsetDate)=OnsetDay));
GO
CREATE OR ALTER PROCEDURE dbo.PatientAllergy_GetByPatientUid @PatientUid UNIQUEIDENTIFIER AS
BEGIN
 SET NOCOUNT ON;
 SELECT pa.*,COALESCE(pa.CreatedByDisplayName,au.DisplayName) AS ActorDisplayName
 FROM dbo.PatientAllergy pa JOIN dbo.Patient p ON p.PatientUid=pa.PatientUid AND p.IsDeleted=0
 LEFT JOIN dbo.ApplicationUser au ON au.UserId=pa.CreatedBy
 WHERE pa.PatientUid=@PatientUid
 ORDER BY CASE WHEN pa.AllergyStatus=N'Active' THEN 0 ELSE 1 END,pa.AllergenName,pa.CreatedAt DESC;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientAllergy_GetByUid @AllergyUid UNIQUEIDENTIFIER,@PatientUid UNIQUEIDENTIFIER=NULL AS
BEGIN
 SET NOCOUNT ON;
 SELECT pa.*,COALESCE(pa.CreatedByDisplayName,au.DisplayName) AS ActorDisplayName
 FROM dbo.PatientAllergy pa JOIN dbo.Patient p ON p.PatientUid=pa.PatientUid AND p.IsDeleted=0
 LEFT JOIN dbo.ApplicationUser au ON au.UserId=pa.CreatedBy
 WHERE pa.AllergyUid=@AllergyUid AND (@PatientUid IS NULL OR pa.PatientUid=@PatientUid);
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientAllergy_GetDocumentationState @PatientUid UNIQUEIDENTIFIER AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @PatientId BIGINT=(SELECT PatientId FROM dbo.Patient WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND IsDeleted=0);
 IF @PatientId IS NULL THROW 52401,'Patient was not found.',1;
 SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.PatientAllergy WHERE PatientUid=@PatientUid AND AllergyStatus=N'Active') THEN N'HasEntries'
 WHEN a.AssertionUid IS NOT NULL THEN N'ExplicitlyNone' ELSE N'NotDocumented' END DocumentationState,
 a.*,u.DisplayName AS VerifiedByDisplayName
 FROM (VALUES(1)) seed(n) OUTER APPLY(SELECT TOP(1) x.* FROM dbo.PatientAllergyAssertion x WHERE x.PatientUid=@PatientUid AND x.Status=N'Active' AND x.AssertionType=N'NoKnownAllergies') a
 LEFT JOIN dbo.ApplicationUser u ON u.UserId=a.VerifiedBy;
 COMMIT;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientAllergy_GetSnapshot @PatientUid UNIQUEIDENTIFIER AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @PatientId BIGINT=(SELECT PatientId FROM dbo.Patient WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND IsDeleted=0);
 IF @PatientId IS NULL THROW 52401,'Patient was not found.',1;
 EXEC dbo.PatientAllergy_GetByPatientUid @PatientUid;
 SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.PatientAllergy WHERE PatientUid=@PatientUid AND AllergyStatus=N'Active') THEN N'HasEntries'
 WHEN a.AssertionUid IS NOT NULL THEN N'ExplicitlyNone' ELSE N'NotDocumented' END DocumentationState,
 a.*,u.DisplayName AS VerifiedByDisplayName
 FROM (VALUES(1)) seed(n) OUTER APPLY(SELECT TOP(1) x.* FROM dbo.PatientAllergyAssertion x WHERE x.PatientUid=@PatientUid AND x.Status=N'Active' AND x.AssertionType=N'NoKnownAllergies') a
 LEFT JOIN dbo.ApplicationUser u ON u.UserId=a.VerifiedBy;
 COMMIT;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
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
 DECLARE @Replace BIT=CASE WHEN @ClinicalFields IS NULL THEN 0 ELSE 1 END,@DIN NVARCHAR(MAX),@Stage NVARCHAR(MAX),@Y INT,@M INT,@D INT;
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
 SELECT @DIN=DrugDIN,@Stage=Stage,@Y=Y,@M=M,@D=D FROM OPENJSON(COALESCE(@ClinicalFields,N'{}')) WITH
 (DrugDIN NVARCHAR(MAX) '$.DrugDIN',Stage NVARCHAR(MAX) '$.LifeStageCode',Y INT '$.OnsetDate.Year',M INT '$.OnsetDate.Month',D INT '$.OnsetDate.Day');
 SET @DIN=NULLIF(LTRIM(RTRIM(@DIN)),N''); SET @Stage=NULLIF(LTRIM(RTRIM(@Stage)),N'');
 IF DATALENGTH(@DIN)>200 THROW 52400,'DIN exceeds 100 characters.',1;
 IF @DIN IS NOT NULL AND (@AllergenType IS NULL OR @AllergenType COLLATE Latin1_General_100_BIN2<>N'Medication') THROW 52400,'DIN entry requires explicit Medication classification. Reconcile any existing DIN deliberately.',1;
 IF @Stage IS NOT NULL AND (@Stage COLLATE Latin1_General_100_BIN2 NOT IN(N'N',N'I',N'C',N'T',N'A') OR DATALENGTH(@Stage)<>2) THROW 52400,'Invalid CT-016 stage.',1;
 IF dbo.HistoryPartialDateValid(@Y,@M,@D)=0 THROW 52400,'Invalid onset date precision.',1;
 IF @Replace=1 SET @OnsetDate=CASE WHEN @D IS NOT NULL THEN DATEFROMPARTS(@Y,@M,@D) END;
 INSERT dbo.PatientAllergy(AllergyUid,PatientUid,AllergenName,AllergenType,Reaction,Severity,OnsetDate,Notes,AllergyStatus,CreatedBy,CreatedByDisplayName,CreatedAt,DrugDIN,LifeStageCode,OnsetYear,OnsetMonth,OnsetDay)
 SELECT @AllergyUid,@PatientUid,LTRIM(RTRIM(@AllergenName)),NULLIF(LTRIM(RTRIM(@AllergenType)),N''),NULLIF(LTRIM(RTRIM(@Reaction)),N''),NULLIF(LTRIM(RTRIM(@Severity)),N''),@OnsetDate,NULLIF(LTRIM(RTRIM(@Notes)),N''),N'Active',@CreatedBy,DisplayName,SYSUTCDATETIME(),@DIN,@Stage,@Y,@M,@D FROM dbo.ApplicationUser WHERE UserId=@CreatedBy;
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
 DECLARE @Replace BIT=CASE WHEN @ClinicalFields IS NULL THEN 0 ELSE 1 END,@DIN NVARCHAR(MAX),@Stage NVARCHAR(MAX),@Y INT,@M INT,@D INT;
 IF NULLIF(LTRIM(RTRIM(@AllergenName)),N'') IS NULL THROW 52400,'Allergen name is required.',1;
 IF @Replace=1 AND (@OnsetDateSpecified=1 OR @OnsetDate IS NOT NULL) THROW 52400,'Supply clinical fields or legacy onset.',1;
 IF @RowVersion IS NULL THROW 52400,'A current allergy RowVersion is required.',1;
 DECLARE @Current BINARY(8),@StoredDate DATE,@StoredY INT,@StoredD INT;
 SELECT @Current=pa.RowVersion,@StoredDate=pa.OnsetDate,@StoredY=pa.OnsetYear,@StoredD=pa.OnsetDay,
 @ClinicalFields=CASE WHEN @Replace=1 THEN @ClinicalFields ELSE (SELECT pa.DrugDIN,pa.LifeStageCode,pa.OnsetYear AS [OnsetDate.Year],pa.OnsetMonth AS [OnsetDate.Month],pa.OnsetDay AS [OnsetDate.Day] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END
 FROM dbo.PatientAllergy pa WITH(UPDLOCK,HOLDLOCK) WHERE pa.PatientUid=@PatientUid AND pa.AllergyUid=@AllergyUid;
 IF @Current IS NULL THROW 52401,'Allergy was not found in the patient context.',1;
 IF @Current<>@RowVersion THROW 52409,'Allergy changed. Reload and review.',1;
 IF @AllergyStatus IS NULL OR @AllergyStatus NOT IN(N'Active',N'Inactive',N'Resolved') THROW 52400,'Select a supported allergy status.',1;
 IF @AllergyStatus=N'Active' AND EXISTS(SELECT 1 FROM dbo.PatientAllergyAssertion WHERE PatientUid=@PatientUid AND Status=N'Active' AND AssertionType=N'NoKnownAllergies') THROW 52408,'Use the confirmed add workflow to replace NKA.',1;
 SET @OldValue=(SELECT @TenantUid AS TenantUid,@RequestCorrelationId AS RequestCorrelationId,x.* FROM dbo.PatientAllergy x WHERE x.PatientUid=@PatientUid AND x.AllergyUid=@AllergyUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 IF @ClinicalFields IS NOT NULL AND (ISJSON(@ClinicalFields)<>1 OR LEFT(LTRIM(REPLACE(REPLACE(REPLACE(@ClinicalFields,NCHAR(9),N' '),NCHAR(10),N' '),NCHAR(13),N' ')),1)<>N'{') THROW 52400,'Clinical fields must be an object.',1;
 SELECT @DIN=DrugDIN,@Stage=Stage,@Y=Y,@M=M,@D=D FROM OPENJSON(COALESCE(@ClinicalFields,N'{}')) WITH
 (DrugDIN NVARCHAR(MAX) '$.DrugDIN',Stage NVARCHAR(MAX) '$.LifeStageCode',Y INT '$.OnsetDate.Year',M INT '$.OnsetDate.Month',D INT '$.OnsetDate.Day');
 SET @DIN=NULLIF(LTRIM(RTRIM(@DIN)),N''); SET @Stage=NULLIF(LTRIM(RTRIM(@Stage)),N'');
 IF DATALENGTH(@DIN)>200 THROW 52400,'DIN exceeds 100 characters.',1;
 IF @DIN IS NOT NULL AND (@AllergenType IS NULL OR @AllergenType COLLATE Latin1_General_100_BIN2<>N'Medication') THROW 52400,'DIN entry requires explicit Medication classification. Reconcile any existing DIN deliberately.',1;
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
 UpdatedBy=@UpdatedBy,UpdatedAt=SYSUTCDATETIME(),DrugDIN=@DIN,LifeStageCode=@Stage,OnsetYear=@Y,OnsetMonth=@M,OnsetDay=@D
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
CREATE OR ALTER PROCEDURE dbo.PatientAllergy_Resolve @PatientUid UNIQUEIDENTIFIER,@AllergyUid UNIQUEIDENTIFIER,@ResolveReason NVARCHAR(500)=NULL,
 @ResolvedBy BIGINT=NULL,@ExpectedRowVersion BINARY(8)=NULL,@RequestCorrelationId NVARCHAR(100)=NULL AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @PatientId BIGINT=(SELECT PatientId FROM dbo.Patient WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND IsDeleted=0);
 IF @PatientId IS NULL THROW 52401,'Patient was not found.',1;
 IF @ResolvedBy IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@ResolvedBy AND IsActive=1) THROW 52403,'An active clinical actor is required.',1;
 DECLARE @TenantUid UNIQUEIDENTIFIER=(SELECT TenantUid FROM dbo.TenantDatabaseIdentity);
 IF @TenantUid IS NULL THROW 52403,'Trusted tenant identity is required.',1;
 SET @RequestCorrelationId=COALESCE(NULLIF(LTRIM(RTRIM(@RequestCorrelationId)),N''),CONVERT(NVARCHAR(100),NEWID()));
 DECLARE @OldValue NVARCHAR(MAX)=NULL,@NewValue NVARCHAR(MAX);
 IF @ExpectedRowVersion IS NULL THROW 52400,'A current allergy RowVersion is required.',1;
 DECLARE @Current BINARY(8),@Status NVARCHAR(30);
 SELECT @Current=RowVersion,@Status=AllergyStatus FROM dbo.PatientAllergy WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND AllergyUid=@AllergyUid;
 IF @Current IS NULL THROW 52401,'Allergy was not found in the patient context.',1;
 IF @Current<>@ExpectedRowVersion THROW 52409,'Allergy changed. Reload and review.',1;
 IF @Status<>N'Resolved'
 BEGIN
 SET @OldValue=(SELECT @TenantUid AS TenantUid,@RequestCorrelationId AS RequestCorrelationId,x.* FROM dbo.PatientAllergy x WHERE x.PatientUid=@PatientUid AND x.AllergyUid=@AllergyUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 UPDATE dbo.PatientAllergy SET AllergyStatus=N'Resolved',UpdatedBy=@ResolvedBy,UpdatedAt=SYSUTCDATETIME() WHERE PatientUid=@PatientUid AND AllergyUid=@AllergyUid AND RowVersion=@ExpectedRowVersion;
 IF @@ROWCOUNT<>1 THROW 52409,'Allergy changed.',1;
 SET @NewValue=(SELECT @TenantUid AS TenantUid,@RequestCorrelationId AS RequestCorrelationId,@ResolveReason AS ResolveReason,x.* FROM dbo.PatientAllergy x WHERE x.PatientUid=@PatientUid AND x.AllergyUid=@AllergyUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,PatientUid,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt,
 AuditEventUid,EventCategory,ResourceType,ResourceUid,Outcome,RequestCorrelationId,SourceApplication)
 VALUES(@ResolvedBy,@PatientId,@PatientUid,N'Resolve',N'PatientAllergy',CONVERT(NVARCHAR(100),@AllergyUid),@OldValue,@NewValue,SYSUTCDATETIME(),
 NEWID(),N'ClinicalMutation',N'PatientAllergy',@AllergyUid,N'Succeeded',@RequestCorrelationId,N'MicroEMR.Api');
 END;
 COMMIT;
 EXEC dbo.PatientAllergy_GetByUid @AllergyUid,@PatientUid;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientAllergy_AssertNoKnownAllergies @PatientUid UNIQUEIDENTIFIER,@VerifiedBy BIGINT,@RequestCorrelationId NVARCHAR(100)=NULL AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @PatientId BIGINT=(SELECT PatientId FROM dbo.Patient WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND IsDeleted=0);
 IF @PatientId IS NULL THROW 52401,'Patient was not found.',1;
 IF @VerifiedBy IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@VerifiedBy AND IsActive=1) THROW 52403,'An active clinical actor is required.',1;
 DECLARE @TenantUid UNIQUEIDENTIFIER=(SELECT TenantUid FROM dbo.TenantDatabaseIdentity);
 IF @TenantUid IS NULL THROW 52403,'Trusted tenant identity is required.',1;
 SET @RequestCorrelationId=COALESCE(NULLIF(LTRIM(RTRIM(@RequestCorrelationId)),N''),CONVERT(NVARCHAR(100),NEWID()));
 DECLARE @OldValue NVARCHAR(MAX)=NULL,@NewValue NVARCHAR(MAX);
 IF EXISTS(SELECT 1 FROM dbo.PatientAllergy WHERE PatientUid=@PatientUid AND AllergyStatus=N'Active') THROW 52408,'NKA cannot be asserted while an active allergy exists.',1;
 DECLARE @AssertionUid UNIQUEIDENTIFIER;
 SELECT @AssertionUid=AssertionUid FROM dbo.PatientAllergyAssertion WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND AssertionType=N'NoKnownAllergies' AND Status=N'Active';
 IF @AssertionUid IS NULL
 BEGIN
  SET @AssertionUid=NEWID();
  INSERT dbo.PatientAllergyAssertion(AssertionUid,PatientUid,AssertionType,Status,VerifiedBy,VerifiedAtUtc) VALUES(@AssertionUid,@PatientUid,N'NoKnownAllergies',N'Active',@VerifiedBy,SYSUTCDATETIME());
 SET @NewValue=(SELECT @TenantUid AS TenantUid,@RequestCorrelationId AS RequestCorrelationId,x.* FROM dbo.PatientAllergyAssertion x WHERE x.PatientUid=@PatientUid AND x.AssertionUid=@AssertionUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,PatientUid,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt,
 AuditEventUid,EventCategory,ResourceType,ResourceUid,Outcome,RequestCorrelationId,SourceApplication)
 VALUES(@VerifiedBy,@PatientId,@PatientUid,N'NoKnownAllergiesAsserted',N'PatientAllergyAssertion',CONVERT(NVARCHAR(100),@AssertionUid),@OldValue,@NewValue,SYSUTCDATETIME(),
 NEWID(),N'ClinicalMutation',N'PatientAllergyAssertion',@AssertionUid,N'Succeeded',@RequestCorrelationId,N'MicroEMR.Api');
 END;
 COMMIT;
 SELECT a.*,u.DisplayName AS VerifiedByDisplayName FROM dbo.PatientAllergyAssertion a JOIN dbo.ApplicationUser u ON u.UserId=a.VerifiedBy WHERE a.PatientUid=@PatientUid AND a.AssertionUid=@AssertionUid;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientAllergy_RevokeNoKnownAllergies @PatientUid UNIQUEIDENTIFIER,@ExpectedRowVersion BINARY(8)=NULL,@RevokedBy BIGINT,
 @Reason NVARCHAR(500)=NULL,@ExpectedAssertionUid UNIQUEIDENTIFIER=NULL,@RequestCorrelationId NVARCHAR(100)=NULL AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @PatientId BIGINT=(SELECT PatientId FROM dbo.Patient WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND IsDeleted=0);
 IF @PatientId IS NULL THROW 52401,'Patient was not found.',1;
 IF @RevokedBy IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@RevokedBy AND IsActive=1) THROW 52403,'An active clinical actor is required.',1;
 DECLARE @TenantUid UNIQUEIDENTIFIER=(SELECT TenantUid FROM dbo.TenantDatabaseIdentity);
 IF @TenantUid IS NULL THROW 52403,'Trusted tenant identity is required.',1;
 SET @RequestCorrelationId=COALESCE(NULLIF(LTRIM(RTRIM(@RequestCorrelationId)),N''),CONVERT(NVARCHAR(100),NEWID()));
 DECLARE @OldValue NVARCHAR(MAX)=NULL,@NewValue NVARCHAR(MAX);
 IF @ExpectedRowVersion IS NULL OR @ExpectedAssertionUid IS NULL THROW 52400,'The reviewed NKA assertion identity and token are required.',1;
 DECLARE @AssertionUid UNIQUEIDENTIFIER,@Current BINARY(8);
 SELECT @AssertionUid=AssertionUid,@Current=RowVersion FROM dbo.PatientAllergyAssertion WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND Status=N'Active' AND AssertionType=N'NoKnownAllergies';
 IF @AssertionUid IS NULL OR @AssertionUid<>@ExpectedAssertionUid OR @Current<>@ExpectedRowVersion THROW 52409,'The NKA assertion changed. Reload and review.',1;
 SET @OldValue=(SELECT @TenantUid AS TenantUid,@RequestCorrelationId AS RequestCorrelationId,x.* FROM dbo.PatientAllergyAssertion x WHERE x.PatientUid=@PatientUid AND x.AssertionUid=@AssertionUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 UPDATE dbo.PatientAllergyAssertion SET Status=N'Revoked',RevokedBy=@RevokedBy,RevokedAtUtc=SYSUTCDATETIME(),RevocationReason=NULLIF(LTRIM(RTRIM(@Reason)),N'')
 WHERE PatientUid=@PatientUid AND AssertionUid=@AssertionUid AND RowVersion=@ExpectedRowVersion;
 IF @@ROWCOUNT<>1 THROW 52409,'NKA changed.',1;
 SET @NewValue=(SELECT @TenantUid AS TenantUid,@RequestCorrelationId AS RequestCorrelationId,x.* FROM dbo.PatientAllergyAssertion x WHERE x.PatientUid=@PatientUid AND x.AssertionUid=@AssertionUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,PatientUid,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt,
 AuditEventUid,EventCategory,ResourceType,ResourceUid,Outcome,RequestCorrelationId,SourceApplication)
 VALUES(@RevokedBy,@PatientId,@PatientUid,N'NoKnownAllergiesRevoked',N'PatientAllergyAssertion',CONVERT(NVARCHAR(100),@AssertionUid),@OldValue,@NewValue,SYSUTCDATETIME(),
 NEWID(),N'ClinicalMutation',N'PatientAllergyAssertion',@AssertionUid,N'Succeeded',@RequestCorrelationId,N'MicroEMR.Api');
 COMMIT;
 SELECT a.*,u.DisplayName AS VerifiedByDisplayName FROM dbo.PatientAllergyAssertion a JOIN dbo.ApplicationUser u ON u.UserId=a.VerifiedBy WHERE a.PatientUid=@PatientUid AND a.AssertionUid=@AssertionUid;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH;
END;
GO
