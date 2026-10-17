SET XACT_ABORT ON;
GO
CREATE TABLE dbo.PatientFamilyMedicalHistory
(
 FamilyHistoryId bigint IDENTITY PRIMARY KEY,
 FamilyHistoryUid uniqueidentifier NOT NULL DEFAULT NEWSEQUENTIALID(),
 PatientUid uniqueidentifier NOT NULL REFERENCES dbo.Patient(PatientUid),
 Relationship nvarchar(100) NOT NULL CHECK (LEN(LTRIM(RTRIM(Relationship))) > 0),
 ConditionDescription nvarchar(1000) NOT NULL CHECK (LEN(LTRIM(RTRIM(ConditionDescription))) > 0),
 AgeAtOnset nvarchar(100) NULL,
 LifeStageCode nvarchar(1) NULL CHECK (LifeStageCode COLLATE Latin1_General_100_BIN2 IN (N'N',N'I',N'C',N'T',N'A')),
 Treatment nvarchar(1000) NULL, Notes nvarchar(1000) NULL,
 Status nvarchar(20) NOT NULL DEFAULT N'Active' CHECK (Status IN (N'Active',N'Archived')),
 CreatedAt datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 CreatedBy bigint NOT NULL REFERENCES dbo.ApplicationUser(UserId),
 UpdatedAt datetime2(0) NULL, UpdatedBy bigint NULL REFERENCES dbo.ApplicationUser(UserId),
 RowVersion rowversion NOT NULL,
 CONSTRAINT UQ_PatientFamilyMedicalHistory_Uid UNIQUE (FamilyHistoryUid)
);
CREATE INDEX IX_PatientFamilyMedicalHistory_Patient_Status
 ON dbo.PatientFamilyMedicalHistory(PatientUid,Status,CreatedAt DESC);
GO
CREATE OR ALTER PROCEDURE dbo.PatientFamilyMedicalHistory_Get
 @PatientUid uniqueidentifier, @FamilyHistoryUid uniqueidentifier
AS
BEGIN
 SET NOCOUNT ON;
 IF NOT EXISTS(SELECT 1 FROM dbo.Patient WHERE PatientUid=@PatientUid AND IsDeleted=0)
  THROW 52700,'Patient not found.',1;
 SELECT h.*,cu.DisplayName CreatedByDisplayName,uu.DisplayName UpdatedByDisplayName
 FROM dbo.PatientFamilyMedicalHistory h
 LEFT JOIN dbo.ApplicationUser cu ON cu.UserId=h.CreatedBy
 LEFT JOIN dbo.ApplicationUser uu ON uu.UserId=h.UpdatedBy
 WHERE h.PatientUid=@PatientUid AND h.FamilyHistoryUid=@FamilyHistoryUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientFamilyMedicalHistory_List
 @PatientUid uniqueidentifier, @Status nvarchar(20)=N'Active'
AS
BEGIN
 SET NOCOUNT ON;
 IF NOT EXISTS(SELECT 1 FROM dbo.Patient WHERE PatientUid=@PatientUid AND IsDeleted=0)
  THROW 52700,'Patient not found.',1;
 IF @Status NOT IN(N'Active',N'Archived',N'All') SET @Status=N'Active';
 SELECT h.*,cu.DisplayName CreatedByDisplayName,uu.DisplayName UpdatedByDisplayName
 FROM dbo.PatientFamilyMedicalHistory h
 LEFT JOIN dbo.ApplicationUser cu ON cu.UserId=h.CreatedBy
 LEFT JOIN dbo.ApplicationUser uu ON uu.UserId=h.UpdatedBy
 WHERE h.PatientUid=@PatientUid AND (@Status=N'All' OR h.Status=@Status)
 ORDER BY h.CreatedAt DESC,h.FamilyHistoryUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientFamilyMedicalHistory_Create
 @PatientUid uniqueidentifier,
 @Relationship nvarchar(100), @ConditionDescription nvarchar(1000),
 @AgeAtOnset nvarchar(100)=NULL, @LifeStageCode nvarchar(10)=NULL,
 @Treatment nvarchar(1000)=NULL, @Notes nvarchar(1000)=NULL,
 @Actor bigint
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 SET @Relationship=LTRIM(RTRIM(@Relationship)); SET @ConditionDescription=LTRIM(RTRIM(@ConditionDescription));
 SET @AgeAtOnset=NULLIF(LTRIM(RTRIM(@AgeAtOnset)),N''); SET @LifeStageCode=NULLIF(LTRIM(RTRIM(@LifeStageCode)),N'');
 SET @Treatment=NULLIF(LTRIM(RTRIM(@Treatment)),N''); SET @Notes=NULLIF(LTRIM(RTRIM(@Notes)),N'');
 IF NULLIF(@Relationship,N'') IS NULL OR NULLIF(@ConditionDescription,N'') IS NULL
    OR (@LifeStageCode IS NOT NULL AND (LEN(@LifeStageCode)<>1 OR @LifeStageCode COLLATE Latin1_General_100_BIN2 NOT IN(N'N',N'I',N'C',N'T',N'A')))
  THROW 52704,'Invalid family history fields.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@Actor AND IsActive=1)
  THROW 52703,'Active clinical actor required.',1;
 DECLARE @PatientId bigint,@OldValue nvarchar(max),@NewValue nvarchar(max);
 BEGIN TRY
 BEGIN TRANSACTION;
 SELECT @PatientId=PatientId FROM dbo.Patient WITH (UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND IsDeleted=0;
 IF @PatientId IS NULL THROW 52700,'Patient not found.',1;
 DECLARE @FamilyHistoryUid uniqueidentifier=NEWID();
 INSERT dbo.PatientFamilyMedicalHistory(FamilyHistoryUid,PatientUid,Relationship,ConditionDescription,AgeAtOnset,LifeStageCode,Treatment,Notes,CreatedBy)
 VALUES(@FamilyHistoryUid,@PatientUid,@Relationship,@ConditionDescription,@AgeAtOnset,@LifeStageCode,@Treatment,@Notes,@Actor);
 SELECT @NewValue=(SELECT h.Relationship,h.ConditionDescription,h.AgeAtOnset,h.LifeStageCode,h.Treatment,h.Notes,h.Status FROM dbo.PatientFamilyMedicalHistory h
  WHERE h.PatientUid=@PatientUid AND h.FamilyHistoryUid=@FamilyHistoryUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
 VALUES(@Actor,@PatientId,N'Create',N'PatientFamilyMedicalHistory',CONVERT(nvarchar(100),@FamilyHistoryUid),@OldValue,@NewValue,SYSUTCDATETIME());
 COMMIT;
 END TRY
 BEGIN CATCH
  IF @@TRANCOUNT>0 ROLLBACK;
  THROW;
 END CATCH;
 EXEC dbo.PatientFamilyMedicalHistory_Get @PatientUid,@FamilyHistoryUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientFamilyMedicalHistory_Update
 @PatientUid uniqueidentifier,
 @FamilyHistoryUid uniqueidentifier,
 @Relationship nvarchar(100), @ConditionDescription nvarchar(1000),
 @AgeAtOnset nvarchar(100)=NULL, @LifeStageCode nvarchar(10)=NULL,
 @Treatment nvarchar(1000)=NULL, @Notes nvarchar(1000)=NULL,
 @ExpectedRowVersion binary(8),
 @Actor bigint
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 SET @Relationship=LTRIM(RTRIM(@Relationship)); SET @ConditionDescription=LTRIM(RTRIM(@ConditionDescription));
 SET @AgeAtOnset=NULLIF(LTRIM(RTRIM(@AgeAtOnset)),N''); SET @LifeStageCode=NULLIF(LTRIM(RTRIM(@LifeStageCode)),N'');
 SET @Treatment=NULLIF(LTRIM(RTRIM(@Treatment)),N''); SET @Notes=NULLIF(LTRIM(RTRIM(@Notes)),N'');
 IF NULLIF(@Relationship,N'') IS NULL OR NULLIF(@ConditionDescription,N'') IS NULL
    OR (@LifeStageCode IS NOT NULL AND (LEN(@LifeStageCode)<>1 OR @LifeStageCode COLLATE Latin1_General_100_BIN2 NOT IN(N'N',N'I',N'C',N'T',N'A')))
  THROW 52704,'Invalid family history fields.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@Actor AND IsActive=1)
  THROW 52703,'Active clinical actor required.',1;
 DECLARE @PatientId bigint,@OldValue nvarchar(max),@NewValue nvarchar(max);
 BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @CurrentVersion binary(8),@Status nvarchar(20);
 SELECT @PatientId=p.PatientId,@CurrentVersion=h.RowVersion,@Status=h.Status,
  @OldValue=(SELECT h.Relationship,h.ConditionDescription,h.AgeAtOnset,h.LifeStageCode,h.Treatment,h.Notes,h.Status FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)
 FROM dbo.PatientFamilyMedicalHistory h WITH (UPDLOCK,HOLDLOCK)
 JOIN dbo.Patient p ON p.PatientUid=h.PatientUid AND p.IsDeleted=0
 WHERE h.PatientUid=@PatientUid AND h.FamilyHistoryUid=@FamilyHistoryUid;
 IF @PatientId IS NULL BEGIN ROLLBACK; RETURN; END;
 IF @ExpectedRowVersion IS NULL OR @CurrentVersion<>@ExpectedRowVersion THROW 52701,'Family history changed. Reload before saving.',1;
 IF @Status<>N'Active' THROW 52702,'Archived family history cannot be changed.',1;
 UPDATE dbo.PatientFamilyMedicalHistory SET Relationship=@Relationship,ConditionDescription=@ConditionDescription,AgeAtOnset=@AgeAtOnset,LifeStageCode=@LifeStageCode,Treatment=@Treatment,Notes=@Notes,UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@Actor
 WHERE PatientUid=@PatientUid AND FamilyHistoryUid=@FamilyHistoryUid AND RowVersion=@ExpectedRowVersion;
 IF @@ROWCOUNT<>1 THROW 52701,'Family history changed. Reload before saving.',1;
 SELECT @NewValue=(SELECT h.Relationship,h.ConditionDescription,h.AgeAtOnset,h.LifeStageCode,h.Treatment,h.Notes,h.Status FROM dbo.PatientFamilyMedicalHistory h
  WHERE h.PatientUid=@PatientUid AND h.FamilyHistoryUid=@FamilyHistoryUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
 VALUES(@Actor,@PatientId,N'Update',N'PatientFamilyMedicalHistory',CONVERT(nvarchar(100),@FamilyHistoryUid),@OldValue,@NewValue,SYSUTCDATETIME());
 COMMIT;
 END TRY
 BEGIN CATCH
  IF @@TRANCOUNT>0 ROLLBACK;
  THROW;
 END CATCH;
 EXEC dbo.PatientFamilyMedicalHistory_Get @PatientUid,@FamilyHistoryUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientFamilyMedicalHistory_Archive
 @PatientUid uniqueidentifier,
 @FamilyHistoryUid uniqueidentifier,
 @ExpectedRowVersion binary(8),
 @Actor bigint
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@Actor AND IsActive=1)
  THROW 52703,'Active clinical actor required.',1;
 DECLARE @PatientId bigint,@OldValue nvarchar(max),@NewValue nvarchar(max);
 BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @CurrentVersion binary(8),@Status nvarchar(20);
 SELECT @PatientId=p.PatientId,@CurrentVersion=h.RowVersion,@Status=h.Status,
  @OldValue=(SELECT h.Relationship,h.ConditionDescription,h.AgeAtOnset,h.LifeStageCode,h.Treatment,h.Notes,h.Status FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)
 FROM dbo.PatientFamilyMedicalHistory h WITH (UPDLOCK,HOLDLOCK)
 JOIN dbo.Patient p ON p.PatientUid=h.PatientUid AND p.IsDeleted=0
 WHERE h.PatientUid=@PatientUid AND h.FamilyHistoryUid=@FamilyHistoryUid;
 IF @PatientId IS NULL BEGIN ROLLBACK; RETURN; END;
 IF @ExpectedRowVersion IS NULL OR @CurrentVersion<>@ExpectedRowVersion THROW 52701,'Family history changed. Reload before saving.',1;
 IF @Status<>N'Active' THROW 52702,'Archived family history cannot be changed.',1;
 UPDATE dbo.PatientFamilyMedicalHistory SET Status=N'Archived',UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@Actor
 WHERE PatientUid=@PatientUid AND FamilyHistoryUid=@FamilyHistoryUid AND RowVersion=@ExpectedRowVersion;
 IF @@ROWCOUNT<>1 THROW 52701,'Family history changed. Reload before saving.',1;
 SELECT @NewValue=(SELECT h.Relationship,h.ConditionDescription,h.AgeAtOnset,h.LifeStageCode,h.Treatment,h.Notes,h.Status FROM dbo.PatientFamilyMedicalHistory h
  WHERE h.PatientUid=@PatientUid AND h.FamilyHistoryUid=@FamilyHistoryUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
 VALUES(@Actor,@PatientId,N'Archive',N'PatientFamilyMedicalHistory',CONVERT(nvarchar(100),@FamilyHistoryUid),@OldValue,@NewValue,SYSUTCDATETIME());
 COMMIT;
 END TRY
 BEGIN CATCH
  IF @@TRANCOUNT>0 ROLLBACK;
  THROW;
 END CATCH;
 EXEC dbo.PatientFamilyMedicalHistory_Get @PatientUid,@FamilyHistoryUid;
END;
GO
