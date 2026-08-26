CREATE TABLE dbo.CdsAlert
(
    CdsAlertUid UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CdsAlert_Uid DEFAULT NEWSEQUENTIALID(),
    PatientUid UNIQUEIDENTIFIER NOT NULL,
    RuleKey NVARCHAR(100) NOT NULL,
    RuleVersion INT NOT NULL,
    FindingFingerprint CHAR(64) NOT NULL,
    Severity NVARCHAR(20) NOT NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_CdsAlert_Status DEFAULT N'Active',
    Title NVARCHAR(200) NOT NULL,
    Explanation NVARCHAR(1000) NOT NULL,
    SuggestedAction NVARCHAR(1000) NOT NULL,
    RuleSourceReference NVARCHAR(500) NULL,
    FirstDetectedAtUtc DATETIME2(0) NOT NULL,
    LastEvaluatedAtUtc DATETIME2(0) NOT NULL,
    AcknowledgedBy BIGINT NULL,
    AcknowledgedAtUtc DATETIME2(0) NULL,
    DismissedBy BIGINT NULL,
    DismissedAtUtc DATETIME2(0) NULL,
    DismissReasonCode NVARCHAR(50) NULL,
    DismissComment NVARCHAR(500) NULL,
    ResolvedAtUtc DATETIME2(0) NULL,
    CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_CdsAlert_Created DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc DATETIME2(0) NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_CdsAlert PRIMARY KEY (CdsAlertUid),
    CONSTRAINT FK_CdsAlert_Patient FOREIGN KEY (PatientUid) REFERENCES dbo.Patient(PatientUid),
    CONSTRAINT FK_CdsAlert_AcknowledgedBy FOREIGN KEY (AcknowledgedBy) REFERENCES dbo.ApplicationUser(UserId),
    CONSTRAINT FK_CdsAlert_DismissedBy FOREIGN KEY (DismissedBy) REFERENCES dbo.ApplicationUser(UserId),
    CONSTRAINT CK_CdsAlert_RuleVersion CHECK (RuleVersion > 0),
    CONSTRAINT CK_CdsAlert_Fingerprint CHECK (FindingFingerprint NOT LIKE '%[^0-9a-f]%' AND LEN(FindingFingerprint) = 64),
    CONSTRAINT CK_CdsAlert_Severity CHECK (Severity IN (N'Info', N'Warning')),
    CONSTRAINT CK_CdsAlert_Status CHECK (Status IN (N'Active', N'Acknowledged', N'Dismissed', N'Resolved')),
    CONSTRAINT CK_CdsAlert_DismissReason CHECK
    (
        (Status <> N'Dismissed' AND DismissReasonCode IS NULL AND DismissedBy IS NULL AND DismissedAtUtc IS NULL)
        OR
        (Status = N'Dismissed' AND DismissReasonCode IN (N'NotApplicable', N'AlreadyAddressed', N'DuplicateFinding', N'Other')
         AND DismissedBy IS NOT NULL AND DismissedAtUtc IS NOT NULL)
    ),
    CONSTRAINT UQ_CdsAlert_Finding UNIQUE (PatientUid, RuleKey, RuleVersion, FindingFingerprint)
);
GO

CREATE INDEX IX_CdsAlert_Patient_Status
ON dbo.CdsAlert (PatientUid, Status, Severity, FirstDetectedAtUtc);
GO

CREATE TABLE dbo.CdsAlertHistory
(
    CdsAlertHistoryUid UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CdsAlertHistory_Uid DEFAULT NEWSEQUENTIALID(),
    CdsAlertUid UNIQUEIDENTIFIER NOT NULL,
    PatientUid UNIQUEIDENTIFIER NOT NULL,
    EventType NVARCHAR(30) NOT NULL,
    ActorUserId BIGINT NULL,
    OccurredAtUtc DATETIME2(0) NOT NULL,
    ReasonCode NVARCHAR(50) NULL,
    Comment NVARCHAR(500) NULL,
    RuleKey NVARCHAR(100) NOT NULL,
    RuleVersion INT NOT NULL,
    CONSTRAINT PK_CdsAlertHistory PRIMARY KEY (CdsAlertHistoryUid),
    CONSTRAINT FK_CdsAlertHistory_Alert FOREIGN KEY (CdsAlertUid) REFERENCES dbo.CdsAlert(CdsAlertUid),
    CONSTRAINT FK_CdsAlertHistory_Patient FOREIGN KEY (PatientUid) REFERENCES dbo.Patient(PatientUid),
    CONSTRAINT FK_CdsAlertHistory_Actor FOREIGN KEY (ActorUserId) REFERENCES dbo.ApplicationUser(UserId),
    CONSTRAINT CK_CdsAlertHistory_Event CHECK (EventType IN (N'Detected', N'Retriggered', N'Acknowledged', N'Dismissed', N'Resolved'))
);
GO

CREATE INDEX IX_CdsAlertHistory_Alert_Occurred
ON dbo.CdsAlertHistory (CdsAlertUid, OccurredAtUtc, CdsAlertHistoryUid);
GO

CREATE TRIGGER dbo.CdsAlertHistory_AppendOnly
ON dbo.CdsAlertHistory
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    THROW 51400, 'CDS alert history is append-only.', 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.CdsAlert_PatientExists
    @PatientUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CONVERT(BIT, CASE WHEN EXISTS
    (
        SELECT 1 FROM dbo.Patient WHERE PatientUid = @PatientUid AND IsDeleted = 0
    ) THEN 1 ELSE 0 END) AS PatientExists;
END;
GO

CREATE OR ALTER PROCEDURE dbo.CdsAlert_List
    @PatientUid UNIQUEIDENTIFIER,
    @IncludeHistory BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT *
    FROM dbo.CdsAlert
    WHERE PatientUid = @PatientUid
      AND (@IncludeHistory = 1 OR Status IN (N'Active', N'Acknowledged'))
    ORDER BY CASE Severity WHEN N'Warning' THEN 0 ELSE 1 END,
             FirstDetectedAtUtc DESC, CdsAlertUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.CdsAlertHistory_List
    @PatientUid UNIQUEIDENTIFIER,
    @CdsAlertUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT h.*, u.DisplayName AS ActorDisplayName
    FROM dbo.CdsAlertHistory h
    LEFT JOIN dbo.ApplicationUser u ON u.UserId = h.ActorUserId
    WHERE h.PatientUid = @PatientUid AND h.CdsAlertUid = @CdsAlertUid
    ORDER BY h.OccurredAtUtc, h.CdsAlertHistoryUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.CdsAlert_RecordFinding
    @PatientUid UNIQUEIDENTIFIER,
    @RuleKey NVARCHAR(100),
    @RuleVersion INT,
    @FindingFingerprint CHAR(64),
    @Severity NVARCHAR(20),
    @Title NVARCHAR(200),
    @Explanation NVARCHAR(1000),
    @SuggestedAction NVARCHAR(1000),
    @RuleSourceReference NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Patient WHERE PatientUid = @PatientUid AND IsDeleted = 0)
        THROW 51404, 'Patient not found.', 1;

    DECLARE @Now DATETIME2(0) = SYSUTCDATETIME();
    DECLARE @AlertUid UNIQUEIDENTIFIER;
    DECLARE @Status NVARCHAR(20);
    DECLARE @ResolvedFindings TABLE
    (
        CdsAlertUid UNIQUEIDENTIFIER NOT NULL,
        PatientUid UNIQUEIDENTIFIER NOT NULL,
        RuleKey NVARCHAR(100) NOT NULL,
        RuleVersion INT NOT NULL
    );
    BEGIN TRANSACTION;

    SELECT @AlertUid = CdsAlertUid, @Status = Status
    FROM dbo.CdsAlert WITH (UPDLOCK, HOLDLOCK)
    WHERE PatientUid = @PatientUid AND RuleKey = @RuleKey
      AND RuleVersion = @RuleVersion AND FindingFingerprint = @FindingFingerprint;

    UPDATE dbo.CdsAlert
    SET Status = N'Resolved', ResolvedAtUtc = @Now, UpdatedAtUtc = @Now
    OUTPUT inserted.CdsAlertUid, inserted.PatientUid, inserted.RuleKey, inserted.RuleVersion
    INTO @ResolvedFindings (CdsAlertUid, PatientUid, RuleKey, RuleVersion)
    WHERE PatientUid = @PatientUid AND RuleKey = @RuleKey
      AND Status IN (N'Active', N'Acknowledged')
      AND NOT (RuleVersion = @RuleVersion AND FindingFingerprint = @FindingFingerprint);

    INSERT dbo.CdsAlertHistory
        (CdsAlertUid, PatientUid, EventType, ActorUserId, OccurredAtUtc,
         ReasonCode, Comment, RuleKey, RuleVersion)
    SELECT CdsAlertUid, PatientUid, N'Resolved', NULL, @Now,
           NULL, NULL, RuleKey, RuleVersion
    FROM @ResolvedFindings;

    IF @AlertUid IS NULL
    BEGIN
        SET @AlertUid = NEWID();
        INSERT dbo.CdsAlert
            (CdsAlertUid, PatientUid, RuleKey, RuleVersion, FindingFingerprint, Severity,
             Status, Title, Explanation, SuggestedAction, RuleSourceReference,
             FirstDetectedAtUtc, LastEvaluatedAtUtc, CreatedAtUtc)
        VALUES
            (@AlertUid, @PatientUid, @RuleKey, @RuleVersion, @FindingFingerprint, @Severity,
             N'Active', @Title, @Explanation, @SuggestedAction, @RuleSourceReference,
             @Now, @Now, @Now);
        INSERT dbo.CdsAlertHistory
            (CdsAlertUid, PatientUid, EventType, OccurredAtUtc, RuleKey, RuleVersion)
        VALUES (@AlertUid, @PatientUid, N'Detected', @Now, @RuleKey, @RuleVersion);
    END
    ELSE IF @Status = N'Resolved'
    BEGIN
        UPDATE dbo.CdsAlert
        SET Status = N'Active', Severity = @Severity, Title = @Title, Explanation = @Explanation,
            SuggestedAction = @SuggestedAction, RuleSourceReference = @RuleSourceReference,
            LastEvaluatedAtUtc = @Now, AcknowledgedBy = NULL, AcknowledgedAtUtc = NULL,
            DismissedBy = NULL, DismissedAtUtc = NULL, DismissReasonCode = NULL,
            DismissComment = NULL, ResolvedAtUtc = NULL, UpdatedAtUtc = @Now
        WHERE CdsAlertUid = @AlertUid;
        INSERT dbo.CdsAlertHistory
            (CdsAlertUid, PatientUid, EventType, OccurredAtUtc, RuleKey, RuleVersion)
        VALUES (@AlertUid, @PatientUid, N'Retriggered', @Now, @RuleKey, @RuleVersion);
    END
    ELSE
    BEGIN
        UPDATE dbo.CdsAlert SET LastEvaluatedAtUtc = @Now, UpdatedAtUtc = @Now
        WHERE CdsAlertUid = @AlertUid;
    END;

    COMMIT;
    SELECT * FROM dbo.CdsAlert WHERE PatientUid = @PatientUid AND CdsAlertUid = @AlertUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.CdsAlert_ResolveRuleFindings
    @PatientUid UNIQUEIDENTIFIER,
    @RuleKey NVARCHAR(100),
    @RuleVersion INT,
    @ExceptFingerprint CHAR(64) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @Now DATETIME2(0) = SYSUTCDATETIME();
    DECLARE @ResolvedFindings TABLE
    (
        CdsAlertUid UNIQUEIDENTIFIER NOT NULL,
        PatientUid UNIQUEIDENTIFIER NOT NULL,
        RuleKey NVARCHAR(100) NOT NULL,
        RuleVersion INT NOT NULL
    );
    BEGIN TRANSACTION;
    UPDATE dbo.CdsAlert
    SET Status = N'Resolved', ResolvedAtUtc = @Now, LastEvaluatedAtUtc = @Now, UpdatedAtUtc = @Now
    OUTPUT inserted.CdsAlertUid, inserted.PatientUid, inserted.RuleKey, inserted.RuleVersion
    INTO @ResolvedFindings (CdsAlertUid, PatientUid, RuleKey, RuleVersion)
    WHERE PatientUid = @PatientUid AND RuleKey = @RuleKey
      AND Status IN (N'Active', N'Acknowledged')
      AND (@ExceptFingerprint IS NULL OR RuleVersion <> @RuleVersion OR FindingFingerprint <> @ExceptFingerprint);

    INSERT dbo.CdsAlertHistory
        (CdsAlertUid, PatientUid, EventType, ActorUserId, OccurredAtUtc,
         ReasonCode, Comment, RuleKey, RuleVersion)
    SELECT CdsAlertUid, PatientUid, N'Resolved', NULL, @Now,
           NULL, NULL, RuleKey, RuleVersion
    FROM @ResolvedFindings;
    COMMIT;
END;
GO

CREATE OR ALTER PROCEDURE dbo.CdsAlert_Acknowledge
    @PatientUid UNIQUEIDENTIFIER,
    @CdsAlertUid UNIQUEIDENTIFIER,
    @ActorUserId BIGINT,
    @ExpectedRowVersion BINARY(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.ApplicationUser WHERE UserId = @ActorUserId AND IsActive = 1)
        THROW 51405, 'Active clinical actor not found.', 1;
    DECLARE @Now DATETIME2(0) = SYSUTCDATETIME(), @PatientId BIGINT,
            @RuleKey NVARCHAR(100), @RuleVersion INT, @Status NVARCHAR(20), @RowVersion BINARY(8);
    BEGIN TRANSACTION;
    SELECT @PatientId = p.PatientId, @RuleKey = a.RuleKey, @RuleVersion = a.RuleVersion,
           @Status = a.Status, @RowVersion = a.RowVersion
    FROM dbo.CdsAlert a WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN dbo.Patient p ON p.PatientUid = a.PatientUid
    WHERE a.PatientUid = @PatientUid AND a.CdsAlertUid = @CdsAlertUid;
    IF @PatientId IS NULL BEGIN ROLLBACK; RETURN; END;
    IF @Status <> N'Active' THROW 51401, 'CDS alert cannot be acknowledged from its current state.', 1;
    IF @RowVersion <> @ExpectedRowVersion THROW 51402, 'CDS alert changed before acknowledgement.', 1;
    UPDATE dbo.CdsAlert SET Status=N'Acknowledged',AcknowledgedBy=@ActorUserId,
        AcknowledgedAtUtc=@Now,UpdatedAtUtc=@Now
    WHERE PatientUid=@PatientUid AND CdsAlertUid=@CdsAlertUid AND RowVersion=@ExpectedRowVersion;
    INSERT dbo.CdsAlertHistory(CdsAlertUid,PatientUid,EventType,ActorUserId,OccurredAtUtc,RuleKey,RuleVersion)
    VALUES(@CdsAlertUid,@PatientUid,N'Acknowledged',@ActorUserId,@Now,@RuleKey,@RuleVersion);
    INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt)
    VALUES(@ActorUserId,@PatientId,N'CdsAlertAcknowledged',N'CdsAlert',CONVERT(NVARCHAR(100),@CdsAlertUid),N'Status=Acknowledged',@Now);
    COMMIT;
    SELECT * FROM dbo.CdsAlert WHERE PatientUid=@PatientUid AND CdsAlertUid=@CdsAlertUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.CdsAlert_Dismiss
    @PatientUid UNIQUEIDENTIFIER,
    @CdsAlertUid UNIQUEIDENTIFIER,
    @ReasonCode NVARCHAR(50),
    @Comment NVARCHAR(500) = NULL,
    @ActorUserId BIGINT,
    @ExpectedRowVersion BINARY(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @Comment = NULLIF(LTRIM(RTRIM(@Comment)), N'');
    IF @ReasonCode NOT IN (N'NotApplicable',N'AlreadyAddressed',N'DuplicateFinding',N'Other')
       OR (@ReasonCode=N'Other' AND @Comment IS NULL)
        THROW 51403, 'A governed dismissal reason is required; Other requires a comment.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@ActorUserId AND IsActive=1)
        THROW 51405, 'Active clinical actor not found.', 1;
    DECLARE @Now DATETIME2(0)=SYSUTCDATETIME(),@PatientId BIGINT,@RuleKey NVARCHAR(100),
            @RuleVersion INT,@Status NVARCHAR(20),@RowVersion BINARY(8);
    BEGIN TRANSACTION;
    SELECT @PatientId=p.PatientId,@RuleKey=a.RuleKey,@RuleVersion=a.RuleVersion,
           @Status=a.Status,@RowVersion=a.RowVersion
    FROM dbo.CdsAlert a WITH(UPDLOCK,HOLDLOCK)
    INNER JOIN dbo.Patient p ON p.PatientUid=a.PatientUid
    WHERE a.PatientUid=@PatientUid AND a.CdsAlertUid=@CdsAlertUid;
    IF @PatientId IS NULL BEGIN ROLLBACK; RETURN; END;
    IF @Status NOT IN(N'Active',N'Acknowledged') THROW 51401,'CDS alert cannot be dismissed from its current state.',1;
    IF @RowVersion<>@ExpectedRowVersion THROW 51402,'CDS alert changed before dismissal.',1;
    UPDATE dbo.CdsAlert SET Status=N'Dismissed',DismissedBy=@ActorUserId,DismissedAtUtc=@Now,
        DismissReasonCode=@ReasonCode,DismissComment=@Comment,UpdatedAtUtc=@Now
    WHERE PatientUid=@PatientUid AND CdsAlertUid=@CdsAlertUid AND RowVersion=@ExpectedRowVersion;
    INSERT dbo.CdsAlertHistory(CdsAlertUid,PatientUid,EventType,ActorUserId,OccurredAtUtc,ReasonCode,Comment,RuleKey,RuleVersion)
    VALUES(@CdsAlertUid,@PatientUid,N'Dismissed',@ActorUserId,@Now,@ReasonCode,@Comment,@RuleKey,@RuleVersion);
    INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt)
    VALUES(@ActorUserId,@PatientId,N'CdsAlertDismissed',N'CdsAlert',CONVERT(NVARCHAR(100),@CdsAlertUid),N'Status=Dismissed;ReasonCode='+@ReasonCode,@Now);
    COMMIT;
    SELECT * FROM dbo.CdsAlert WHERE PatientUid=@PatientUid AND CdsAlertUid=@CdsAlertUid;
END;
GO
