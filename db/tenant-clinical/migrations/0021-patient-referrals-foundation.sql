IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Patient')
      AND name = N'UQ_Patient_PatientUid'
)
BEGIN
    CREATE UNIQUE INDEX UQ_Patient_PatientUid
        ON dbo.Patient(PatientUid);
END;
GO

IF OBJECT_ID(N'dbo.PatientReferral', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PatientReferral
    (
        PatientReferralId BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_PatientReferral PRIMARY KEY,
        ReferralUid UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_PatientReferral_ReferralUid DEFAULT NEWSEQUENTIALID(),
        PatientUid UNIQUEIDENTIFIER NOT NULL,
        RecipientName NVARCHAR(200) NOT NULL,
        RecipientOrganization NVARCHAR(200) NULL,
        RecipientPhone NVARCHAR(30) NULL,
        RecipientFax NVARCHAR(30) NULL,
        Reason NVARCHAR(1000) NOT NULL,
        ClinicalSummary NVARCHAR(MAX) NULL,
        Status NVARCHAR(30) NOT NULL
            CONSTRAINT DF_PatientReferral_Status DEFAULT N'Draft',
        CreatedAt DATETIME2(0) NOT NULL
            CONSTRAINT DF_PatientReferral_CreatedAt DEFAULT SYSUTCDATETIME(),
        CreatedBy BIGINT NOT NULL,
        UpdatedAt DATETIME2(0) NULL,
        UpdatedBy BIGINT NULL,
        SentAt DATETIME2(0) NULL,
        ResponseReceivedAt DATETIME2(0) NULL,
        ClosedAt DATETIME2(0) NULL,
        RowVersion ROWVERSION NOT NULL,

        CONSTRAINT UQ_PatientReferral_ReferralUid UNIQUE (ReferralUid),
        CONSTRAINT CK_PatientReferral_Status CHECK
            (Status IN (N'Draft', N'Sent', N'ResponseReceived', N'Closed')),
        CONSTRAINT FK_PatientReferral_Patient FOREIGN KEY (PatientUid)
            REFERENCES dbo.Patient(PatientUid),
        CONSTRAINT FK_PatientReferral_CreatedBy FOREIGN KEY (CreatedBy)
            REFERENCES dbo.ApplicationUser(UserId),
        CONSTRAINT FK_PatientReferral_UpdatedBy FOREIGN KEY (UpdatedBy)
            REFERENCES dbo.ApplicationUser(UserId)
    );

    CREATE INDEX IX_PatientReferral_PatientUid_CreatedAt
        ON dbo.PatientReferral(PatientUid, CreatedAt DESC);

    CREATE INDEX IX_PatientReferral_PatientUid_Status
        ON dbo.PatientReferral(PatientUid, Status);
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientReferral_GetByPatientUid
    @PatientUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        r.ReferralUid,
        r.PatientUid,
        r.RecipientName,
        r.RecipientOrganization,
        r.RecipientPhone,
        r.RecipientFax,
        r.Reason,
        r.ClinicalSummary,
        r.Status,
        r.CreatedAt,
        r.CreatedBy,
        r.UpdatedAt,
        r.UpdatedBy,
        r.SentAt,
        r.ResponseReceivedAt,
        r.ClosedAt,
        r.RowVersion
    FROM dbo.PatientReferral AS r
    WHERE r.PatientUid = @PatientUid
    ORDER BY r.CreatedAt DESC, r.PatientReferralId DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientReferral_GetByUid
    @PatientUid UNIQUEIDENTIFIER,
    @ReferralUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        r.ReferralUid,
        r.PatientUid,
        r.RecipientName,
        r.RecipientOrganization,
        r.RecipientPhone,
        r.RecipientFax,
        r.Reason,
        r.ClinicalSummary,
        r.Status,
        r.CreatedAt,
        r.CreatedBy,
        r.UpdatedAt,
        r.UpdatedBy,
        r.SentAt,
        r.ResponseReceivedAt,
        r.ClosedAt,
        r.RowVersion
    FROM dbo.PatientReferral AS r
    WHERE r.PatientUid = @PatientUid
      AND r.ReferralUid = @ReferralUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientReferral_Create
    @PatientUid UNIQUEIDENTIFIER,
    @RecipientName NVARCHAR(200),
    @RecipientOrganization NVARCHAR(200) = NULL,
    @RecipientPhone NVARCHAR(30) = NULL,
    @RecipientFax NVARCHAR(30) = NULL,
    @Reason NVARCHAR(1000),
    @ClinicalSummary NVARCHAR(MAX) = NULL,
    @CreatedBy BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @PatientId BIGINT;
    DECLARE @ReferralUid UNIQUEIDENTIFIER = NEWID();

    SELECT @PatientId = p.PatientId
    FROM dbo.Patient AS p
    WHERE p.PatientUid = @PatientUid
      AND p.IsDeleted = 0;

    IF @PatientId IS NULL
        THROW 51500, 'Patient not found.', 1;

    IF NULLIF(LTRIM(RTRIM(@RecipientName)), N'') IS NULL
        THROW 51501, 'Recipient name is required.', 1;

    IF NULLIF(LTRIM(RTRIM(@Reason)), N'') IS NULL
        THROW 51502, 'Referral reason is required.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.ApplicationUser
        WHERE UserId = @CreatedBy
          AND IsActive = 1
    )
        THROW 51503, 'Active clinical user not found.', 1;

    BEGIN TRANSACTION;

    INSERT dbo.PatientReferral
    (
        ReferralUid,
        PatientUid,
        RecipientName,
        RecipientOrganization,
        RecipientPhone,
        RecipientFax,
        Reason,
        ClinicalSummary,
        CreatedBy
    )
    VALUES
    (
        @ReferralUid,
        @PatientUid,
        LTRIM(RTRIM(@RecipientName)),
        NULLIF(LTRIM(RTRIM(@RecipientOrganization)), N''),
        NULLIF(LTRIM(RTRIM(@RecipientPhone)), N''),
        NULLIF(LTRIM(RTRIM(@RecipientFax)), N''),
        LTRIM(RTRIM(@Reason)),
        NULLIF(LTRIM(RTRIM(@ClinicalSummary)), N''),
        @CreatedBy
    );

    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
    BEGIN
        INSERT dbo.AuditLog
        (
            UserId,
            PatientId,
            ActionName,
            EntityName,
            EntityId,
            OldValue,
            NewValue,
            CreatedAt
        )
        VALUES
        (
            @CreatedBy,
            @PatientId,
            N'Create',
            N'PatientReferral',
            CONVERT(NVARCHAR(100), @ReferralUid),
            NULL,
            N'Status=Draft',
            SYSUTCDATETIME()
        );
    END;

    COMMIT TRANSACTION;

    EXEC dbo.PatientReferral_GetByUid
        @PatientUid = @PatientUid,
        @ReferralUid = @ReferralUid;
END;
GO
