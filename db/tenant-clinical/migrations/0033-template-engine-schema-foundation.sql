IF COL_LENGTH('dbo.DocumentTemplate', 'TemplateKind') IS NULL
    ALTER TABLE dbo.DocumentTemplate ADD TemplateKind NVARCHAR(20) NOT NULL
        CONSTRAINT DF_DocumentTemplate_TemplateKind DEFAULT N'Document';
GO

IF COL_LENGTH('dbo.DocumentTemplate', 'Category') IS NULL
BEGIN
    ALTER TABLE dbo.DocumentTemplate ADD Category NVARCHAR(100) NULL;
    UPDATE dbo.DocumentTemplate SET Category = TemplateType WHERE Category IS NULL;
END;
GO

IF COL_LENGTH('dbo.DocumentTemplate', 'TemplateScope') IS NULL
    ALTER TABLE dbo.DocumentTemplate ADD TemplateScope NVARCHAR(20) NOT NULL
        CONSTRAINT DF_DocumentTemplate_TemplateScope DEFAULT N'Clinic';
GO

IF COL_LENGTH('dbo.DocumentTemplate', 'OwnerUserId') IS NULL
    ALTER TABLE dbo.DocumentTemplate ADD OwnerUserId BIGINT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DocumentTemplate_TemplateKind')
    ALTER TABLE dbo.DocumentTemplate ADD CONSTRAINT CK_DocumentTemplate_TemplateKind
        CHECK (TemplateKind IN (N'Document', N'Encounter'));
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DocumentTemplate_TemplateScope')
    ALTER TABLE dbo.DocumentTemplate ADD CONSTRAINT CK_DocumentTemplate_TemplateScope
        CHECK (TemplateScope IN (N'System', N'Clinic', N'Personal'));
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DocumentTemplate_PersonalOwner')
    ALTER TABLE dbo.DocumentTemplate ADD CONSTRAINT CK_DocumentTemplate_PersonalOwner
        CHECK (TemplateScope <> N'Personal' OR OwnerUserId IS NOT NULL);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DocumentTemplate_OwnerUserId')
    ALTER TABLE dbo.DocumentTemplate WITH CHECK ADD CONSTRAINT FK_DocumentTemplate_OwnerUserId
        FOREIGN KEY (OwnerUserId) REFERENCES dbo.ApplicationUser(UserId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.DocumentTemplate') AND name = N'IX_DocumentTemplate_KindScopeOwner')
    CREATE INDEX IX_DocumentTemplate_KindScopeOwner
        ON dbo.DocumentTemplate(TemplateKind, TemplateScope, OwnerUserId, IsActive);
GO

IF COL_LENGTH('dbo.DocumentTemplateVersion', 'SchemaVersion') IS NULL
    ALTER TABLE dbo.DocumentTemplateVersion ADD SchemaVersion INT NOT NULL
        CONSTRAINT DF_DocumentTemplateVersion_SchemaVersion DEFAULT 1;
GO

IF COL_LENGTH('dbo.DocumentTemplateVersion', 'DefinitionJson') IS NULL
    ALTER TABLE dbo.DocumentTemplateVersion ADD DefinitionJson NVARCHAR(MAX) NOT NULL
        CONSTRAINT DF_DocumentTemplateVersion_DefinitionJson DEFAULT N'{"schemaVersion":1,"sections":[]}';
GO

IF COL_LENGTH('dbo.DocumentTemplateVersion', 'PublishedBy') IS NULL
BEGIN
    ALTER TABLE dbo.DocumentTemplateVersion ADD PublishedBy BIGINT NULL;
    UPDATE dbo.DocumentTemplateVersion
    SET PublishedBy = COALESCE(UpdatedBy, CreatedBy)
    WHERE VersionStatus = N'Published' AND PublishedBy IS NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DocumentTemplateVersion_SchemaVersion')
    ALTER TABLE dbo.DocumentTemplateVersion ADD CONSTRAINT CK_DocumentTemplateVersion_SchemaVersion
        CHECK (SchemaVersion > 0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DocumentTemplateVersion_DefinitionJson')
    ALTER TABLE dbo.DocumentTemplateVersion ADD CONSTRAINT CK_DocumentTemplateVersion_DefinitionJson
        CHECK (ISJSON(DefinitionJson) = 1
            AND TRY_CONVERT(INT, JSON_VALUE(DefinitionJson, '$.schemaVersion')) = SchemaVersion
            AND JSON_QUERY(DefinitionJson, '$.sections') IS NOT NULL);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DocumentTemplateVersion_PublishedBy')
    ALTER TABLE dbo.DocumentTemplateVersion WITH CHECK ADD CONSTRAINT FK_DocumentTemplateVersion_PublishedBy
        FOREIGN KEY (PublishedBy) REFERENCES dbo.ApplicationUser(UserId);
GO

CREATE OR ALTER PROCEDURE dbo.DocumentTemplateVersion_GetByUid
    @TemplateVersionUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TemplateVersionUid, TemplateUid, VersionNumber, TemplateContent,
        SchemaVersion, DefinitionJson, VersionStatus, IsCurrent, PublishedAt, PublishedBy,
        CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, RowVersion
    FROM dbo.DocumentTemplateVersion WHERE TemplateVersionUid = @TemplateVersionUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.DocumentTemplateVersion_GetByTemplateUid
    @TemplateUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TemplateVersionUid, TemplateUid, VersionNumber, TemplateContent,
        SchemaVersion, DefinitionJson, VersionStatus, IsCurrent, PublishedAt, PublishedBy,
        CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, RowVersion
    FROM dbo.DocumentTemplateVersion WHERE TemplateUid = @TemplateUid ORDER BY VersionNumber DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.DocumentTemplateVersion_CreateDraft
    @TemplateUid UNIQUEIDENTIFIER, @CreatedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @TemplateVersionUid UNIQUEIDENTIFIER = NEWID(), @VersionNumber INT,
        @TemplateContent NVARCHAR(MAX), @SchemaVersion INT, @DefinitionJson NVARCHAR(MAX);
    BEGIN TRANSACTION;
    IF NOT EXISTS (SELECT 1 FROM dbo.DocumentTemplate WITH (UPDLOCK, HOLDLOCK) WHERE TemplateUid=@TemplateUid)
    BEGIN ROLLBACK TRANSACTION; RETURN; END;
    SELECT @VersionNumber=ISNULL(MAX(VersionNumber),0)+1 FROM dbo.DocumentTemplateVersion WITH (UPDLOCK,HOLDLOCK) WHERE TemplateUid=@TemplateUid;
    SELECT @TemplateContent=TemplateContent, @SchemaVersion=SchemaVersion, @DefinitionJson=DefinitionJson
    FROM dbo.DocumentTemplateVersion WHERE TemplateUid=@TemplateUid AND IsCurrent=1 AND VersionStatus=N'Published';
    IF @TemplateContent IS NULL BEGIN ROLLBACK TRANSACTION; THROW 51033, 'The template has no current published version.', 1; END;
    INSERT dbo.DocumentTemplateVersion(TemplateVersionUid,TemplateUid,VersionNumber,TemplateContent,SchemaVersion,DefinitionJson,VersionStatus,IsCurrent,CreatedAt,CreatedBy)
    VALUES(@TemplateVersionUid,@TemplateUid,@VersionNumber,@TemplateContent,@SchemaVersion,@DefinitionJson,N'Draft',0,SYSUTCDATETIME(),@CreatedBy);
    IF OBJECT_ID(N'dbo.AuditLog',N'U') IS NOT NULL INSERT dbo.AuditLog(UserId,ActionName,EntityName,EntityId,NewValue,CreatedAt)
        VALUES(@CreatedBy,N'CreateDraft',N'DocumentTemplateVersion',CONVERT(NVARCHAR(100),@TemplateVersionUid),CONCAT(N'Version ',@VersionNumber),SYSUTCDATETIME());
    COMMIT TRANSACTION; EXEC dbo.DocumentTemplateVersion_GetByUid @TemplateVersionUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.DocumentTemplateVersion_UpdateDraft
    @TemplateUid UNIQUEIDENTIFIER, @TemplateVersionUid UNIQUEIDENTIFIER,
    @TemplateContent NVARCHAR(MAX), @ExpectedRowVersion BINARY(8), @UpdatedBy BIGINT = NULL,
    @SchemaVersion INT = NULL, @DefinitionJson NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ResolvedSchemaVersion INT = COALESCE(@SchemaVersion, (SELECT SchemaVersion FROM dbo.DocumentTemplateVersion WHERE TemplateVersionUid=@TemplateVersionUid));
    IF @DefinitionJson IS NOT NULL AND (ISJSON(@DefinitionJson)<>1
        OR TRY_CONVERT(INT,JSON_VALUE(@DefinitionJson,'$.schemaVersion'))<>@ResolvedSchemaVersion
        OR JSON_QUERY(@DefinitionJson,'$.sections') IS NULL)
        THROW 51035, 'Template definition JSON is invalid or its schemaVersion does not match.', 1;
    UPDATE dbo.DocumentTemplateVersion SET TemplateContent=COALESCE(@TemplateContent,N''),
        SchemaVersion=@ResolvedSchemaVersion, DefinitionJson=COALESCE(@DefinitionJson,DefinitionJson),
        UpdatedAt=SYSUTCDATETIME(), UpdatedBy=@UpdatedBy
    WHERE TemplateUid=@TemplateUid AND TemplateVersionUid=@TemplateVersionUid AND VersionStatus=N'Draft' AND RowVersion=@ExpectedRowVersion;
    IF @@ROWCOUNT=0
    BEGIN
        IF EXISTS(SELECT 1 FROM dbo.DocumentTemplateVersion WHERE TemplateUid=@TemplateUid AND TemplateVersionUid=@TemplateVersionUid AND VersionStatus<>N'Draft') THROW 51031, 'Published or retired template versions are immutable.', 1;
        IF EXISTS(SELECT 1 FROM dbo.DocumentTemplateVersion WHERE TemplateUid=@TemplateUid AND TemplateVersionUid=@TemplateVersionUid) THROW 51032, 'The template version was updated by another user.', 1;
        RETURN;
    END;
    IF OBJECT_ID(N'dbo.AuditLog',N'U') IS NOT NULL INSERT dbo.AuditLog(UserId,ActionName,EntityName,EntityId,NewValue,CreatedAt)
        VALUES(@UpdatedBy,N'UpdateDraft',N'DocumentTemplateVersion',CONVERT(NVARCHAR(100),@TemplateVersionUid),N'Draft content and definition updated',SYSUTCDATETIME());
    EXEC dbo.DocumentTemplateVersion_GetByUid @TemplateVersionUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.DocumentTemplateVersion_Publish
    @TemplateUid UNIQUEIDENTIFIER, @TemplateVersionUid UNIQUEIDENTIFIER,
    @ExpectedRowVersion BINARY(8), @PublishedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRANSACTION;
    IF NOT EXISTS(SELECT 1 FROM dbo.DocumentTemplateVersion WITH(UPDLOCK,HOLDLOCK) WHERE TemplateUid=@TemplateUid AND TemplateVersionUid=@TemplateVersionUid AND VersionStatus=N'Draft' AND RowVersion=@ExpectedRowVersion)
    BEGIN
        IF EXISTS(SELECT 1 FROM dbo.DocumentTemplateVersion WHERE TemplateUid=@TemplateUid AND TemplateVersionUid=@TemplateVersionUid AND VersionStatus<>N'Draft') BEGIN ROLLBACK; THROW 51031, 'Only draft template versions can be published.', 1; END;
        IF EXISTS(SELECT 1 FROM dbo.DocumentTemplateVersion WHERE TemplateUid=@TemplateUid AND TemplateVersionUid=@TemplateVersionUid) BEGIN ROLLBACK; THROW 51032, 'The template version was updated by another user.', 1; END;
        ROLLBACK; RETURN;
    END;
    UPDATE dbo.DocumentTemplateVersion SET VersionStatus=N'Retired',IsCurrent=0,UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@PublishedBy WHERE TemplateUid=@TemplateUid AND IsCurrent=1;
    UPDATE dbo.DocumentTemplateVersion SET VersionStatus=N'Published',IsCurrent=1,PublishedAt=SYSUTCDATETIME(),PublishedBy=@PublishedBy,UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@PublishedBy
    WHERE TemplateUid=@TemplateUid AND TemplateVersionUid=@TemplateVersionUid AND VersionStatus=N'Draft' AND RowVersion=@ExpectedRowVersion;
    IF @@ROWCOUNT<>1 BEGIN ROLLBACK; THROW 51032, 'The template version was updated by another user.', 1; END;
    UPDATE t SET TemplateHtml=v.TemplateContent,UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@PublishedBy FROM dbo.DocumentTemplate t JOIN dbo.DocumentTemplateVersion v ON v.TemplateUid=t.TemplateUid AND v.TemplateVersionUid=@TemplateVersionUid WHERE t.TemplateUid=@TemplateUid;
    IF OBJECT_ID(N'dbo.AuditLog',N'U') IS NOT NULL INSERT dbo.AuditLog(UserId,ActionName,EntityName,EntityId,NewValue,CreatedAt)
        VALUES(@PublishedBy,N'Publish',N'DocumentTemplateVersion',CONVERT(NVARCHAR(100),@TemplateVersionUid),N'Published',SYSUTCDATETIME());
    COMMIT; EXEC dbo.DocumentTemplateVersion_GetByUid @TemplateVersionUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.DocumentTemplate_Create
    @TemplateName NVARCHAR(200), @DocumentType NVARCHAR(100),
    @TemplateContent NVARCHAR(MAX), @CreatedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF NULLIF(LTRIM(RTRIM(@TemplateName)),N'') IS NULL THROW 51020, 'Template name is required.', 1;
    IF NULLIF(LTRIM(RTRIM(@DocumentType)),N'') IS NULL THROW 51021, 'Document type is required.', 1;
    DECLARE @TemplateUid UNIQUEIDENTIFIER=NEWID(), @TemplateVersionUid UNIQUEIDENTIFIER=NEWID();
    BEGIN TRANSACTION;
    INSERT dbo.DocumentTemplate(TemplateUid,TemplateName,TemplateType,Category,TemplateKind,TemplateScope,TemplateHtml,IsActive,CreatedAt,CreatedBy)
    VALUES(@TemplateUid,LTRIM(RTRIM(@TemplateName)),LTRIM(RTRIM(@DocumentType)),LTRIM(RTRIM(@DocumentType)),N'Document',N'Clinic',COALESCE(@TemplateContent,N''),1,SYSUTCDATETIME(),@CreatedBy);
    INSERT dbo.DocumentTemplateVersion(TemplateVersionUid,TemplateUid,VersionNumber,TemplateContent,SchemaVersion,DefinitionJson,VersionStatus,IsCurrent,PublishedAt,PublishedBy,CreatedAt,CreatedBy)
    VALUES(@TemplateVersionUid,@TemplateUid,1,COALESCE(@TemplateContent,N''),1,N'{"schemaVersion":1,"sections":[]}',N'Published',1,SYSUTCDATETIME(),@CreatedBy,SYSUTCDATETIME(),@CreatedBy);
    COMMIT TRANSACTION; EXEC dbo.DocumentTemplate_GetByUid @TemplateUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.DocumentTemplate_Update
    @TemplateUid UNIQUEIDENTIFIER, @TemplateName NVARCHAR(200),
    @DocumentType NVARCHAR(100), @TemplateContent NVARCHAR(MAX), @UpdatedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF NULLIF(LTRIM(RTRIM(@TemplateName)),N'') IS NULL THROW 51020, 'Template name is required.', 1;
    IF NULLIF(LTRIM(RTRIM(@DocumentType)),N'') IS NULL THROW 51021, 'Document type is required.', 1;
    IF EXISTS(SELECT 1 FROM dbo.DocumentTemplateVersion WHERE TemplateUid=@TemplateUid AND IsCurrent=1 AND TemplateContent<>COALESCE(@TemplateContent,N''))
        THROW 51031, 'Published template content cannot be edited in place.', 1;
    UPDATE dbo.DocumentTemplate SET TemplateName=LTRIM(RTRIM(@TemplateName)),TemplateType=LTRIM(RTRIM(@DocumentType)),
        Category=LTRIM(RTRIM(@DocumentType)),UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@UpdatedBy WHERE TemplateUid=@TemplateUid;
    IF @@ROWCOUNT=0 RETURN;
    EXEC dbo.DocumentTemplate_GetByUid @TemplateUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.DocumentTemplate_GetActive
AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.TemplateUid,t.TemplateName,t.TemplateType AS DocumentType,t.Description,t.IsActive,
        t.TemplateKind,t.Category,t.TemplateScope,t.OwnerUserId,v.TemplateVersionUid,v.VersionNumber AS CurrentVersion
    FROM dbo.DocumentTemplate t JOIN dbo.DocumentTemplateVersion v ON v.TemplateUid=t.TemplateUid AND v.IsCurrent=1 AND v.VersionStatus=N'Published'
    WHERE t.IsActive=1 ORDER BY t.TemplateName;
END;
GO

CREATE OR ALTER PROCEDURE dbo.DocumentTemplate_GetByUid @TemplateUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.TemplateUid,t.TemplateName,t.TemplateType AS DocumentType,t.Description,COALESCE(v.TemplateContent,t.TemplateHtml) AS TemplateContent,
        t.IsActive,t.TemplateKind,t.Category,t.TemplateScope,t.OwnerUserId,t.CreatedAt,t.CreatedBy,cu.DisplayName AS CreatedByDisplayName,
        t.UpdatedAt,t.UpdatedBy,uu.DisplayName AS UpdatedByDisplayName,t.RowVersion,v.TemplateVersionUid,v.VersionNumber AS CurrentVersion
    FROM dbo.DocumentTemplate t LEFT JOIN dbo.DocumentTemplateVersion v ON v.TemplateUid=t.TemplateUid AND v.IsCurrent=1 AND v.VersionStatus=N'Published'
    LEFT JOIN dbo.ApplicationUser cu ON cu.UserId=t.CreatedBy LEFT JOIN dbo.ApplicationUser uu ON uu.UserId=t.UpdatedBy WHERE t.TemplateUid=@TemplateUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.DocumentTemplate_GetAll @StatusFilter NVARCHAR(50)=N'Active'
AS
BEGIN
    SET NOCOUNT ON; IF @StatusFilter NOT IN(N'Active',N'Inactive',N'All') SET @StatusFilter=N'Active';
    SELECT t.TemplateUid,t.TemplateName,t.TemplateType AS DocumentType,t.Description,COALESCE(v.TemplateContent,t.TemplateHtml) AS TemplateContent,
        t.IsActive,t.TemplateKind,t.Category,t.TemplateScope,t.OwnerUserId,t.CreatedAt,t.CreatedBy,cu.DisplayName AS CreatedByDisplayName,
        t.UpdatedAt,t.UpdatedBy,uu.DisplayName AS UpdatedByDisplayName,t.RowVersion,v.TemplateVersionUid,v.VersionNumber AS CurrentVersion
    FROM dbo.DocumentTemplate t LEFT JOIN dbo.DocumentTemplateVersion v ON v.TemplateUid=t.TemplateUid AND v.IsCurrent=1 AND v.VersionStatus=N'Published'
    LEFT JOIN dbo.ApplicationUser cu ON cu.UserId=t.CreatedBy LEFT JOIN dbo.ApplicationUser uu ON uu.UserId=t.UpdatedBy
    WHERE @StatusFilter=N'All' OR (@StatusFilter=N'Active' AND t.IsActive=1) OR (@StatusFilter=N'Inactive' AND t.IsActive=0)
    ORDER BY t.IsActive DESC,t.TemplateName;
END;
GO
