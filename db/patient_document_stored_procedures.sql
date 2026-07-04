/*
    Patient Documents API stored procedures.

    This script creates the schema pieces and procedures required by
    PatientDocumentRepository. The API contract uses UIDs, so the procedures
    do not depend on internal identity columns such as PatientDocumentId,
    PatientId, or TemplateId on the document rows.
*/

IF OBJECT_ID(N'dbo.DocumentTemplate', N'U') IS NULL
BEGIN
    THROW 51010, 'Required table dbo.DocumentTemplate was not found.', 1;
END;
GO

IF OBJECT_ID(N'dbo.PatientDocument', N'U') IS NULL
BEGIN
    THROW 51011, 'Required table dbo.PatientDocument was not found.', 1;
END;
GO

IF OBJECT_ID(N'dbo.Patient', N'U') IS NULL
BEGIN
    THROW 51012, 'Required table dbo.Patient was not found.', 1;
END;
GO

IF COL_LENGTH('dbo.DocumentTemplate', 'TemplateUid') IS NULL
BEGIN
    ALTER TABLE dbo.DocumentTemplate
        ADD TemplateUid UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_DocumentTemplate_TemplateUid DEFAULT NEWID()
            WITH VALUES;
END;
GO

IF COL_LENGTH('dbo.DocumentTemplate', 'TemplateName') IS NULL
BEGIN
    ALTER TABLE dbo.DocumentTemplate
        ADD TemplateName NVARCHAR(150) NOT NULL
            CONSTRAINT DF_DocumentTemplate_TemplateName DEFAULT N'Untitled'
            WITH VALUES;
END;
GO

IF COL_LENGTH('dbo.DocumentTemplate', 'TemplateType') IS NULL
BEGIN
    ALTER TABLE dbo.DocumentTemplate
        ADD TemplateType NVARCHAR(100) NOT NULL
            CONSTRAINT DF_DocumentTemplate_TemplateType DEFAULT N'General'
            WITH VALUES;
END;
GO

IF COL_LENGTH('dbo.DocumentTemplate', 'TemplateHtml') IS NULL
BEGIN
    ALTER TABLE dbo.DocumentTemplate
        ADD TemplateHtml NVARCHAR(MAX) NOT NULL
            CONSTRAINT DF_DocumentTemplate_TemplateHtml DEFAULT N''
            WITH VALUES;
END;
GO

IF COL_LENGTH('dbo.DocumentTemplate', 'Description') IS NULL
BEGIN
    ALTER TABLE dbo.DocumentTemplate
        ADD Description NVARCHAR(500) NULL;
END;
GO

IF COL_LENGTH('dbo.DocumentTemplate', 'IsActive') IS NULL
BEGIN
    ALTER TABLE dbo.DocumentTemplate
        ADD IsActive BIT NOT NULL
            CONSTRAINT DF_DocumentTemplate_IsActive DEFAULT CONVERT(BIT, 1)
            WITH VALUES;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.DocumentTemplate')
        AND name = N'UQ_DocumentTemplate_TemplateUid'
)
BEGIN
    ALTER TABLE dbo.DocumentTemplate
        ADD CONSTRAINT UQ_DocumentTemplate_TemplateUid UNIQUE (TemplateUid);
END;
GO

IF COL_LENGTH('dbo.PatientDocument', 'PatientDocumentUid') IS NULL
BEGIN
    ALTER TABLE dbo.PatientDocument
        ADD PatientDocumentUid UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_PatientDocument_PatientDocumentUid DEFAULT NEWID()
            WITH VALUES;
END;
GO

IF COL_LENGTH('dbo.PatientDocument', 'PatientUid') IS NULL
BEGIN
    ALTER TABLE dbo.PatientDocument
        ADD PatientUid UNIQUEIDENTIFIER NULL;
END;
GO

IF COL_LENGTH('dbo.PatientDocument', 'TemplateUid') IS NULL
BEGIN
    ALTER TABLE dbo.PatientDocument
        ADD TemplateUid UNIQUEIDENTIFIER NULL;
END;
GO

IF COL_LENGTH('dbo.PatientDocument', 'DocumentTitle') IS NULL
BEGIN
    ALTER TABLE dbo.PatientDocument
        ADD DocumentTitle NVARCHAR(255) NOT NULL
            CONSTRAINT DF_PatientDocument_DocumentTitle DEFAULT N'Untitled'
            WITH VALUES;
END;
GO

IF COL_LENGTH('dbo.PatientDocument', 'DocumentType') IS NULL
BEGIN
    ALTER TABLE dbo.PatientDocument
        ADD DocumentType NVARCHAR(100) NOT NULL
            CONSTRAINT DF_PatientDocument_DocumentType DEFAULT N'General'
            WITH VALUES;
END;
GO

IF COL_LENGTH('dbo.PatientDocument', 'DocumentStatus') IS NULL
BEGIN
    ALTER TABLE dbo.PatientDocument
        ADD DocumentStatus NVARCHAR(50) NOT NULL
            CONSTRAINT DF_PatientDocument_DocumentStatus DEFAULT N'Draft'
            WITH VALUES;
END;
GO

IF COL_LENGTH('dbo.PatientDocument', 'DocumentDate') IS NULL
BEGIN
    ALTER TABLE dbo.PatientDocument
        ADD DocumentDate DATETIME2 NULL;
END;
GO

IF COL_LENGTH('dbo.PatientDocument', 'IsDeleted') IS NULL
BEGIN
    ALTER TABLE dbo.PatientDocument
        ADD IsDeleted BIT NOT NULL
            CONSTRAINT DF_PatientDocument_IsDeleted DEFAULT CONVERT(BIT, 0)
            WITH VALUES;
END;
GO

IF COL_LENGTH('dbo.PatientDocument', 'CreatedAt') IS NULL
BEGIN
    ALTER TABLE dbo.PatientDocument
        ADD CreatedAt DATETIME2 NOT NULL
            CONSTRAINT DF_PatientDocument_CreatedAt DEFAULT SYSUTCDATETIME()
            WITH VALUES;
END;
GO

IF COL_LENGTH('dbo.PatientDocument', 'CreatedBy') IS NULL
BEGIN
    ALTER TABLE dbo.PatientDocument
        ADD CreatedBy BIGINT NULL;
END;
GO

IF COL_LENGTH('dbo.PatientDocument', 'UpdatedAt') IS NULL
BEGIN
    ALTER TABLE dbo.PatientDocument
        ADD UpdatedAt DATETIME2 NULL;
END;
GO

IF COL_LENGTH('dbo.PatientDocument', 'RowVersion') IS NULL
BEGIN
    ALTER TABLE dbo.PatientDocument
        ADD RowVersion ROWVERSION;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.PatientDocument')
        AND name = N'UQ_PatientDocument_PatientDocumentUid'
)
BEGIN
    ALTER TABLE dbo.PatientDocument
        ADD CONSTRAINT UQ_PatientDocument_PatientDocumentUid UNIQUE (PatientDocumentUid);
END;
GO

IF OBJECT_ID(N'dbo.PatientDocumentContent', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PatientDocumentContent
    (
        PatientDocumentContentId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        PatientDocumentUid UNIQUEIDENTIFIER NOT NULL,
        DocumentContent NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL
            CONSTRAINT DF_PatientDocumentContent_CreatedAt
            DEFAULT SYSUTCDATETIME(),
        CreatedBy BIGINT NULL,
        UpdatedAt DATETIME2 NULL,
        UpdatedBy BIGINT NULL,
        RowVersion ROWVERSION NOT NULL,

        CONSTRAINT FK_PatientDocumentContent_PatientDocumentUid
            FOREIGN KEY (PatientDocumentUid)
            REFERENCES dbo.PatientDocument(PatientDocumentUid),

        CONSTRAINT UQ_PatientDocumentContent_PatientDocumentUid
            UNIQUE (PatientDocumentUid)
    );
END;
GO

IF COL_LENGTH('dbo.PatientDocumentContent', 'PatientDocumentUid') IS NULL
BEGIN
    ALTER TABLE dbo.PatientDocumentContent
        ADD PatientDocumentUid UNIQUEIDENTIFIER NULL;
END;
GO

IF COL_LENGTH('dbo.PatientDocumentContent', 'DocumentContent') IS NULL
BEGIN
    ALTER TABLE dbo.PatientDocumentContent
        ADD DocumentContent NVARCHAR(MAX) NULL;
END;
GO

IF COL_LENGTH('dbo.PatientDocumentContent', 'CreatedAt') IS NULL
BEGIN
    ALTER TABLE dbo.PatientDocumentContent
        ADD CreatedAt DATETIME2 NOT NULL
            CONSTRAINT DF_PatientDocumentContent_CreatedAt_Missing
            DEFAULT SYSUTCDATETIME()
            WITH VALUES;
END;
GO

IF COL_LENGTH('dbo.PatientDocumentContent', 'CreatedBy') IS NULL
BEGIN
    ALTER TABLE dbo.PatientDocumentContent
        ADD CreatedBy BIGINT NULL;
END;
GO

IF COL_LENGTH('dbo.PatientDocumentContent', 'UpdatedAt') IS NULL
BEGIN
    ALTER TABLE dbo.PatientDocumentContent
        ADD UpdatedAt DATETIME2 NULL;
END;
GO

CREATE OR ALTER PROCEDURE dbo.DocumentTemplate_GetActive
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        dt.TemplateUid AS TemplateUid,
        dt.TemplateName AS TemplateName,
        dt.TemplateType AS DocumentType,
        dt.Description AS Description,
        dt.IsActive AS IsActive
    FROM dbo.DocumentTemplate AS dt
    WHERE dt.IsActive = CONVERT(BIT, 1)
    ORDER BY
        dt.TemplateName;
END;
GO

CREATE OR ALTER PROCEDURE dbo.DocumentTemplate_GetByUid
    @TemplateUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        dt.TemplateUid AS TemplateUid,
        dt.TemplateName AS TemplateName,
        dt.TemplateType AS DocumentType,
        dt.Description AS Description,
        dt.TemplateHtml AS TemplateContent,
        dt.IsActive AS IsActive
    FROM dbo.DocumentTemplate AS dt
    WHERE dt.TemplateUid = @TemplateUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientDocument_GetByPatientUid
    @PatientUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        pd.PatientDocumentUid AS DocumentUid,
        pd.PatientUid AS PatientUid,
        pd.TemplateUid AS TemplateUid,
        pd.DocumentType AS DocumentType,
        pd.DocumentTitle AS Title,
        pd.DocumentStatus AS DocumentStatus,
        pd.CreatedAt AS CreatedAt,
        pd.UpdatedAt AS UpdatedAt,
        pd.CreatedBy AS CreatedBy,
        au.DisplayName AS CreatedByDisplayName
    FROM dbo.PatientDocument AS pd
    LEFT JOIN dbo.ApplicationUser AS au
        ON au.UserId = pd.CreatedBy
    WHERE pd.PatientUid = @PatientUid
        AND pd.IsDeleted = CONVERT(BIT, 0)
    ORDER BY
        COALESCE(pd.DocumentDate, pd.CreatedAt) DESC,
        pd.PatientDocumentUid DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientDocument_GetByUid
    @DocumentUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        pd.PatientDocumentUid AS DocumentUid,
        pd.PatientUid AS PatientUid,
        pd.TemplateUid AS TemplateUid,
        pd.DocumentType AS DocumentType,
        pd.DocumentTitle AS Title,
        pd.DocumentStatus AS DocumentStatus,
        pdc.DocumentContent AS DocumentContent,
        pd.CreatedBy AS CreatedBy,
        au.DisplayName AS CreatedByDisplayName,
        pd.CreatedAt AS CreatedAt,
        pd.UpdatedAt AS UpdatedAt,
        pd.RowVersion AS RowVersion
    FROM dbo.PatientDocument AS pd
    LEFT JOIN dbo.PatientDocumentContent AS pdc
        ON pdc.PatientDocumentUid = pd.PatientDocumentUid
    LEFT JOIN dbo.ApplicationUser AS au
        ON au.UserId = pd.CreatedBy
    WHERE pd.PatientDocumentUid = @DocumentUid
        AND pd.IsDeleted = CONVERT(BIT, 0);
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

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.Patient AS p
        WHERE p.PatientUid = @PatientUid
    )
    BEGIN
        THROW 51001, 'The requested patient was not found.', 1;
    END;

    IF @TemplateUid IS NOT NULL
        AND NOT EXISTS
        (
            SELECT 1
            FROM dbo.DocumentTemplate AS dt
            WHERE dt.TemplateUid = @TemplateUid
                AND dt.IsActive = CONVERT(BIT, 1)
        )
    BEGIN
        THROW 51002, 'The selected document template is invalid or inactive.', 1;
    END;

    BEGIN TRANSACTION;

    INSERT INTO dbo.PatientDocument
    (
        PatientDocumentUid,
        PatientUid,
        TemplateUid,
        DocumentTitle,
        DocumentType,
        DocumentStatus,
        DocumentDate,
        CreatedAt,
        CreatedBy,
        IsDeleted
    )
    VALUES
    (
        @DocumentUid,
        @PatientUid,
        @TemplateUid,
        @Title,
        @DocumentType,
        N'Draft',
        SYSUTCDATETIME(),
        SYSUTCDATETIME(),
        @CreatedBy,
        CONVERT(BIT, 0)
    );

    INSERT INTO dbo.PatientDocumentContent
    (
        PatientDocumentUid,
        DocumentContent,
        CreatedAt,
        CreatedBy
    )
    VALUES
    (
        @DocumentUid,
        @DocumentContent,
        SYSUTCDATETIME(),
        @CreatedBy
    );

    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
    BEGIN
        INSERT INTO dbo.AuditLog
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
        SELECT
            @CreatedBy,
            p.PatientId,
            N'Create',
            N'PatientDocument',
            CONVERT(NVARCHAR(100), @DocumentUid),
            NULL,
            @Title,
            SYSUTCDATETIME()
        FROM dbo.Patient AS p
        WHERE p.PatientUid = @PatientUid;
    END;

    COMMIT TRANSACTION;

    EXEC dbo.PatientDocument_GetByUid
        @DocumentUid = @DocumentUid;
END;
GO
