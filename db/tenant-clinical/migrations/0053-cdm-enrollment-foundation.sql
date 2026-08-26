CREATE TABLE dbo.ChronicDiseaseEnrollment
(
    ChronicDiseaseEnrollmentId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ChronicDiseaseEnrollment PRIMARY KEY,
    ChronicDiseaseEnrollmentUid UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ChronicDiseaseEnrollment_Uid DEFAULT NEWSEQUENTIALID(),
    PatientUid UNIQUEIDENTIFIER NOT NULL,
    PatientProblemUid UNIQUEIDENTIFIER NOT NULL,
    ProgramKey NVARCHAR(100) NOT NULL,
    ProgramVersion INT NOT NULL,
    ProgramName NVARCHAR(200) NOT NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_ChronicDiseaseEnrollment_Status DEFAULT N'Active',
    EnrolledBy BIGINT NOT NULL,
    EnrolledAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_ChronicDiseaseEnrollment_EnrolledAt DEFAULT SYSUTCDATETIME(),
    InactivatedBy BIGINT NULL,
    InactivatedAtUtc DATETIME2(0) NULL,
    InactivationReason NVARCHAR(500) NULL,
    UpdatedAtUtc DATETIME2(0) NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT UQ_ChronicDiseaseEnrollment_Uid UNIQUE (ChronicDiseaseEnrollmentUid),
    CONSTRAINT FK_ChronicDiseaseEnrollment_Patient FOREIGN KEY (PatientUid) REFERENCES dbo.Patient(PatientUid),
    CONSTRAINT FK_ChronicDiseaseEnrollment_Problem FOREIGN KEY (PatientProblemUid) REFERENCES dbo.PatientProblem(PatientProblemUid),
    CONSTRAINT FK_ChronicDiseaseEnrollment_EnrolledBy FOREIGN KEY (EnrolledBy) REFERENCES dbo.ApplicationUser(UserId),
    CONSTRAINT FK_ChronicDiseaseEnrollment_InactivatedBy FOREIGN KEY (InactivatedBy) REFERENCES dbo.ApplicationUser(UserId),
    CONSTRAINT CK_ChronicDiseaseEnrollment_ProgramVersion CHECK (ProgramVersion > 0),
    CONSTRAINT CK_ChronicDiseaseEnrollment_Status CHECK (Status IN (N'Active', N'Inactive')),
    CONSTRAINT CK_ChronicDiseaseEnrollment_InactiveState CHECK
    (
        (Status = N'Active' AND InactivatedBy IS NULL AND InactivatedAtUtc IS NULL AND InactivationReason IS NULL)
        OR (Status = N'Inactive' AND InactivatedBy IS NOT NULL AND InactivatedAtUtc IS NOT NULL)
    )
);
GO

CREATE UNIQUE INDEX UX_ChronicDiseaseEnrollment_ActiveProgram
ON dbo.ChronicDiseaseEnrollment(PatientUid, ProgramKey) WHERE Status = N'Active';
GO
CREATE INDEX IX_ChronicDiseaseEnrollment_PatientStatus
ON dbo.ChronicDiseaseEnrollment(PatientUid, Status, EnrolledAtUtc DESC);
GO

CREATE OR ALTER PROCEDURE dbo.ChronicDiseaseEnrollment_ListByPatient
    @PatientUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT e.ChronicDiseaseEnrollmentUid,e.PatientUid,e.PatientProblemUid,p.ProblemName,
        e.ProgramKey,e.ProgramVersion,e.ProgramName,e.Status,e.EnrolledBy,eu.DisplayName EnrolledByDisplayName,
        e.EnrolledAtUtc,e.InactivatedBy,e.InactivatedAtUtc,e.InactivationReason,e.RowVersion
    FROM dbo.ChronicDiseaseEnrollment e
    INNER JOIN dbo.PatientProblem p ON p.PatientProblemUid=e.PatientProblemUid AND p.PatientUid=e.PatientUid
    LEFT JOIN dbo.ApplicationUser eu ON eu.UserId=e.EnrolledBy
    WHERE e.PatientUid=@PatientUid
    ORDER BY CASE e.Status WHEN N'Active' THEN 0 ELSE 1 END,e.EnrolledAtUtc DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ChronicDiseaseEnrollment_GetByUid
    @PatientUid UNIQUEIDENTIFIER,@ChronicDiseaseEnrollmentUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT e.ChronicDiseaseEnrollmentUid,e.PatientUid,e.PatientProblemUid,p.ProblemName,
        e.ProgramKey,e.ProgramVersion,e.ProgramName,e.Status,e.EnrolledBy,eu.DisplayName EnrolledByDisplayName,
        e.EnrolledAtUtc,e.InactivatedBy,e.InactivatedAtUtc,e.InactivationReason,e.RowVersion
    FROM dbo.ChronicDiseaseEnrollment e
    INNER JOIN dbo.PatientProblem p ON p.PatientProblemUid=e.PatientProblemUid AND p.PatientUid=e.PatientUid
    LEFT JOIN dbo.ApplicationUser eu ON eu.UserId=e.EnrolledBy
    WHERE e.PatientUid=@PatientUid AND e.ChronicDiseaseEnrollmentUid=@ChronicDiseaseEnrollmentUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ChronicDiseaseEnrollment_Create
    @PatientUid UNIQUEIDENTIFIER,@PatientProblemUid UNIQUEIDENTIFIER,@ProgramKey NVARCHAR(100),
    @ProgramVersion INT,@ProgramName NVARCHAR(200),@EnrolledBy BIGINT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @PatientId BIGINT,@EnrollmentUid UNIQUEIDENTIFIER=NEWID();
    BEGIN TRANSACTION;
    SELECT @PatientId=PatientId FROM dbo.Patient WITH (UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND IsDeleted=0;
    IF @PatientId IS NULL BEGIN ROLLBACK; THROW 51530,'Patient not found.',1; END;
    IF NOT EXISTS(SELECT 1 FROM dbo.PatientProblem WITH (UPDLOCK,HOLDLOCK)
        WHERE PatientUid=@PatientUid AND PatientProblemUid=@PatientProblemUid AND ProblemStatus=N'Active')
        BEGIN ROLLBACK; THROW 51531,'An active Problem belonging to the patient is required.',1; END;
    IF EXISTS(SELECT 1 FROM dbo.ChronicDiseaseEnrollment WITH (UPDLOCK,HOLDLOCK)
        WHERE PatientUid=@PatientUid AND ProgramKey=@ProgramKey AND Status=N'Active')
        BEGIN ROLLBACK; THROW 51532,'The patient already has an active enrollment for this program.',1; END;
    INSERT dbo.ChronicDiseaseEnrollment(ChronicDiseaseEnrollmentUid,PatientUid,PatientProblemUid,ProgramKey,ProgramVersion,ProgramName,EnrolledBy)
    VALUES(@EnrollmentUid,@PatientUid,@PatientProblemUid,@ProgramKey,@ProgramVersion,@ProgramName,@EnrolledBy);
    INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
    VALUES(@EnrolledBy,@PatientId,N'CdmEnrollmentCreated',N'ChronicDiseaseEnrollment',CONVERT(NVARCHAR(100),@EnrollmentUid),NULL,
        CONCAT(N'ProgramKey=',@ProgramKey,N';Version=',@ProgramVersion,N';Status=Active'),SYSUTCDATETIME());
    COMMIT;
    EXEC dbo.ChronicDiseaseEnrollment_GetByUid @PatientUid,@EnrollmentUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ChronicDiseaseEnrollment_Inactivate
    @PatientUid UNIQUEIDENTIFIER,@ChronicDiseaseEnrollmentUid UNIQUEIDENTIFIER,@RowVersion BINARY(8),
    @InactivationReason NVARCHAR(500)=NULL,@InactivatedBy BIGINT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @PatientId BIGINT,@Status NVARCHAR(20);
    BEGIN TRANSACTION;
    SELECT @PatientId=p.PatientId,@Status=e.Status FROM dbo.ChronicDiseaseEnrollment e WITH (UPDLOCK,HOLDLOCK)
    INNER JOIN dbo.Patient p ON p.PatientUid=e.PatientUid AND p.IsDeleted=0
    WHERE e.PatientUid=@PatientUid AND e.ChronicDiseaseEnrollmentUid=@ChronicDiseaseEnrollmentUid;
    IF @PatientId IS NULL BEGIN ROLLBACK; RETURN; END;
    IF @Status<>N'Active' BEGIN ROLLBACK; THROW 51533,'Enrollment is already inactive.',1; END;
    UPDATE dbo.ChronicDiseaseEnrollment SET Status=N'Inactive',InactivatedBy=@InactivatedBy,
        InactivatedAtUtc=SYSUTCDATETIME(),InactivationReason=NULLIF(LTRIM(RTRIM(@InactivationReason)),N''),UpdatedAtUtc=SYSUTCDATETIME()
    WHERE PatientUid=@PatientUid AND ChronicDiseaseEnrollmentUid=@ChronicDiseaseEnrollmentUid AND RowVersion=@RowVersion;
    IF @@ROWCOUNT=0 BEGIN ROLLBACK; THROW 51534,'Enrollment changed; reload and try again.',1; END;
    INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
    VALUES(@InactivatedBy,@PatientId,N'CdmEnrollmentInactivated',N'ChronicDiseaseEnrollment',CONVERT(NVARCHAR(100),@ChronicDiseaseEnrollmentUid),N'Status=Active',N'Status=Inactive',SYSUTCDATETIME());
    COMMIT;
    EXEC dbo.ChronicDiseaseEnrollment_GetByUid @PatientUid,@ChronicDiseaseEnrollmentUid;
END;
GO
