SET XACT_ABORT ON;
GO
CREATE TABLE dbo.PatientReferralDocument
(
    ReferralUid UNIQUEIDENTIFIER NOT NULL,
    DocumentUid UNIQUEIDENTIFIER NOT NULL,
    LinkedAt DATETIME2(0) NOT NULL CONSTRAINT DF_PatientReferralDocument_LinkedAt DEFAULT SYSUTCDATETIME(),
    LinkedBy BIGINT NOT NULL,
    CONSTRAINT PK_PatientReferralDocument PRIMARY KEY (ReferralUid, DocumentUid),
    CONSTRAINT FK_PatientReferralDocument_Referral FOREIGN KEY (ReferralUid) REFERENCES dbo.PatientReferral(ReferralUid),
    CONSTRAINT FK_PatientReferralDocument_Document FOREIGN KEY (DocumentUid) REFERENCES dbo.PatientDocument(PatientDocumentUid),
    CONSTRAINT FK_PatientReferralDocument_LinkedBy FOREIGN KEY (LinkedBy) REFERENCES dbo.ApplicationUser(UserId)
);
GO
CREATE OR ALTER PROCEDURE dbo.PatientReferralDocument_GetByReferralUid
 @PatientUid UNIQUEIDENTIFIER, @ReferralUid UNIQUEIDENTIFIER
AS BEGIN SET NOCOUNT ON;
 SELECT d.PatientDocumentUid DocumentUid,d.DocumentTitle Title,d.DocumentType,d.DocumentStatus,
 d.CreatedAt,d.CreatedBy,cu.DisplayName CreatedByDisplayName,l.LinkedAt,l.LinkedBy,lu.DisplayName LinkedByDisplayName
 FROM dbo.PatientReferralDocument l
 JOIN dbo.PatientReferral r ON r.ReferralUid=l.ReferralUid AND r.PatientUid=@PatientUid
 JOIN dbo.PatientDocument d ON d.PatientDocumentUid=l.DocumentUid AND d.PatientUid=@PatientUid AND d.IsDeleted=0
 LEFT JOIN dbo.ApplicationUser cu ON cu.UserId=d.CreatedBy LEFT JOIN dbo.ApplicationUser lu ON lu.UserId=l.LinkedBy
 WHERE l.ReferralUid=@ReferralUid ORDER BY l.LinkedAt,d.DocumentTitle;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientReferralDocument_Link
 @PatientUid UNIQUEIDENTIFIER,@ReferralUid UNIQUEIDENTIFIER,@DocumentUid UNIQUEIDENTIFIER,
 @ExpectedRowVersion BINARY(8),@Actor BIGINT
AS BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;BEGIN TRANSACTION;
 DECLARE @Status NVARCHAR(30),@Version BINARY(8),@PatientId BIGINT;
 SELECT @Status=r.Status,@Version=r.RowVersion,@PatientId=p.PatientId FROM dbo.PatientReferral r WITH(UPDLOCK,HOLDLOCK)
 JOIN dbo.Patient p ON p.PatientUid=r.PatientUid AND p.IsDeleted=0 WHERE r.ReferralUid=@ReferralUid AND r.PatientUid=@PatientUid;
 IF @Status IS NULL BEGIN ROLLBACK;THROW 51600,'Referral not found.',1;END;
 IF NOT EXISTS(SELECT 1 FROM dbo.PatientDocument WHERE PatientDocumentUid=@DocumentUid AND PatientUid=@PatientUid AND IsDeleted=0)
  BEGIN ROLLBACK;THROW 51601,'Document not found.',1;END;
 IF @Status<>N'Draft' BEGIN ROLLBACK;THROW 51602,'Referral is not Draft.',1;END;
 IF @Version<>@ExpectedRowVersion BEGIN ROLLBACK;THROW 51603,'Referral concurrency conflict.',1;END;
 IF EXISTS(SELECT 1 FROM dbo.PatientReferralDocument WHERE ReferralUid=@ReferralUid AND DocumentUid=@DocumentUid)
  BEGIN ROLLBACK;THROW 51605,'Document already linked.',1;END;
 IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@Actor AND IsActive=1)
  BEGIN ROLLBACK;THROW 51606,'Active actor not found.',1;END;
 INSERT dbo.PatientReferralDocument(ReferralUid,DocumentUid,LinkedBy)VALUES(@ReferralUid,@DocumentUid,@Actor);
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
 VALUES(@Actor,@PatientId,N'LinkDocument',N'PatientReferral',CONVERT(NVARCHAR(100),@ReferralUid),NULL,CONVERT(NVARCHAR(100),@DocumentUid),SYSUTCDATETIME());
 COMMIT;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientReferralDocument_Unlink
 @PatientUid UNIQUEIDENTIFIER,@ReferralUid UNIQUEIDENTIFIER,@DocumentUid UNIQUEIDENTIFIER,
 @ExpectedRowVersion BINARY(8),@Actor BIGINT
AS BEGIN SET NOCOUNT ON;SET XACT_ABORT ON;BEGIN TRANSACTION;
 DECLARE @Status NVARCHAR(30),@Version BINARY(8),@PatientId BIGINT;
 SELECT @Status=r.Status,@Version=r.RowVersion,@PatientId=p.PatientId FROM dbo.PatientReferral r WITH(UPDLOCK,HOLDLOCK)
 JOIN dbo.Patient p ON p.PatientUid=r.PatientUid AND p.IsDeleted=0 WHERE r.ReferralUid=@ReferralUid AND r.PatientUid=@PatientUid;
 IF @Status IS NULL BEGIN ROLLBACK;THROW 51600,'Referral not found.',1;END;
 IF @Status<>N'Draft' BEGIN ROLLBACK;THROW 51602,'Referral is not Draft.',1;END;
 IF @Version<>@ExpectedRowVersion BEGIN ROLLBACK;THROW 51603,'Referral concurrency conflict.',1;END;
 IF NOT EXISTS(SELECT 1 FROM dbo.PatientReferralDocument WHERE ReferralUid=@ReferralUid AND DocumentUid=@DocumentUid)
  BEGIN ROLLBACK;THROW 51604,'Link not found.',1;END;
 DELETE FROM dbo.PatientReferralDocument WHERE ReferralUid=@ReferralUid AND DocumentUid=@DocumentUid;
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
 VALUES(@Actor,@PatientId,N'UnlinkDocument',N'PatientReferral',CONVERT(NVARCHAR(100),@ReferralUid),CONVERT(NVARCHAR(100),@DocumentUid),NULL,SYSUTCDATETIME());
 COMMIT;
END;
GO
