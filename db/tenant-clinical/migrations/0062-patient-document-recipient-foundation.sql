SET XACT_ABORT ON;
GO

IF COL_LENGTH('dbo.PatientDocument', 'FinalizedAt') IS NULL
    ALTER TABLE dbo.PatientDocument ADD FinalizedAt DATETIME2(0) NULL;
IF COL_LENGTH('dbo.PatientDocument', 'FinalizedByUserId') IS NULL
    ALTER TABLE dbo.PatientDocument ADD FinalizedByUserId BIGINT NULL;
IF COL_LENGTH('dbo.PatientDocument', 'SignerProviderId') IS NULL
    ALTER TABLE dbo.PatientDocument ADD SignerProviderId BIGINT NULL;
IF COL_LENGTH('dbo.PatientDocument', 'SignerDisplayNameSnapshot') IS NULL
    ALTER TABLE dbo.PatientDocument ADD SignerDisplayNameSnapshot NVARCHAR(200) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PatientDocument_FinalizedByUser')
    ALTER TABLE dbo.PatientDocument ADD CONSTRAINT FK_PatientDocument_FinalizedByUser
        FOREIGN KEY (FinalizedByUserId) REFERENCES dbo.ApplicationUser(UserId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PatientDocument_SignerProvider')
    ALTER TABLE dbo.PatientDocument ADD CONSTRAINT FK_PatientDocument_SignerProvider
        FOREIGN KEY (SignerProviderId) REFERENCES dbo.Provider(ProviderId);
GO

IF OBJECT_ID(N'dbo.PatientDocumentRecipient', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PatientDocumentRecipient
    (
        PatientDocumentRecipientId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PatientDocumentRecipient PRIMARY KEY,
        RecipientUid UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PatientDocumentRecipient_Uid DEFAULT NEWID(),
        PatientDocumentId BIGINT NOT NULL,
        RecipientOrder INT NOT NULL,
        RecipientType NVARCHAR(2) NOT NULL,
        ProviderId BIGINT NOT NULL,
        DisplayNameSnapshot NVARCHAR(200) NOT NULL,
        OrganizationNameSnapshot NVARCHAR(200) NULL,
        FaxSnapshot NVARCHAR(30) NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_PatientDocumentRecipient_IsDeleted DEFAULT 0,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_PatientDocumentRecipient_CreatedAt DEFAULT SYSUTCDATETIME(),
        CreatedBy BIGINT NOT NULL,
        ReplacedAt DATETIME2(0) NULL,
        ReplacedBy BIGINT NULL,
        CONSTRAINT UQ_PatientDocumentRecipient_Uid UNIQUE (RecipientUid),
        CONSTRAINT FK_PatientDocumentRecipient_Document FOREIGN KEY (PatientDocumentId) REFERENCES dbo.PatientDocument(PatientDocumentId),
        CONSTRAINT FK_PatientDocumentRecipient_Provider FOREIGN KEY (ProviderId) REFERENCES dbo.Provider(ProviderId),
        CONSTRAINT FK_PatientDocumentRecipient_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.ApplicationUser(UserId),
        CONSTRAINT FK_PatientDocumentRecipient_ReplacedBy FOREIGN KEY (ReplacedBy) REFERENCES dbo.ApplicationUser(UserId),
        CONSTRAINT CK_PatientDocumentRecipient_Type CHECK (RecipientType IN (N'TO', N'CC')),
        CONSTRAINT CK_PatientDocumentRecipient_Order CHECK (RecipientOrder > 0)
    );
    CREATE UNIQUE INDEX UX_PatientDocumentRecipient_ActiveOrder
        ON dbo.PatientDocumentRecipient(PatientDocumentId, RecipientOrder) WHERE IsDeleted = 0;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientDocumentRecipient_List
    @PatientUid UNIQUEIDENTIFIER, @DocumentUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.RecipientUid, r.RecipientOrder, r.RecipientType, v.ProviderUid,
        r.DisplayNameSnapshot, r.OrganizationNameSnapshot, r.FaxSnapshot
    FROM dbo.PatientDocumentRecipient AS r
    JOIN dbo.PatientDocument AS d ON d.PatientDocumentId = r.PatientDocumentId
    JOIN dbo.Provider AS v ON v.ProviderId = r.ProviderId
    WHERE d.PatientUid = @PatientUid AND d.PatientDocumentUid = @DocumentUid
        AND d.IsDeleted = 0 AND r.IsDeleted = 0
    ORDER BY r.RecipientOrder, r.PatientDocumentRecipientId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientDocumentRecipient_ReplaceDraft
    @PatientUid UNIQUEIDENTIFIER, @DocumentUid UNIQUEIDENTIFIER,
    @ExpectedRowVersion BINARY(8), @RecipientsJson NVARCHAR(MAX), @UpdatedBy BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @RecipientsJson IS NULL OR ISJSON(@RecipientsJson) <> 1 OR LEFT(LTRIM(@RecipientsJson), 1) <> N'['
        THROW 51904, 'Recipients must be a JSON array.', 1;

    DECLARE @Requested TABLE
    (
        RecipientOrder INT NULL,
        RecipientType NVARCHAR(20) NULL,
        ProviderUid UNIQUEIDENTIFIER NULL
    );
    INSERT @Requested(RecipientOrder, RecipientType, ProviderUid)
    SELECT RecipientOrder, RecipientType, ProviderUid
    FROM OPENJSON(@RecipientsJson) WITH
    (
        RecipientOrder INT '$.recipientOrder',
        RecipientType NVARCHAR(20) '$.recipientType',
        ProviderUid UNIQUEIDENTIFIER '$.providerUid'
    );
    IF EXISTS (SELECT 1 FROM @Requested WHERE RecipientOrder IS NULL OR RecipientOrder < 1
        OR RecipientType NOT IN (N'TO', N'CC') OR RecipientType IS NULL OR ProviderUid IS NULL)
        THROW 51905, 'Recipient order, type, and provider are required.', 1;
    IF EXISTS (SELECT 1 FROM @Requested GROUP BY RecipientOrder HAVING COUNT(*) > 1)
        OR EXISTS (SELECT 1 FROM @Requested GROUP BY ProviderUid HAVING COUNT(*) > 1)
        OR EXISTS (SELECT 1 FROM @Requested HAVING COUNT(*) > 0 AND MAX(RecipientOrder) <> COUNT(*))
        THROW 51905, 'Recipients must have unique providers and consecutive order.', 1;

    BEGIN TRANSACTION;
    DECLARE @DocumentId BIGINT, @PatientId BIGINT, @Status NVARCHAR(50),
        @Version BINARY(8), @NewVersion BINARY(8);
    SELECT @DocumentId = d.PatientDocumentId, @PatientId = d.PatientId,
        @Status = d.DocumentStatus, @Version = d.RowVersion
    FROM dbo.PatientDocument AS d WITH (UPDLOCK, HOLDLOCK)
    JOIN dbo.Patient AS p ON p.PatientId = d.PatientId AND p.PatientUid = @PatientUid AND p.IsDeleted = 0
    WHERE d.PatientDocumentUid = @DocumentUid AND d.PatientUid = @PatientUid AND d.IsDeleted = 0;
    IF @DocumentId IS NULL THROW 51900, 'Patient document was not found.', 1;
    IF @Status <> N'Draft' THROW 51901, 'Only draft patient document recipients can be changed.', 1;
    IF @Version <> @ExpectedRowVersion THROW 51902, 'Patient document changed.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.ApplicationUser WHERE UserId = @UpdatedBy AND IsActive = 1)
        THROW 51906, 'An active actor is required.', 1;
    IF (SELECT COUNT(*) FROM @Requested) <>
       (SELECT COUNT(*) FROM @Requested AS requested
        JOIN dbo.Provider AS provider ON provider.ProviderUid = requested.ProviderUid AND provider.IsActive = 1)
        THROW 51903, 'A selected provider was not found or is inactive.', 1;

    UPDATE dbo.PatientDocumentRecipient SET IsDeleted = 1, ReplacedAt = SYSUTCDATETIME(), ReplacedBy = @UpdatedBy
    WHERE PatientDocumentId = @DocumentId AND IsDeleted = 0;
    INSERT dbo.PatientDocumentRecipient
        (PatientDocumentId, RecipientOrder, RecipientType, ProviderId,
         DisplayNameSnapshot, OrganizationNameSnapshot, FaxSnapshot, CreatedBy)
    SELECT @DocumentId, requested.RecipientOrder, requested.RecipientType, provider.ProviderId,
        provider.DisplayName, provider.OrganizationName, provider.Fax, @UpdatedBy
    FROM @Requested AS requested
    JOIN dbo.Provider AS provider ON provider.ProviderUid = requested.ProviderUid AND provider.IsActive = 1;

    UPDATE dbo.PatientDocument SET UpdatedAt = SYSUTCDATETIME()
    WHERE PatientDocumentId = @DocumentId AND DocumentStatus = N'Draft' AND RowVersion = @ExpectedRowVersion;
    IF @@ROWCOUNT <> 1 THROW 51902, 'Patient document changed.', 1;
    SELECT @NewVersion = RowVersion FROM dbo.PatientDocument WHERE PatientDocumentId = @DocumentId;
    INSERT dbo.AuditLog(UserId, PatientId, ActionName, EntityName, EntityId, NewValue, CreatedAt)
    VALUES(@UpdatedBy, @PatientId, N'ReplaceDraftRecipients', N'PatientDocument',
        CONVERT(NVARCHAR(100), @DocumentUid), N'Draft recipients replaced', SYSUTCDATETIME());
    COMMIT TRANSACTION;

    SELECT @DocumentUid AS DocumentUid, @NewVersion AS RowVersion;
    EXEC dbo.PatientDocumentRecipient_List @PatientUid, @DocumentUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientDocument_GetByUid @DocumentUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT pd.PatientDocumentUid AS DocumentUid, pd.PatientUid,
        pd.TemplateUid, pd.TemplateVersionUid, pd.DocumentType,
        pd.DocumentTitle AS Title, pd.DocumentStatus,
        content.DocumentContent, content.StructuredDataJson, pd.CreatedBy,
        applicationUser.DisplayName AS CreatedByDisplayName,
        pd.CreatedAt, pd.UpdatedAt, pd.RowVersion,
        content.RowVersion AS ContentRowVersion,
        pd.FinalizedAt, pd.FinalizedByUserId, signer.ProviderUid AS SignerProviderUid,
        pd.SignerDisplayNameSnapshot
    FROM dbo.PatientDocument AS pd
    LEFT JOIN dbo.PatientDocumentContent AS content ON content.PatientDocumentUid = pd.PatientDocumentUid
    LEFT JOIN dbo.ApplicationUser AS applicationUser ON applicationUser.UserId = pd.CreatedBy
    LEFT JOIN dbo.Provider AS signer ON signer.ProviderId = pd.SignerProviderId
    WHERE pd.PatientDocumentUid = @DocumentUid AND pd.IsDeleted = 0;
END;
GO
