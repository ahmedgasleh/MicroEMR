SET XACT_ABORT ON;
GO

ALTER TABLE dbo.PatientReferral ADD
    FollowUpDueAt DATETIME2(0) NULL,
    ResponseDocumentUid UNIQUEIDENTIFIER NULL;
GO

ALTER TABLE dbo.PatientReferral ADD CONSTRAINT FK_PatientReferral_ResponseDocument
    FOREIGN KEY (ResponseDocumentUid) REFERENCES dbo.PatientDocument(PatientDocumentUid);
GO

CREATE INDEX IX_PatientReferral_FollowUpDueAt
    ON dbo.PatientReferral(FollowUpDueAt)
    WHERE FollowUpDueAt IS NOT NULL;
GO

CREATE OR ALTER PROCEDURE dbo.PatientReferral_GetByPatientUid @PatientUid UNIQUEIDENTIFIER AS
BEGIN SET NOCOUNT ON;
 SELECT r.ReferralUid,r.PatientUid,r.RecipientName,r.RecipientOrganization,r.RecipientPhone,r.RecipientFax,
  r.Reason,r.ClinicalSummary,r.ReferringProviderUid,r.ReferringProviderDisplayNameSnapshot,
  r.ReferringProviderCredentialSnapshot,r.ArtifactUid,r.Status,r.CreatedAt,r.CreatedBy,r.UpdatedAt,r.UpdatedBy,
  r.SentAt,r.FollowUpDueAt,r.ResponseReceivedAt,r.ClosedAt,r.ResponseDocumentUid,
  d.DocumentTitle AS ResponseDocumentTitle,r.RowVersion
 FROM dbo.PatientReferral r
 LEFT JOIN dbo.PatientDocument d ON d.PatientDocumentUid=r.ResponseDocumentUid AND d.PatientUid=r.PatientUid AND d.IsDeleted=0
 WHERE r.PatientUid=@PatientUid ORDER BY r.CreatedAt DESC,r.PatientReferralId DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientReferral_GetByUid @PatientUid UNIQUEIDENTIFIER,@ReferralUid UNIQUEIDENTIFIER AS
BEGIN SET NOCOUNT ON;
 SELECT r.ReferralUid,r.PatientUid,r.RecipientName,r.RecipientOrganization,r.RecipientPhone,r.RecipientFax,
  r.Reason,r.ClinicalSummary,r.ReferringProviderUid,r.ReferringProviderDisplayNameSnapshot,
  r.ReferringProviderCredentialSnapshot,r.ArtifactUid,r.Status,r.CreatedAt,r.CreatedBy,r.UpdatedAt,r.UpdatedBy,
  r.SentAt,r.FollowUpDueAt,r.ResponseReceivedAt,r.ClosedAt,r.ResponseDocumentUid,
  d.DocumentTitle AS ResponseDocumentTitle,r.RowVersion
 FROM dbo.PatientReferral r
 LEFT JOIN dbo.PatientDocument d ON d.PatientDocumentUid=r.ResponseDocumentUid AND d.PatientUid=r.PatientUid AND d.IsDeleted=0
 WHERE r.PatientUid=@PatientUid AND r.ReferralUid=@ReferralUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientReferral_SetFollowUpDue
 @PatientUid UNIQUEIDENTIFIER,@ReferralUid UNIQUEIDENTIFIER,@FollowUpDueAt DATETIME2(0)=NULL,
 @ExpectedRowVersion BINARY(8),@UpdatedBy BIGINT AS
BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;BEGIN TRANSACTION;
 DECLARE @PatientId BIGINT,@Status NVARCHAR(30),@Version BINARY(8),@OldDue DATETIME2(0),@ChangedAt DATETIME2(0)=SYSUTCDATETIME();
 SELECT @PatientId=p.PatientId,@Status=r.Status,@Version=r.RowVersion,@OldDue=r.FollowUpDueAt
 FROM dbo.PatientReferral r WITH(UPDLOCK,HOLDLOCK)
 JOIN dbo.Patient p ON p.PatientUid=r.PatientUid AND p.IsDeleted=0
 WHERE r.PatientUid=@PatientUid AND r.ReferralUid=@ReferralUid;
 IF @PatientId IS NULL BEGIN ROLLBACK;THROW 51510,'Referral not found.',1;END;
 IF @Version<>@ExpectedRowVersion BEGIN ROLLBACK;THROW 51512,'Referral concurrency conflict.',1;END;
 IF @Status NOT IN(N'Draft',N'Sent') BEGIN ROLLBACK;THROW 51514,'Follow-up is not editable in this status.',1;END;
 IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@UpdatedBy AND IsActive=1) BEGIN ROLLBACK;THROW 51513,'Active clinical user not found.',1;END;
 IF (@OldDue=@FollowUpDueAt OR (@OldDue IS NULL AND @FollowUpDueAt IS NULL)) BEGIN ROLLBACK;THROW 51514,'Follow-up date is unchanged.',1;END;
 UPDATE dbo.PatientReferral SET FollowUpDueAt=@FollowUpDueAt,UpdatedAt=@ChangedAt,UpdatedBy=@UpdatedBy
 WHERE PatientUid=@PatientUid AND ReferralUid=@ReferralUid;
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
 VALUES(@UpdatedBy,@PatientId,
  CASE WHEN @FollowUpDueAt IS NULL THEN N'ReferralFollowUpCleared' WHEN @OldDue IS NULL THEN N'ReferralFollowUpScheduled' ELSE N'ReferralFollowUpChanged' END,
  N'PatientReferral',CONVERT(NVARCHAR(100),@ReferralUid),
  CASE WHEN @OldDue IS NULL THEN NULL ELSE CONCAT(N'FollowUpDueAt=',CONVERT(NVARCHAR(33),@OldDue,126),N'Z') END,
  CASE WHEN @FollowUpDueAt IS NULL THEN NULL ELSE CONCAT(N'FollowUpDueAt=',CONVERT(NVARCHAR(33),@FollowUpDueAt,126),N'Z') END,@ChangedAt);
 COMMIT;EXEC dbo.PatientReferral_GetByUid @PatientUid,@ReferralUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientReferral_MarkResponseReceived
 @PatientUid UNIQUEIDENTIFIER,@ReferralUid UNIQUEIDENTIFIER,@ExpectedRowVersion BINARY(8),@UpdatedBy BIGINT AS
BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;BEGIN TRANSACTION;
 DECLARE @PatientId BIGINT,@Status NVARCHAR(30),@Version BINARY(8),@ChangedAt DATETIME2(0)=SYSUTCDATETIME();
 SELECT @PatientId=p.PatientId,@Status=r.Status,@Version=r.RowVersion FROM dbo.PatientReferral r WITH(UPDLOCK,HOLDLOCK)
 JOIN dbo.Patient p ON p.PatientUid=r.PatientUid AND p.IsDeleted=0 WHERE r.PatientUid=@PatientUid AND r.ReferralUid=@ReferralUid;
 IF @PatientId IS NULL BEGIN ROLLBACK;THROW 51510,'Referral not found.',1;END;
 IF @Status<>N'Sent' BEGIN ROLLBACK;THROW 51511,'Invalid referral transition.',1;END;
 IF @Version<>@ExpectedRowVersion BEGIN ROLLBACK;THROW 51512,'Referral concurrency conflict.',1;END;
 IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@UpdatedBy AND IsActive=1) BEGIN ROLLBACK;THROW 51513,'Active clinical user not found.',1;END;
 UPDATE dbo.PatientReferral SET Status=N'ResponseReceived',ResponseReceivedAt=@ChangedAt,UpdatedAt=@ChangedAt,UpdatedBy=@UpdatedBy
 WHERE PatientUid=@PatientUid AND ReferralUid=@ReferralUid;
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
 VALUES(@UpdatedBy,@PatientId,N'ReferralResponseReceived',N'PatientReferral',CONVERT(NVARCHAR(100),@ReferralUid),N'Status=Sent',N'Status=ResponseReceived',@ChangedAt);
 COMMIT;EXEC dbo.PatientReferral_GetByUid @PatientUid,@ReferralUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientReferral_SetResponseDocument
 @PatientUid UNIQUEIDENTIFIER,@ReferralUid UNIQUEIDENTIFIER,@DocumentUid UNIQUEIDENTIFIER=NULL,
 @ExpectedRowVersion BINARY(8),@UpdatedBy BIGINT AS
BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;BEGIN TRANSACTION;
 DECLARE @PatientId BIGINT,@Status NVARCHAR(30),@Version BINARY(8),@OldDocumentUid UNIQUEIDENTIFIER,@ChangedAt DATETIME2(0)=SYSUTCDATETIME();
 SELECT @PatientId=p.PatientId,@Status=r.Status,@Version=r.RowVersion,@OldDocumentUid=r.ResponseDocumentUid
 FROM dbo.PatientReferral r WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Patient p ON p.PatientUid=r.PatientUid AND p.IsDeleted=0
 WHERE r.PatientUid=@PatientUid AND r.ReferralUid=@ReferralUid;
 IF @PatientId IS NULL BEGIN ROLLBACK;THROW 51510,'Referral not found.',1;END;
 IF @Version<>@ExpectedRowVersion BEGIN ROLLBACK;THROW 51512,'Referral concurrency conflict.',1;END;
 IF @Status<>N'ResponseReceived' BEGIN ROLLBACK;THROW 51514,'Response document is only editable after response receipt.',1;END;
 IF @DocumentUid IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.PatientDocument WHERE PatientDocumentUid=@DocumentUid AND PatientUid=@PatientUid AND IsDeleted=0)
  BEGIN ROLLBACK;THROW 51515,'Response document does not belong to patient.',1;END;
 IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@UpdatedBy AND IsActive=1) BEGIN ROLLBACK;THROW 51513,'Active clinical user not found.',1;END;
 IF (@OldDocumentUid=@DocumentUid OR (@OldDocumentUid IS NULL AND @DocumentUid IS NULL)) BEGIN ROLLBACK;THROW 51514,'Response document is unchanged.',1;END;
 UPDATE dbo.PatientReferral SET ResponseDocumentUid=@DocumentUid,UpdatedAt=@ChangedAt,UpdatedBy=@UpdatedBy
 WHERE PatientUid=@PatientUid AND ReferralUid=@ReferralUid;
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
 VALUES(@UpdatedBy,@PatientId,CASE WHEN @DocumentUid IS NULL THEN N'ReferralResponseDocumentUnlinked' ELSE N'ReferralResponseDocumentLinked' END,
  N'PatientReferral',CONVERT(NVARCHAR(100),@ReferralUid),CONVERT(NVARCHAR(36),@OldDocumentUid),CONVERT(NVARCHAR(36),@DocumentUid),@ChangedAt);
 COMMIT;EXEC dbo.PatientReferral_GetByUid @PatientUid,@ReferralUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientReferral_Close
 @PatientUid UNIQUEIDENTIFIER,@ReferralUid UNIQUEIDENTIFIER,@ExpectedRowVersion BINARY(8),@UpdatedBy BIGINT AS
BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;BEGIN TRANSACTION;
 DECLARE @PatientId BIGINT,@Status NVARCHAR(30),@Version BINARY(8),@ChangedAt DATETIME2(0)=SYSUTCDATETIME();
 SELECT @PatientId=p.PatientId,@Status=r.Status,@Version=r.RowVersion FROM dbo.PatientReferral r WITH(UPDLOCK,HOLDLOCK)
 JOIN dbo.Patient p ON p.PatientUid=r.PatientUid AND p.IsDeleted=0 WHERE r.PatientUid=@PatientUid AND r.ReferralUid=@ReferralUid;
 IF @PatientId IS NULL BEGIN ROLLBACK;THROW 51510,'Referral not found.',1;END;
 IF @Status<>N'ResponseReceived' BEGIN ROLLBACK;THROW 51511,'Invalid referral transition.',1;END;
 IF @Version<>@ExpectedRowVersion BEGIN ROLLBACK;THROW 51512,'Referral concurrency conflict.',1;END;
 IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@UpdatedBy AND IsActive=1) BEGIN ROLLBACK;THROW 51513,'Active clinical user not found.',1;END;
 UPDATE dbo.PatientReferral SET Status=N'Closed',ClosedAt=@ChangedAt,UpdatedAt=@ChangedAt,UpdatedBy=@UpdatedBy
 WHERE PatientUid=@PatientUid AND ReferralUid=@ReferralUid;
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
 VALUES(@UpdatedBy,@PatientId,N'ReferralClosed',N'PatientReferral',CONVERT(NVARCHAR(100),@ReferralUid),N'Status=ResponseReceived',N'Status=Closed',@ChangedAt);
 COMMIT;EXEC dbo.PatientReferral_GetByUid @PatientUid,@ReferralUid;
END;
GO
