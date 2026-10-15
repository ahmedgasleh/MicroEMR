/* Step 87: independent initial/refill dispensing instructions.
   Nullable columns preserve legacy records; no clinical values or artifacts are backfilled. */
IF OBJECT_ID(N'dbo.PatientPrescription',N'U') IS NULL
    THROW 51500,'Prescription foundation is required.',1;
GO
ALTER TABLE dbo.PatientPrescription ADD
    InitialDaysSupply INT NULL,
    RefillQuantity DECIMAL(18,3) NULL,
    RefillQuantityUnit NVARCHAR(50) NULL,
    RefillDaysSupply INT NULL;
GO
ALTER TABLE dbo.PatientPrescription ADD
    CONSTRAINT CK_PatientPrescription_InitialDaysSupply CHECK (InitialDaysSupply IS NULL OR InitialDaysSupply>0),
    CONSTRAINT CK_PatientPrescription_RefillDaysSupply CHECK (RefillDaysSupply IS NULL OR RefillDaysSupply>0),
    CONSTRAINT CK_PatientPrescription_RefillQuantityPair CHECK (
        (RefillQuantity IS NULL AND RefillQuantityUnit IS NULL) OR
        (RefillQuantity IS NOT NULL AND RefillQuantity>0 AND RefillQuantityUnit IS NOT NULL AND LEN(LTRIM(RTRIM(RefillQuantityUnit)))>0)),
    CONSTRAINT CK_PatientPrescription_RefillInstructions CHECK (
        AuthorizedRepeats>0 OR (RefillQuantity IS NULL AND RefillQuantityUnit IS NULL AND RefillDaysSupply IS NULL));
GO
CREATE OR ALTER PROCEDURE dbo.PatientPrescription_GetByPatientUid @PatientUid UNIQUEIDENTIFIER AS
BEGIN
 SET NOCOUNT ON;
 SELECT PrescriptionUid,PatientUid,Status,ProductName,ProductIdentifierNamespace,ProductIdentifierValue,ProductDisplayText,
 StrengthValue,StrengthUnit,DoseAmount,DoseUnit,Route,FrequencyCode,FrequencyDisplay,Prn,Directions,Quantity,QuantityUnit,
 AuthorizedRepeats,InitialDaysSupply,RefillQuantity,RefillQuantityUnit,RefillDaysSupply,Indication,PrescribedDate,StartDate,CreatedBy,CreatedAtUtc,UpdatedBy,UpdatedAtUtc,PrescriberUserId,
 PrescriberProviderUid,PrescriberDisplayNameSnapshot,PrescriberCredentialSnapshot,ProductDisplaySnapshot,FinalizedBy,
 FinalizedAtUtc,CancelledBy,CancelledAtUtc,CancellationReason,SupersedesPrescriptionUid,SupersededByPrescriptionUid,ArtifactUid,RowVersion
 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid ORDER BY PrescribedDate DESC,CreatedAtUtc DESC;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientPrescription_GetByUid @PatientUid UNIQUEIDENTIFIER,@PrescriptionUid UNIQUEIDENTIFIER AS
BEGIN
 SET NOCOUNT ON;
 SELECT PrescriptionUid,PatientUid,Status,ProductName,ProductIdentifierNamespace,ProductIdentifierValue,ProductDisplayText,
 StrengthValue,StrengthUnit,DoseAmount,DoseUnit,Route,FrequencyCode,FrequencyDisplay,Prn,Directions,Quantity,QuantityUnit,
 AuthorizedRepeats,InitialDaysSupply,RefillQuantity,RefillQuantityUnit,RefillDaysSupply,Indication,PrescribedDate,StartDate,CreatedBy,CreatedAtUtc,UpdatedBy,UpdatedAtUtc,PrescriberUserId,
 PrescriberProviderUid,PrescriberDisplayNameSnapshot,PrescriberCredentialSnapshot,ProductDisplaySnapshot,FinalizedBy,
 FinalizedAtUtc,CancelledBy,CancelledAtUtc,CancellationReason,SupersedesPrescriptionUid,SupersededByPrescriptionUid,ArtifactUid,RowVersion
 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientPrescription_CreateDraft
 @PatientUid UNIQUEIDENTIFIER,@ProductName NVARCHAR(200),@ProductIdentifierNamespace NVARCHAR(100)=NULL,@ProductIdentifierValue NVARCHAR(100)=NULL,
 @ProductDisplayText NVARCHAR(300),@StrengthValue DECIMAL(18,6)=NULL,@StrengthUnit NVARCHAR(50)=NULL,@DoseAmount DECIMAL(18,6)=NULL,
 @DoseUnit NVARCHAR(50)=NULL,@Route NVARCHAR(100),@FrequencyCode NVARCHAR(40),@FrequencyDisplay NVARCHAR(100),@Prn BIT,
 @Directions NVARCHAR(1000),@Quantity DECIMAL(18,3),@QuantityUnit NVARCHAR(50),@AuthorizedRepeats INT,@Indication NVARCHAR(500)=NULL,
 @PrescribedDate DATE,@StartDate DATE=NULL,@ActorUserId BIGINT,@SupersedesPrescriptionUid UNIQUEIDENTIFIER=NULL,
 @InitialDaysSupply INT=NULL,@RefillQuantity DECIMAL(18,3)=NULL,@RefillQuantityUnit NVARCHAR(50)=NULL,@RefillDaysSupply INT=NULL AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @PatientId BIGINT,@ProviderUid UNIQUEIDENTIFIER,@Uid UNIQUEIDENTIFIER=NEWID();
 SELECT @PatientId=PatientId FROM dbo.Patient WHERE PatientUid=@PatientUid AND IsDeleted=0;
 SELECT @ProviderUid=p.ProviderUid FROM dbo.ApplicationUser u JOIN dbo.Provider p ON p.ProviderId=u.ProviderId AND p.IsActive=1 WHERE u.UserId=@ActorUserId AND u.IsActive=1;
 IF @PatientId IS NULL THROW 51501,'Patient not found.',1;
 IF @ProviderUid IS NULL THROW 51502,'An active mapped provider is required to prescribe.',1;
 IF @SupersedesPrescriptionUid IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.PatientPrescription WHERE PatientUid=@PatientUid AND PrescriptionUid=@SupersedesPrescriptionUid AND Status=N'Finalized') THROW 51503,'Correction source must be a finalized prescription for this patient.',1;
 BEGIN TRANSACTION;
 INSERT dbo.PatientPrescription(PrescriptionUid,PatientUid,ProductName,ProductIdentifierNamespace,ProductIdentifierValue,ProductDisplayText,StrengthValue,StrengthUnit,DoseAmount,DoseUnit,Route,FrequencyCode,FrequencyDisplay,Prn,Directions,Quantity,QuantityUnit,AuthorizedRepeats,InitialDaysSupply,RefillQuantity,RefillQuantityUnit,RefillDaysSupply,Indication,PrescribedDate,StartDate,CreatedBy,PrescriberUserId,PrescriberProviderUid,SupersedesPrescriptionUid)
 VALUES(@Uid,@PatientUid,LTRIM(RTRIM(@ProductName)),NULLIF(LTRIM(RTRIM(@ProductIdentifierNamespace)),N''),NULLIF(LTRIM(RTRIM(@ProductIdentifierValue)),N''),LTRIM(RTRIM(@ProductDisplayText)),@StrengthValue,NULLIF(LTRIM(RTRIM(@StrengthUnit)),N''),@DoseAmount,NULLIF(LTRIM(RTRIM(@DoseUnit)),N''),LTRIM(RTRIM(@Route)),@FrequencyCode,@FrequencyDisplay,@Prn,LTRIM(RTRIM(@Directions)),@Quantity,LTRIM(RTRIM(@QuantityUnit)),@AuthorizedRepeats,@InitialDaysSupply,@RefillQuantity,NULLIF(LTRIM(RTRIM(@RefillQuantityUnit)),N''),@RefillDaysSupply,NULLIF(LTRIM(RTRIM(@Indication)),N''),@PrescribedDate,@StartDate,@ActorUserId,@ActorUserId,@ProviderUid,@SupersedesPrescriptionUid);
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt) VALUES(@ActorUserId,@PatientId,N'PrescriptionDraftCreated',N'PatientPrescription',CONVERT(NVARCHAR(100),@Uid),N'Draft created',SYSUTCDATETIME());
 COMMIT; EXEC dbo.PatientPrescription_GetByUid @PatientUid,@Uid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientPrescription_UpdateDraft
 @PatientUid UNIQUEIDENTIFIER,@PrescriptionUid UNIQUEIDENTIFIER,@ProductName NVARCHAR(200),@ProductIdentifierNamespace NVARCHAR(100)=NULL,@ProductIdentifierValue NVARCHAR(100)=NULL,
 @ProductDisplayText NVARCHAR(300),@StrengthValue DECIMAL(18,6)=NULL,@StrengthUnit NVARCHAR(50)=NULL,@DoseAmount DECIMAL(18,6)=NULL,@DoseUnit NVARCHAR(50)=NULL,
 @Route NVARCHAR(100),@FrequencyCode NVARCHAR(40),@FrequencyDisplay NVARCHAR(100),@Prn BIT,@Directions NVARCHAR(1000),@Quantity DECIMAL(18,3),@QuantityUnit NVARCHAR(50),
 @AuthorizedRepeats INT,@Indication NVARCHAR(500)=NULL,@PrescribedDate DATE,@StartDate DATE=NULL,@ActorUserId BIGINT,@RowVersion BINARY(8),
 @InitialDaysSupply INT=NULL,@RefillQuantity DECIMAL(18,3)=NULL,@RefillQuantityUnit NVARCHAR(50)=NULL,@RefillDaysSupply INT=NULL AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;DECLARE @PatientId BIGINT;
 BEGIN TRANSACTION;
 SELECT @PatientId=p.PatientId FROM dbo.PatientPrescription x WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Patient p ON p.PatientUid=x.PatientUid AND p.IsDeleted=0 WHERE x.PatientUid=@PatientUid AND x.PrescriptionUid=@PrescriptionUid;
 IF @PatientId IS NULL BEGIN ROLLBACK;RETURN;END;
 UPDATE dbo.PatientPrescription SET ProductName=LTRIM(RTRIM(@ProductName)),ProductIdentifierNamespace=NULLIF(LTRIM(RTRIM(@ProductIdentifierNamespace)),N''),ProductIdentifierValue=NULLIF(LTRIM(RTRIM(@ProductIdentifierValue)),N''),ProductDisplayText=LTRIM(RTRIM(@ProductDisplayText)),StrengthValue=@StrengthValue,StrengthUnit=NULLIF(LTRIM(RTRIM(@StrengthUnit)),N''),DoseAmount=@DoseAmount,DoseUnit=NULLIF(LTRIM(RTRIM(@DoseUnit)),N''),Route=LTRIM(RTRIM(@Route)),FrequencyCode=@FrequencyCode,FrequencyDisplay=@FrequencyDisplay,Prn=@Prn,Directions=LTRIM(RTRIM(@Directions)),Quantity=@Quantity,QuantityUnit=LTRIM(RTRIM(@QuantityUnit)),AuthorizedRepeats=@AuthorizedRepeats,InitialDaysSupply=@InitialDaysSupply,RefillQuantity=@RefillQuantity,RefillQuantityUnit=NULLIF(LTRIM(RTRIM(@RefillQuantityUnit)),N''),RefillDaysSupply=@RefillDaysSupply,Indication=NULLIF(LTRIM(RTRIM(@Indication)),N''),PrescribedDate=@PrescribedDate,StartDate=@StartDate,UpdatedBy=@ActorUserId,UpdatedAtUtc=SYSUTCDATETIME()
 WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid AND Status=N'Draft' AND PrescriberUserId=@ActorUserId AND RowVersion=@RowVersion;
 IF @@ROWCOUNT=0 BEGIN ROLLBACK;THROW 51504,'Draft is stale, immutable, or belongs to another prescriber.',1;END;
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt) VALUES(@ActorUserId,@PatientId,N'PrescriptionDraftUpdated',N'PatientPrescription',CONVERT(NVARCHAR(100),@PrescriptionUid),N'Draft updated',SYSUTCDATETIME());
 COMMIT;EXEC dbo.PatientPrescription_GetByUid @PatientUid,@PrescriptionUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientPrescription_Finalize @PatientUid UNIQUEIDENTIFIER,@PrescriptionUid UNIQUEIDENTIFIER,@ActorUserId BIGINT,@RowVersion BINARY(8) AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;DECLARE @PatientId BIGINT,@ProviderUid UNIQUEIDENTIFIER,@ProviderName NVARCHAR(200),@Credential NVARCHAR(200),@ArtifactUid UNIQUEIDENTIFIER=NEWID(),@Original UNIQUEIDENTIFIER,@Html NVARCHAR(MAX);
 BEGIN TRANSACTION;
 SELECT @ProviderUid=p.ProviderUid,@ProviderName=p.DisplayName,@Credential=CONCAT(p.ProviderType,CASE WHEN p.BillingNumber IS NULL THEN N'' ELSE N' | '+p.BillingNumber END) FROM dbo.ApplicationUser u JOIN dbo.Provider p ON p.ProviderId=u.ProviderId AND p.IsActive=1 WHERE u.UserId=@ActorUserId AND u.IsActive=1;
 SELECT @PatientId=p.PatientId,@Original=x.SupersedesPrescriptionUid FROM dbo.PatientPrescription x WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Patient p ON p.PatientUid=x.PatientUid AND p.IsDeleted=0 WHERE x.PatientUid=@PatientUid AND x.PrescriptionUid=@PrescriptionUid AND x.Status=N'Draft' AND x.PrescriberUserId=@ActorUserId AND x.RowVersion=@RowVersion;
 IF @ProviderUid IS NULL BEGIN ROLLBACK;THROW 51502,'An active mapped provider is required to prescribe.',1;END;
 IF @PatientId IS NULL BEGIN ROLLBACK;THROW 51504,'Draft is stale, immutable, missing, or belongs to another prescriber.',1;END;
 SELECT @Html=(SELECT CONCAT(p.FirstName,N' ',p.LastName) PatientName,p.DateOfBirth,x.PrescribedDate,x.ProductDisplayText,x.StrengthValue,x.StrengthUnit,x.DoseAmount,x.DoseUnit,x.Route,x.FrequencyDisplay,x.Prn,x.Directions,x.Quantity,x.QuantityUnit,x.AuthorizedRepeats,x.InitialDaysSupply,x.RefillQuantity,x.RefillQuantityUnit,x.RefillDaysSupply,1 RefillInstructionsVersion,x.Indication,@ProviderName PrescriberDisplayName,@Credential PrescriberCredential,x.PrescriptionUid FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) FROM dbo.PatientPrescription x JOIN dbo.Patient p ON p.PatientUid=x.PatientUid WHERE x.PrescriptionUid=@PrescriptionUid;
 UPDATE dbo.PatientPrescription SET Status=N'Finalized',PrescriberProviderUid=@ProviderUid,PrescriberDisplayNameSnapshot=@ProviderName,PrescriberCredentialSnapshot=@Credential,ProductDisplaySnapshot=ProductDisplayText,FinalizedBy=@ActorUserId,FinalizedAtUtc=SYSUTCDATETIME(),ArtifactUid=@ArtifactUid WHERE PrescriptionUid=@PrescriptionUid;
 INSERT dbo.PatientPrescriptionArtifact(ArtifactUid,PrescriptionUid,PatientUid,ArtifactJson,CreatedBy) VALUES(@ArtifactUid,@PrescriptionUid,@PatientUid,@Html,@ActorUserId);
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt) VALUES(@ActorUserId,@PatientId,N'PrescriptionFinalized',N'PatientPrescription',CONVERT(NVARCHAR(100),@PrescriptionUid),N'Prescription finalized',SYSUTCDATETIME());
 IF @Original IS NOT NULL BEGIN
   UPDATE dbo.PatientPrescription SET Status=N'Superseded',SupersededByPrescriptionUid=@PrescriptionUid WHERE PatientUid=@PatientUid AND PrescriptionUid=@Original AND Status=N'Finalized';
   IF @@ROWCOUNT<>1 BEGIN ROLLBACK;THROW 51505,'The correction source is no longer eligible for supersession.',1;END;
   INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt) VALUES(@ActorUserId,@PatientId,N'PrescriptionSuperseded',N'PatientPrescription',CONVERT(NVARCHAR(100),@Original),N'Superseded by corrected prescription',SYSUTCDATETIME());
 END;
 COMMIT;EXEC dbo.PatientPrescription_GetByUid @PatientUid,@PrescriptionUid;
END;
GO
