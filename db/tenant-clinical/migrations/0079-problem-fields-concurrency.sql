-- Additive Problems fields and mandatory concurrent/audited writes. No clinical backfill.
SET XACT_ABORT ON;
GO
ALTER TABLE dbo.PatientProblem ADD
OnsetYear SMALLINT NULL,
OnsetMonth TINYINT NULL,
OnsetDay TINYINT NULL,
ClinicalResolutionYear SMALLINT NULL,
ClinicalResolutionMonth TINYINT NULL,
ClinicalResolutionDay TINYINT NULL,
LifeStageCode NVARCHAR(1) NULL,
Notes NVARCHAR(1000) NULL;
GO
ALTER TABLE dbo.PatientProblem ADD
 CONSTRAINT CK_Problem_OnsetParts CHECK(dbo.HistoryPartialDateValid(OnsetYear,OnsetMonth,OnsetDay)=1),
 CONSTRAINT CK_Problem_ResolutionParts CHECK(dbo.HistoryPartialDateValid(ClinicalResolutionYear,ClinicalResolutionMonth,ClinicalResolutionDay)=1),
 CONSTRAINT CK_Problem_LifeStage CHECK(LifeStageCode IS NULL OR LifeStageCode COLLATE Latin1_General_100_BIN2 IN(N'N',N'I',N'C',N'T',N'A')),
 CONSTRAINT CK_Problem_OnsetCompatibility CHECK(OnsetYear IS NULL OR (OnsetDay IS NULL AND OnsetDate IS NULL) OR
  (OnsetDay IS NOT NULL AND OnsetDate IS NOT NULL AND YEAR(OnsetDate)=OnsetYear AND MONTH(OnsetDate)=OnsetMonth AND DAY(OnsetDate)=OnsetDay)),
 CONSTRAINT CK_Problem_ResolutionStatus CHECK(ProblemStatus=N'Resolved' OR ClinicalResolutionYear IS NULL);
GO
CREATE OR ALTER PROCEDURE dbo.PatientProblem_GetByPatientUid
    @PatientUid UNIQUEIDENTIFIER, @StatusFilter NVARCHAR(50) = N'Active'
AS
BEGIN
    SET NOCOUNT ON;
    SET @StatusFilter = CASE WHEN @StatusFilter IN (N'Active', N'Resolved', N'All') THEN @StatusFilter ELSE N'Active' END;
    SELECT pp.PatientProblemUid, pp.PatientUid, pp.ProblemName, pp.ProblemDescription, pp.OnsetDate,
        pp.ProblemStatus, pp.ResolvedAt, pp.ResolvedBy, resolvedUser.DisplayName AS ResolvedByDisplayName,
        pp.ResolutionReason, pp.CreatedAt, pp.CreatedBy, createdUser.DisplayName AS CreatedByDisplayName,
        pp.UpdatedAt, pp.UpdatedBy, pp.RowVersion, pp.OnsetYear,pp.OnsetMonth,pp.OnsetDay,pp.ClinicalResolutionYear,pp.ClinicalResolutionMonth,pp.ClinicalResolutionDay,pp.LifeStageCode,pp.Notes
    FROM dbo.PatientProblem pp
    JOIN dbo.Patient p ON p.PatientUid=pp.PatientUid AND p.IsDeleted=0
    LEFT JOIN dbo.ApplicationUser createdUser ON createdUser.UserId = pp.CreatedBy
    LEFT JOIN dbo.ApplicationUser resolvedUser ON resolvedUser.UserId = pp.ResolvedBy
    WHERE pp.PatientUid = @PatientUid AND (@StatusFilter = N'All' OR pp.ProblemStatus = @StatusFilter)
    ORDER BY CASE WHEN pp.ProblemStatus = N'Active' THEN 0 ELSE 1 END, COALESCE(pp.UpdatedAt, pp.CreatedAt) DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientProblem_GetByUid
    @PatientUid UNIQUEIDENTIFIER, @PatientProblemUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT pp.PatientProblemUid, pp.PatientUid, pp.ProblemName, pp.ProblemDescription, pp.OnsetDate,
        pp.ProblemStatus, pp.ResolvedAt, pp.ResolvedBy, resolvedUser.DisplayName AS ResolvedByDisplayName,
        pp.ResolutionReason, pp.CreatedAt, pp.CreatedBy, createdUser.DisplayName AS CreatedByDisplayName,
        pp.UpdatedAt, pp.UpdatedBy, pp.RowVersion, pp.OnsetYear,pp.OnsetMonth,pp.OnsetDay,pp.ClinicalResolutionYear,pp.ClinicalResolutionMonth,pp.ClinicalResolutionDay,pp.LifeStageCode,pp.Notes
    FROM dbo.PatientProblem pp
    JOIN dbo.Patient p ON p.PatientUid=pp.PatientUid AND p.IsDeleted=0
    LEFT JOIN dbo.ApplicationUser createdUser ON createdUser.UserId = pp.CreatedBy
    LEFT JOIN dbo.ApplicationUser resolvedUser ON resolvedUser.UserId = pp.ResolvedBy
    WHERE pp.PatientUid = @PatientUid AND pp.PatientProblemUid = @PatientProblemUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientProblem_Create
 @PatientUid UNIQUEIDENTIFIER,@ProblemName NVARCHAR(200),@ProblemDescription NVARCHAR(1000)=NULL,@OnsetDate DATE=NULL,@CreatedBy BIGINT=NULL,
 @ClinicalFields NVARCHAR(MAX)=NULL,@OnsetDateSpecified BIT=0,@RequestCorrelationId NVARCHAR(100)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF NULLIF(LTRIM(RTRIM(@ProblemName)),N'') IS NULL THROW 51074,'Problem name is required.',1;
 IF @CreatedBy IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@CreatedBy AND IsActive=1) THROW 51075,'An active clinical actor is required.',1;
 SET @RequestCorrelationId=COALESCE(NULLIF(LTRIM(RTRIM(@RequestCorrelationId)),N''),CONVERT(NVARCHAR(100),NEWID()));
 DECLARE @TenantUid UNIQUEIDENTIFIER=(SELECT TenantUid FROM dbo.TenantDatabaseIdentity);
 IF @TenantUid IS NULL THROW 51075,'Trusted tenant identity is required.',1;
 DECLARE @OnsetYear INT,@OnsetMonth INT,@OnsetDay INT,@LifeStageCode NVARCHAR(MAX),@Notes NVARCHAR(MAX);
 IF @ClinicalFields IS NOT NULL AND (@OnsetDateSpecified=1 OR @OnsetDate IS NOT NULL) THROW 51074,'Supply either clinical fields or legacy onset.',1;
 IF @ClinicalFields IS NOT NULL AND (ISJSON(@ClinicalFields)<>1 OR
 LEFT(LTRIM(REPLACE(REPLACE(REPLACE(@ClinicalFields,NCHAR(9),N' '),NCHAR(10),N' '),NCHAR(13),N' ')),1)<>N'{') THROW 51074,'Clinical fields must be an object.',1;
 SELECT @OnsetYear=OnsetYear,@OnsetMonth=OnsetMonth,@OnsetDay=OnsetDay,@LifeStageCode=LifeStageCode,@Notes=Notes
 FROM OPENJSON(COALESCE(@ClinicalFields,N'{}')) WITH(OnsetYear INT '$.OnsetDate.Year',OnsetMonth INT '$.OnsetDate.Month',OnsetDay INT '$.OnsetDate.Day',LifeStageCode NVARCHAR(MAX) '$.LifeStageCode',Notes NVARCHAR(MAX) '$.Notes');
 SET @LifeStageCode=NULLIF(LTRIM(RTRIM(@LifeStageCode)),N''); SET @Notes=NULLIF(LTRIM(RTRIM(@Notes)),N'');
 IF dbo.HistoryPartialDateValid(@OnsetYear,@OnsetMonth,@OnsetDay)=0 THROW 51074,'Invalid onset date precision.',1;
 IF @LifeStageCode IS NOT NULL AND @LifeStageCode COLLATE Latin1_General_100_BIN2 NOT IN(N'N',N'I',N'C',N'T',N'A') THROW 51074,'Invalid life stage.',1;
 IF DATALENGTH(@Notes)>2000 THROW 51074,'Clinical notes exceed 1000 characters.',1;
 IF @ClinicalFields IS NOT NULL SET @OnsetDate=CASE WHEN @OnsetDay IS NOT NULL THEN DATEFROMPARTS(@OnsetYear,@OnsetMonth,@OnsetDay) END;
 BEGIN TRANSACTION;
 DECLARE @PatientId BIGINT=(SELECT PatientId FROM dbo.Patient WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND IsDeleted=0);
 IF @PatientId IS NULL THROW 51071,'Patient was not found.',1;
 DECLARE @PatientProblemUid UNIQUEIDENTIFIER=NEWID(),@OldValue NVARCHAR(MAX)=NULL,@NewValue NVARCHAR(MAX);
 INSERT dbo.PatientProblem(PatientProblemUid,PatientUid,ProblemName,ProblemDescription,OnsetDate,CreatedBy,OnsetYear,OnsetMonth,OnsetDay,LifeStageCode,Notes)
 VALUES(@PatientProblemUid,@PatientUid,LTRIM(RTRIM(@ProblemName)),NULLIF(LTRIM(RTRIM(@ProblemDescription)),N''),@OnsetDate,@CreatedBy,@OnsetYear,@OnsetMonth,@OnsetDay,@LifeStageCode,@Notes);
 SET @NewValue=(SELECT @TenantUid AS TenantUid,@RequestCorrelationId AS RequestCorrelationId,pp.* FROM dbo.PatientProblem pp WHERE pp.PatientUid=@PatientUid AND pp.PatientProblemUid=@PatientProblemUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,PatientUid,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt,
  AuditEventUid,EventCategory,ResourceType,ResourceUid,Outcome,RequestCorrelationId,SourceApplication)
 VALUES(@CreatedBy,@PatientId,@PatientUid,N'Create',N'PatientProblem',CONVERT(NVARCHAR(100),@PatientProblemUid),@OldValue,@NewValue,SYSUTCDATETIME(),
  NEWID(),N'ClinicalMutation',N'PatientProblem',@PatientProblemUid,'Succeeded',@RequestCorrelationId,N'MicroEMR.Api');
 COMMIT;
 EXEC dbo.PatientProblem_GetByUid @PatientUid,@PatientProblemUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientProblem_Update
 @PatientUid UNIQUEIDENTIFIER,@PatientProblemUid UNIQUEIDENTIFIER,@ProblemName NVARCHAR(200),@ProblemDescription NVARCHAR(1000)=NULL,@OnsetDate DATE=NULL,@UpdatedBy BIGINT=NULL,
 @ExpectedRowVersion BINARY(8)=NULL,@ClinicalFields NVARCHAR(MAX)=NULL,@OnsetDateSpecified BIT=0,@RequestCorrelationId NVARCHAR(100)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF NULLIF(LTRIM(RTRIM(@ProblemName)),N'') IS NULL THROW 51074,'Problem name is required.',1;
 IF @UpdatedBy IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@UpdatedBy AND IsActive=1) THROW 51075,'An active clinical actor is required.',1;
 SET @RequestCorrelationId=COALESCE(NULLIF(LTRIM(RTRIM(@RequestCorrelationId)),N''),CONVERT(NVARCHAR(100),NEWID()));
 DECLARE @TenantUid UNIQUEIDENTIFIER=(SELECT TenantUid FROM dbo.TenantDatabaseIdentity);
 IF @TenantUid IS NULL THROW 51075,'Trusted tenant identity is required.',1;
 DECLARE @OnsetYear INT,@OnsetMonth INT,@OnsetDay INT,@LifeStageCode NVARCHAR(MAX),@Notes NVARCHAR(MAX);
 IF @ExpectedRowVersion IS NULL THROW 51073,'A current RowVersion is required.',1;
 DECLARE @ReplaceClinical BIT=CASE WHEN @ClinicalFields IS NULL THEN 0 ELSE 1 END,
  @OldValue NVARCHAR(MAX),@NewValue NVARCHAR(MAX),@Status NVARCHAR(50),@Current BINARY(8),@StoredOnset DATE,@StoredYear INT,@StoredDay INT;
 IF @ReplaceClinical=1 AND (@OnsetDateSpecified=1 OR @OnsetDate IS NOT NULL) THROW 51074,'Supply either clinical fields or legacy onset.',1;
 BEGIN TRANSACTION;
 DECLARE @PatientId BIGINT=(SELECT PatientId FROM dbo.Patient WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND IsDeleted=0);
 IF @PatientId IS NULL BEGIN ROLLBACK; RETURN; END;
 SELECT @Current=pp.RowVersion,@Status=pp.ProblemStatus,@StoredOnset=pp.OnsetDate,@StoredYear=pp.OnsetYear,@StoredDay=pp.OnsetDay,
 @ClinicalFields=CASE WHEN @ReplaceClinical=1 THEN @ClinicalFields ELSE (SELECT pp.OnsetYear AS [OnsetDate.Year],pp.OnsetMonth AS [OnsetDate.Month],pp.OnsetDay AS [OnsetDate.Day],pp.LifeStageCode,pp.Notes FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END
 FROM dbo.PatientProblem pp WITH(UPDLOCK,HOLDLOCK) WHERE pp.PatientUid=@PatientUid AND pp.PatientProblemUid=@PatientProblemUid;
 IF @Current IS NULL BEGIN ROLLBACK; RETURN; END;
 IF @ExpectedRowVersion<>@Current THROW 51073,'The problem changed. Reload before saving.',1;
 IF @Status<>N'Active' THROW 51072,'Resolved problems cannot be edited.',1;
 SET @OldValue=(SELECT @TenantUid AS TenantUid,@RequestCorrelationId AS RequestCorrelationId,pp.* FROM dbo.PatientProblem pp WHERE pp.PatientUid=@PatientUid AND pp.PatientProblemUid=@PatientProblemUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 IF @ClinicalFields IS NOT NULL AND (ISJSON(@ClinicalFields)<>1 OR
 LEFT(LTRIM(REPLACE(REPLACE(REPLACE(@ClinicalFields,NCHAR(9),N' '),NCHAR(10),N' '),NCHAR(13),N' ')),1)<>N'{') THROW 51074,'Clinical fields must be an object.',1;
 SELECT @OnsetYear=OnsetYear,@OnsetMonth=OnsetMonth,@OnsetDay=OnsetDay,@LifeStageCode=LifeStageCode,@Notes=Notes
 FROM OPENJSON(COALESCE(@ClinicalFields,N'{}')) WITH(OnsetYear INT '$.OnsetDate.Year',OnsetMonth INT '$.OnsetDate.Month',OnsetDay INT '$.OnsetDate.Day',LifeStageCode NVARCHAR(MAX) '$.LifeStageCode',Notes NVARCHAR(MAX) '$.Notes');
 SET @LifeStageCode=NULLIF(LTRIM(RTRIM(@LifeStageCode)),N''); SET @Notes=NULLIF(LTRIM(RTRIM(@Notes)),N'');
 IF dbo.HistoryPartialDateValid(@OnsetYear,@OnsetMonth,@OnsetDay)=0 THROW 51074,'Invalid onset date precision.',1;
 IF @LifeStageCode IS NOT NULL AND @LifeStageCode COLLATE Latin1_General_100_BIN2 NOT IN(N'N',N'I',N'C',N'T',N'A') THROW 51074,'Invalid life stage.',1;
 IF DATALENGTH(@Notes)>2000 THROW 51074,'Clinical notes exceed 1000 characters.',1;
 IF @ReplaceClinical=1 SET @OnsetDate=CASE WHEN @OnsetDay IS NOT NULL THEN DATEFROMPARTS(@OnsetYear,@OnsetMonth,@OnsetDay) END;
 ELSE IF @OnsetDateSpecified=1 OR @OnsetDate IS NOT NULL
 BEGIN
  IF @OnsetDate IS NULL AND @StoredYear IS NOT NULL AND @StoredDay IS NULL THROW 51074,'Use the updated editor to clear a partial onset date.',1;
  SET @OnsetYear=NULL;SET @OnsetMonth=NULL;SET @OnsetDay=NULL;
 END
 ELSE SET @OnsetDate=@StoredOnset;
 UPDATE dbo.PatientProblem SET ProblemName=LTRIM(RTRIM(@ProblemName)),ProblemDescription=NULLIF(LTRIM(RTRIM(@ProblemDescription)),N''),
 OnsetDate=@OnsetDate,OnsetYear=@OnsetYear,OnsetMonth=@OnsetMonth,OnsetDay=@OnsetDay,LifeStageCode=@LifeStageCode,Notes=@Notes,UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@UpdatedBy
 WHERE PatientUid=@PatientUid AND PatientProblemUid=@PatientProblemUid AND RowVersion=@ExpectedRowVersion;
 IF @@ROWCOUNT<>1 THROW 51073,'The problem changed.',1;
 SET @NewValue=(SELECT @TenantUid AS TenantUid,@RequestCorrelationId AS RequestCorrelationId,pp.* FROM dbo.PatientProblem pp WHERE pp.PatientUid=@PatientUid AND pp.PatientProblemUid=@PatientProblemUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,PatientUid,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt,
  AuditEventUid,EventCategory,ResourceType,ResourceUid,Outcome,RequestCorrelationId,SourceApplication)
 VALUES(@UpdatedBy,@PatientId,@PatientUid,N'Update',N'PatientProblem',CONVERT(NVARCHAR(100),@PatientProblemUid),@OldValue,@NewValue,SYSUTCDATETIME(),
  NEWID(),N'ClinicalMutation',N'PatientProblem',@PatientProblemUid,'Succeeded',@RequestCorrelationId,N'MicroEMR.Api');
 COMMIT;
 EXEC dbo.PatientProblem_GetByUid @PatientUid,@PatientProblemUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientProblem_Resolve
 @PatientUid UNIQUEIDENTIFIER,@PatientProblemUid UNIQUEIDENTIFIER,@ResolutionReason NVARCHAR(500)=NULL,@ResolvedBy BIGINT=NULL,
 @ExpectedRowVersion BINARY(8)=NULL,@ClinicalResolutionYear INT=NULL,@ClinicalResolutionMonth INT=NULL,@ClinicalResolutionDay INT=NULL,@RequestCorrelationId NVARCHAR(100)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @ResolvedBy IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@ResolvedBy AND IsActive=1) THROW 51075,'An active clinical actor is required.',1;
 SET @RequestCorrelationId=COALESCE(NULLIF(LTRIM(RTRIM(@RequestCorrelationId)),N''),CONVERT(NVARCHAR(100),NEWID()));
 DECLARE @TenantUid UNIQUEIDENTIFIER=(SELECT TenantUid FROM dbo.TenantDatabaseIdentity);
 IF @TenantUid IS NULL THROW 51075,'Trusted tenant identity is required.',1;
 IF @ExpectedRowVersion IS NULL THROW 51073,'A current RowVersion is required.',1;
 IF dbo.HistoryPartialDateValid(@ClinicalResolutionYear,@ClinicalResolutionMonth,@ClinicalResolutionDay)=0 THROW 51074,'Invalid clinical resolution precision.',1;
 BEGIN TRANSACTION;
 DECLARE @PatientId BIGINT=(SELECT PatientId FROM dbo.Patient WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND IsDeleted=0),
 @Current BINARY(8),@Status NVARCHAR(50),@OnsetYear INT,@OnsetMonth INT,@OnsetDay INT,@OldValue NVARCHAR(MAX),@NewValue NVARCHAR(MAX);
 IF @PatientId IS NULL BEGIN ROLLBACK; RETURN; END;
 SELECT @Current=RowVersion,@Status=ProblemStatus,@OnsetYear=COALESCE(OnsetYear,YEAR(OnsetDate)),@OnsetMonth=COALESCE(OnsetMonth,MONTH(OnsetDate)),@OnsetDay=COALESCE(OnsetDay,DAY(OnsetDate))
 FROM dbo.PatientProblem WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND PatientProblemUid=@PatientProblemUid;
 IF @Current IS NULL BEGIN ROLLBACK; RETURN; END;
 IF @Current<>@ExpectedRowVersion THROW 51073,'The problem changed. Reload before resolving.',1;
 IF @Status<>N'Active' THROW 51072,'The problem is already resolved.',1;
 IF @ClinicalResolutionYear IS NOT NULL AND @OnsetYear IS NOT NULL AND
 DATEFROMPARTS(@ClinicalResolutionYear,COALESCE(@ClinicalResolutionMonth,12),COALESCE(@ClinicalResolutionDay,DAY(EOMONTH(DATEFROMPARTS(@ClinicalResolutionYear,COALESCE(@ClinicalResolutionMonth,12),1)))))
 < DATEFROMPARTS(@OnsetYear,COALESCE(@OnsetMonth,1),COALESCE(@OnsetDay,1)) THROW 51074,'Clinical resolution cannot precede onset.',1;
 SET @OldValue=(SELECT @TenantUid AS TenantUid,@RequestCorrelationId AS RequestCorrelationId,pp.* FROM dbo.PatientProblem pp WHERE pp.PatientUid=@PatientUid AND pp.PatientProblemUid=@PatientProblemUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 UPDATE dbo.PatientProblem SET ProblemStatus=N'Resolved',ResolvedAt=SYSUTCDATETIME(),ResolvedBy=@ResolvedBy,ResolutionReason=NULLIF(LTRIM(RTRIM(@ResolutionReason)),N''),
 ClinicalResolutionYear=@ClinicalResolutionYear,ClinicalResolutionMonth=@ClinicalResolutionMonth,ClinicalResolutionDay=@ClinicalResolutionDay,UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@ResolvedBy
 WHERE PatientUid=@PatientUid AND PatientProblemUid=@PatientProblemUid AND RowVersion=@ExpectedRowVersion;
 IF @@ROWCOUNT<>1 THROW 51073,'The problem changed.',1;
 SET @NewValue=(SELECT @TenantUid AS TenantUid,@RequestCorrelationId AS RequestCorrelationId,pp.* FROM dbo.PatientProblem pp WHERE pp.PatientUid=@PatientUid AND pp.PatientProblemUid=@PatientProblemUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,PatientUid,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt,
  AuditEventUid,EventCategory,ResourceType,ResourceUid,Outcome,RequestCorrelationId,SourceApplication)
 VALUES(@ResolvedBy,@PatientId,@PatientUid,N'Resolve',N'PatientProblem',CONVERT(NVARCHAR(100),@PatientProblemUid),@OldValue,@NewValue,SYSUTCDATETIME(),
  NEWID(),N'ClinicalMutation',N'PatientProblem',@PatientProblemUid,'Succeeded',@RequestCorrelationId,N'MicroEMR.Api');
 COMMIT;
 EXEC dbo.PatientProblem_GetByUid @PatientUid,@PatientProblemUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientEncounterDiagnoses_Save
 @PatientUid UNIQUEIDENTIFIER,@EncounterUid UNIQUEIDENTIFIER,@ExpectedRowVersion BINARY(8),
 @DiagnosesJson NVARCHAR(MAX),@SaveToCpp BIT,@Actor BIGINT,@RequestCorrelationId NVARCHAR(100)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 SET @RequestCorrelationId=COALESCE(NULLIF(@RequestCorrelationId,N''),CONVERT(NVARCHAR(100),NEWID()));
 IF @Actor IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@Actor)
  THROW 51203,'A clinical actor is required.',1;
 IF ISJSON(@DiagnosesJson)<>1 OR LEFT(LTRIM(@DiagnosesJson),1)<>N'[' THROW 51203,'Invalid diagnoses.',1;
 DECLARE @Input TABLE(SortOrder INT,DiagnosisUid UNIQUEIDENTIFIER,Name NVARCHAR(200),Description NVARCHAR(1000),OnsetDate DATE,ProblemUid UNIQUEIDENTIFIER);
 -- Validate lengths before typed OPENJSON can truncate values.
 IF EXISTS(SELECT 1 FROM OPENJSON(@DiagnosesJson) j CROSS APPLY OPENJSON(j.value) WITH
   (Name NVARCHAR(MAX) '$.Name',Description NVARCHAR(MAX) '$.Description') x WHERE j.type<>5
  OR NULLIF(LTRIM(RTRIM(x.Name)),N'') IS NULL
  OR LEN(x.Name)>200 OR LEN(x.Description)>1000)
  THROW 51203,'Invalid diagnosis details.',1;
 INSERT @Input(SortOrder,DiagnosisUid,Name,Description,OnsetDate)
 SELECT CONVERT(INT,j.[key]),x.DiagnosisUid,LTRIM(RTRIM(x.Name)),NULLIF(LTRIM(RTRIM(x.Description)),N''),x.OnsetDate
 FROM OPENJSON(@DiagnosesJson) j CROSS APPLY OPENJSON(j.value) WITH
  (DiagnosisUid UNIQUEIDENTIFIER '$.DiagnosisUid',Name NVARCHAR(200) '$.Name',Description NVARCHAR(1000) '$.Description',OnsetDate DATE '$.OnsetDate') x;
 IF (SELECT COUNT(*) FROM @Input)>50 OR EXISTS(SELECT 1 FROM @Input GROUP BY Name COLLATE Latin1_General_100_CI_AS HAVING COUNT(*)>1)
  OR EXISTS(SELECT 1 FROM @Input WHERE DiagnosisUid IS NOT NULL GROUP BY DiagnosisUid HAVING COUNT(*)>1)
  THROW 51203,'Diagnoses must be distinct and limited to 50.',1;
 DECLARE @PatientId BIGINT,@Status NVARCHAR(50),@Current BINARY(8),@Before NVARCHAR(MAX),@After NVARCHAR(MAX);
 BEGIN TRY
  BEGIN TRANSACTION;
  -- Serialize this workflow for a patient, including matching existing active CPP Problems.
  SELECT @PatientId=PatientId FROM dbo.Patient WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND IsDeleted=0;
  SELECT @Status=EncounterStatus,@Current=RowVersion FROM dbo.PatientEncounter WITH(UPDLOCK,HOLDLOCK)
   WHERE PatientUid=@PatientUid AND EncounterUid=@EncounterUid;
  IF @PatientId IS NULL OR @Status IS NULL BEGIN ROLLBACK; RETURN; END;
  IF @Status<>N'Open' THROW 51201,'The encounter is not editable.',1;
  IF @ExpectedRowVersion IS NULL OR @Current<>@ExpectedRowVersion THROW 51200,'The encounter changed.',1;
  IF EXISTS(SELECT 1 FROM @Input i WHERE i.DiagnosisUid IS NOT NULL AND NOT EXISTS
   (SELECT 1 FROM dbo.PatientEncounterDiagnosis d WHERE d.DiagnosisUid=i.DiagnosisUid AND d.PatientUid=@PatientUid AND d.EncounterUid=@EncounterUid AND d.IsDeleted=0))
   THROW 51202,'Diagnosis ownership is invalid.',1;
  SELECT @Before=(SELECT DiagnosisUid,Name,Description,OnsetDate,PatientProblemUid,SortOrder FROM dbo.PatientEncounterDiagnosis
   WHERE PatientUid=@PatientUid AND EncounterUid=@EncounterUid AND IsDeleted=0 ORDER BY SortOrder FOR JSON PATH,INCLUDE_NULL_VALUES);
  -- Preserve a prior link only while the encounter diagnosis name is unchanged. Encounter-only never mutates Problems.
  UPDATE i SET ProblemUid=d.PatientProblemUid FROM @Input i JOIN dbo.PatientEncounterDiagnosis d ON d.DiagnosisUid=i.DiagnosisUid
   WHERE d.Name COLLATE Latin1_General_100_CI_AS=i.Name COLLATE Latin1_General_100_CI_AS;
  IF @SaveToCpp=1
  BEGIN
   DECLARE @Index INT=0,@Count INT=(SELECT COUNT(*) FROM @Input),@Name NVARCHAR(200),@Description NVARCHAR(1000),@Onset DATE,@Problem UNIQUEIDENTIFIER;
   -- Capture the existing authoritative create procedure's result without emitting extra HTTP result sets.
   DECLARE @Created TABLE(PatientProblemUid UNIQUEIDENTIFIER,PatientUid UNIQUEIDENTIFIER,ProblemName NVARCHAR(200),ProblemDescription NVARCHAR(1000),
    OnsetDate DATE,ProblemStatus NVARCHAR(50),ResolvedAt DATETIME2,ResolvedBy BIGINT,ResolvedByDisplayName NVARCHAR(MAX),ResolutionReason NVARCHAR(500),
    CreatedAt DATETIME2,CreatedBy BIGINT,CreatedByDisplayName NVARCHAR(MAX),UpdatedAt DATETIME2,UpdatedBy BIGINT,RowVersion BINARY(8),OnsetYear SMALLINT,OnsetMonth TINYINT,OnsetDay TINYINT,ClinicalResolutionYear SMALLINT,ClinicalResolutionMonth TINYINT,ClinicalResolutionDay TINYINT,LifeStageCode NVARCHAR(1),Notes NVARCHAR(1000));
   WHILE @Index<@Count
   BEGIN
    SELECT @Name=Name,@Description=Description,@Onset=OnsetDate FROM @Input WHERE SortOrder=@Index;
    SET @Problem=NULL;
    SELECT TOP(1) @Problem=PatientProblemUid FROM dbo.PatientProblem WITH(UPDLOCK,HOLDLOCK)
     WHERE PatientUid=@PatientUid AND ProblemStatus=N'Active'
      AND LTRIM(RTRIM(ProblemName)) COLLATE Latin1_General_100_CI_AS=@Name COLLATE Latin1_General_100_CI_AS
     ORDER BY CreatedAt,PatientProblemUid;
    IF @Problem IS NULL
    BEGIN
     DELETE FROM @Created; -- Temporary result buffer only; no clinical deletion.
     INSERT @Created EXEC dbo.PatientProblem_Create @PatientUid,@Name,@Description,@Onset,@Actor,@RequestCorrelationId=@RequestCorrelationId;
     SELECT @Problem=PatientProblemUid FROM @Created WHERE PatientUid=@PatientUid;
     IF @Problem IS NULL THROW 51203,'CPP creation did not return a problem.',1;
    END;
    UPDATE @Input SET ProblemUid=@Problem WHERE SortOrder=@Index;
    SET @Index+=1;
   END;
  END;
  UPDATE d SET IsDeleted=1,UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@Actor
   FROM dbo.PatientEncounterDiagnosis d WHERE d.PatientUid=@PatientUid AND d.EncounterUid=@EncounterUid AND d.IsDeleted=0
    AND NOT EXISTS(SELECT 1 FROM @Input i WHERE i.DiagnosisUid=d.DiagnosisUid);
  UPDATE d SET Name=i.Name,Description=i.Description,OnsetDate=i.OnsetDate,PatientProblemUid=i.ProblemUid,SortOrder=i.SortOrder,
    UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@Actor
   FROM dbo.PatientEncounterDiagnosis d JOIN @Input i ON i.DiagnosisUid=d.DiagnosisUid
   WHERE d.PatientUid=@PatientUid AND d.EncounterUid=@EncounterUid AND d.IsDeleted=0;
  INSERT dbo.PatientEncounterDiagnosis(DiagnosisUid,PatientUid,EncounterUid,Name,Description,OnsetDate,PatientProblemUid,SortOrder,CreatedBy)
   SELECT NEWID(),@PatientUid,@EncounterUid,Name,Description,OnsetDate,ProblemUid,SortOrder,@Actor FROM @Input WHERE DiagnosisUid IS NULL;
  UPDATE dbo.PatientEncounter SET UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@Actor WHERE PatientUid=@PatientUid AND EncounterUid=@EncounterUid;
  SELECT @After=(SELECT DiagnosisUid,Name,Description,OnsetDate,PatientProblemUid,SortOrder FROM dbo.PatientEncounterDiagnosis
   WHERE PatientUid=@PatientUid AND EncounterUid=@EncounterUid AND IsDeleted=0 ORDER BY SortOrder FOR JSON PATH,INCLUDE_NULL_VALUES);
  -- Mandatory audit insert is in the same transaction: audit failure rolls back both destinations.
  INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
   VALUES(@Actor,@PatientId,N'EncounterDiagnosesSaved',N'PatientEncounter',CONVERT(NVARCHAR(100),@EncounterUid),@Before,@After,SYSUTCDATETIME());
  DECLARE @HistoryDescription NVARCHAR(500)=CASE WHEN @SaveToCpp=1 THEN N'Discrete diagnoses saved to encounter and CPP.' ELSE N'Discrete diagnoses saved to encounter only.' END;
  EXEC dbo.PatientEncounterHistory_Create @EncounterUid,@PatientUid,N'DiagnosesSaved',@HistoryDescription,@Status,@Status,NULL,@Actor,0;
  COMMIT;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK;
  THROW;
 END CATCH;
 EXEC dbo.PatientEncounterDiagnoses_Get @PatientUid,@EncounterUid;
END;
GO
