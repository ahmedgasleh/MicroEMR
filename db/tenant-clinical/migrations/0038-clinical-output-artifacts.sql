SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.ClinicalOutputArtifact', N'U') IS NULL
BEGIN
 CREATE TABLE dbo.ClinicalOutputArtifact(
  ClinicalOutputArtifactId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ClinicalOutputArtifact PRIMARY KEY,
  ArtifactUid UNIQUEIDENTIFIER NOT NULL,
  PatientUid UNIQUEIDENTIFIER NOT NULL,
  SourceType NVARCHAR(30) NOT NULL,
  SourceUid UNIQUEIDENTIFIER NOT NULL,
  TemplateVersionUid UNIQUEIDENTIFIER NOT NULL,
  ArtifactType NVARCHAR(30) NOT NULL,
  StorageProvider NVARCHAR(30) NOT NULL,
  StorageKey NVARCHAR(700) NOT NULL,
  MimeType NVARCHAR(100) NOT NULL,
  FileSizeBytes BIGINT NOT NULL,
  Sha256 CHAR(64) NOT NULL,
  ArtifactStatus NVARCHAR(20) NOT NULL CONSTRAINT DF_ClinicalOutputArtifact_Status DEFAULT N'Available',
  FailureCode NVARCHAR(100) NULL,
  CreatedBy BIGINT NULL,
  CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_ClinicalOutputArtifact_CreatedAt DEFAULT SYSUTCDATETIME(),
  RowVersion ROWVERSION NOT NULL,
  CONSTRAINT UQ_ClinicalOutputArtifact_Uid UNIQUE(ArtifactUid),
  CONSTRAINT UQ_ClinicalOutputArtifact_StorageKey UNIQUE(StorageKey),
  CONSTRAINT CK_ClinicalOutputArtifact_SourceType CHECK(SourceType IN(N'Encounter',N'PatientDocument')),
  CONSTRAINT CK_ClinicalOutputArtifact_Type CHECK(ArtifactType=N'FinalPdf'),
  CONSTRAINT CK_ClinicalOutputArtifact_Status CHECK(ArtifactStatus IN(N'Available',N'Failed')),
  CONSTRAINT CK_ClinicalOutputArtifact_Size CHECK(FileSizeBytes>=0),
  CONSTRAINT FK_ClinicalOutputArtifact_TemplateVersion FOREIGN KEY(TemplateVersionUid) REFERENCES dbo.DocumentTemplateVersion(TemplateVersionUid)
 );
 CREATE INDEX IX_ClinicalOutputArtifact_Source ON dbo.ClinicalOutputArtifact(SourceType,SourceUid,CreatedAt DESC);
 CREATE INDEX IX_ClinicalOutputArtifact_Patient ON dbo.ClinicalOutputArtifact(PatientUid,CreatedAt DESC);
 CREATE UNIQUE INDEX UX_ClinicalOutputArtifact_EncounterFinal
  ON dbo.ClinicalOutputArtifact(SourceType,SourceUid,ArtifactType) WHERE ArtifactStatus=N'Available' AND SourceType=N'Encounter';
END;
GO

CREATE OR ALTER PROCEDURE dbo.ClinicalOutputArtifact_GetFinalBySource @SourceType NVARCHAR(30),@SourceUid UNIQUEIDENTIFIER AS
BEGIN SET NOCOUNT ON;
 SELECT TOP(1) ArtifactUid,PatientUid,SourceType,SourceUid,TemplateVersionUid,ArtifactType,StorageProvider,
  StorageKey,MimeType,FileSizeBytes,Sha256,ArtifactStatus,CreatedBy,CreatedAt
 FROM dbo.ClinicalOutputArtifact WHERE SourceType=@SourceType AND SourceUid=@SourceUid
  AND ArtifactType=N'FinalPdf' AND ArtifactStatus=N'Available' ORDER BY CreatedAt DESC,ClinicalOutputArtifactId DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ClinicalOutputArtifact_Create
 @ArtifactUid UNIQUEIDENTIFIER,@PatientUid UNIQUEIDENTIFIER,@SourceType NVARCHAR(30),@SourceUid UNIQUEIDENTIFIER,
 @TemplateVersionUid UNIQUEIDENTIFIER,@ArtifactType NVARCHAR(30),@StorageProvider NVARCHAR(30),@StorageKey NVARCHAR(700),
 @MimeType NVARCHAR(100),@FileSizeBytes BIGINT,@Sha256 CHAR(64),@CreatedBy BIGINT=NULL AS
BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;
 IF @SourceType<>N'Encounter' OR NOT EXISTS(SELECT 1 FROM dbo.PatientEncounter e JOIN dbo.DocumentTemplateVersion v ON v.TemplateVersionUid=e.TemplateVersionUid
   WHERE e.EncounterUid=@SourceUid AND e.PatientUid=@PatientUid AND e.TemplateVersionUid=@TemplateVersionUid AND e.EncounterStatus=N'Signed')
  THROW 51200,'The artifact source relationship is invalid.',1;
 IF EXISTS(SELECT 1 FROM dbo.ClinicalOutputArtifact WHERE SourceType=@SourceType AND SourceUid=@SourceUid AND ArtifactType=@ArtifactType AND ArtifactStatus=N'Available')
  THROW 51201,'The final artifact already exists.',1;
 INSERT dbo.ClinicalOutputArtifact(ArtifactUid,PatientUid,SourceType,SourceUid,TemplateVersionUid,ArtifactType,StorageProvider,StorageKey,MimeType,FileSizeBytes,Sha256,CreatedBy)
 VALUES(@ArtifactUid,@PatientUid,@SourceType,@SourceUid,@TemplateVersionUid,@ArtifactType,@StorageProvider,@StorageKey,@MimeType,@FileSizeBytes,@Sha256,@CreatedBy);
 IF OBJECT_ID(N'dbo.AuditLog',N'U') IS NOT NULL INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
  SELECT @CreatedBy,p.PatientId,N'CreateFinalPdf',N'ClinicalOutputArtifact',CONVERT(NVARCHAR(100),@ArtifactUid),NULL,N'Final PDF artifact created',SYSUTCDATETIME()
  FROM dbo.Patient p WHERE p.PatientUid=@PatientUid;
 EXEC dbo.ClinicalOutputArtifact_GetFinalBySource @SourceType,@SourceUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ClinicalOutputArtifact_RecordFailure
 @PatientUid UNIQUEIDENTIFIER,@SourceType NVARCHAR(30),@SourceUid UNIQUEIDENTIFIER,@TemplateVersionUid UNIQUEIDENTIFIER,
 @CreatedBy BIGINT=NULL,@FailureCode NVARCHAR(100) AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.ClinicalOutputArtifact(ArtifactUid,PatientUid,SourceType,SourceUid,TemplateVersionUid,ArtifactType,StorageProvider,StorageKey,MimeType,FileSizeBytes,Sha256,ArtifactStatus,FailureCode,CreatedBy)
 VALUES(NEWID(),@PatientUid,@SourceType,@SourceUid,@TemplateVersionUid,N'FinalPdf',N'FileSystem',CONCAT(N'failed/',NEWID()),N'application/pdf',0,REPLICATE('0',64),N'Failed',LEFT(@FailureCode,100),@CreatedBy);
 IF OBJECT_ID(N'dbo.AuditLog',N'U') IS NOT NULL INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
  SELECT @CreatedBy,p.PatientId,N'FinalPdfFailed',N'PatientEncounter',CONVERT(NVARCHAR(100),@SourceUid),NULL,N'Final PDF artifact generation failed',SYSUTCDATETIME()
  FROM dbo.Patient p WHERE p.PatientUid=@PatientUid;
END;
GO
