SET XACT_ABORT ON;
GO

CREATE UNIQUE INDEX UX_ClinicalOutputArtifact_PatientDocumentFinal
    ON dbo.ClinicalOutputArtifact(SourceType, SourceUid, ArtifactType)
    WHERE ArtifactStatus = N'Available' AND SourceType = N'PatientDocument';
GO

-- Both procedures run on the same connection in a caller-owned transaction. The
-- aggregate lock is held through rendering/storage; stale requests never render.
CREATE OR ALTER PROCEDURE dbo.PatientDocument_PrepareSignConsultation
    @PatientUid UNIQUEIDENTIFIER, @DocumentUid UNIQUEIDENTIFIER,
    @ExpectedRowVersion BINARY(8), @ActorUserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @@TRANCOUNT = 0 THROW 51915, 'Signing requires a transaction.', 1;
    DECLARE @DocumentId BIGINT, @Status NVARCHAR(50), @Version BINARY(8),
        @TemplateUid UNIQUEIDENTIFIER, @TemplateVersionUid UNIQUEIDENTIFIER;
    SELECT @DocumentId = d.PatientDocumentId, @Status = d.DocumentStatus,
        @Version = d.RowVersion, @TemplateUid = d.TemplateUid, @TemplateVersionUid = d.TemplateVersionUid
    FROM dbo.PatientDocument d WITH(UPDLOCK, HOLDLOCK)
    JOIN dbo.Patient p ON p.PatientId = d.PatientId AND p.PatientUid = @PatientUid AND p.IsDeleted = 0
    WHERE d.PatientDocumentUid = @DocumentUid AND d.PatientUid = @PatientUid AND d.IsDeleted = 0;
    IF @DocumentId IS NULL THROW 51910, 'Consultation not found.', 1;
    IF @Status <> N'Draft' THROW 51911, 'Only Draft consultations can be signed.', 1;
    IF @ExpectedRowVersion IS NULL OR @Version <> @ExpectedRowVersion
        THROW 51912, 'Document changed. Reload before signing.', 1;
    IF NOT EXISTS(SELECT 1 FROM dbo.DocumentTemplate t WITH(HOLDLOCK)
        JOIN dbo.DocumentTemplateVersion v WITH(HOLDLOCK) ON v.TemplateUid = t.TemplateUid
        WHERE t.TemplateUid = @TemplateUid AND t.TemplateType = N'CONSULTATION_REPORT' AND t.TemplateKind = N'Document'
            AND v.TemplateVersionUid = @TemplateVersionUid)
        THROW 51913, 'A Consultation Report template is required.', 1;
    IF NOT EXISTS(SELECT 1 FROM dbo.PatientDocumentContent WITH(HOLDLOCK)
        WHERE PatientDocumentUid = @DocumentUid AND StructuredDataJson IS NOT NULL)
        THROW 51913, 'Structured consultation content is required.', 1;
    IF NOT EXISTS(SELECT 1 FROM dbo.PatientDocumentRecipient WITH(HOLDLOCK)
        WHERE PatientDocumentId = @DocumentId AND IsDeleted = 0 AND RecipientType = N'TO')
        THROW 51913, 'Save at least one TO recipient before signing.', 1;
    IF EXISTS(SELECT 1 FROM dbo.PatientDocumentRecipient r WITH(HOLDLOCK)
        LEFT JOIN dbo.Provider p WITH(HOLDLOCK) ON p.ProviderId = r.ProviderId AND p.IsActive = 1
        WHERE r.PatientDocumentId = @DocumentId AND r.IsDeleted = 0 AND p.ProviderId IS NULL)
        THROW 51913, 'Recipients must reference active Providers. Review and save recipients.', 1;
    DECLARE @SignerUid UNIQUEIDENTIFIER, @SignerName NVARCHAR(200);
    SELECT @SignerUid = p.ProviderUid, @SignerName = p.DisplayName
    FROM dbo.ApplicationUser u WITH(HOLDLOCK)
    JOIN dbo.Provider p WITH(HOLDLOCK) ON p.ProviderId = u.ProviderId AND p.IsActive = 1
    WHERE u.UserId = @ActorUserId AND u.IsActive = 1;
    IF @SignerUid IS NULL THROW 51914, 'Signing requires an active linked Provider.', 1;
    SELECT @SignerUid AS SignerProviderUid, @SignerName AS SignerDisplayName,
        CONVERT(DATETIME2(0), SYSUTCDATETIME()) AS SignedAt;
    EXEC dbo.PatientDocument_GetByUid @DocumentUid;
    EXEC dbo.PatientDocumentRecipient_List @PatientUid, @DocumentUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientDocument_CommitSignConsultation
    @PatientUid UNIQUEIDENTIFIER, @DocumentUid UNIQUEIDENTIFIER,
    @ExpectedRowVersion BINARY(8), @ActorUserId BIGINT,
    @SignerProviderUid UNIQUEIDENTIFIER, @SignerDisplayName NVARCHAR(200), @SignedAt DATETIME2(0),
    @ArtifactUid UNIQUEIDENTIFIER, @StorageKey NVARCHAR(700), @FileSizeBytes BIGINT, @Sha256 CHAR(64)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @@TRANCOUNT = 0 THROW 51915, 'Signing requires a transaction.', 1;
    DECLARE @DocumentId BIGINT, @PatientId BIGINT, @TemplateVersionUid UNIQUEIDENTIFIER, @SignerId BIGINT;
    SELECT @DocumentId = d.PatientDocumentId, @PatientId = d.PatientId, @TemplateVersionUid = d.TemplateVersionUid
    FROM dbo.PatientDocument d WITH(UPDLOCK, HOLDLOCK)
    JOIN dbo.Patient p ON p.PatientId = d.PatientId AND p.PatientUid = @PatientUid AND p.IsDeleted = 0
    JOIN dbo.DocumentTemplate t ON t.TemplateUid = d.TemplateUid AND t.TemplateType = N'CONSULTATION_REPORT' AND t.TemplateKind = N'Document'
    WHERE d.PatientDocumentUid = @DocumentUid AND d.PatientUid = @PatientUid AND d.IsDeleted = 0
        AND d.DocumentStatus = N'Draft' AND d.RowVersion = @ExpectedRowVersion;
    IF @DocumentId IS NULL THROW 51912, 'Document changed. Reload before signing.', 1;
    SELECT @SignerId = p.ProviderId FROM dbo.ApplicationUser u
    JOIN dbo.Provider p ON p.ProviderId = u.ProviderId AND p.ProviderUid = @SignerProviderUid AND p.IsActive = 1
    WHERE u.UserId = @ActorUserId AND u.IsActive = 1 AND p.DisplayName = @SignerDisplayName;
    IF @SignerId IS NULL THROW 51914, 'Signing requires an active linked Provider.', 1;
    IF NOT EXISTS(SELECT 1 FROM dbo.PatientDocumentRecipient
        WHERE PatientDocumentId = @DocumentId AND IsDeleted = 0 AND RecipientType = N'TO')
        THROW 51913, 'Save at least one TO recipient before signing.', 1;
    IF @FileSizeBytes <= 0 OR LEN(@Sha256) <> 64 OR NULLIF(@StorageKey, N'') IS NULL OR @SignedAt IS NULL
        THROW 51915, 'A stored final PDF is required.', 1;

    INSERT dbo.ClinicalOutputArtifact(ArtifactUid, PatientUid, SourceType, SourceUid, TemplateVersionUid,
        ArtifactType, StorageProvider, StorageKey, MimeType, FileSizeBytes, Sha256, CreatedBy)
    VALUES(@ArtifactUid, @PatientUid, N'PatientDocument', @DocumentUid, @TemplateVersionUid,
        N'FinalPdf', N'FileSystem', @StorageKey, N'application/pdf', @FileSizeBytes, @Sha256, @ActorUserId);
    UPDATE dbo.PatientDocument SET DocumentStatus = N'Signed', FinalizedAt = @SignedAt,
        FinalizedByUserId = @ActorUserId, SignerProviderId = @SignerId,
        SignerDisplayNameSnapshot = @SignerDisplayName, UpdatedAt = @SignedAt
    WHERE PatientDocumentId = @DocumentId AND DocumentStatus = N'Draft' AND RowVersion = @ExpectedRowVersion;
    IF @@ROWCOUNT <> 1 THROW 51912, 'Document changed. Reload before signing.', 1;
    INSERT dbo.AuditLog(UserId, PatientId, ActionName, EntityName, EntityId, NewValue, CreatedAt)
    VALUES(@ActorUserId, @PatientId, N'PatientDocumentSigned', N'PatientDocument',
        CONVERT(NVARCHAR(100), @DocumentUid), N'Consultation signed; final PDF preserved', @SignedAt),
        (@ActorUserId, @PatientId, N'CreateFinalPdf', N'ClinicalOutputArtifact',
        CONVERT(NVARCHAR(100), @ArtifactUid), N'Final PDF artifact created', @SignedAt);
    EXEC dbo.PatientDocument_GetByUid @DocumentUid;
END;
GO
