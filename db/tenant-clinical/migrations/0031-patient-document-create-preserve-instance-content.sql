SET XACT_ABORT ON;
GO

CREATE OR ALTER PROCEDURE dbo.PatientDocument_Create
    @PatientUid UNIQUEIDENTIFIER,
    @TemplateUid UNIQUEIDENTIFIER = NULL,
    @DocumentType NVARCHAR(100),
    @Title NVARCHAR(250),
    @DocumentContent NVARCHAR(MAX) = NULL,
    @CreatedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @DocumentUid UNIQUEIDENTIFIER = NEWID();
    DECLARE @PatientId BIGINT;
    DECLARE @TemplateVersionUid UNIQUEIDENTIFIER;
    DECLARE @TemplateContent NVARCHAR(MAX);
    DECLARE @ResolvedContent NVARCHAR(MAX) = @DocumentContent;

    BEGIN TRANSACTION;
    SELECT @PatientId = PatientId
    FROM dbo.Patient WITH (HOLDLOCK)
    WHERE PatientUid = @PatientUid AND IsDeleted = 0;

    IF @PatientId IS NULL
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51001, 'The requested patient was not found.', 1;
    END;

    IF @TemplateUid IS NOT NULL
    BEGIN
        SELECT @TemplateVersionUid = version.TemplateVersionUid,
               @TemplateContent = version.TemplateContent
        FROM dbo.DocumentTemplate AS template WITH (HOLDLOCK)
        INNER JOIN dbo.DocumentTemplateVersion AS version WITH (HOLDLOCK)
            ON version.TemplateUid = template.TemplateUid
           AND version.IsCurrent = 1
           AND version.VersionStatus = N'Published'
        WHERE template.TemplateUid = @TemplateUid AND template.IsActive = 1;

        IF @TemplateVersionUid IS NULL
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 51002, 'The selected document template has no active published version.', 1;
        END;

        IF @ResolvedContent IS NULL
            SET @ResolvedContent = @TemplateContent;
    END;

    INSERT dbo.PatientDocument
    (
        PatientDocumentUid, PatientId, PatientUid, TemplateUid, TemplateVersionUid,
        DocumentTitle, DocumentType, DocumentStatus, DocumentDate,
        CreatedAt, CreatedBy, IsDeleted
    )
    VALUES
    (
        @DocumentUid, @PatientId, @PatientUid, @TemplateUid, @TemplateVersionUid,
        @Title, @DocumentType, N'Draft', SYSUTCDATETIME(),
        SYSUTCDATETIME(), @CreatedBy, 0
    );

    INSERT dbo.PatientDocumentContent
        (PatientDocumentUid, DocumentContent, CreatedAt, CreatedBy)
    VALUES
        (@DocumentUid, @ResolvedContent, SYSUTCDATETIME(), @CreatedBy);

    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
        INSERT dbo.AuditLog(UserId, PatientId, ActionName, EntityName, EntityId, NewValue, CreatedAt)
        VALUES (@CreatedBy, @PatientId, N'Create', N'PatientDocument',
            CONVERT(NVARCHAR(100), @DocumentUid), @Title, SYSUTCDATETIME());

    COMMIT TRANSACTION;
    EXEC dbo.PatientDocument_GetByUid @DocumentUid;
END;
GO
