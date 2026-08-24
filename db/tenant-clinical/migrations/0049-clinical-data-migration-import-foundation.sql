SET XACT_ABORT ON;
GO

ALTER TABLE dbo.ClinicalDataMigrationBatch DROP CONSTRAINT CK_ClinicalDataMigrationBatch_Status;
ALTER TABLE dbo.ClinicalDataMigrationBatch ADD CONSTRAINT CK_ClinicalDataMigrationBatch_Status
    CHECK(Status IN(N'Created',N'Validating',N'ValidationFailed',N'Validated',N'Importing',N'Imported',N'ImportFailed'));
ALTER TABLE dbo.ClinicalDataMigrationBatch ADD
    ImportStartedAtUtc DATETIME2(0) NULL,
    ImportCompletedAtUtc DATETIME2(0) NULL,
    ImportRequestedBy BIGINT NULL,
    MigrationActorUserId BIGINT NULL,
    AttemptedPatients INT NOT NULL CONSTRAINT DF_ClinicalDataMigrationBatch_AttemptedPatients DEFAULT 0,
    CreatedPatients INT NOT NULL CONSTRAINT DF_ClinicalDataMigrationBatch_CreatedPatients DEFAULT 0,
    ReusedPatients INT NOT NULL CONSTRAINT DF_ClinicalDataMigrationBatch_ReusedPatients DEFAULT 0,
    ImportedProblems INT NOT NULL CONSTRAINT DF_ClinicalDataMigrationBatch_ImportedProblems DEFAULT 0,
    SkippedRecords INT NOT NULL CONSTRAINT DF_ClinicalDataMigrationBatch_SkippedRecords DEFAULT 0,
    ImportFailedPatients INT NOT NULL CONSTRAINT DF_ClinicalDataMigrationBatch_ImportFailedPatients DEFAULT 0,
    CONSTRAINT FK_ClinicalDataMigrationBatch_ImportRequestedBy FOREIGN KEY(ImportRequestedBy) REFERENCES dbo.ApplicationUser(UserId),
    CONSTRAINT FK_ClinicalDataMigrationBatch_MigrationActor FOREIGN KEY(MigrationActorUserId) REFERENCES dbo.ApplicationUser(UserId);
GO

ALTER TABLE dbo.ClinicalDataMigrationStagedPatient ADD
    ImportStatus NVARCHAR(20) NOT NULL CONSTRAINT DF_ClinicalDataMigrationStagedPatient_ImportStatus DEFAULT N'Pending',
    ImportedAtUtc DATETIME2(0) NULL,
    ImportErrorCode NVARCHAR(100) NULL,
    ImportErrorMessage NVARCHAR(500) NULL,
    CONSTRAINT CK_ClinicalDataMigrationStagedPatient_ImportStatus CHECK(ImportStatus IN(N'Pending',N'Imported',N'Failed'));
GO

ALTER TABLE dbo.ClinicalDataMigrationStagedProblem ADD
    TargetPatientProblemUid UNIQUEIDENTIFIER NULL,
    ImportStatus NVARCHAR(20) NOT NULL CONSTRAINT DF_ClinicalDataMigrationStagedProblem_ImportStatus DEFAULT N'Pending',
    ImportedAtUtc DATETIME2(0) NULL,
    ImportErrorCode NVARCHAR(100) NULL,
    ImportErrorMessage NVARCHAR(500) NULL,
    CONSTRAINT CK_ClinicalDataMigrationStagedProblem_ImportStatus CHECK(ImportStatus IN(N'Pending',N'Imported',N'Failed'));
GO

CREATE TABLE dbo.ClinicalDataMigrationSourceMapping
(
    ClinicalDataMigrationSourceMappingId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ClinicalDataMigrationSourceMapping PRIMARY KEY,
    SourceMappingUid UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ClinicalDataMigrationSourceMapping_Uid DEFAULT NEWSEQUENTIALID(),
    MigrationBatchUid UNIQUEIDENTIFIER NOT NULL,
    SourceSystem NVARCHAR(100) NOT NULL,
    RecordType NVARCHAR(30) NOT NULL,
    SourceObjectId NVARCHAR(200) NOT NULL,
    SourcePatientId NVARCHAR(200) NOT NULL,
    TargetPatientUid UNIQUEIDENTIFIER NOT NULL,
    TargetObjectUid UNIQUEIDENTIFIER NOT NULL,
    SourceCreatedAt DATETIMEOFFSET(0) NULL,
    SourceUpdatedAt DATETIMEOFFSET(0) NULL,
    SourceAuthor NVARCHAR(200) NULL,
    ImportedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_ClinicalDataMigrationSourceMapping_ImportedAt DEFAULT SYSUTCDATETIME(),
    ImportedBy BIGINT NOT NULL,
    CONSTRAINT UQ_ClinicalDataMigrationSourceMapping_Uid UNIQUE(SourceMappingUid),
    CONSTRAINT UQ_ClinicalDataMigrationSourceMapping_Source UNIQUE(SourceSystem,RecordType,SourceObjectId),
    CONSTRAINT FK_ClinicalDataMigrationSourceMapping_Batch FOREIGN KEY(MigrationBatchUid) REFERENCES dbo.ClinicalDataMigrationBatch(MigrationBatchUid),
    CONSTRAINT FK_ClinicalDataMigrationSourceMapping_Patient FOREIGN KEY(TargetPatientUid) REFERENCES dbo.Patient(PatientUid),
    CONSTRAINT FK_ClinicalDataMigrationSourceMapping_ImportedBy FOREIGN KEY(ImportedBy) REFERENCES dbo.ApplicationUser(UserId),
    CONSTRAINT CK_ClinicalDataMigrationSourceMapping_Type CHECK(RecordType IN(N'Patient',N'Problem'))
);
GO
CREATE INDEX IX_ClinicalDataMigrationSourceMapping_Batch
ON dbo.ClinicalDataMigrationSourceMapping(MigrationBatchUid,RecordType,SourcePatientId);
GO

IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE Username=N'system-data-migration')
    INSERT dbo.ApplicationUser(Username,DisplayName,Email,ProviderId,IsActive,CreatedAt,AuthSubjectId)
    VALUES(N'system-data-migration',N'Clinical Data Migration Service',NULL,NULL,1,SYSUTCDATETIME(),NULL);
IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE Username=N'system-data-migration' AND AuthSubjectId IS NULL)
    THROW 52505,'The reserved migration service actor username is already assigned to an interactive identity.',1;
GO

CREATE OR ALTER PROCEDURE dbo.ClinicalDataMigration_ImportValidatedBatch
    @MigrationBatchUid UNIQUEIDENTIFIER,
    @InitiatingOperator BIGINT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @LockResult INT,@LockResource NVARCHAR(255)=N'ClinicalDataMigrationImport:'+CONVERT(NVARCHAR(36),@MigrationBatchUid);
    EXEC @LockResult=sys.sp_getapplock @Resource=@LockResource,@LockMode=N'Exclusive',@LockOwner=N'Session',@LockTimeout=0;
    IF @LockResult<0 THROW 52500,'The migration batch is already being imported.',1;
    BEGIN TRY
        DECLARE @Status NVARCHAR(30),@RequestedBy BIGINT,@Fingerprint CHAR(64),@SourceSystem NVARCHAR(100),@MigrationActor BIGINT,@FirstStart BIT=0;
        SELECT @Status=Status,@RequestedBy=RequestedBy,@Fingerprint=PackageFingerprint,@SourceSystem=SourceSystem
        FROM dbo.ClinicalDataMigrationBatch WITH(UPDLOCK,HOLDLOCK) WHERE MigrationBatchUid=@MigrationBatchUid;
        IF @Status IS NULL THROW 52501,'The migration batch was not found.',1;
        IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@InitiatingOperator AND IsActive=1) THROW 52502,'The initiating migration operator is not active.',1;
        IF @Status=N'Imported' BEGIN EXEC sys.sp_releaseapplock @Resource=@LockResource,@LockOwner=N'Session'; EXEC dbo.ClinicalDataMigration_GetImportResult @MigrationBatchUid,1; RETURN; END;
        IF @Status NOT IN(N'Validated',N'ImportFailed',N'Importing') THROW 52503,'Only a successfully validated migration batch can be imported.',1;
        IF EXISTS(SELECT 1 FROM dbo.ClinicalDataMigrationStagedPatient WHERE MigrationBatchUid=@MigrationBatchUid AND (ValidationState=N'Invalid' OR MappingStatus IN(N'RequiresReview',N'Invalid'))) THROW 52504,'The migration batch contains unresolved patient mappings.',1;
        IF EXISTS(SELECT 1 FROM dbo.ClinicalDataMigrationStagedProblem WHERE MigrationBatchUid=@MigrationBatchUid AND ValidationState<>N'Valid') THROW 52504,'The migration batch contains unresolved problem records.',1;
        SELECT @MigrationActor=UserId FROM dbo.ApplicationUser WHERE Username=N'system-data-migration' AND IsActive=1 AND AuthSubjectId IS NULL;
        IF @MigrationActor IS NULL THROW 52505,'The migration service actor is unavailable.',1;
        IF @Status<>N'Importing'
        BEGIN
            SET @FirstStart=CASE WHEN EXISTS(SELECT 1 FROM dbo.ClinicalDataMigrationBatch WHERE MigrationBatchUid=@MigrationBatchUid AND ImportStartedAtUtc IS NULL) THEN 1 ELSE 0 END;
            UPDATE dbo.ClinicalDataMigrationBatch SET Status=N'Importing',ImportStartedAtUtc=COALESCE(ImportStartedAtUtc,SYSUTCDATETIME()),ImportCompletedAtUtc=NULL,ImportRequestedBy=COALESCE(ImportRequestedBy,@InitiatingOperator),MigrationActorUserId=@MigrationActor WHERE MigrationBatchUid=@MigrationBatchUid;
            IF @FirstStart=1 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt)
                VALUES(@InitiatingOperator,NULL,N'DataMigrationStarted',N'ClinicalDataMigrationBatch',CONVERT(NVARCHAR(100),@MigrationBatchUid),(SELECT @SourceSystem SourceSystem,@Fingerprint PackageFingerprint,N'Importing' Status FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),SYSUTCDATETIME());
        END;

        DECLARE patient_cursor CURSOR LOCAL FAST_FORWARD FOR
            SELECT StagedPatientUid FROM dbo.ClinicalDataMigrationStagedPatient WHERE MigrationBatchUid=@MigrationBatchUid AND ImportStatus<>N'Imported' ORDER BY StagedPatientId;
        DECLARE @StagedPatientUid UNIQUEIDENTIFIER;
        OPEN patient_cursor; FETCH NEXT FROM patient_cursor INTO @StagedPatientUid;
        WHILE @@FETCH_STATUS=0
        BEGIN
            BEGIN TRY
                BEGIN TRANSACTION;
                DECLARE @MappingStatus NVARCHAR(30),@TargetPatientUid UNIQUEIDENTIFIER,@SourceObjectId NVARCHAR(200),@SourcePatientId NVARCHAR(200),
                    @HealthCardNumber NVARCHAR(50),@HealthCardVersion NVARCHAR(10),@FirstName NVARCHAR(100),@MiddleName NVARCHAR(100),@LastName NVARCHAR(100),@DateOfBirth DATE,
                    @SexAtBirth NVARCHAR(20),@GenderIdentity NVARCHAR(50),@PreferredName NVARCHAR(100),@PhoneNumber NVARCHAR(30),@AlternatePhoneNumber NVARCHAR(30),@Email NVARCHAR(255),
                    @AddressLine1 NVARCHAR(255),@AddressLine2 NVARCHAR(255),@City NVARCHAR(100),@Province NVARCHAR(50),@PostalCode NVARCHAR(20),@CountryCode NVARCHAR(2),
                    @SourceCreatedAt DATETIMEOFFSET(0),@SourceUpdatedAt DATETIMEOFFSET(0),@SourceAuthor NVARCHAR(200),@PatientId BIGINT,@CreatedPatient BIT=0;
                SELECT @MappingStatus=MappingStatus,@TargetPatientUid=TargetPatientUid,@SourceObjectId=SourceObjectId,@SourcePatientId=SourcePatientId,
                    @HealthCardNumber=HealthCardNumber,@HealthCardVersion=HealthCardVersion,@FirstName=FirstName,@MiddleName=MiddleName,@LastName=LastName,@DateOfBirth=DateOfBirth,
                    @SexAtBirth=SexAtBirth,@GenderIdentity=GenderIdentity,@PreferredName=PreferredName,@PhoneNumber=PhoneNumber,@AlternatePhoneNumber=AlternatePhoneNumber,@Email=Email,
                    @AddressLine1=AddressLine1,@AddressLine2=AddressLine2,@City=City,@Province=Province,@PostalCode=PostalCode,@CountryCode=CountryCode,
                    @SourceCreatedAt=SourceCreatedAt,@SourceUpdatedAt=SourceUpdatedAt,@SourceAuthor=SourceAuthor
                FROM dbo.ClinicalDataMigrationStagedPatient WITH(UPDLOCK,HOLDLOCK) WHERE MigrationBatchUid=@MigrationBatchUid AND StagedPatientUid=@StagedPatientUid AND ValidationState=N'Valid';
                IF @SourcePatientId IS NULL THROW 52506,'The staged patient is not import eligible.',1;
                DECLARE @ExistingMapped UNIQUEIDENTIFIER=(SELECT TargetObjectUid FROM dbo.ClinicalDataMigrationSourceMapping WHERE SourceSystem=@SourceSystem AND RecordType=N'Patient' AND SourceObjectId=@SourceObjectId);
                IF @ExistingMapped IS NOT NULL BEGIN SET @TargetPatientUid=@ExistingMapped;SET @MappingStatus=N'MappedExisting';END
                ELSE IF @MappingStatus=N'ReadyToCreate'
                BEGIN
                    SET @TargetPatientUid=NEWID();DECLARE @ChartNumber NVARCHAR(50)=N'P-'+UPPER(LEFT(REPLACE(CONVERT(NVARCHAR(36),@TargetPatientUid),N'-',N''),16));
                    INSERT dbo.Patient(PatientUid,ChartNumber,HealthCardNumber,HealthCardVersion,FirstName,MiddleName,LastName,DateOfBirth,SexAtBirth,GenderIdentity,PreferredName,PhoneNumber,AlternatePhoneNumber,Email,AddressLine1,AddressLine2,City,Province,PostalCode,CountryCode,IsActive,IsDeleted,CreatedAt,CreatedBy)
                    VALUES(@TargetPatientUid,@ChartNumber,NULLIF(LTRIM(RTRIM(@HealthCardNumber)),N''),NULLIF(LTRIM(RTRIM(@HealthCardVersion)),N''),LTRIM(RTRIM(@FirstName)),NULLIF(LTRIM(RTRIM(@MiddleName)),N''),LTRIM(RTRIM(@LastName)),@DateOfBirth,NULLIF(LTRIM(RTRIM(@SexAtBirth)),N''),NULLIF(LTRIM(RTRIM(@GenderIdentity)),N''),NULLIF(LTRIM(RTRIM(@PreferredName)),N''),NULLIF(LTRIM(RTRIM(@PhoneNumber)),N''),NULLIF(LTRIM(RTRIM(@AlternatePhoneNumber)),N''),NULLIF(LTRIM(RTRIM(@Email)),N''),NULLIF(LTRIM(RTRIM(@AddressLine1)),N''),NULLIF(LTRIM(RTRIM(@AddressLine2)),N''),NULLIF(LTRIM(RTRIM(@City)),N''),NULLIF(LTRIM(RTRIM(@Province)),N''),NULLIF(LTRIM(RTRIM(@PostalCode)),N''),COALESCE(NULLIF(LTRIM(RTRIM(@CountryCode)),N''),N'CA'),1,0,SYSUTCDATETIME(),@MigrationActor);
                    SET @PatientId=SCOPE_IDENTITY();SET @CreatedPatient=1;
                    INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt) VALUES(@MigrationActor,@PatientId,N'MigrationCreate',N'Patient',CONVERT(NVARCHAR(100),@TargetPatientUid),N'Created through controlled clinical migration.',SYSUTCDATETIME());
                END
                ELSE IF @MappingStatus=N'MappedExisting'
                BEGIN SELECT @PatientId=PatientId FROM dbo.Patient WHERE PatientUid=@TargetPatientUid AND IsDeleted=0;IF @PatientId IS NULL THROW 52507,'The approved target patient is unavailable.',1;END
                ELSE THROW 52504,'The patient mapping is unresolved.',1;
                IF @PatientId IS NULL SELECT @PatientId=PatientId FROM dbo.Patient WHERE PatientUid=@TargetPatientUid AND IsDeleted=0;
                IF @ExistingMapped IS NULL INSERT dbo.ClinicalDataMigrationSourceMapping(MigrationBatchUid,SourceSystem,RecordType,SourceObjectId,SourcePatientId,TargetPatientUid,TargetObjectUid,SourceCreatedAt,SourceUpdatedAt,SourceAuthor,ImportedBy)
                    VALUES(@MigrationBatchUid,@SourceSystem,N'Patient',@SourceObjectId,@SourcePatientId,@TargetPatientUid,@TargetPatientUid,@SourceCreatedAt,@SourceUpdatedAt,@SourceAuthor,@MigrationActor);
                UPDATE dbo.ClinicalDataMigrationStagedPatient SET TargetPatientUid=@TargetPatientUid,MappingStatus=@MappingStatus,ImportStatus=N'Imported',ImportedAtUtc=SYSUTCDATETIME(),ImportErrorCode=NULL,ImportErrorMessage=NULL WHERE StagedPatientUid=@StagedPatientUid;

                DECLARE problem_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT StagedProblemUid FROM dbo.ClinicalDataMigrationStagedProblem WHERE MigrationBatchUid=@MigrationBatchUid AND SourcePatientId=@SourcePatientId AND ImportStatus<>N'Imported' ORDER BY StagedProblemId;
                DECLARE @StagedProblemUid UNIQUEIDENTIFIER;OPEN problem_cursor;FETCH NEXT FROM problem_cursor INTO @StagedProblemUid;
                WHILE @@FETCH_STATUS=0
                BEGIN
                    DECLARE @ProblemSourceId NVARCHAR(200),@ProblemName NVARCHAR(200),@ProblemDescription NVARCHAR(1000),@OnsetDate DATE,@ProblemStatus NVARCHAR(20),@ResolvedDate DATE,@ProblemSourceCreated DATETIMEOFFSET(0),@ProblemSourceUpdated DATETIMEOFFSET(0),@ProblemSourceAuthor NVARCHAR(200),@TargetProblemUid UNIQUEIDENTIFIER;
                    SELECT @ProblemSourceId=SourceObjectId,@ProblemName=ProblemName,@ProblemDescription=ProblemDescription,@OnsetDate=OnsetDate,@ProblemStatus=ProblemStatus,@ResolvedDate=ResolvedDate,@ProblemSourceCreated=SourceCreatedAt,@ProblemSourceUpdated=SourceUpdatedAt,@ProblemSourceAuthor=SourceAuthor FROM dbo.ClinicalDataMigrationStagedProblem WITH(UPDLOCK,HOLDLOCK) WHERE StagedProblemUid=@StagedProblemUid AND ValidationState=N'Valid';
                    SELECT @TargetProblemUid=TargetObjectUid FROM dbo.ClinicalDataMigrationSourceMapping WHERE SourceSystem=@SourceSystem AND RecordType=N'Problem' AND SourceObjectId=@ProblemSourceId;
                    IF @TargetProblemUid IS NULL
                    BEGIN
                        SET @TargetProblemUid=NEWID();
                        INSERT dbo.PatientProblem(PatientProblemUid,PatientUid,ProblemName,ProblemDescription,OnsetDate,ProblemStatus,ResolvedAt,ResolvedBy,CreatedAt,CreatedBy)
                        VALUES(@TargetProblemUid,@TargetPatientUid,LTRIM(RTRIM(@ProblemName)),NULLIF(LTRIM(RTRIM(@ProblemDescription)),N''),@OnsetDate,@ProblemStatus,CASE WHEN @ProblemStatus=N'Resolved' THEN CONVERT(DATETIME2(0),@ResolvedDate) END,CASE WHEN @ProblemStatus=N'Resolved' THEN @MigrationActor END,SYSUTCDATETIME(),@MigrationActor);
                        INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt) VALUES(@MigrationActor,@PatientId,N'MigrationCreate',N'PatientProblem',CONVERT(NVARCHAR(100),@TargetProblemUid),N'Created through controlled clinical migration.',SYSUTCDATETIME());
                        INSERT dbo.ClinicalDataMigrationSourceMapping(MigrationBatchUid,SourceSystem,RecordType,SourceObjectId,SourcePatientId,TargetPatientUid,TargetObjectUid,SourceCreatedAt,SourceUpdatedAt,SourceAuthor,ImportedBy)
                        VALUES(@MigrationBatchUid,@SourceSystem,N'Problem',@ProblemSourceId,@SourcePatientId,@TargetPatientUid,@TargetProblemUid,@ProblemSourceCreated,@ProblemSourceUpdated,@ProblemSourceAuthor,@MigrationActor);
                    END
                    ELSE IF NOT EXISTS(SELECT 1 FROM dbo.PatientProblem WHERE PatientProblemUid=@TargetProblemUid AND PatientUid=@TargetPatientUid) THROW 52508,'The existing source problem mapping is inconsistent.',1;
                    UPDATE dbo.ClinicalDataMigrationStagedProblem SET TargetPatientProblemUid=@TargetProblemUid,ImportStatus=N'Imported',ImportedAtUtc=SYSUTCDATETIME(),ImportErrorCode=NULL,ImportErrorMessage=NULL WHERE StagedProblemUid=@StagedProblemUid;
                    FETCH NEXT FROM problem_cursor INTO @StagedProblemUid;
                END
                CLOSE problem_cursor;DEALLOCATE problem_cursor;COMMIT;
            END TRY
            BEGIN CATCH
                IF CURSOR_STATUS('local','problem_cursor')>=0 CLOSE problem_cursor;IF CURSOR_STATUS('local','problem_cursor')>-3 DEALLOCATE problem_cursor;
                IF XACT_STATE()<>0 ROLLBACK;
                UPDATE dbo.ClinicalDataMigrationStagedPatient SET ImportStatus=N'Failed',ImportErrorCode=N'PatientAggregateImportFailed',ImportErrorMessage=N'The patient aggregate could not be imported.' WHERE StagedPatientUid=@StagedPatientUid;
                UPDATE p SET ImportStatus=N'Failed',ImportErrorCode=N'PatientAggregateImportFailed',ImportErrorMessage=N'The patient aggregate could not be imported.' FROM dbo.ClinicalDataMigrationStagedProblem p JOIN dbo.ClinicalDataMigrationStagedPatient s ON s.MigrationBatchUid=p.MigrationBatchUid AND s.SourcePatientId=p.SourcePatientId WHERE s.StagedPatientUid=@StagedPatientUid AND p.ImportStatus<>N'Imported';
            END CATCH
            FETCH NEXT FROM patient_cursor INTO @StagedPatientUid;
        END
        CLOSE patient_cursor;DEALLOCATE patient_cursor;
        DECLARE @Attempted INT=(SELECT COUNT(*) FROM dbo.ClinicalDataMigrationStagedPatient WHERE MigrationBatchUid=@MigrationBatchUid),
            @Created INT=(SELECT COUNT(*) FROM dbo.ClinicalDataMigrationStagedPatient WHERE MigrationBatchUid=@MigrationBatchUid AND MappingStatus=N'ReadyToCreate' AND ImportStatus=N'Imported'),
            @Reused INT=(SELECT COUNT(*) FROM dbo.ClinicalDataMigrationStagedPatient WHERE MigrationBatchUid=@MigrationBatchUid AND MappingStatus=N'MappedExisting' AND ImportStatus=N'Imported'),
            @Problems INT=(SELECT COUNT(*) FROM dbo.ClinicalDataMigrationStagedProblem WHERE MigrationBatchUid=@MigrationBatchUid AND ImportStatus=N'Imported'),
            @FailedPatients INT=(SELECT COUNT(*) FROM dbo.ClinicalDataMigrationStagedPatient WHERE MigrationBatchUid=@MigrationBatchUid AND ImportStatus=N'Failed');
        DECLARE @FinalStatus NVARCHAR(30)=CASE WHEN @FailedPatients>0 THEN N'ImportFailed' ELSE N'Imported' END;
        UPDATE dbo.ClinicalDataMigrationBatch SET Status=@FinalStatus,ImportCompletedAtUtc=SYSUTCDATETIME(),AttemptedPatients=@Attempted,CreatedPatients=@Created,ReusedPatients=@Reused,ImportedProblems=@Problems,SkippedRecords=0,ImportFailedPatients=@FailedPatients WHERE MigrationBatchUid=@MigrationBatchUid;
        INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt) VALUES(@InitiatingOperator,NULL,CASE WHEN @FinalStatus=N'Imported' THEN N'DataMigrationCompleted' ELSE N'DataMigrationFailed' END,N'ClinicalDataMigrationBatch',CONVERT(NVARCHAR(100),@MigrationBatchUid),(SELECT @SourceSystem SourceSystem,@Fingerprint PackageFingerprint,@FinalStatus Status,@Attempted AttemptedPatients,@Created CreatedPatients,@Reused ReusedPatients,@Problems ImportedProblems,@FailedPatients FailedPatients FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),SYSUTCDATETIME());
        EXEC sys.sp_releaseapplock @Resource=@LockResource,@LockOwner=N'Session';EXEC dbo.ClinicalDataMigration_GetImportResult @MigrationBatchUid,0;
    END TRY
    BEGIN CATCH
        IF CURSOR_STATUS('local','patient_cursor')>=0 CLOSE patient_cursor;IF CURSOR_STATUS('local','patient_cursor')>-3 DEALLOCATE patient_cursor;
        IF XACT_STATE()<>0 ROLLBACK;
        IF EXISTS(SELECT 1 FROM dbo.ClinicalDataMigrationBatch WHERE MigrationBatchUid=@MigrationBatchUid AND Status=N'Importing')
        BEGIN
            UPDATE dbo.ClinicalDataMigrationBatch SET Status=N'ImportFailed',ImportCompletedAtUtc=SYSUTCDATETIME() WHERE MigrationBatchUid=@MigrationBatchUid;
            INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt)
            VALUES(@InitiatingOperator,NULL,N'DataMigrationFailed',N'ClinicalDataMigrationBatch',CONVERT(NVARCHAR(100),@MigrationBatchUid),(SELECT @SourceSystem SourceSystem,@Fingerprint PackageFingerprint,N'ImportFailed' Status,N'UnexpectedImportFailure' ErrorCode FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),SYSUTCDATETIME());
        END
        EXEC sys.sp_releaseapplock @Resource=@LockResource,@LockOwner=N'Session';THROW;
    END CATCH
END;
GO

CREATE OR ALTER PROCEDURE dbo.ClinicalDataMigration_GetImportResult @MigrationBatchUid UNIQUEIDENTIFIER,@Replay BIT=0
AS
BEGIN SET NOCOUNT ON;SELECT MigrationBatchUid,Status,AttemptedPatients,CreatedPatients,ReusedPatients,ImportedProblems,SkippedRecords,ImportFailedPatients,@Replay Replayed FROM dbo.ClinicalDataMigrationBatch WHERE MigrationBatchUid=@MigrationBatchUid;END;
GO
