-- Discrete encounter diagnoses. All diagnosis/CPP/audit changes share one tenant transaction.
SET XACT_ABORT ON;
GO
IF OBJECT_ID(N'dbo.PatientEncounterDiagnosis',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.PatientEncounterDiagnosis
 (
  DiagnosisUid UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PatientEncounterDiagnosis PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
  PatientUid UNIQUEIDENTIFIER NOT NULL,
  EncounterUid UNIQUEIDENTIFIER NOT NULL,
  Name NVARCHAR(200) NOT NULL,
  Description NVARCHAR(1000) NULL,
  OnsetDate DATE NULL,
  PatientProblemUid UNIQUEIDENTIFIER NULL,
  SortOrder INT NOT NULL,
  IsDeleted BIT NOT NULL DEFAULT 0,
  CreatedAt DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(), CreatedBy BIGINT NOT NULL,
  UpdatedAt DATETIME2(0) NULL, UpdatedBy BIGINT NULL,
  RowVersion ROWVERSION NOT NULL,
  CONSTRAINT FK_EncounterDiagnosis_Problem FOREIGN KEY(PatientProblemUid) REFERENCES dbo.PatientProblem(PatientProblemUid)
 );
 CREATE INDEX IX_EncounterDiagnosis_PatientEncounter ON dbo.PatientEncounterDiagnosis(PatientUid,EncounterUid,IsDeleted,SortOrder);
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientEncounterDiagnoses_Get
 @PatientUid UNIQUEIDENTIFIER,@EncounterUid UNIQUEIDENTIFIER
AS
BEGIN
 SET NOCOUNT ON;
 IF NOT EXISTS(SELECT 1 FROM dbo.PatientEncounter e JOIN dbo.Patient p ON p.PatientUid=e.PatientUid
  WHERE e.PatientUid=@PatientUid AND e.EncounterUid=@EncounterUid AND p.IsDeleted=0) RETURN;
 SELECT PatientUid,EncounterUid,RowVersion,EncounterStatus FROM dbo.PatientEncounter WHERE PatientUid=@PatientUid AND EncounterUid=@EncounterUid;
 SELECT DiagnosisUid,Name,Description,OnsetDate,PatientProblemUid FROM dbo.PatientEncounterDiagnosis
  WHERE PatientUid=@PatientUid AND EncounterUid=@EncounterUid AND IsDeleted=0 ORDER BY SortOrder,DiagnosisUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientEncounterDiagnoses_Save
 @PatientUid UNIQUEIDENTIFIER,@EncounterUid UNIQUEIDENTIFIER,@ExpectedRowVersion BINARY(8),
 @DiagnosesJson NVARCHAR(MAX),@SaveToCpp BIT,@Actor BIGINT
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
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
    CreatedAt DATETIME2,CreatedBy BIGINT,CreatedByDisplayName NVARCHAR(MAX),UpdatedAt DATETIME2,UpdatedBy BIGINT,RowVersion BINARY(8));
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
     INSERT @Created EXEC dbo.PatientProblem_Create @PatientUid,@Name,@Description,@Onset,@Actor;
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
