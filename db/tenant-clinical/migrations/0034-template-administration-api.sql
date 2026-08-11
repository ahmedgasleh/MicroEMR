CREATE OR ALTER PROCEDURE dbo.DocumentTemplateAdmin_Create
    @TemplateName NVARCHAR(200), @TemplateKind NVARCHAR(20), @Category NVARCHAR(100),
    @TemplateScope NVARCHAR(20), @OwnerUserId BIGINT = NULL, @SchemaVersion INT,
    @DefinitionJson NVARCHAR(MAX), @CreatedBy BIGINT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF @TemplateKind NOT IN(N'Document',N'Encounter') THROW 51040,'Invalid template kind.',1;
    IF @TemplateScope NOT IN(N'Clinic',N'Personal') THROW 51041,'Invalid template scope.',1;
    IF @TemplateScope=N'Personal' AND @OwnerUserId IS NULL THROW 51042,'Personal template owner is required.',1;
    DECLARE @TemplateUid UNIQUEIDENTIFIER=NEWID(),@VersionUid UNIQUEIDENTIFIER=NEWID();
    BEGIN TRANSACTION;
    INSERT dbo.DocumentTemplate(TemplateUid,TemplateName,TemplateType,Category,TemplateKind,TemplateScope,OwnerUserId,TemplateHtml,IsActive,CreatedAt,CreatedBy)
    VALUES(@TemplateUid,LTRIM(RTRIM(@TemplateName)),LTRIM(RTRIM(@Category)),LTRIM(RTRIM(@Category)),@TemplateKind,@TemplateScope,@OwnerUserId,N'',1,SYSUTCDATETIME(),@CreatedBy);
    INSERT dbo.DocumentTemplateVersion(TemplateVersionUid,TemplateUid,VersionNumber,TemplateContent,SchemaVersion,DefinitionJson,VersionStatus,IsCurrent,CreatedAt,CreatedBy)
    VALUES(@VersionUid,@TemplateUid,1,N'',@SchemaVersion,@DefinitionJson,N'Draft',0,SYSUTCDATETIME(),@CreatedBy);
    INSERT dbo.AuditLog(UserId,ActionName,EntityName,EntityId,NewValue,CreatedAt) VALUES(@CreatedBy,N'Create',N'DocumentTemplate',CONVERT(NVARCHAR(100),@TemplateUid),N'Logical template with initial draft created',SYSUTCDATETIME());
    COMMIT; EXEC dbo.DocumentTemplate_GetByUid @TemplateUid; EXEC dbo.DocumentTemplateVersion_GetByUid @VersionUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.DocumentTemplateAdmin_SetActive
    @TemplateUid UNIQUEIDENTIFIER,@IsActive BIT,@ExpectedRowVersion BINARY(8),@UpdatedBy BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.DocumentTemplate SET IsActive=@IsActive,UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@UpdatedBy
    WHERE TemplateUid=@TemplateUid AND RowVersion=@ExpectedRowVersion;
    IF @@ROWCOUNT=0 BEGIN IF EXISTS(SELECT 1 FROM dbo.DocumentTemplate WHERE TemplateUid=@TemplateUid) THROW 51043,'The template was updated by another user.',1; RETURN; END;
    INSERT dbo.AuditLog(UserId,ActionName,EntityName,EntityId,NewValue,CreatedAt)
    VALUES(@UpdatedBy,CASE WHEN @IsActive=1 THEN N'Reactivate' ELSE N'Deactivate' END,N'DocumentTemplate',CONVERT(NVARCHAR(100),@TemplateUid),CASE WHEN @IsActive=1 THEN N'Active' ELSE N'Inactive' END,SYSUTCDATETIME());
    EXEC dbo.DocumentTemplate_GetByUid @TemplateUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.DocumentTemplateAdmin_UpdateMetadata
    @TemplateUid UNIQUEIDENTIFIER,@TemplateName NVARCHAR(200),@TemplateKind NVARCHAR(20),@Category NVARCHAR(100),
    @TemplateScope NVARCHAR(20),@OwnerUserId BIGINT=NULL,@ExpectedRowVersion BINARY(8),@UpdatedBy BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    IF @TemplateKind NOT IN(N'Document',N'Encounter') THROW 51040,'Invalid template kind.',1;
    IF @TemplateScope NOT IN(N'Clinic',N'Personal') THROW 51041,'Invalid template scope.',1;
    IF @TemplateScope=N'Personal' AND @OwnerUserId IS NULL THROW 51042,'Personal template owner is required.',1;
    UPDATE dbo.DocumentTemplate SET TemplateName=LTRIM(RTRIM(@TemplateName)),TemplateType=LTRIM(RTRIM(@Category)),Category=LTRIM(RTRIM(@Category)),TemplateKind=@TemplateKind,TemplateScope=@TemplateScope,OwnerUserId=@OwnerUserId,UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@UpdatedBy
    WHERE TemplateUid=@TemplateUid AND RowVersion=@ExpectedRowVersion;
    IF @@ROWCOUNT=0 BEGIN IF EXISTS(SELECT 1 FROM dbo.DocumentTemplate WHERE TemplateUid=@TemplateUid) THROW 51043,'The template was updated by another user.',1; RETURN; END;
    INSERT dbo.AuditLog(UserId,ActionName,EntityName,EntityId,NewValue,CreatedAt) VALUES(@UpdatedBy,N'UpdateMetadata',N'DocumentTemplate',CONVERT(NVARCHAR(100),@TemplateUid),N'Template metadata updated',SYSUTCDATETIME());
    EXEC dbo.DocumentTemplate_GetByUid @TemplateUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.DocumentTemplateAdmin_Clone
    @SourceTemplateUid UNIQUEIDENTIFIER,@SourceTemplateVersionUid UNIQUEIDENTIFIER=NULL,@TemplateName NVARCHAR(200),
    @TemplateScope NVARCHAR(20),@OwnerUserId BIGINT=NULL,@CreatedBy BIGINT
AS
BEGIN
    SET NOCOUNT ON;SET XACT_ABORT ON;
    IF @TemplateScope NOT IN(N'Clinic',N'Personal') THROW 51041,'Invalid template scope.',1;
    IF @TemplateScope=N'Personal' AND @OwnerUserId IS NULL THROW 51042,'Personal template owner is required.',1;
    DECLARE @TemplateUid UNIQUEIDENTIFIER=NEWID(),@VersionUid UNIQUEIDENTIFIER=NEWID(),@Kind NVARCHAR(20),@Category NVARCHAR(100),@Content NVARCHAR(MAX),@SchemaVersion INT,@Json NVARCHAR(MAX);
    SELECT @Kind=t.TemplateKind,@Category=t.Category,@Content=v.TemplateContent,@SchemaVersion=v.SchemaVersion,@Json=v.DefinitionJson
    FROM dbo.DocumentTemplate t JOIN dbo.DocumentTemplateVersion v ON v.TemplateUid=t.TemplateUid
    WHERE t.TemplateUid=@SourceTemplateUid AND ((@SourceTemplateVersionUid IS NOT NULL AND v.TemplateVersionUid=@SourceTemplateVersionUid) OR (@SourceTemplateVersionUid IS NULL AND v.IsCurrent=1 AND v.VersionStatus=N'Published'));
    IF @Json IS NULL THROW 51044,'The source template version was not found.',1;
    BEGIN TRANSACTION;
    INSERT dbo.DocumentTemplate(TemplateUid,TemplateName,TemplateType,Category,TemplateKind,TemplateScope,OwnerUserId,TemplateHtml,IsActive,CreatedAt,CreatedBy)
    VALUES(@TemplateUid,LTRIM(RTRIM(@TemplateName)),@Category,@Category,@Kind,@TemplateScope,@OwnerUserId,N'',1,SYSUTCDATETIME(),@CreatedBy);
    INSERT dbo.DocumentTemplateVersion(TemplateVersionUid,TemplateUid,VersionNumber,TemplateContent,SchemaVersion,DefinitionJson,VersionStatus,IsCurrent,CreatedAt,CreatedBy)
    VALUES(@VersionUid,@TemplateUid,1,@Content,@SchemaVersion,@Json,N'Draft',0,SYSUTCDATETIME(),@CreatedBy);
    INSERT dbo.AuditLog(UserId,ActionName,EntityName,EntityId,NewValue,CreatedAt) VALUES(@CreatedBy,N'Clone',N'DocumentTemplate',CONVERT(NVARCHAR(100),@TemplateUid),CONCAT(N'Cloned from ',CONVERT(NVARCHAR(100),@SourceTemplateUid)),SYSUTCDATETIME());
    COMMIT;EXEC dbo.DocumentTemplate_GetByUid @TemplateUid;EXEC dbo.DocumentTemplateVersion_GetByUid @VersionUid;
END;
GO
