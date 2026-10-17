SET XACT_ABORT ON;
GO
CREATE TABLE dbo.PatientRiskFactor
(
 RiskFactorId bigint IDENTITY PRIMARY KEY,
 RiskFactorUid uniqueidentifier NOT NULL DEFAULT NEWSEQUENTIALID(),
 PatientUid uniqueidentifier NOT NULL REFERENCES dbo.Patient(PatientUid),
 RiskFactor nvarchar(200) NOT NULL CHECK (LEN(LTRIM(RTRIM(RiskFactor))) > 0),
 ExposureDetails nvarchar(1000) NULL,
 LifeStageCode nvarchar(1) NULL CHECK (LifeStageCode COLLATE Latin1_General_100_BIN2 IN (N'N',N'I',N'C',N'T',N'A')),
 Notes nvarchar(1000) NULL,
 Status nvarchar(20) NOT NULL DEFAULT N'Active' CHECK (Status IN (N'Active',N'Archived')),
 CreatedAt datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 CreatedBy bigint NOT NULL REFERENCES dbo.ApplicationUser(UserId),
 UpdatedAt datetime2(0) NULL, UpdatedBy bigint NULL REFERENCES dbo.ApplicationUser(UserId),
 RowVersion rowversion NOT NULL,
 CONSTRAINT UQ_PatientRiskFactor_Uid UNIQUE (RiskFactorUid)
);
CREATE INDEX IX_PatientRiskFactor_Patient_Status
 ON dbo.PatientRiskFactor(PatientUid,Status,CreatedAt DESC);
GO
CREATE OR ALTER PROCEDURE dbo.PatientRiskFactor_Get
 @PatientUid uniqueidentifier, @RiskFactorUid uniqueidentifier
AS
BEGIN
 SET NOCOUNT ON;
 IF NOT EXISTS(SELECT 1 FROM dbo.Patient WHERE PatientUid=@PatientUid AND IsDeleted=0)
  THROW 52800,'Patient not found.',1;
 SELECT h.*,cu.DisplayName CreatedByDisplayName,uu.DisplayName UpdatedByDisplayName
 FROM dbo.PatientRiskFactor h
 LEFT JOIN dbo.ApplicationUser cu ON cu.UserId=h.CreatedBy
 LEFT JOIN dbo.ApplicationUser uu ON uu.UserId=h.UpdatedBy
 WHERE h.PatientUid=@PatientUid AND h.RiskFactorUid=@RiskFactorUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientRiskFactor_List
 @PatientUid uniqueidentifier, @Status nvarchar(20)=N'Active'
AS
BEGIN
 SET NOCOUNT ON;
 IF NOT EXISTS(SELECT 1 FROM dbo.Patient WHERE PatientUid=@PatientUid AND IsDeleted=0)
  THROW 52800,'Patient not found.',1;
 IF @Status NOT IN(N'Active',N'Archived',N'All') SET @Status=N'Active';
 SELECT h.*,cu.DisplayName CreatedByDisplayName,uu.DisplayName UpdatedByDisplayName
 FROM dbo.PatientRiskFactor h
 LEFT JOIN dbo.ApplicationUser cu ON cu.UserId=h.CreatedBy
 LEFT JOIN dbo.ApplicationUser uu ON uu.UserId=h.UpdatedBy
 WHERE h.PatientUid=@PatientUid AND (@Status=N'All' OR h.Status=@Status)
 ORDER BY h.CreatedAt DESC,h.RiskFactorUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientRiskFactor_Create
 @PatientUid uniqueidentifier,
 @RiskFactor nvarchar(200), @ExposureDetails nvarchar(1000)=NULL,
 @LifeStageCode nvarchar(10)=NULL,
 @Notes nvarchar(1000)=NULL,
 @Actor bigint
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 SET @RiskFactor=LTRIM(RTRIM(@RiskFactor)); SET @ExposureDetails=NULLIF(LTRIM(RTRIM(@ExposureDetails)),N'');
 SET @LifeStageCode=NULLIF(LTRIM(RTRIM(@LifeStageCode)),N'');
 SET @Notes=NULLIF(LTRIM(RTRIM(@Notes)),N'');
 IF NULLIF(@RiskFactor,N'') IS NULL
    OR (@LifeStageCode IS NOT NULL AND (LEN(@LifeStageCode)<>1 OR @LifeStageCode COLLATE Latin1_General_100_BIN2 NOT IN(N'N',N'I',N'C',N'T',N'A')))
  THROW 52804,'Invalid risk factor fields.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@Actor AND IsActive=1)
  THROW 52803,'Active clinical actor required.',1;
 DECLARE @PatientId bigint,@OldValue nvarchar(max),@NewValue nvarchar(max);
 BEGIN TRY
 BEGIN TRANSACTION;
 SELECT @PatientId=PatientId FROM dbo.Patient WITH (UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND IsDeleted=0;
 IF @PatientId IS NULL THROW 52800,'Patient not found.',1;
 DECLARE @RiskFactorUid uniqueidentifier=NEWID();
 INSERT dbo.PatientRiskFactor(RiskFactorUid,PatientUid,RiskFactor,ExposureDetails,LifeStageCode,Notes,CreatedBy)
 VALUES(@RiskFactorUid,@PatientUid,@RiskFactor,@ExposureDetails,@LifeStageCode,@Notes,@Actor);
 SELECT @NewValue=(SELECT h.RiskFactor,h.ExposureDetails,h.LifeStageCode,h.Notes,h.Status FROM dbo.PatientRiskFactor h
  WHERE h.PatientUid=@PatientUid AND h.RiskFactorUid=@RiskFactorUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
 VALUES(@Actor,@PatientId,N'Create',N'PatientRiskFactor',CONVERT(nvarchar(100),@RiskFactorUid),@OldValue,@NewValue,SYSUTCDATETIME());
 COMMIT;
 END TRY
 BEGIN CATCH
  IF @@TRANCOUNT>0 ROLLBACK;
  THROW;
 END CATCH;
 EXEC dbo.PatientRiskFactor_Get @PatientUid,@RiskFactorUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientRiskFactor_Update
 @PatientUid uniqueidentifier,
 @RiskFactorUid uniqueidentifier,
 @RiskFactor nvarchar(200), @ExposureDetails nvarchar(1000)=NULL,
 @LifeStageCode nvarchar(10)=NULL,
 @Notes nvarchar(1000)=NULL,
 @ExpectedRowVersion binary(8),
 @Actor bigint
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 SET @RiskFactor=LTRIM(RTRIM(@RiskFactor)); SET @ExposureDetails=NULLIF(LTRIM(RTRIM(@ExposureDetails)),N'');
 SET @LifeStageCode=NULLIF(LTRIM(RTRIM(@LifeStageCode)),N'');
 SET @Notes=NULLIF(LTRIM(RTRIM(@Notes)),N'');
 IF NULLIF(@RiskFactor,N'') IS NULL
    OR (@LifeStageCode IS NOT NULL AND (LEN(@LifeStageCode)<>1 OR @LifeStageCode COLLATE Latin1_General_100_BIN2 NOT IN(N'N',N'I',N'C',N'T',N'A')))
  THROW 52804,'Invalid risk factor fields.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@Actor AND IsActive=1)
  THROW 52803,'Active clinical actor required.',1;
 DECLARE @PatientId bigint,@OldValue nvarchar(max),@NewValue nvarchar(max);
 BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @CurrentVersion binary(8),@Status nvarchar(20);
 SELECT @PatientId=p.PatientId,@CurrentVersion=h.RowVersion,@Status=h.Status,
  @OldValue=(SELECT h.RiskFactor,h.ExposureDetails,h.LifeStageCode,h.Notes,h.Status FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)
 FROM dbo.PatientRiskFactor h WITH (UPDLOCK,HOLDLOCK)
 JOIN dbo.Patient p ON p.PatientUid=h.PatientUid AND p.IsDeleted=0
 WHERE h.PatientUid=@PatientUid AND h.RiskFactorUid=@RiskFactorUid;
 IF @PatientId IS NULL BEGIN ROLLBACK; RETURN; END;
 IF @ExpectedRowVersion IS NULL OR @CurrentVersion<>@ExpectedRowVersion THROW 52801,'Risk factor changed. Reload before saving.',1;
 IF @Status<>N'Active' THROW 52802,'Archived risk factor cannot be changed.',1;
 UPDATE dbo.PatientRiskFactor SET RiskFactor=@RiskFactor,ExposureDetails=@ExposureDetails,LifeStageCode=@LifeStageCode,Notes=@Notes,UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@Actor
 WHERE PatientUid=@PatientUid AND RiskFactorUid=@RiskFactorUid AND RowVersion=@ExpectedRowVersion;
 IF @@ROWCOUNT<>1 THROW 52801,'Risk factor changed. Reload before saving.',1;
 SELECT @NewValue=(SELECT h.RiskFactor,h.ExposureDetails,h.LifeStageCode,h.Notes,h.Status FROM dbo.PatientRiskFactor h
  WHERE h.PatientUid=@PatientUid AND h.RiskFactorUid=@RiskFactorUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
 VALUES(@Actor,@PatientId,N'Update',N'PatientRiskFactor',CONVERT(nvarchar(100),@RiskFactorUid),@OldValue,@NewValue,SYSUTCDATETIME());
 COMMIT;
 END TRY
 BEGIN CATCH
  IF @@TRANCOUNT>0 ROLLBACK;
  THROW;
 END CATCH;
 EXEC dbo.PatientRiskFactor_Get @PatientUid,@RiskFactorUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientRiskFactor_Archive
 @PatientUid uniqueidentifier,
 @RiskFactorUid uniqueidentifier,
 @ExpectedRowVersion binary(8),
 @Actor bigint
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@Actor AND IsActive=1)
  THROW 52803,'Active clinical actor required.',1;
 DECLARE @PatientId bigint,@OldValue nvarchar(max),@NewValue nvarchar(max);
 BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @CurrentVersion binary(8),@Status nvarchar(20);
 SELECT @PatientId=p.PatientId,@CurrentVersion=h.RowVersion,@Status=h.Status,
  @OldValue=(SELECT h.RiskFactor,h.ExposureDetails,h.LifeStageCode,h.Notes,h.Status FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)
 FROM dbo.PatientRiskFactor h WITH (UPDLOCK,HOLDLOCK)
 JOIN dbo.Patient p ON p.PatientUid=h.PatientUid AND p.IsDeleted=0
 WHERE h.PatientUid=@PatientUid AND h.RiskFactorUid=@RiskFactorUid;
 IF @PatientId IS NULL BEGIN ROLLBACK; RETURN; END;
 IF @ExpectedRowVersion IS NULL OR @CurrentVersion<>@ExpectedRowVersion THROW 52801,'Risk factor changed. Reload before saving.',1;
 IF @Status<>N'Active' THROW 52802,'Archived risk factor cannot be changed.',1;
 UPDATE dbo.PatientRiskFactor SET Status=N'Archived',UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@Actor
 WHERE PatientUid=@PatientUid AND RiskFactorUid=@RiskFactorUid AND RowVersion=@ExpectedRowVersion;
 IF @@ROWCOUNT<>1 THROW 52801,'Risk factor changed. Reload before saving.',1;
 SELECT @NewValue=(SELECT h.RiskFactor,h.ExposureDetails,h.LifeStageCode,h.Notes,h.Status FROM dbo.PatientRiskFactor h
  WHERE h.PatientUid=@PatientUid AND h.RiskFactorUid=@RiskFactorUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
 VALUES(@Actor,@PatientId,N'Archive',N'PatientRiskFactor',CONVERT(nvarchar(100),@RiskFactorUid),@OldValue,@NewValue,SYSUTCDATETIME());
 COMMIT;
 END TRY
 BEGIN CATCH
  IF @@TRANCOUNT>0 ROLLBACK;
  THROW;
 END CATCH;
 EXEC dbo.PatientRiskFactor_Get @PatientUid,@RiskFactorUid;
END;
GO
