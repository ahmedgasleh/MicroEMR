SET XACT_ABORT ON;
GO

CREATE TABLE dbo.ClinicalDataMigrationBatch
(
    ClinicalDataMigrationBatchId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ClinicalDataMigrationBatch PRIMARY KEY,
    MigrationBatchUid UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ClinicalDataMigrationBatch_Uid DEFAULT NEWSEQUENTIALID(),
    SourceSystem NVARCHAR(100) NOT NULL,
    SourceSystemVersion NVARCHAR(100) NULL,
    PackageUid UNIQUEIDENTIFIER NOT NULL,
    PackageSchemaVersion NVARCHAR(50) NULL,
    PackageFingerprint CHAR(64) NOT NULL,
    Status NVARCHAR(30) NOT NULL,
    ValidationMode NVARCHAR(30) NOT NULL CONSTRAINT DF_ClinicalDataMigrationBatch_Mode DEFAULT N'ValidateOnly',
    RequestedBy BIGINT NOT NULL,
    CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_ClinicalDataMigrationBatch_Created DEFAULT SYSUTCDATETIME(),
    ValidationStartedAtUtc DATETIME2(0) NULL,
    ValidationCompletedAtUtc DATETIME2(0) NULL,
    TotalRecords INT NOT NULL CONSTRAINT DF_ClinicalDataMigrationBatch_Total DEFAULT 0,
    ValidRecords INT NOT NULL CONSTRAINT DF_ClinicalDataMigrationBatch_Valid DEFAULT 0,
    WarningRecords INT NOT NULL CONSTRAINT DF_ClinicalDataMigrationBatch_Warning DEFAULT 0,
    FailedRecords INT NOT NULL CONSTRAINT DF_ClinicalDataMigrationBatch_Failed DEFAULT 0,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT UQ_ClinicalDataMigrationBatch_Uid UNIQUE(MigrationBatchUid),
    CONSTRAINT UQ_ClinicalDataMigrationBatch_SourceFingerprint UNIQUE(SourceSystem,PackageFingerprint),
    CONSTRAINT UQ_ClinicalDataMigrationBatch_SourcePackage UNIQUE(SourceSystem,PackageUid),
    CONSTRAINT FK_ClinicalDataMigrationBatch_RequestedBy FOREIGN KEY(RequestedBy) REFERENCES dbo.ApplicationUser(UserId),
    CONSTRAINT CK_ClinicalDataMigrationBatch_Status CHECK(Status IN(N'Created',N'Validating',N'ValidationFailed',N'Validated')),
    CONSTRAINT CK_ClinicalDataMigrationBatch_Mode CHECK(ValidationMode=N'ValidateOnly'),
    CONSTRAINT CK_ClinicalDataMigrationBatch_Fingerprint CHECK(PackageFingerprint NOT LIKE '%[^0-9a-f]%' AND LEN(PackageFingerprint)=64),
    CONSTRAINT CK_ClinicalDataMigrationBatch_Counts CHECK(TotalRecords>=0 AND ValidRecords>=0 AND WarningRecords>=0 AND FailedRecords>=0)
);
GO

CREATE TABLE dbo.ClinicalDataMigrationStagedPatient
(
    StagedPatientId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ClinicalDataMigrationStagedPatient PRIMARY KEY,
    StagedPatientUid UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ClinicalDataMigrationStagedPatient_Uid DEFAULT NEWSEQUENTIALID(),
    MigrationBatchUid UNIQUEIDENTIFIER NOT NULL,
    SourceSystem NVARCHAR(100) NOT NULL,
    SourceObjectId NVARCHAR(200) NOT NULL,
    SourcePatientId NVARCHAR(200) NOT NULL,
    RecordType NVARCHAR(30) NOT NULL CONSTRAINT DF_ClinicalDataMigrationStagedPatient_Type DEFAULT N'Patient',
    SourceCreatedAt DATETIMEOFFSET(0) NULL, SourceUpdatedAt DATETIMEOFFSET(0) NULL, SourceAuthor NVARCHAR(200) NULL,
    ChartNumber NVARCHAR(50) NULL, HealthCardNumber NVARCHAR(50) NULL, HealthCardVersion NVARCHAR(10) NULL,
    FirstName NVARCHAR(100) NOT NULL, MiddleName NVARCHAR(100) NULL, LastName NVARCHAR(100) NOT NULL, DateOfBirth DATE NULL,
    SexAtBirth NVARCHAR(20) NULL, GenderIdentity NVARCHAR(50) NULL, PreferredName NVARCHAR(100) NULL,
    PhoneNumber NVARCHAR(30) NULL, AlternatePhoneNumber NVARCHAR(30) NULL, Email NVARCHAR(255) NULL,
    AddressLine1 NVARCHAR(255) NULL, AddressLine2 NVARCHAR(255) NULL, City NVARCHAR(100) NULL,
    Province NVARCHAR(50) NULL, PostalCode NVARCHAR(20) NULL, CountryCode NVARCHAR(2) NOT NULL,
    MappingStatus NVARCHAR(30) NOT NULL, TargetPatientUid UNIQUEIDENTIFIER NULL, ValidationState NVARCHAR(20) NOT NULL,
    ErrorCount INT NOT NULL, WarningCount INT NOT NULL, StagedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_ClinicalDataMigrationStagedPatient_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_ClinicalDataMigrationStagedPatient_Uid UNIQUE(StagedPatientUid),
    CONSTRAINT FK_ClinicalDataMigrationStagedPatient_Batch FOREIGN KEY(MigrationBatchUid) REFERENCES dbo.ClinicalDataMigrationBatch(MigrationBatchUid),
    CONSTRAINT CK_ClinicalDataMigrationStagedPatient_Type CHECK(RecordType=N'Patient'),
    CONSTRAINT CK_ClinicalDataMigrationStagedPatient_Mapping CHECK(MappingStatus IN(N'ReadyToCreate',N'MappedExisting',N'RequiresReview',N'Invalid')),
    CONSTRAINT CK_ClinicalDataMigrationStagedPatient_State CHECK(ValidationState IN(N'Valid',N'Warning',N'Invalid')),
    CONSTRAINT CK_ClinicalDataMigrationStagedPatient_Counts CHECK(ErrorCount>=0 AND WarningCount>=0)
);
GO
CREATE INDEX IX_ClinicalDataMigrationStagedPatient_BatchSourceObject
ON dbo.ClinicalDataMigrationStagedPatient(MigrationBatchUid,SourceObjectId);
GO

CREATE TABLE dbo.ClinicalDataMigrationStagedProblem
(
    StagedProblemId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ClinicalDataMigrationStagedProblem PRIMARY KEY,
    StagedProblemUid UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ClinicalDataMigrationStagedProblem_Uid DEFAULT NEWSEQUENTIALID(),
    MigrationBatchUid UNIQUEIDENTIFIER NOT NULL, SourceSystem NVARCHAR(100) NOT NULL,
    SourceObjectId NVARCHAR(200) NOT NULL, SourcePatientId NVARCHAR(200) NOT NULL,
    RecordType NVARCHAR(30) NOT NULL CONSTRAINT DF_ClinicalDataMigrationStagedProblem_Type DEFAULT N'Problem',
    SourceCreatedAt DATETIMEOFFSET(0) NULL, SourceUpdatedAt DATETIMEOFFSET(0) NULL, SourceAuthor NVARCHAR(200) NULL,
    ProblemName NVARCHAR(200) NOT NULL, ProblemDescription NVARCHAR(1000) NULL, OnsetDate DATE NULL,
    ProblemStatus NVARCHAR(20) NOT NULL, ResolvedDate DATE NULL, ValidationState NVARCHAR(20) NOT NULL,
    ErrorCount INT NOT NULL, WarningCount INT NOT NULL, StagedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_ClinicalDataMigrationStagedProblem_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_ClinicalDataMigrationStagedProblem_Uid UNIQUE(StagedProblemUid),
    CONSTRAINT FK_ClinicalDataMigrationStagedProblem_Batch FOREIGN KEY(MigrationBatchUid) REFERENCES dbo.ClinicalDataMigrationBatch(MigrationBatchUid),
    CONSTRAINT CK_ClinicalDataMigrationStagedProblem_Type CHECK(RecordType=N'Problem'),
    CONSTRAINT CK_ClinicalDataMigrationStagedProblem_Status CHECK(ProblemStatus IN(N'Active',N'Resolved')),
    CONSTRAINT CK_ClinicalDataMigrationStagedProblem_State CHECK(ValidationState IN(N'Valid',N'Warning',N'Invalid')),
    CONSTRAINT CK_ClinicalDataMigrationStagedProblem_Counts CHECK(ErrorCount>=0 AND WarningCount>=0)
);
GO
CREATE INDEX IX_ClinicalDataMigrationStagedProblem_BatchSourceObject
ON dbo.ClinicalDataMigrationStagedProblem(MigrationBatchUid,SourceObjectId);
GO

CREATE TABLE dbo.ClinicalDataMigrationValidationIssue
(
    ValidationIssueId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ClinicalDataMigrationValidationIssue PRIMARY KEY,
    MigrationBatchUid UNIQUEIDENTIFIER NOT NULL, Code NVARCHAR(100) NOT NULL,
    Severity NVARCHAR(20) NOT NULL, RecordType NVARCHAR(30) NOT NULL, SourceObjectId NVARCHAR(200) NULL,
    Message NVARCHAR(500) NOT NULL, CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_ClinicalDataMigrationValidationIssue_At DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_ClinicalDataMigrationValidationIssue_Batch FOREIGN KEY(MigrationBatchUid) REFERENCES dbo.ClinicalDataMigrationBatch(MigrationBatchUid),
    CONSTRAINT CK_ClinicalDataMigrationValidationIssue_Severity CHECK(Severity IN(N'Error',N'Warning')),
    CONSTRAINT CK_ClinicalDataMigrationValidationIssue_RecordType CHECK(RecordType IN(N'Package',N'Patient',N'Problem'))
);
GO
CREATE INDEX IX_ClinicalDataMigrationValidationIssue_Batch
ON dbo.ClinicalDataMigrationValidationIssue(MigrationBatchUid,ValidationIssueId);
GO

CREATE OR ALTER PROCEDURE dbo.ClinicalDataMigration_BeginValidation
    @MigrationBatchUid UNIQUEIDENTIFIER,@SourceSystem NVARCHAR(100),@SourceSystemVersion NVARCHAR(100)=NULL,
    @PackageUid UNIQUEIDENTIFIER,@PackageSchemaVersion NVARCHAR(50)=NULL,@PackageFingerprint CHAR(64),@RequestedBy BIGINT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF NULLIF(LTRIM(RTRIM(@SourceSystem)),N'') IS NULL OR @PackageUid='00000000-0000-0000-0000-000000000000' OR @MigrationBatchUid='00000000-0000-0000-0000-000000000000' THROW 52400,'Invalid migration package identity.',1;
    IF @PackageFingerprint LIKE '%[^0-9a-f]%' OR LEN(@PackageFingerprint)<>64 THROW 52400,'Invalid migration package fingerprint.',1;
    IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@RequestedBy AND IsActive=1) THROW 52402,'Active migration validation actor was not found.',1;
    BEGIN TRANSACTION;
    DECLARE @Existing UNIQUEIDENTIFIER,@ExistingStatus NVARCHAR(30),@ExistingFingerprint CHAR(64);
    SELECT @Existing=MigrationBatchUid,@ExistingStatus=Status,@ExistingFingerprint=PackageFingerprint
    FROM dbo.ClinicalDataMigrationBatch WITH(UPDLOCK,HOLDLOCK) WHERE SourceSystem=LTRIM(RTRIM(@SourceSystem)) AND PackageUid=@PackageUid;
    IF @Existing IS NOT NULL
    BEGIN
        IF @ExistingFingerprint<>@PackageFingerprint BEGIN ROLLBACK; THROW 52401,'Package UID was previously used with different content.',1; END;
        COMMIT; SELECT @Existing AS MigrationBatchUid,CAST(1 AS BIT) AS ReusedExistingBatch,@ExistingStatus AS Status; RETURN;
    END;
    INSERT dbo.ClinicalDataMigrationBatch(MigrationBatchUid,SourceSystem,SourceSystemVersion,PackageUid,PackageSchemaVersion,PackageFingerprint,Status,ValidationMode,RequestedBy,ValidationStartedAtUtc)
    VALUES(@MigrationBatchUid,LTRIM(RTRIM(@SourceSystem)),NULLIF(LTRIM(RTRIM(@SourceSystemVersion)),N''),@PackageUid,NULLIF(LTRIM(RTRIM(@PackageSchemaVersion)),N''),@PackageFingerprint,N'Validating',N'ValidateOnly',@RequestedBy,SYSUTCDATETIME());
    COMMIT; SELECT @MigrationBatchUid AS MigrationBatchUid,CAST(0 AS BIT) AS ReusedExistingBatch,N'Validating' AS Status;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ClinicalDataMigration_FindPatientMatch
    @SourceSystem NVARCHAR(100),@SourcePatientId NVARCHAR(200),@HealthCardNumber NVARCHAR(50)=NULL,
    @FirstName NVARCHAR(100),@LastName NVARCHAR(100),@DateOfBirth DATE=NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Mapped UNIQUEIDENTIFIER=(SELECT TOP(1) TargetPatientUid FROM dbo.ClinicalDataMigrationStagedPatient WHERE SourceSystem=@SourceSystem AND SourcePatientId=@SourcePatientId AND TargetPatientUid IS NOT NULL ORDER BY StagedPatientId DESC);
    IF @Mapped IS NOT NULL BEGIN SELECT @Mapped PatientUid,1 StrongMatchCount,0 DemographicMatchCount; RETURN; END;
    DECLARE @Strong INT=0,@StrongUid UNIQUEIDENTIFIER=NULL;
    IF NULLIF(LTRIM(RTRIM(@HealthCardNumber)),N'') IS NOT NULL
    BEGIN SELECT @Strong=COUNT(*),@StrongUid=CASE WHEN COUNT(*)=1 THEN MAX(PatientUid) END FROM dbo.Patient WHERE IsDeleted=0 AND HealthCardNumber=LTRIM(RTRIM(@HealthCardNumber)); END;
    DECLARE @Demographic INT=(SELECT COUNT(*) FROM dbo.Patient WHERE IsDeleted=0 AND FirstName=LTRIM(RTRIM(@FirstName)) AND LastName=LTRIM(RTRIM(@LastName)) AND DateOfBirth=@DateOfBirth);
    SELECT @StrongUid PatientUid,@Strong StrongMatchCount,@Demographic DemographicMatchCount;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ClinicalDataMigration_StagePatient
    @MigrationBatchUid UNIQUEIDENTIFIER,@SourceSystem NVARCHAR(100),@SourceObjectId NVARCHAR(200),@SourcePatientId NVARCHAR(200),
    @ChartNumber NVARCHAR(50)=NULL,@HealthCardNumber NVARCHAR(50)=NULL,@HealthCardVersion NVARCHAR(10)=NULL,
    @FirstName NVARCHAR(100),@MiddleName NVARCHAR(100)=NULL,@LastName NVARCHAR(100),@DateOfBirth DATE=NULL,
    @SexAtBirth NVARCHAR(20)=NULL,@GenderIdentity NVARCHAR(50)=NULL,@PreferredName NVARCHAR(100)=NULL,
    @PhoneNumber NVARCHAR(30)=NULL,@AlternatePhoneNumber NVARCHAR(30)=NULL,@Email NVARCHAR(255)=NULL,
    @AddressLine1 NVARCHAR(255)=NULL,@AddressLine2 NVARCHAR(255)=NULL,@City NVARCHAR(100)=NULL,@Province NVARCHAR(50)=NULL,@PostalCode NVARCHAR(20)=NULL,@CountryCode NVARCHAR(2),
    @SourceCreatedAt DATETIMEOFFSET(0)=NULL,@SourceUpdatedAt DATETIMEOFFSET(0)=NULL,@SourceAuthor NVARCHAR(200)=NULL,
    @MappingStatus NVARCHAR(30),@TargetPatientUid UNIQUEIDENTIFIER=NULL,@ValidationState NVARCHAR(20),@ErrorCount INT,@WarningCount INT
AS
BEGIN
 SET NOCOUNT ON;
 IF NOT EXISTS(SELECT 1 FROM dbo.ClinicalDataMigrationBatch WHERE MigrationBatchUid=@MigrationBatchUid AND Status=N'Validating' AND ValidationMode=N'ValidateOnly') THROW 52403,'Migration batch is not validating.',1;
 INSERT dbo.ClinicalDataMigrationStagedPatient(MigrationBatchUid,SourceSystem,SourceObjectId,SourcePatientId,SourceCreatedAt,SourceUpdatedAt,SourceAuthor,ChartNumber,HealthCardNumber,HealthCardVersion,FirstName,MiddleName,LastName,DateOfBirth,SexAtBirth,GenderIdentity,PreferredName,PhoneNumber,AlternatePhoneNumber,Email,AddressLine1,AddressLine2,City,Province,PostalCode,CountryCode,MappingStatus,TargetPatientUid,ValidationState,ErrorCount,WarningCount)
 VALUES(@MigrationBatchUid,@SourceSystem,@SourceObjectId,@SourcePatientId,@SourceCreatedAt,@SourceUpdatedAt,@SourceAuthor,@ChartNumber,@HealthCardNumber,@HealthCardVersion,@FirstName,@MiddleName,@LastName,@DateOfBirth,@SexAtBirth,@GenderIdentity,@PreferredName,@PhoneNumber,@AlternatePhoneNumber,@Email,@AddressLine1,@AddressLine2,@City,@Province,@PostalCode,@CountryCode,@MappingStatus,@TargetPatientUid,@ValidationState,@ErrorCount,@WarningCount);
END;
GO

CREATE OR ALTER PROCEDURE dbo.ClinicalDataMigration_StageProblem
 @MigrationBatchUid UNIQUEIDENTIFIER,@SourceSystem NVARCHAR(100),@SourceObjectId NVARCHAR(200),@SourcePatientId NVARCHAR(200),
 @ProblemName NVARCHAR(200),@ProblemDescription NVARCHAR(1000)=NULL,@OnsetDate DATE=NULL,@ProblemStatus NVARCHAR(20),@ResolvedDate DATE=NULL,
 @SourceCreatedAt DATETIMEOFFSET(0)=NULL,@SourceUpdatedAt DATETIMEOFFSET(0)=NULL,@SourceAuthor NVARCHAR(200)=NULL,@ValidationState NVARCHAR(20),@ErrorCount INT,@WarningCount INT
AS
BEGIN
 SET NOCOUNT ON;
 IF NOT EXISTS(SELECT 1 FROM dbo.ClinicalDataMigrationBatch WHERE MigrationBatchUid=@MigrationBatchUid AND Status=N'Validating' AND ValidationMode=N'ValidateOnly') THROW 52403,'Migration batch is not validating.',1;
 INSERT dbo.ClinicalDataMigrationStagedProblem(MigrationBatchUid,SourceSystem,SourceObjectId,SourcePatientId,SourceCreatedAt,SourceUpdatedAt,SourceAuthor,ProblemName,ProblemDescription,OnsetDate,ProblemStatus,ResolvedDate,ValidationState,ErrorCount,WarningCount)
 VALUES(@MigrationBatchUid,@SourceSystem,@SourceObjectId,@SourcePatientId,@SourceCreatedAt,@SourceUpdatedAt,@SourceAuthor,@ProblemName,@ProblemDescription,@OnsetDate,@ProblemStatus,@ResolvedDate,@ValidationState,@ErrorCount,@WarningCount);
END;
GO

CREATE OR ALTER PROCEDURE dbo.ClinicalDataMigration_AddIssue
 @MigrationBatchUid UNIQUEIDENTIFIER,@Code NVARCHAR(100),@Severity NVARCHAR(20),@RecordType NVARCHAR(30),@SourceObjectId NVARCHAR(200)=NULL,@Message NVARCHAR(500)
AS
BEGIN SET NOCOUNT ON; INSERT dbo.ClinicalDataMigrationValidationIssue(MigrationBatchUid,Code,Severity,RecordType,SourceObjectId,Message) VALUES(@MigrationBatchUid,@Code,@Severity,@RecordType,@SourceObjectId,@Message); END;
GO

CREATE OR ALTER PROCEDURE dbo.ClinicalDataMigration_CompleteValidation @MigrationBatchUid UNIQUEIDENTIFIER,@RequestedBy BIGINT
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRANSACTION;
 DECLARE @PatientTotal INT=(SELECT COUNT(*) FROM dbo.ClinicalDataMigrationStagedPatient WHERE MigrationBatchUid=@MigrationBatchUid),@ProblemTotal INT=(SELECT COUNT(*) FROM dbo.ClinicalDataMigrationStagedProblem WHERE MigrationBatchUid=@MigrationBatchUid);
 DECLARE @Valid INT=(SELECT COUNT(*) FROM dbo.ClinicalDataMigrationStagedPatient WHERE MigrationBatchUid=@MigrationBatchUid AND ValidationState=N'Valid')+(SELECT COUNT(*) FROM dbo.ClinicalDataMigrationStagedProblem WHERE MigrationBatchUid=@MigrationBatchUid AND ValidationState=N'Valid');
 DECLARE @Warnings INT=(SELECT COUNT(*) FROM dbo.ClinicalDataMigrationStagedPatient WHERE MigrationBatchUid=@MigrationBatchUid AND ValidationState=N'Warning')+(SELECT COUNT(*) FROM dbo.ClinicalDataMigrationStagedProblem WHERE MigrationBatchUid=@MigrationBatchUid AND ValidationState=N'Warning');
 DECLARE @Failed INT=(SELECT COUNT(*) FROM dbo.ClinicalDataMigrationStagedPatient WHERE MigrationBatchUid=@MigrationBatchUid AND ValidationState=N'Invalid')+(SELECT COUNT(*) FROM dbo.ClinicalDataMigrationStagedProblem WHERE MigrationBatchUid=@MigrationBatchUid AND ValidationState=N'Invalid');
 DECLARE @Status NVARCHAR(30)=CASE WHEN @Failed>0 THEN N'ValidationFailed' ELSE N'Validated' END;
 UPDATE dbo.ClinicalDataMigrationBatch SET Status=@Status,ValidationCompletedAtUtc=SYSUTCDATETIME(),TotalRecords=@PatientTotal+@ProblemTotal,ValidRecords=@Valid,WarningRecords=@Warnings,FailedRecords=@Failed WHERE MigrationBatchUid=@MigrationBatchUid AND Status=N'Validating' AND RequestedBy=@RequestedBy;
 IF @@ROWCOUNT<>1 BEGIN ROLLBACK; THROW 52403,'Migration batch is not validating.',1; END;
 DECLARE @SourceSystem NVARCHAR(100),@Fingerprint CHAR(64);SELECT @SourceSystem=SourceSystem,@Fingerprint=PackageFingerprint FROM dbo.ClinicalDataMigrationBatch WHERE MigrationBatchUid=@MigrationBatchUid;
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt)
 VALUES(@RequestedBy,NULL,CASE WHEN @Status=N'Validated' THEN N'DataMigrationValidated' ELSE N'DataMigrationValidationFailed' END,N'ClinicalDataMigrationBatch',CONVERT(NVARCHAR(100),@MigrationBatchUid),(SELECT @SourceSystem SourceSystem,@Fingerprint PackageFingerprint,@Status Status,@PatientTotal+@ProblemTotal TotalRecords,@Valid ValidRecords,@Warnings WarningRecords,@Failed FailedRecords FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),SYSUTCDATETIME());
 COMMIT;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ClinicalDataMigration_GetBatch @MigrationBatchUid UNIQUEIDENTIFIER
AS
BEGIN
 SET NOCOUNT ON;
 SELECT MigrationBatchUid,SourceSystem,PackageUid,PackageFingerprint,Status,TotalRecords,ValidRecords,WarningRecords,FailedRecords FROM dbo.ClinicalDataMigrationBatch WHERE MigrationBatchUid=@MigrationBatchUid;
 SELECT N'Patient' RecordType,COUNT(*) TotalRecords,SUM(CASE WHEN ValidationState=N'Valid' THEN 1 ELSE 0 END) ValidRecords,SUM(CASE WHEN ValidationState=N'Warning' THEN 1 ELSE 0 END) WarningRecords,SUM(CASE WHEN ValidationState=N'Invalid' THEN 1 ELSE 0 END) FailedRecords FROM dbo.ClinicalDataMigrationStagedPatient WHERE MigrationBatchUid=@MigrationBatchUid
 UNION ALL SELECT N'Problem',COUNT(*),SUM(CASE WHEN ValidationState=N'Valid' THEN 1 ELSE 0 END),SUM(CASE WHEN ValidationState=N'Warning' THEN 1 ELSE 0 END),SUM(CASE WHEN ValidationState=N'Invalid' THEN 1 ELSE 0 END) FROM dbo.ClinicalDataMigrationStagedProblem WHERE MigrationBatchUid=@MigrationBatchUid;
 SELECT Code,COUNT(*) IssueCount FROM dbo.ClinicalDataMigrationValidationIssue WHERE MigrationBatchUid=@MigrationBatchUid GROUP BY Code ORDER BY Code;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ClinicalDataMigration_ListIssues @MigrationBatchUid UNIQUEIDENTIFIER,@Skip INT,@Take INT
AS
BEGIN SET NOCOUNT ON; SELECT Code,Severity,RecordType,SourceObjectId,Message,CreatedAtUtc FROM dbo.ClinicalDataMigrationValidationIssue WHERE MigrationBatchUid=@MigrationBatchUid ORDER BY ValidationIssueId OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY; END;
GO
