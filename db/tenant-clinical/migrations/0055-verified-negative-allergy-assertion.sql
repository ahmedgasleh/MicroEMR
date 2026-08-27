CREATE TABLE dbo.PatientAllergyAssertion
(
    PatientAllergyAssertionId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PatientAllergyAssertion PRIMARY KEY,
    AssertionUid UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PatientAllergyAssertion_Uid DEFAULT NEWSEQUENTIALID(),
    PatientUid UNIQUEIDENTIFIER NOT NULL,
    AssertionType NVARCHAR(40) NOT NULL,
    Status NVARCHAR(20) NOT NULL,
    VerifiedBy BIGINT NOT NULL,
    VerifiedAtUtc DATETIME2(0) NOT NULL,
    RevokedBy BIGINT NULL,
    RevokedAtUtc DATETIME2(0) NULL,
    RevocationReason NVARCHAR(500) NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT UQ_PatientAllergyAssertion_Uid UNIQUE (AssertionUid),
    CONSTRAINT FK_PatientAllergyAssertion_Patient FOREIGN KEY (PatientUid) REFERENCES dbo.Patient(PatientUid),
    CONSTRAINT FK_PatientAllergyAssertion_VerifiedBy FOREIGN KEY (VerifiedBy) REFERENCES dbo.ApplicationUser(UserId),
    CONSTRAINT FK_PatientAllergyAssertion_RevokedBy FOREIGN KEY (RevokedBy) REFERENCES dbo.ApplicationUser(UserId),
    CONSTRAINT CK_PatientAllergyAssertion_Type CHECK (AssertionType = N'NoKnownAllergies'),
    CONSTRAINT CK_PatientAllergyAssertion_Status CHECK (Status IN (N'Active', N'Revoked')),
    CONSTRAINT CK_PatientAllergyAssertion_Revocation CHECK
    ((Status = N'Active' AND RevokedBy IS NULL AND RevokedAtUtc IS NULL) OR
     (Status = N'Revoked' AND RevokedBy IS NOT NULL AND RevokedAtUtc IS NOT NULL))
);
GO
CREATE UNIQUE INDEX UX_PatientAllergyAssertion_ActiveNka
ON dbo.PatientAllergyAssertion(PatientUid, AssertionType) WHERE Status = N'Active';
GO
CREATE INDEX IX_PatientAllergyAssertion_PatientHistory
ON dbo.PatientAllergyAssertion(PatientUid, VerifiedAtUtc DESC);
GO

CREATE OR ALTER PROCEDURE dbo.PatientAllergy_GetDocumentationState @PatientUid UNIQUEIDENTIFIER AS
BEGIN
 SET NOCOUNT ON;
 IF NOT EXISTS(SELECT 1 FROM dbo.Patient WHERE PatientUid=@PatientUid AND IsDeleted=0) RETURN;
 SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.PatientAllergy WHERE PatientUid=@PatientUid AND AllergyStatus=N'Active') THEN N'HasEntries'
             WHEN a.AssertionUid IS NOT NULL THEN N'ExplicitlyNone' ELSE N'NotDocumented' END DocumentationState,
        a.AssertionUid,a.PatientUid,a.Status,a.VerifiedBy,u.DisplayName VerifiedByDisplayName,a.VerifiedAtUtc,
        a.RevokedBy,a.RevokedAtUtc,a.RevocationReason,a.RowVersion
 FROM (VALUES(1)) seed(n)
 OUTER APPLY(SELECT TOP(1) x.* FROM dbo.PatientAllergyAssertion x
             WHERE x.PatientUid=@PatientUid AND x.AssertionType=N'NoKnownAllergies' AND x.Status=N'Active') a
 LEFT JOIN dbo.ApplicationUser u ON u.UserId=a.VerifiedBy;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientAllergy_Update
 @PatientUid UNIQUEIDENTIFIER,@AllergyUid UNIQUEIDENTIFIER,@AllergenName NVARCHAR(200),@AllergenType NVARCHAR(100)=NULL,
 @Reaction NVARCHAR(500)=NULL,@Severity NVARCHAR(30)=NULL,@OnsetDate DATE=NULL,@AllergyStatus NVARCHAR(30),
 @Notes NVARCHAR(1000)=NULL,@UpdatedBy BIGINT=NULL,@RowVersion BINARY(8) AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @PatientId BIGINT,@Now DATETIME2(0)=SYSUTCDATETIME();
 BEGIN TRANSACTION;
 SELECT @PatientId=PatientId FROM dbo.Patient WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND IsDeleted=0;
 IF @PatientId IS NULL BEGIN ROLLBACK; RETURN; END;
 IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@UpdatedBy AND IsActive=1) THROW 51055,'Active tenant clinical actor not found.',1;
 IF LTRIM(RTRIM(@AllergyStatus))=N'Active' AND EXISTS(SELECT 1 FROM dbo.PatientAllergyAssertion WHERE PatientUid=@PatientUid AND AssertionType=N'NoKnownAllergies' AND Status=N'Active')
  THROW 51058,'No Known Allergies is currently documented. Use the confirmed add-Allergy workflow to replace it.',1;
 UPDATE dbo.PatientAllergy SET AllergenName=LTRIM(RTRIM(@AllergenName)),AllergenType=NULLIF(LTRIM(RTRIM(@AllergenType)),N''),
  Reaction=NULLIF(LTRIM(RTRIM(@Reaction)),N''),Severity=NULLIF(LTRIM(RTRIM(@Severity)),N''),OnsetDate=@OnsetDate,
  AllergyStatus=LTRIM(RTRIM(@AllergyStatus)),Notes=NULLIF(LTRIM(RTRIM(@Notes)),N''),UpdatedBy=@UpdatedBy,UpdatedAt=@Now
 WHERE PatientUid=@PatientUid AND AllergyUid=@AllergyUid AND RowVersion=@RowVersion;
 IF @@ROWCOUNT=0 THROW 51052,'The allergy was changed by another user.',1;
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt)
 VALUES(@UpdatedBy,@PatientId,N'Update',N'PatientAllergy',CONVERT(NVARCHAR(100),@AllergyUid),N'Allergy updated',@Now);
 COMMIT;
 EXEC dbo.PatientAllergy_GetByUid @AllergyUid=@AllergyUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientAllergy_AssertNoKnownAllergies @PatientUid UNIQUEIDENTIFIER,@VerifiedBy BIGINT AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @PatientId BIGINT,@AssertionUid UNIQUEIDENTIFIER,@Now DATETIME2(0)=SYSUTCDATETIME();
 BEGIN TRANSACTION;
 SELECT @PatientId=PatientId FROM dbo.Patient WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND IsDeleted=0;
 IF @PatientId IS NULL BEGIN ROLLBACK; RETURN; END;
 IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@VerifiedBy AND IsActive=1) THROW 51055,'Active tenant clinical actor not found.',1;
 IF EXISTS(SELECT 1 FROM dbo.PatientAllergy WHERE PatientUid=@PatientUid AND AllergyStatus=N'Active') THROW 51056,'No Known Allergies cannot be documented while an active allergy exists.',1;
 SELECT @AssertionUid=AssertionUid FROM dbo.PatientAllergyAssertion WITH(UPDLOCK,HOLDLOCK)
 WHERE PatientUid=@PatientUid AND AssertionType=N'NoKnownAllergies' AND Status=N'Active';
 IF @AssertionUid IS NULL
 BEGIN
  SET @AssertionUid=NEWID();
  INSERT dbo.PatientAllergyAssertion(AssertionUid,PatientUid,AssertionType,Status,VerifiedBy,VerifiedAtUtc)
  VALUES(@AssertionUid,@PatientUid,N'NoKnownAllergies',N'Active',@VerifiedBy,@Now);
  INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt)
  VALUES(@VerifiedBy,@PatientId,N'NoKnownAllergiesAsserted',N'PatientAllergyAssertion',CONVERT(NVARCHAR(100),@AssertionUid),N'Status=Active',@Now);
 END;
 COMMIT;
 SELECT a.AssertionUid,a.PatientUid,a.Status,a.VerifiedBy,u.DisplayName VerifiedByDisplayName,a.VerifiedAtUtc,a.RevokedBy,a.RevokedAtUtc,a.RevocationReason,a.RowVersion
 FROM dbo.PatientAllergyAssertion a JOIN dbo.ApplicationUser u ON u.UserId=a.VerifiedBy WHERE a.AssertionUid=@AssertionUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientAllergy_RevokeNoKnownAllergies
 @PatientUid UNIQUEIDENTIFIER,@ExpectedRowVersion BINARY(8),@RevokedBy BIGINT,@Reason NVARCHAR(500)=NULL AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @PatientId BIGINT,@AssertionUid UNIQUEIDENTIFIER,@CurrentRowVersion BINARY(8),@Now DATETIME2(0)=SYSUTCDATETIME();
 BEGIN TRANSACTION;
 SELECT @PatientId=PatientId FROM dbo.Patient WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND IsDeleted=0;
 IF @PatientId IS NULL BEGIN ROLLBACK; RETURN; END;
 IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@RevokedBy AND IsActive=1) THROW 51055,'Active tenant clinical actor not found.',1;
 SELECT @AssertionUid=AssertionUid,@CurrentRowVersion=RowVersion FROM dbo.PatientAllergyAssertion WITH(UPDLOCK,HOLDLOCK)
 WHERE PatientUid=@PatientUid AND AssertionType=N'NoKnownAllergies' AND Status=N'Active';
 IF @AssertionUid IS NULL BEGIN COMMIT; RETURN; END;
 IF @CurrentRowVersion<>@ExpectedRowVersion THROW 51057,'The No Known Allergies assertion was changed by another user.',1;
 UPDATE dbo.PatientAllergyAssertion SET Status=N'Revoked',RevokedBy=@RevokedBy,RevokedAtUtc=@Now,RevocationReason=NULLIF(LTRIM(RTRIM(@Reason)),N'')
 WHERE AssertionUid=@AssertionUid AND RowVersion=@ExpectedRowVersion;
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt)
 VALUES(@RevokedBy,@PatientId,N'NoKnownAllergiesRevoked',N'PatientAllergyAssertion',CONVERT(NVARCHAR(100),@AssertionUid),N'Status=Revoked',@Now);
 COMMIT;
 SELECT a.AssertionUid,a.PatientUid,a.Status,a.VerifiedBy,u.DisplayName VerifiedByDisplayName,a.VerifiedAtUtc,a.RevokedBy,a.RevokedAtUtc,a.RevocationReason,a.RowVersion
 FROM dbo.PatientAllergyAssertion a JOIN dbo.ApplicationUser u ON u.UserId=a.VerifiedBy WHERE a.AssertionUid=@AssertionUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientAllergy_Create
 @PatientUid UNIQUEIDENTIFIER,@AllergenName NVARCHAR(200),@AllergenType NVARCHAR(100)=NULL,@Reaction NVARCHAR(500)=NULL,
 @Severity NVARCHAR(30)=NULL,@OnsetDate DATE=NULL,@Notes NVARCHAR(1000)=NULL,@CreatedBy BIGINT=NULL,
 @CreatedByDisplayName NVARCHAR(200)=NULL,@ConfirmReplaceNoKnownAllergies BIT=0 AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @PatientId BIGINT,@AllergyUid UNIQUEIDENTIFIER=NEWID(),@AssertionUid UNIQUEIDENTIFIER,@Now DATETIME2(0)=SYSUTCDATETIME(),@ActorName NVARCHAR(200);
 BEGIN TRANSACTION;
 SELECT @PatientId=PatientId FROM dbo.Patient WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND IsDeleted=0;
 IF @PatientId IS NULL THROW 51051,'The requested patient was not found.',1;
 SELECT @ActorName=DisplayName FROM dbo.ApplicationUser WHERE UserId=@CreatedBy AND IsActive=1;
 IF @ActorName IS NULL THROW 51055,'Active tenant clinical actor not found.',1;
 SELECT @AssertionUid=AssertionUid FROM dbo.PatientAllergyAssertion WITH(UPDLOCK,HOLDLOCK)
 WHERE PatientUid=@PatientUid AND AssertionType=N'NoKnownAllergies' AND Status=N'Active';
 IF @AssertionUid IS NOT NULL AND @ConfirmReplaceNoKnownAllergies=0 THROW 51058,'No Known Allergies is currently documented. Confirm replacement before adding this allergy.',1;
 IF @AssertionUid IS NOT NULL
 BEGIN
  UPDATE dbo.PatientAllergyAssertion SET Status=N'Revoked',RevokedBy=@CreatedBy,RevokedAtUtc=@Now,RevocationReason=N'Replaced by active allergy' WHERE AssertionUid=@AssertionUid;
  INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt)
  VALUES(@CreatedBy,@PatientId,N'NoKnownAllergiesRevoked',N'PatientAllergyAssertion',CONVERT(NVARCHAR(100),@AssertionUid),N'Status=Revoked;Reason=ReplacedByAllergy',@Now);
 END;
 INSERT dbo.PatientAllergy(AllergyUid,PatientUid,AllergenName,AllergenType,Reaction,Severity,OnsetDate,Notes,AllergyStatus,CreatedBy,CreatedByDisplayName,CreatedAt)
 VALUES(@AllergyUid,@PatientUid,LTRIM(RTRIM(@AllergenName)),NULLIF(LTRIM(RTRIM(@AllergenType)),N''),NULLIF(LTRIM(RTRIM(@Reaction)),N''),NULLIF(LTRIM(RTRIM(@Severity)),N''),@OnsetDate,NULLIF(LTRIM(RTRIM(@Notes)),N''),N'Active',@CreatedBy,@ActorName,@Now);
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt)
 VALUES(@CreatedBy,@PatientId,N'Create',N'PatientAllergy',CONVERT(NVARCHAR(100),@AllergyUid),N'Allergy created',@Now);
 COMMIT;
 EXEC dbo.PatientAllergy_GetByUid @AllergyUid=@AllergyUid;
END;
GO
