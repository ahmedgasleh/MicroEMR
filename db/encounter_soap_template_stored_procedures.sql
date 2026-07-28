IF OBJECT_ID(N'dbo.EncounterSoapTemplate',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.EncounterSoapTemplate(
  EncounterSoapTemplateId BIGINT IDENTITY(1,1) PRIMARY KEY,
  EncounterSoapTemplateUid UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_EncounterSoapTemplate_Uid DEFAULT NEWSEQUENTIALID(),
  TemplateName NVARCHAR(200) NOT NULL, EncounterType NVARCHAR(100) NULL,
  SubjectiveTemplate NVARCHAR(MAX) NULL,ObjectiveTemplate NVARCHAR(MAX) NULL,
  AssessmentTemplate NVARCHAR(MAX) NULL,PlanTemplate NVARCHAR(MAX) NULL,
  IsActive BIT NOT NULL CONSTRAINT DF_EncounterSoapTemplate_Active DEFAULT 1,
  CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_EncounterSoapTemplate_Created DEFAULT SYSUTCDATETIME(),
  CreatedBy BIGINT NULL,UpdatedAt DATETIME2(0) NULL,UpdatedBy BIGINT NULL,RowVersion ROWVERSION NOT NULL,
  CONSTRAINT UQ_EncounterSoapTemplate_Uid UNIQUE(EncounterSoapTemplateUid));
 CREATE INDEX IX_EncounterSoapTemplate_IsActive_TemplateName ON dbo.EncounterSoapTemplate(IsActive,TemplateName);
END;
GO
CREATE OR ALTER PROCEDURE dbo.EncounterSoapTemplate_GetByUid @EncounterSoapTemplateUid UNIQUEIDENTIFIER AS
BEGIN SET NOCOUNT ON; SELECT t.*,cu.DisplayName CreatedByDisplayName,uu.DisplayName UpdatedByDisplayName FROM dbo.EncounterSoapTemplate t LEFT JOIN dbo.ApplicationUser cu ON cu.UserId=t.CreatedBy LEFT JOIN dbo.ApplicationUser uu ON uu.UserId=t.UpdatedBy WHERE t.EncounterSoapTemplateUid=@EncounterSoapTemplateUid; END;
GO
CREATE OR ALTER PROCEDURE dbo.EncounterSoapTemplate_GetAll @StatusFilter NVARCHAR(50)=N'Active' AS
BEGIN SET NOCOUNT ON;IF @StatusFilter NOT IN(N'Active',N'Inactive',N'All')SET @StatusFilter=N'Active';SELECT t.*,cu.DisplayName CreatedByDisplayName,uu.DisplayName UpdatedByDisplayName FROM dbo.EncounterSoapTemplate t LEFT JOIN dbo.ApplicationUser cu ON cu.UserId=t.CreatedBy LEFT JOIN dbo.ApplicationUser uu ON uu.UserId=t.UpdatedBy WHERE @StatusFilter=N'All' OR (@StatusFilter=N'Active' AND t.IsActive=1) OR (@StatusFilter=N'Inactive' AND t.IsActive=0) ORDER BY t.IsActive DESC,t.TemplateName;END;
GO
CREATE OR ALTER PROCEDURE dbo.EncounterSoapTemplate_Create @TemplateName NVARCHAR(200),@EncounterType NVARCHAR(100)=NULL,@SubjectiveTemplate NVARCHAR(MAX)=NULL,@ObjectiveTemplate NVARCHAR(MAX)=NULL,@AssessmentTemplate NVARCHAR(MAX)=NULL,@PlanTemplate NVARCHAR(MAX)=NULL,@CreatedBy BIGINT=NULL AS
BEGIN SET NOCOUNT ON;IF NULLIF(LTRIM(RTRIM(@TemplateName)),N'') IS NULL THROW 51100,'Template name is required.',1;DECLARE @Uid UNIQUEIDENTIFIER=NEWID();INSERT dbo.EncounterSoapTemplate(EncounterSoapTemplateUid,TemplateName,EncounterType,SubjectiveTemplate,ObjectiveTemplate,AssessmentTemplate,PlanTemplate,CreatedBy)VALUES(@Uid,LTRIM(RTRIM(@TemplateName)),NULLIF(LTRIM(RTRIM(@EncounterType)),N''),NULLIF(@SubjectiveTemplate,N''),NULLIF(@ObjectiveTemplate,N''),NULLIF(@AssessmentTemplate,N''),NULLIF(@PlanTemplate,N''),@CreatedBy);EXEC dbo.EncounterSoapTemplate_GetByUid @Uid;END;
GO
CREATE OR ALTER PROCEDURE dbo.EncounterSoapTemplate_Update @EncounterSoapTemplateUid UNIQUEIDENTIFIER,@TemplateName NVARCHAR(200),@EncounterType NVARCHAR(100)=NULL,@SubjectiveTemplate NVARCHAR(MAX)=NULL,@ObjectiveTemplate NVARCHAR(MAX)=NULL,@AssessmentTemplate NVARCHAR(MAX)=NULL,@PlanTemplate NVARCHAR(MAX)=NULL,@UpdatedBy BIGINT=NULL AS
BEGIN SET NOCOUNT ON;IF NULLIF(LTRIM(RTRIM(@TemplateName)),N'') IS NULL THROW 51100,'Template name is required.',1;UPDATE dbo.EncounterSoapTemplate SET TemplateName=LTRIM(RTRIM(@TemplateName)),EncounterType=NULLIF(LTRIM(RTRIM(@EncounterType)),N''),SubjectiveTemplate=NULLIF(@SubjectiveTemplate,N''),ObjectiveTemplate=NULLIF(@ObjectiveTemplate,N''),AssessmentTemplate=NULLIF(@AssessmentTemplate,N''),PlanTemplate=NULLIF(@PlanTemplate,N''),UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@UpdatedBy WHERE EncounterSoapTemplateUid=@EncounterSoapTemplateUid;IF @@ROWCOUNT=0 RETURN;EXEC dbo.EncounterSoapTemplate_GetByUid @EncounterSoapTemplateUid;END;
GO
CREATE OR ALTER PROCEDURE dbo.EncounterSoapTemplate_SetActive @EncounterSoapTemplateUid UNIQUEIDENTIFIER,@IsActive BIT,@UpdatedBy BIGINT=NULL AS
BEGIN SET NOCOUNT ON;UPDATE dbo.EncounterSoapTemplate SET IsActive=@IsActive,UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@UpdatedBy WHERE EncounterSoapTemplateUid=@EncounterSoapTemplateUid;IF @@ROWCOUNT=0 RETURN;EXEC dbo.EncounterSoapTemplate_GetByUid @EncounterSoapTemplateUid;END;
GO
