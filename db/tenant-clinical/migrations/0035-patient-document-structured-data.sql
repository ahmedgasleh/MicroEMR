SET XACT_ABORT ON;
GO

IF COL_LENGTH('dbo.PatientDocumentContent', 'StructuredDataJson') IS NULL
    ALTER TABLE dbo.PatientDocumentContent ADD StructuredDataJson NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PatientDocumentContent_StructuredDataJson')
    ALTER TABLE dbo.PatientDocumentContent ADD CONSTRAINT CK_PatientDocumentContent_StructuredDataJson
        CHECK (StructuredDataJson IS NULL OR ISJSON(StructuredDataJson) = 1);
GO

CREATE OR ALTER PROCEDURE dbo.PatientDocument_GetByUid
    @DocumentUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT pd.PatientDocumentUid AS DocumentUid, pd.PatientUid,
        pd.TemplateUid, pd.TemplateVersionUid, pd.DocumentType,
        pd.DocumentTitle AS Title, pd.DocumentStatus,
        content.DocumentContent, content.StructuredDataJson, pd.CreatedBy,
        applicationUser.DisplayName AS CreatedByDisplayName,
        pd.CreatedAt, pd.UpdatedAt, pd.RowVersion,
        content.RowVersion AS ContentRowVersion
    FROM dbo.PatientDocument AS pd
    LEFT JOIN dbo.PatientDocumentContent AS content ON content.PatientDocumentUid = pd.PatientDocumentUid
    LEFT JOIN dbo.ApplicationUser AS applicationUser ON applicationUser.UserId = pd.CreatedBy
    WHERE pd.PatientDocumentUid = @DocumentUid AND pd.IsDeleted = 0;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientDocument_Create
    @PatientUid UNIQUEIDENTIFIER,
    @TemplateUid UNIQUEIDENTIFIER = NULL,
    @TemplateVersionUid UNIQUEIDENTIFIER = NULL,
    @DocumentType NVARCHAR(100),
    @Title NVARCHAR(250),
    @DocumentContent NVARCHAR(MAX) = NULL,
    @StructuredDataJson NVARCHAR(MAX) = NULL,
    @CreatedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @DocumentUid UNIQUEIDENTIFIER = NEWID(), @PatientId BIGINT,
        @ResolvedTemplateVersionUid UNIQUEIDENTIFIER, @TemplateContent NVARCHAR(MAX),
        @ResolvedContent NVARCHAR(MAX) = @DocumentContent;
    IF @StructuredDataJson IS NOT NULL AND ISJSON(@StructuredDataJson) <> 1
        THROW 51084, 'Structured patient document data must be valid JSON.', 1;

    BEGIN TRANSACTION;
    SELECT @PatientId = PatientId FROM dbo.Patient WITH (HOLDLOCK)
    WHERE PatientUid = @PatientUid AND IsDeleted = 0;
    IF @PatientId IS NULL BEGIN ROLLBACK; THROW 51001, 'The requested patient was not found.', 1; END;

    IF @TemplateUid IS NOT NULL
    BEGIN
        SELECT @ResolvedTemplateVersionUid=version.TemplateVersionUid,@TemplateContent=version.TemplateContent
        FROM dbo.DocumentTemplate AS template WITH (HOLDLOCK)
        JOIN dbo.DocumentTemplateVersion AS version WITH (HOLDLOCK)
          ON version.TemplateUid=template.TemplateUid AND version.TemplateVersionUid=@TemplateVersionUid
         AND version.IsCurrent=1 AND version.VersionStatus=N'Published'
        WHERE template.TemplateUid=@TemplateUid AND template.IsActive=1 AND template.TemplateKind=N'Document';
        IF @ResolvedTemplateVersionUid IS NULL BEGIN ROLLBACK; THROW 51002, 'The selected document template version is not active and published.', 1; END;
        IF @ResolvedContent IS NULL SET @ResolvedContent=@TemplateContent;
    END;

    INSERT dbo.PatientDocument(PatientDocumentUid,PatientId,PatientUid,TemplateUid,TemplateVersionUid,
        DocumentTitle,DocumentType,DocumentStatus,DocumentDate,CreatedAt,CreatedBy,IsDeleted)
    VALUES(@DocumentUid,@PatientId,@PatientUid,@TemplateUid,@ResolvedTemplateVersionUid,
        @Title,@DocumentType,N'Draft',SYSUTCDATETIME(),SYSUTCDATETIME(),@CreatedBy,0);
    INSERT dbo.PatientDocumentContent(PatientDocumentUid,DocumentContent,StructuredDataJson,CreatedAt,CreatedBy)
    VALUES(@DocumentUid,@ResolvedContent,@StructuredDataJson,SYSUTCDATETIME(),@CreatedBy);
    IF OBJECT_ID(N'dbo.AuditLog',N'U') IS NOT NULL
        INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt)
        VALUES(@CreatedBy,@PatientId,N'Create',N'PatientDocument',CONVERT(NVARCHAR(100),@DocumentUid),@Title,SYSUTCDATETIME());
    COMMIT; EXEC dbo.PatientDocument_GetByUid @DocumentUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientDocument_UpdateDraft
    @DocumentUid UNIQUEIDENTIFIER,@DocumentTitle NVARCHAR(250),@DocumentType NVARCHAR(100),
    @DocumentContent NVARCHAR(MAX),@StructuredDataJson NVARCHAR(MAX)=NULL,
    @ExpectedDocumentRowVersion BINARY(8),@ExpectedContentRowVersion BINARY(8),@UpdatedBy BIGINT=NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF @StructuredDataJson IS NOT NULL AND ISJSON(@StructuredDataJson) <> 1
        THROW 51084, 'Structured patient document data must be valid JSON.', 1;
    DECLARE @PatientId BIGINT,@DocumentStatus NVARCHAR(50),@DocumentRowVersion BINARY(8),
        @ContentRowVersion BINARY(8),@ExistingStructuredDataJson NVARCHAR(MAX);
    BEGIN TRANSACTION;
    SELECT @PatientId=document.PatientId,@DocumentStatus=document.DocumentStatus,@DocumentRowVersion=document.RowVersion,
        @ContentRowVersion=content.RowVersion,@ExistingStructuredDataJson=content.StructuredDataJson
    FROM dbo.PatientDocument document WITH(UPDLOCK,HOLDLOCK)
    LEFT JOIN dbo.PatientDocumentContent content WITH(UPDLOCK,HOLDLOCK) ON content.PatientDocumentUid=document.PatientDocumentUid
    WHERE document.PatientDocumentUid=@DocumentUid AND document.IsDeleted=0;
    IF @PatientId IS NULL BEGIN ROLLBACK; THROW 51080, 'The requested patient document was not found.', 1; END;
    IF @DocumentStatus<>N'Draft' BEGIN ROLLBACK; THROW 51081, 'Only draft patient documents can be edited.', 1; END;
    IF @ContentRowVersion IS NULL BEGIN ROLLBACK; THROW 51083, 'The patient document content was not found.', 1; END;
    IF @DocumentRowVersion<>@ExpectedDocumentRowVersion OR @ContentRowVersion<>@ExpectedContentRowVersion
        BEGIN ROLLBACK; THROW 51082, 'The patient document was changed by another user.', 1; END;
    IF (@ExistingStructuredDataJson IS NULL AND @StructuredDataJson IS NOT NULL)
       OR (@ExistingStructuredDataJson IS NOT NULL AND @StructuredDataJson IS NULL)
        BEGIN ROLLBACK; THROW 51084, 'Patient document storage mode cannot be changed.', 1; END;
    UPDATE dbo.PatientDocument SET DocumentTitle=LTRIM(RTRIM(@DocumentTitle)),DocumentType=LTRIM(RTRIM(@DocumentType)),UpdatedAt=SYSUTCDATETIME()
    WHERE PatientDocumentUid=@DocumentUid AND IsDeleted=0 AND DocumentStatus=N'Draft' AND RowVersion=@ExpectedDocumentRowVersion;
    IF @@ROWCOUNT<>1 BEGIN ROLLBACK; THROW 51082, 'The patient document was changed by another user.', 1; END;
    UPDATE dbo.PatientDocumentContent SET DocumentContent=@DocumentContent,StructuredDataJson=@StructuredDataJson,
        UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@UpdatedBy
    WHERE PatientDocumentUid=@DocumentUid AND RowVersion=@ExpectedContentRowVersion;
    IF @@ROWCOUNT<>1 BEGIN ROLLBACK; THROW 51082, 'The patient document was changed by another user.', 1; END;
    IF OBJECT_ID(N'dbo.AuditLog',N'U') IS NOT NULL
        INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt)
        VALUES(@UpdatedBy,@PatientId,N'UpdateDraft',N'PatientDocument',CONVERT(NVARCHAR(100),@DocumentUid),N'Draft document updated',SYSUTCDATETIME());
    COMMIT; EXEC dbo.PatientDocument_GetByUid @DocumentUid;
END;
GO
