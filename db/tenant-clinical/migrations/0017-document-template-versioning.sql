IF OBJECT_ID(N'dbo.DocumentTemplateVersion', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentTemplateVersion
    (
        DocumentTemplateVersionId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        TemplateVersionUid UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_DocumentTemplateVersion_Uid DEFAULT NEWID(),
        TemplateUid UNIQUEIDENTIFIER NOT NULL,
        VersionNumber INT NOT NULL,
        TemplateContent NVARCHAR(MAX) NOT NULL,
        VersionStatus NVARCHAR(20) NOT NULL,
        IsCurrent BIT NOT NULL CONSTRAINT DF_DocumentTemplateVersion_IsCurrent DEFAULT 0,
        PublishedAt DATETIME2(0) NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_DocumentTemplateVersion_CreatedAt DEFAULT SYSUTCDATETIME(),
        CreatedBy BIGINT NULL,
        UpdatedAt DATETIME2(0) NULL,
        UpdatedBy BIGINT NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT UQ_DocumentTemplateVersion_Uid UNIQUE (TemplateVersionUid),
        CONSTRAINT UQ_DocumentTemplateVersion_Number UNIQUE (TemplateUid, VersionNumber),
        CONSTRAINT FK_DocumentTemplateVersion_TemplateUid FOREIGN KEY (TemplateUid)
            REFERENCES dbo.DocumentTemplate(TemplateUid),
        CONSTRAINT CK_DocumentTemplateVersion_Status CHECK (VersionStatus IN (N'Draft', N'Published', N'Retired')),
        CONSTRAINT CK_DocumentTemplateVersion_Current CHECK
            ((IsCurrent = 1 AND VersionStatus = N'Published') OR IsCurrent = 0)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.DocumentTemplateVersion')
      AND name = N'UX_DocumentTemplateVersion_Current'
)
BEGIN
    CREATE UNIQUE INDEX UX_DocumentTemplateVersion_Current
        ON dbo.DocumentTemplateVersion(TemplateUid)
        WHERE IsCurrent = 1;
END;
GO

IF COL_LENGTH('dbo.PatientDocument', 'TemplateVersionUid') IS NULL
BEGIN
    ALTER TABLE dbo.PatientDocument ADD TemplateVersionUid UNIQUEIDENTIFIER NULL;
END;
GO

INSERT INTO dbo.DocumentTemplateVersion
(
    TemplateVersionUid, TemplateUid, VersionNumber, TemplateContent,
    VersionStatus, IsCurrent, PublishedAt, CreatedAt, CreatedBy
)
SELECT
    NEWID(), template.TemplateUid, 1, template.TemplateHtml,
    N'Published', 1, template.CreatedAt, template.CreatedAt, template.CreatedBy
FROM dbo.DocumentTemplate AS template
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.DocumentTemplateVersion AS version
    WHERE version.TemplateUid = template.TemplateUid
);
GO

UPDATE document
SET TemplateVersionUid = version.TemplateVersionUid
FROM dbo.PatientDocument AS document
INNER JOIN dbo.DocumentTemplateVersion AS version
    ON version.TemplateUid = document.TemplateUid
   AND version.VersionNumber = 1
WHERE document.TemplateUid IS NOT NULL
  AND document.TemplateVersionUid IS NULL;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.PatientDocument')
      AND name = N'FK_PatientDocument_TemplateVersionUid'
)
BEGIN
    ALTER TABLE dbo.PatientDocument WITH CHECK
        ADD CONSTRAINT FK_PatientDocument_TemplateVersionUid
        FOREIGN KEY (TemplateVersionUid)
        REFERENCES dbo.DocumentTemplateVersion(TemplateVersionUid);
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.PatientDocument')
      AND name = N'IX_PatientDocument_TemplateVersionUid'
)
BEGIN
    CREATE INDEX IX_PatientDocument_TemplateVersionUid
        ON dbo.PatientDocument(TemplateVersionUid)
        WHERE TemplateVersionUid IS NOT NULL;
END;
GO

CREATE OR ALTER PROCEDURE dbo.DocumentTemplateVersion_GetByUid
    @TemplateVersionUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TemplateVersionUid, TemplateUid, VersionNumber, TemplateContent,
        VersionStatus, IsCurrent, PublishedAt, CreatedAt, CreatedBy,
        UpdatedAt, UpdatedBy, RowVersion
    FROM dbo.DocumentTemplateVersion
    WHERE TemplateVersionUid = @TemplateVersionUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.DocumentTemplateVersion_GetByTemplateUid
    @TemplateUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TemplateVersionUid, TemplateUid, VersionNumber, TemplateContent,
        VersionStatus, IsCurrent, PublishedAt, CreatedAt, CreatedBy,
        UpdatedAt, UpdatedBy, RowVersion
    FROM dbo.DocumentTemplateVersion
    WHERE TemplateUid = @TemplateUid
    ORDER BY VersionNumber DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.DocumentTemplateVersion_CreateDraft
    @TemplateUid UNIQUEIDENTIFIER,
    @CreatedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @TemplateVersionUid UNIQUEIDENTIFIER = NEWID();
    DECLARE @VersionNumber INT;
    DECLARE @TemplateContent NVARCHAR(MAX);

    BEGIN TRANSACTION;
    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.DocumentTemplate WITH (UPDLOCK, HOLDLOCK)
        WHERE TemplateUid = @TemplateUid
    )
    BEGIN
        ROLLBACK TRANSACTION;
        RETURN;
    END;

    SELECT @VersionNumber = ISNULL(MAX(VersionNumber), 0) + 1
    FROM dbo.DocumentTemplateVersion WITH (UPDLOCK, HOLDLOCK)
    WHERE TemplateUid = @TemplateUid;

    SELECT @TemplateContent = TemplateContent
    FROM dbo.DocumentTemplateVersion
    WHERE TemplateUid = @TemplateUid AND IsCurrent = 1 AND VersionStatus = N'Published';

    IF @TemplateContent IS NULL
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51033, 'The template has no current published version.', 1;
    END;

    INSERT INTO dbo.DocumentTemplateVersion
    (
        TemplateVersionUid, TemplateUid, VersionNumber, TemplateContent,
        VersionStatus, IsCurrent, CreatedAt, CreatedBy
    )
    VALUES
    (
        @TemplateVersionUid, @TemplateUid, @VersionNumber, @TemplateContent,
        N'Draft', 0, SYSUTCDATETIME(), @CreatedBy
    );

    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
        INSERT dbo.AuditLog(UserId, ActionName, EntityName, EntityId, NewValue, CreatedAt)
        VALUES(@CreatedBy, N'CreateDraft', N'DocumentTemplateVersion',
            CONVERT(NVARCHAR(100), @TemplateVersionUid),
            CONCAT(N'Version ', @VersionNumber), SYSUTCDATETIME());

    COMMIT TRANSACTION;
    EXEC dbo.DocumentTemplateVersion_GetByUid @TemplateVersionUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.DocumentTemplateVersion_UpdateDraft
    @TemplateUid UNIQUEIDENTIFIER,
    @TemplateVersionUid UNIQUEIDENTIFIER,
    @TemplateContent NVARCHAR(MAX),
    @ExpectedRowVersion BINARY(8),
    @UpdatedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.DocumentTemplateVersion
    SET TemplateContent = COALESCE(@TemplateContent, N''),
        UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UpdatedBy
    WHERE TemplateUid = @TemplateUid
      AND TemplateVersionUid = @TemplateVersionUid
      AND VersionStatus = N'Draft'
      AND RowVersion = @ExpectedRowVersion;

    IF @@ROWCOUNT = 0
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.DocumentTemplateVersion WHERE TemplateUid = @TemplateUid AND TemplateVersionUid = @TemplateVersionUid AND VersionStatus <> N'Draft')
            THROW 51031, 'Published or retired template versions are immutable.', 1;
        IF EXISTS (SELECT 1 FROM dbo.DocumentTemplateVersion WHERE TemplateUid = @TemplateUid AND TemplateVersionUid = @TemplateVersionUid)
            THROW 51032, 'The template version was updated by another user.', 1;
        RETURN;
    END;

    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
        INSERT dbo.AuditLog(UserId, ActionName, EntityName, EntityId, NewValue, CreatedAt)
        VALUES(@UpdatedBy, N'UpdateDraft', N'DocumentTemplateVersion',
            CONVERT(NVARCHAR(100), @TemplateVersionUid), N'Draft content updated', SYSUTCDATETIME());

    EXEC dbo.DocumentTemplateVersion_GetByUid @TemplateVersionUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.DocumentTemplateVersion_Publish
    @TemplateUid UNIQUEIDENTIFIER,
    @TemplateVersionUid UNIQUEIDENTIFIER,
    @ExpectedRowVersion BINARY(8),
    @PublishedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.DocumentTemplateVersion WITH (UPDLOCK, HOLDLOCK)
        WHERE TemplateUid = @TemplateUid
          AND TemplateVersionUid = @TemplateVersionUid
          AND VersionStatus = N'Draft'
          AND RowVersion = @ExpectedRowVersion
    )
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.DocumentTemplateVersion WHERE TemplateUid = @TemplateUid AND TemplateVersionUid = @TemplateVersionUid AND VersionStatus <> N'Draft')
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 51031, 'Only draft template versions can be published.', 1;
        END;
        IF EXISTS (SELECT 1 FROM dbo.DocumentTemplateVersion WHERE TemplateUid = @TemplateUid AND TemplateVersionUid = @TemplateVersionUid)
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 51032, 'The template version was updated by another user.', 1;
        END;
        ROLLBACK TRANSACTION;
        RETURN;
    END;

    UPDATE dbo.DocumentTemplateVersion
    SET VersionStatus = N'Retired', IsCurrent = 0,
        UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @PublishedBy
    WHERE TemplateUid = @TemplateUid AND IsCurrent = 1;

    UPDATE dbo.DocumentTemplateVersion
    SET VersionStatus = N'Published', IsCurrent = 1,
        PublishedAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @PublishedBy
    WHERE TemplateUid = @TemplateUid AND TemplateVersionUid = @TemplateVersionUid
      AND VersionStatus = N'Draft' AND RowVersion = @ExpectedRowVersion;

    IF @@ROWCOUNT <> 1
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51032, 'The template version was updated by another user.', 1;
    END;

    UPDATE template
    SET TemplateHtml = version.TemplateContent,
        UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @PublishedBy
    FROM dbo.DocumentTemplate AS template
    INNER JOIN dbo.DocumentTemplateVersion AS version
        ON version.TemplateUid = template.TemplateUid
       AND version.TemplateVersionUid = @TemplateVersionUid
    WHERE template.TemplateUid = @TemplateUid;

    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
        INSERT dbo.AuditLog(UserId, ActionName, EntityName, EntityId, NewValue, CreatedAt)
        VALUES(@PublishedBy, N'Publish', N'DocumentTemplateVersion',
            CONVERT(NVARCHAR(100), @TemplateVersionUid), N'Published', SYSUTCDATETIME());

    COMMIT TRANSACTION;
    EXEC dbo.DocumentTemplateVersion_GetByUid @TemplateVersionUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.DocumentTemplateVersion_Retire
    @TemplateUid UNIQUEIDENTIFIER,
    @TemplateVersionUid UNIQUEIDENTIFIER,
    @ExpectedRowVersion BINARY(8),
    @RetiredBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.DocumentTemplateVersion
    SET VersionStatus = N'Retired', IsCurrent = 0,
        UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @RetiredBy
    WHERE TemplateUid = @TemplateUid
      AND TemplateVersionUid = @TemplateVersionUid
      AND VersionStatus = N'Published'
      AND IsCurrent = 0
      AND RowVersion = @ExpectedRowVersion;

    IF @@ROWCOUNT = 0
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.DocumentTemplateVersion WHERE TemplateUid = @TemplateUid AND TemplateVersionUid = @TemplateVersionUid AND IsCurrent = 1)
            THROW 51034, 'The current published template version cannot be retired.', 1;
        IF EXISTS (SELECT 1 FROM dbo.DocumentTemplateVersion WHERE TemplateUid = @TemplateUid AND TemplateVersionUid = @TemplateVersionUid)
            THROW 51032, 'The template version was updated by another user.', 1;
        RETURN;
    END;

    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
        INSERT dbo.AuditLog(UserId, ActionName, EntityName, EntityId, NewValue, CreatedAt)
        VALUES(@RetiredBy, N'Retire', N'DocumentTemplateVersion',
            CONVERT(NVARCHAR(100), @TemplateVersionUid), N'Retired', SYSUTCDATETIME());

    EXEC dbo.DocumentTemplateVersion_GetByUid @TemplateVersionUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.DocumentTemplate_GetActive
AS
BEGIN
    SET NOCOUNT ON;
    SELECT template.TemplateUid, template.TemplateName,
        template.TemplateType AS DocumentType, template.Description,
        template.IsActive, version.TemplateVersionUid,
        version.VersionNumber AS CurrentVersion
    FROM dbo.DocumentTemplate AS template
    INNER JOIN dbo.DocumentTemplateVersion AS version
        ON version.TemplateUid = template.TemplateUid
       AND version.IsCurrent = 1
       AND version.VersionStatus = N'Published'
    WHERE template.IsActive = 1
    ORDER BY template.TemplateName;
END;
GO

CREATE OR ALTER PROCEDURE dbo.DocumentTemplate_GetByUid
    @TemplateUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT template.TemplateUid, template.TemplateName,
        template.TemplateType AS DocumentType, template.Description,
        COALESCE(version.TemplateContent, template.TemplateHtml) AS TemplateContent,
        template.IsActive, template.CreatedAt, template.CreatedBy,
        createdUser.DisplayName AS CreatedByDisplayName,
        template.UpdatedAt, template.UpdatedBy,
        updatedUser.DisplayName AS UpdatedByDisplayName,
        template.RowVersion, version.TemplateVersionUid,
        version.VersionNumber AS CurrentVersion
    FROM dbo.DocumentTemplate AS template
    LEFT JOIN dbo.DocumentTemplateVersion AS version
        ON version.TemplateUid = template.TemplateUid
       AND version.IsCurrent = 1
       AND version.VersionStatus = N'Published'
    LEFT JOIN dbo.ApplicationUser AS createdUser ON createdUser.UserId = template.CreatedBy
    LEFT JOIN dbo.ApplicationUser AS updatedUser ON updatedUser.UserId = template.UpdatedBy
    WHERE template.TemplateUid = @TemplateUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.DocumentTemplate_GetAll
    @StatusFilter NVARCHAR(50) = N'Active'
AS
BEGIN
    SET NOCOUNT ON;
    IF @StatusFilter NOT IN (N'Active', N'Inactive', N'All') SET @StatusFilter = N'Active';
    SELECT template.TemplateUid, template.TemplateName,
        template.TemplateType AS DocumentType, template.Description,
        COALESCE(version.TemplateContent, template.TemplateHtml) AS TemplateContent,
        template.IsActive, template.CreatedAt, template.CreatedBy,
        createdUser.DisplayName AS CreatedByDisplayName,
        template.UpdatedAt, template.UpdatedBy,
        updatedUser.DisplayName AS UpdatedByDisplayName,
        template.RowVersion, version.TemplateVersionUid,
        version.VersionNumber AS CurrentVersion
    FROM dbo.DocumentTemplate AS template
    LEFT JOIN dbo.DocumentTemplateVersion AS version
        ON version.TemplateUid = template.TemplateUid
       AND version.IsCurrent = 1
       AND version.VersionStatus = N'Published'
    LEFT JOIN dbo.ApplicationUser AS createdUser ON createdUser.UserId = template.CreatedBy
    LEFT JOIN dbo.ApplicationUser AS updatedUser ON updatedUser.UserId = template.UpdatedBy
    WHERE @StatusFilter = N'All'
       OR (@StatusFilter = N'Active' AND template.IsActive = 1)
       OR (@StatusFilter = N'Inactive' AND template.IsActive = 0)
    ORDER BY template.IsActive DESC, template.TemplateName;
END;
GO

CREATE OR ALTER PROCEDURE dbo.DocumentTemplate_Create
    @TemplateName NVARCHAR(200),
    @DocumentType NVARCHAR(100),
    @TemplateContent NVARCHAR(MAX),
    @CreatedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF NULLIF(LTRIM(RTRIM(@TemplateName)), N'') IS NULL THROW 51020, 'Template name is required.', 1;
    IF NULLIF(LTRIM(RTRIM(@DocumentType)), N'') IS NULL THROW 51021, 'Document type is required.', 1;
    DECLARE @TemplateUid UNIQUEIDENTIFIER = NEWID();
    DECLARE @TemplateVersionUid UNIQUEIDENTIFIER = NEWID();
    BEGIN TRANSACTION;
    INSERT dbo.DocumentTemplate
        (TemplateUid, TemplateName, TemplateType, TemplateHtml, IsActive, CreatedAt, CreatedBy)
    VALUES
        (@TemplateUid, LTRIM(RTRIM(@TemplateName)), LTRIM(RTRIM(@DocumentType)),
         COALESCE(@TemplateContent, N''), 1, SYSUTCDATETIME(), @CreatedBy);
    INSERT dbo.DocumentTemplateVersion
        (TemplateVersionUid, TemplateUid, VersionNumber, TemplateContent,
         VersionStatus, IsCurrent, PublishedAt, CreatedAt, CreatedBy)
    VALUES
        (@TemplateVersionUid, @TemplateUid, 1, COALESCE(@TemplateContent, N''),
         N'Published', 1, SYSUTCDATETIME(), SYSUTCDATETIME(), @CreatedBy);
    COMMIT TRANSACTION;
    EXEC dbo.DocumentTemplate_GetByUid @TemplateUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.DocumentTemplate_Update
    @TemplateUid UNIQUEIDENTIFIER,
    @TemplateName NVARCHAR(200),
    @DocumentType NVARCHAR(100),
    @TemplateContent NVARCHAR(MAX),
    @UpdatedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF NULLIF(LTRIM(RTRIM(@TemplateName)), N'') IS NULL THROW 51020, 'Template name is required.', 1;
    IF NULLIF(LTRIM(RTRIM(@DocumentType)), N'') IS NULL THROW 51021, 'Document type is required.', 1;
    IF EXISTS
    (
        SELECT 1 FROM dbo.DocumentTemplateVersion
        WHERE TemplateUid = @TemplateUid AND IsCurrent = 1
          AND TemplateContent <> COALESCE(@TemplateContent, N'')
    )
        THROW 51031, 'Published template content cannot be edited in place.', 1;

    UPDATE dbo.DocumentTemplate
    SET TemplateName = LTRIM(RTRIM(@TemplateName)),
        TemplateType = LTRIM(RTRIM(@DocumentType)),
        UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UpdatedBy
    WHERE TemplateUid = @TemplateUid;
    IF @@ROWCOUNT = 0 RETURN;
    EXEC dbo.DocumentTemplate_GetByUid @TemplateUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientDocument_GetByPatientUid
    @PatientUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT pd.PatientDocumentUid AS DocumentUid, pd.PatientUid,
        pd.TemplateUid, pd.TemplateVersionUid, pd.DocumentType,
        pd.DocumentTitle AS Title, pd.DocumentStatus,
        pd.CreatedAt, pd.UpdatedAt, pd.CreatedBy,
        applicationUser.DisplayName AS CreatedByDisplayName
    FROM dbo.PatientDocument AS pd
    LEFT JOIN dbo.ApplicationUser AS applicationUser ON applicationUser.UserId = pd.CreatedBy
    WHERE pd.PatientUid = @PatientUid AND pd.IsDeleted = 0
    ORDER BY COALESCE(pd.DocumentDate, pd.CreatedAt) DESC, pd.PatientDocumentUid DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientDocument_GetByUid
    @DocumentUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT pd.PatientDocumentUid AS DocumentUid, pd.PatientUid,
        pd.TemplateUid, pd.TemplateVersionUid, pd.DocumentType,
        pd.DocumentTitle AS Title, pd.DocumentStatus,
        content.DocumentContent, pd.CreatedBy,
        applicationUser.DisplayName AS CreatedByDisplayName,
        pd.CreatedAt, pd.UpdatedAt, pd.RowVersion
    FROM dbo.PatientDocument AS pd
    LEFT JOIN dbo.PatientDocumentContent AS content
        ON content.PatientDocumentUid = pd.PatientDocumentUid
    LEFT JOIN dbo.ApplicationUser AS applicationUser ON applicationUser.UserId = pd.CreatedBy
    WHERE pd.PatientDocumentUid = @DocumentUid AND pd.IsDeleted = 0;
END;
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
    DECLARE @TemplateVersionUid UNIQUEIDENTIFIER;
    DECLARE @ResolvedContent NVARCHAR(MAX) = @DocumentContent;

    BEGIN TRANSACTION;
    IF NOT EXISTS (SELECT 1 FROM dbo.Patient WHERE PatientUid = @PatientUid)
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51001, 'The requested patient was not found.', 1;
    END;

    IF @TemplateUid IS NOT NULL
    BEGIN
        SELECT @TemplateVersionUid = version.TemplateVersionUid,
               @ResolvedContent = version.TemplateContent
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
    END;

    INSERT dbo.PatientDocument
    (
        PatientDocumentUid, PatientUid, TemplateUid, TemplateVersionUid,
        DocumentTitle, DocumentType, DocumentStatus, DocumentDate,
        CreatedAt, CreatedBy, IsDeleted
    )
    VALUES
    (
        @DocumentUid, @PatientUid, @TemplateUid, @TemplateVersionUid,
        @Title, @DocumentType, N'Draft', SYSUTCDATETIME(),
        SYSUTCDATETIME(), @CreatedBy, 0
    );

    INSERT dbo.PatientDocumentContent
        (PatientDocumentUid, DocumentContent, CreatedAt, CreatedBy)
    VALUES
        (@DocumentUid, @ResolvedContent, SYSUTCDATETIME(), @CreatedBy);

    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
        INSERT dbo.AuditLog(UserId, PatientId, ActionName, EntityName, EntityId, NewValue, CreatedAt)
        SELECT @CreatedBy, PatientId, N'Create', N'PatientDocument',
            CONVERT(NVARCHAR(100), @DocumentUid), @Title, SYSUTCDATETIME()
        FROM dbo.Patient WHERE PatientUid = @PatientUid;

    COMMIT TRANSACTION;
    EXEC dbo.PatientDocument_GetByUid @DocumentUid;
END;
GO
