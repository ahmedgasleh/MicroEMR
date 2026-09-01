SET XACT_ABORT ON;
GO

ALTER TABLE dbo.PatientReferral ADD
    ReferringProviderUid UNIQUEIDENTIFIER NULL,
    ReferringProviderDisplayNameSnapshot NVARCHAR(200) NULL,
    ReferringProviderCredentialSnapshot NVARCHAR(200) NULL,
    ArtifactUid UNIQUEIDENTIFIER NULL;
GO

CREATE TABLE dbo.PatientReferralArtifact
(
    ArtifactUid UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PatientReferralArtifact PRIMARY KEY,
    ReferralUid UNIQUEIDENTIFIER NOT NULL,
    PatientUid UNIQUEIDENTIFIER NOT NULL,
    MimeType NVARCHAR(100) NOT NULL CONSTRAINT DF_PatientReferralArtifact_Mime DEFAULT N'application/pdf',
    FileName NVARCHAR(260) NOT NULL,
    PdfContent VARBINARY(MAX) NOT NULL,
    FileSizeBytes BIGINT NOT NULL,
    Sha256 CHAR(64) NOT NULL,
    SnapshotJson NVARCHAR(MAX) NOT NULL,
    CreatedBy BIGINT NOT NULL,
    CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_PatientReferralArtifact_Created DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_PatientReferralArtifact_Referral UNIQUE(ReferralUid),
    CONSTRAINT FK_PatientReferralArtifact_Referral FOREIGN KEY(ReferralUid) REFERENCES dbo.PatientReferral(ReferralUid),
    CONSTRAINT FK_PatientReferralArtifact_Actor FOREIGN KEY(CreatedBy) REFERENCES dbo.ApplicationUser(UserId),
    CONSTRAINT CK_PatientReferralArtifact_Size CHECK(FileSizeBytes > 0),
    CONSTRAINT CK_PatientReferralArtifact_Hash CHECK(LEN(Sha256) = 64),
    CONSTRAINT CK_PatientReferralArtifact_SnapshotJson CHECK(ISJSON(SnapshotJson) = 1)
);
GO

CREATE OR ALTER PROCEDURE dbo.PatientReferral_GetByPatientUid @PatientUid UNIQUEIDENTIFIER AS
BEGIN SET NOCOUNT ON;
 SELECT r.ReferralUid,r.PatientUid,r.RecipientName,r.RecipientOrganization,r.RecipientPhone,r.RecipientFax,
  r.Reason,r.ClinicalSummary,r.ReferringProviderUid,r.ReferringProviderDisplayNameSnapshot,
  r.ReferringProviderCredentialSnapshot,r.ArtifactUid,r.Status,r.CreatedAt,r.CreatedBy,r.UpdatedAt,r.UpdatedBy,
  r.SentAt,r.ResponseReceivedAt,r.ClosedAt,r.RowVersion
 FROM dbo.PatientReferral r WHERE r.PatientUid=@PatientUid ORDER BY r.CreatedAt DESC,r.PatientReferralId DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientReferral_GetByUid @PatientUid UNIQUEIDENTIFIER,@ReferralUid UNIQUEIDENTIFIER AS
BEGIN SET NOCOUNT ON;
 SELECT r.ReferralUid,r.PatientUid,r.RecipientName,r.RecipientOrganization,r.RecipientPhone,r.RecipientFax,
  r.Reason,r.ClinicalSummary,r.ReferringProviderUid,r.ReferringProviderDisplayNameSnapshot,
  r.ReferringProviderCredentialSnapshot,r.ArtifactUid,r.Status,r.CreatedAt,r.CreatedBy,r.UpdatedAt,r.UpdatedBy,
  r.SentAt,r.ResponseReceivedAt,r.ClosedAt,r.RowVersion
 FROM dbo.PatientReferral r WHERE r.PatientUid=@PatientUid AND r.ReferralUid=@ReferralUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientReferral_Create
 @PatientUid UNIQUEIDENTIFIER,@RecipientName NVARCHAR(200),@RecipientOrganization NVARCHAR(200)=NULL,
 @RecipientPhone NVARCHAR(30)=NULL,@RecipientFax NVARCHAR(30)=NULL,@Reason NVARCHAR(1000),
 @ClinicalSummary NVARCHAR(MAX)=NULL,@ReferringProviderUid UNIQUEIDENTIFIER=NULL,@CreatedBy BIGINT AS
BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;
 DECLARE @PatientId BIGINT,@ReferralUid UNIQUEIDENTIFIER=NEWID();
 SELECT @PatientId=PatientId FROM dbo.Patient WHERE PatientUid=@PatientUid AND IsDeleted=0;
 IF @PatientId IS NULL THROW 51500,'Patient not found.',1;
 IF NULLIF(LTRIM(RTRIM(@RecipientName)),N'') IS NULL THROW 51501,'Recipient name is required.',1;
 IF NULLIF(LTRIM(RTRIM(@Reason)),N'') IS NULL THROW 51502,'Referral reason is required.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@CreatedBy AND IsActive=1) THROW 51503,'Active clinical user not found.',1;
 IF @ReferringProviderUid IS NULL OR @ReferringProviderUid='00000000-0000-0000-0000-000000000000'
  SELECT @ReferringProviderUid=p.ProviderUid FROM dbo.ApplicationUser u JOIN dbo.Provider p ON p.ProviderId=u.ProviderId AND p.IsActive=1 WHERE u.UserId=@CreatedBy AND u.IsActive=1;
 IF NOT EXISTS(SELECT 1 FROM dbo.Provider WHERE ProviderUid=@ReferringProviderUid AND IsActive=1) THROW 51504,'Active referring provider not found.',1;
 BEGIN TRANSACTION;
 INSERT dbo.PatientReferral(ReferralUid,PatientUid,RecipientName,RecipientOrganization,RecipientPhone,RecipientFax,
  Reason,ClinicalSummary,ReferringProviderUid,CreatedBy)
 VALUES(@ReferralUid,@PatientUid,LTRIM(RTRIM(@RecipientName)),NULLIF(LTRIM(RTRIM(@RecipientOrganization)),N''),
  NULLIF(LTRIM(RTRIM(@RecipientPhone)),N''),NULLIF(LTRIM(RTRIM(@RecipientFax)),N''),LTRIM(RTRIM(@Reason)),
  NULLIF(LTRIM(RTRIM(@ClinicalSummary)),N''),@ReferringProviderUid,@CreatedBy);
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt)
 VALUES(@CreatedBy,@PatientId,N'Create',N'PatientReferral',CONVERT(NVARCHAR(100),@ReferralUid),N'Status=Draft',SYSUTCDATETIME());
 COMMIT;EXEC dbo.PatientReferral_GetByUid @PatientUid,@ReferralUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientReferral_UpdateDraft
 @PatientUid UNIQUEIDENTIFIER,@ReferralUid UNIQUEIDENTIFIER,@RecipientName NVARCHAR(200),
 @RecipientOrganization NVARCHAR(200)=NULL,@RecipientPhone NVARCHAR(30)=NULL,@RecipientFax NVARCHAR(30)=NULL,
 @Reason NVARCHAR(1000),@ClinicalSummary NVARCHAR(MAX)=NULL,@ReferringProviderUid UNIQUEIDENTIFIER,
 @ExpectedRowVersion BINARY(8),@UpdatedBy BIGINT AS
BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;BEGIN TRANSACTION;
 DECLARE @PatientId BIGINT;
 SELECT @PatientId=p.PatientId FROM dbo.PatientReferral r WITH(UPDLOCK,HOLDLOCK)
 JOIN dbo.Patient p ON p.PatientUid=r.PatientUid AND p.IsDeleted=0
 WHERE r.PatientUid=@PatientUid AND r.ReferralUid=@ReferralUid;
 IF @PatientId IS NULL BEGIN ROLLBACK;THROW 51510,'Referral not found.',1;END;
 IF NOT EXISTS(SELECT 1 FROM dbo.Provider WHERE ProviderUid=@ReferringProviderUid AND IsActive=1)
  BEGIN ROLLBACK;THROW 51504,'Active referring provider not found.',1;END;
 UPDATE dbo.PatientReferral SET RecipientName=LTRIM(RTRIM(@RecipientName)),
  RecipientOrganization=NULLIF(LTRIM(RTRIM(@RecipientOrganization)),N''),RecipientPhone=NULLIF(LTRIM(RTRIM(@RecipientPhone)),N''),
  RecipientFax=NULLIF(LTRIM(RTRIM(@RecipientFax)),N''),Reason=LTRIM(RTRIM(@Reason)),
  ClinicalSummary=NULLIF(LTRIM(RTRIM(@ClinicalSummary)),N''),ReferringProviderUid=@ReferringProviderUid,
  UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@UpdatedBy
 WHERE PatientUid=@PatientUid AND ReferralUid=@ReferralUid AND Status=N'Draft' AND RowVersion=@ExpectedRowVersion;
 IF @@ROWCOUNT=0 BEGIN ROLLBACK;THROW 51512,'Referral is stale or no longer Draft.',1;END;
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt)
 VALUES(@UpdatedBy,@PatientId,N'UpdateDraft',N'PatientReferral',CONVERT(NVARCHAR(100),@ReferralUid),N'Draft updated',SYSUTCDATETIME());
 COMMIT;EXEC dbo.PatientReferral_GetByUid @PatientUid,@ReferralUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientReferral_GetActiveProviders AS
BEGIN SET NOCOUNT ON;
 SELECT ProviderUid,DisplayName,ProviderType,Specialty FROM dbo.Provider WHERE IsActive=1 ORDER BY DisplayName,ProviderUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientReferral_GetProvider @ProviderUid UNIQUEIDENTIFIER AS
BEGIN SET NOCOUNT ON;
 SELECT ProviderUid,DisplayName,ProviderType,BillingNumber,Specialty FROM dbo.Provider
 WHERE ProviderUid=@ProviderUid AND IsActive=1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientReferral_Send
 @PatientUid UNIQUEIDENTIFIER,@ReferralUid UNIQUEIDENTIFIER,@ExpectedRowVersion BINARY(8),@UpdatedBy BIGINT,
 @ArtifactUid UNIQUEIDENTIFIER,@SentAt DATETIME2(0),@PdfContent VARBINARY(MAX),@FileName NVARCHAR(260),@Sha256 CHAR(64),
 @SnapshotJson NVARCHAR(MAX),@ProviderDisplayName NVARCHAR(200),@ProviderCredential NVARCHAR(200)=NULL AS
BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;BEGIN TRANSACTION;
 DECLARE @PatientId BIGINT,@Status NVARCHAR(30),@Version BINARY(8),@ProviderUid UNIQUEIDENTIFIER,@ChangedAt DATETIME2(0)=@SentAt;
 SELECT @PatientId=p.PatientId,@Status=r.Status,@Version=r.RowVersion,@ProviderUid=r.ReferringProviderUid
 FROM dbo.PatientReferral r WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Patient p ON p.PatientUid=r.PatientUid AND p.IsDeleted=0
 WHERE r.PatientUid=@PatientUid AND r.ReferralUid=@ReferralUid;
 IF @PatientId IS NULL BEGIN ROLLBACK;THROW 51510,'Referral not found.',1;END;
 IF @Status<>N'Draft' BEGIN ROLLBACK;THROW 51511,'Invalid referral transition.',1;END;
 IF @Version<>@ExpectedRowVersion BEGIN ROLLBACK;THROW 51512,'Referral concurrency conflict.',1;END;
 IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@UpdatedBy AND IsActive=1) BEGIN ROLLBACK;THROW 51513,'Active clinical user not found.',1;END;
 IF NOT EXISTS(SELECT 1 FROM dbo.Provider WHERE ProviderUid=@ProviderUid AND IsActive=1) BEGIN ROLLBACK;THROW 51504,'Active referring provider not found.',1;END;
 INSERT dbo.PatientReferralArtifact(ArtifactUid,ReferralUid,PatientUid,FileName,PdfContent,FileSizeBytes,Sha256,SnapshotJson,CreatedBy)
 VALUES(@ArtifactUid,@ReferralUid,@PatientUid,@FileName,@PdfContent,DATALENGTH(@PdfContent),@Sha256,@SnapshotJson,@UpdatedBy);
 UPDATE dbo.PatientReferral SET Status=N'Sent',SentAt=@ChangedAt,UpdatedAt=@ChangedAt,UpdatedBy=@UpdatedBy,
  ReferringProviderDisplayNameSnapshot=@ProviderDisplayName,ReferringProviderCredentialSnapshot=@ProviderCredential,ArtifactUid=@ArtifactUid
 WHERE PatientUid=@PatientUid AND ReferralUid=@ReferralUid;
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
 VALUES(@UpdatedBy,@PatientId,N'ReferralSent',N'PatientReferral',CONVERT(NVARCHAR(100),@ReferralUid),N'Status=Draft',
  CONCAT(N'Status=Sent;ArtifactUid=',CONVERT(NVARCHAR(36),@ArtifactUid)),@ChangedAt);
 COMMIT;EXEC dbo.PatientReferral_GetByUid @PatientUid,@ReferralUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientReferralArtifact_Get @PatientUid UNIQUEIDENTIFIER,@ReferralUid UNIQUEIDENTIFIER AS
BEGIN SET NOCOUNT ON;
 SELECT a.ArtifactUid,a.MimeType,a.FileName,a.PdfContent,a.FileSizeBytes,a.Sha256,a.SnapshotJson,a.CreatedAtUtc
 FROM dbo.PatientReferralArtifact a JOIN dbo.PatientReferral r ON r.ReferralUid=a.ReferralUid AND r.PatientUid=a.PatientUid
 WHERE a.PatientUid=@PatientUid AND a.ReferralUid=@ReferralUid AND r.ArtifactUid=a.ArtifactUid;
END;
GO

-- Prevent legacy callers from creating a Sent referral without its immutable artifact.
CREATE OR ALTER PROCEDURE dbo.PatientReferral_MarkSent
 @PatientUid UNIQUEIDENTIFIER,@ReferralUid UNIQUEIDENTIFIER,@ExpectedRowVersion BINARY(8),@UpdatedBy BIGINT AS
BEGIN
 SET NOCOUNT ON;
 THROW 51511,'Referral Send requires an immutable referral letter artifact.',1;
END;
GO

-- Supporting-document membership is part of the Draft aggregate and must advance its RowVersion.
CREATE OR ALTER PROCEDURE dbo.PatientReferralDocument_Link
 @PatientUid UNIQUEIDENTIFIER,@ReferralUid UNIQUEIDENTIFIER,@DocumentUid UNIQUEIDENTIFIER,@ExpectedRowVersion BINARY(8),@Actor BIGINT AS
BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;BEGIN TRANSACTION;
 DECLARE @Status NVARCHAR(30),@Version BINARY(8),@PatientId BIGINT;
 SELECT @Status=r.Status,@Version=r.RowVersion,@PatientId=p.PatientId FROM dbo.PatientReferral r WITH(UPDLOCK,HOLDLOCK)
 JOIN dbo.Patient p ON p.PatientUid=r.PatientUid AND p.IsDeleted=0 WHERE r.ReferralUid=@ReferralUid AND r.PatientUid=@PatientUid;
 IF @Status IS NULL BEGIN ROLLBACK;THROW 51600,'Referral not found.',1;END;
 IF NOT EXISTS(SELECT 1 FROM dbo.PatientDocument WHERE PatientDocumentUid=@DocumentUid AND PatientUid=@PatientUid AND IsDeleted=0) BEGIN ROLLBACK;THROW 51601,'Document not found.',1;END;
 IF @Status<>N'Draft' BEGIN ROLLBACK;THROW 51602,'Referral is not Draft.',1;END;
 IF @Version<>@ExpectedRowVersion BEGIN ROLLBACK;THROW 51603,'Referral concurrency conflict.',1;END;
 IF EXISTS(SELECT 1 FROM dbo.PatientReferralDocument WHERE ReferralUid=@ReferralUid AND DocumentUid=@DocumentUid) BEGIN ROLLBACK;THROW 51605,'Document already linked.',1;END;
 INSERT dbo.PatientReferralDocument(ReferralUid,DocumentUid,LinkedBy)VALUES(@ReferralUid,@DocumentUid,@Actor);
 UPDATE dbo.PatientReferral SET UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@Actor WHERE ReferralUid=@ReferralUid;
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt)VALUES(@Actor,@PatientId,N'LinkDocument',N'PatientReferral',CONVERT(NVARCHAR(100),@ReferralUid),CONVERT(NVARCHAR(100),@DocumentUid),SYSUTCDATETIME());
 COMMIT;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientReferralDocument_Unlink
 @PatientUid UNIQUEIDENTIFIER,@ReferralUid UNIQUEIDENTIFIER,@DocumentUid UNIQUEIDENTIFIER,@ExpectedRowVersion BINARY(8),@Actor BIGINT AS
BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;BEGIN TRANSACTION;
 DECLARE @Status NVARCHAR(30),@Version BINARY(8),@PatientId BIGINT;
 SELECT @Status=r.Status,@Version=r.RowVersion,@PatientId=p.PatientId FROM dbo.PatientReferral r WITH(UPDLOCK,HOLDLOCK)
 JOIN dbo.Patient p ON p.PatientUid=r.PatientUid AND p.IsDeleted=0 WHERE r.ReferralUid=@ReferralUid AND r.PatientUid=@PatientUid;
 IF @Status IS NULL BEGIN ROLLBACK;THROW 51600,'Referral not found.',1;END;
 IF @Status<>N'Draft' BEGIN ROLLBACK;THROW 51602,'Referral is not Draft.',1;END;
 IF @Version<>@ExpectedRowVersion BEGIN ROLLBACK;THROW 51603,'Referral concurrency conflict.',1;END;
 DELETE dbo.PatientReferralDocument WHERE ReferralUid=@ReferralUid AND DocumentUid=@DocumentUid;
 IF @@ROWCOUNT=0 BEGIN ROLLBACK;THROW 51604,'Link not found.',1;END;
 UPDATE dbo.PatientReferral SET UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@Actor WHERE ReferralUid=@ReferralUid;
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,CreatedAt)VALUES(@Actor,@PatientId,N'UnlinkDocument',N'PatientReferral',CONVERT(NVARCHAR(100),@ReferralUid),CONVERT(NVARCHAR(100),@DocumentUid),SYSUTCDATETIME());
 COMMIT;
END;
GO
