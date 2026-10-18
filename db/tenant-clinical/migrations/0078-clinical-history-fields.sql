-- Additive only: no clinical backfill or row update.
SET XACT_ABORT ON;
GO
CREATE OR ALTER FUNCTION dbo.HistoryPartialDateValid(@Year INT,@Month INT,@Day INT)
RETURNS BIT AS
BEGIN
 IF @Year IS NULL RETURN CASE WHEN @Month IS NULL AND @Day IS NULL THEN 1 ELSE 0 END;
 IF @Year NOT BETWEEN 1 AND 9999 RETURN 0;
 IF @Month IS NULL RETURN CASE WHEN @Day IS NULL THEN 1 ELSE 0 END;
 IF @Month NOT BETWEEN 1 AND 12 RETURN 0;
 IF @Day IS NULL RETURN 1;
 IF TRY_CONVERT(DATE,CONCAT(RIGHT('0000'+CONVERT(VARCHAR(4),@Year),4),'-',RIGHT('0'+CONVERT(VARCHAR(2),@Month),2),'-',RIGHT('0'+CONVERT(VARCHAR(2),@Day),2)),23) IS NULL RETURN 0;
 RETURN 1;
END;
GO
ALTER TABLE dbo.PatientClinicalHistory ADD
 OnsetYear SMALLINT NULL,
 OnsetMonth TINYINT NULL,
 OnsetDay TINYINT NULL,
 ResolutionYear SMALLINT NULL,
 ResolutionMonth TINYINT NULL,
 ResolutionDay TINYINT NULL,
 ProcedureYear SMALLINT NULL,
 ProcedureMonth TINYINT NULL,
 ProcedureDay TINYINT NULL,
 LifeStageCode NVARCHAR(1) NULL,
 ClinicalStatus NVARCHAR(20) NULL,
 Notes NVARCHAR(1000) NULL;
GO
ALTER TABLE dbo.PatientClinicalHistory ADD CONSTRAINT CK_History_OnsetPrecision CHECK(dbo.HistoryPartialDateValid(OnsetYear,OnsetMonth,OnsetDay)=1);
GO
ALTER TABLE dbo.PatientClinicalHistory ADD CONSTRAINT CK_History_ResolutionPrecision CHECK(dbo.HistoryPartialDateValid(ResolutionYear,ResolutionMonth,ResolutionDay)=1);
GO
ALTER TABLE dbo.PatientClinicalHistory ADD CONSTRAINT CK_History_ProcedurePrecision CHECK(dbo.HistoryPartialDateValid(ProcedureYear,ProcedureMonth,ProcedureDay)=1);
GO
ALTER TABLE dbo.PatientClinicalHistory ADD
 CONSTRAINT CK_History_LifeStage CHECK(LifeStageCode IS NULL OR LifeStageCode COLLATE Latin1_General_100_BIN2 IN(N'N',N'I',N'C',N'T',N'A')),
 CONSTRAINT CK_History_ClinicalStatus CHECK(ClinicalStatus IS NULL OR ClinicalStatus COLLATE Latin1_General_100_BIN2 IN(N'Active',N'Resolved')),
 CONSTRAINT CK_History_KindFields CHECK((HistoryType=N'Medical' AND ProcedureYear IS NULL) OR (HistoryType=N'Surgical' AND OnsetYear IS NULL AND ResolutionYear IS NULL AND LifeStageCode IS NULL AND ClinicalStatus IS NULL)),
 CONSTRAINT CK_History_Resolution CHECK(ClinicalStatus IS NULL OR ClinicalStatus<>N'Active' OR ResolutionYear IS NULL);
GO
CREATE OR ALTER PROCEDURE dbo.PatientClinicalHistory_List
    @PatientUid UNIQUEIDENTIFIER,
    @Status NVARCHAR(20) = N'Active'
AS
BEGIN
    SET NOCOUNT ON;
    IF @Status NOT IN (N'Active', N'Archived', N'All') SET @Status = N'Active';
    SELECT h.HistoryUid, h.PatientUid, h.HistoryType, h.Description, h.RelevantDate,
        h.Status, h.CreatedAt, h.CreatedBy, cu.DisplayName AS CreatedByDisplayName,
        h.UpdatedAt, h.UpdatedBy, uu.DisplayName AS UpdatedByDisplayName, h.RowVersion, h.OnsetYear,h.OnsetMonth,h.OnsetDay,h.ResolutionYear,h.ResolutionMonth,h.ResolutionDay,h.ProcedureYear,h.ProcedureMonth,h.ProcedureDay,h.LifeStageCode,h.ClinicalStatus,h.Notes
    FROM dbo.PatientClinicalHistory AS h
    JOIN dbo.Patient AS p ON p.PatientUid=h.PatientUid AND p.IsDeleted=0
    LEFT JOIN dbo.ApplicationUser AS cu ON cu.UserId = h.CreatedBy
    LEFT JOIN dbo.ApplicationUser AS uu ON uu.UserId = h.UpdatedBy
    WHERE h.PatientUid = @PatientUid AND (@Status = N'All' OR h.Status = @Status)
    ORDER BY CASE h.HistoryType WHEN N'Medical' THEN 0 ELSE 1 END,
        COALESCE(h.OnsetYear,h.ProcedureYear,YEAR(h.RelevantDate)) DESC,
        COALESCE(h.OnsetMonth,h.ProcedureMonth,MONTH(h.RelevantDate)) DESC,
        COALESCE(h.OnsetDay,h.ProcedureDay,DAY(h.RelevantDate)) DESC,h.CreatedAt DESC,h.HistoryUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientClinicalHistory_Get
    @PatientUid UNIQUEIDENTIFIER,
    @HistoryUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT h.HistoryUid, h.PatientUid, h.HistoryType, h.Description, h.RelevantDate,
        h.Status, h.CreatedAt, h.CreatedBy, cu.DisplayName AS CreatedByDisplayName,
        h.UpdatedAt, h.UpdatedBy, uu.DisplayName AS UpdatedByDisplayName, h.RowVersion, h.OnsetYear,h.OnsetMonth,h.OnsetDay,h.ResolutionYear,h.ResolutionMonth,h.ResolutionDay,h.ProcedureYear,h.ProcedureMonth,h.ProcedureDay,h.LifeStageCode,h.ClinicalStatus,h.Notes
    FROM dbo.PatientClinicalHistory AS h
    JOIN dbo.Patient AS p ON p.PatientUid=h.PatientUid AND p.IsDeleted=0
    LEFT JOIN dbo.ApplicationUser AS cu ON cu.UserId = h.CreatedBy
    LEFT JOIN dbo.ApplicationUser AS uu ON uu.UserId = h.UpdatedBy
    WHERE h.PatientUid = @PatientUid AND h.HistoryUid = @HistoryUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientClinicalHistory_Create
 @PatientUid UNIQUEIDENTIFIER,@HistoryType NVARCHAR(20),@Description NVARCHAR(1000),@RelevantDate DATE=NULL,@Actor BIGINT,@ClinicalFields NVARCHAR(MAX)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
    IF @HistoryType NOT IN(N'Medical',N'Surgical') THROW 52100,'History type is invalid.',1;
    IF NULLIF(LTRIM(RTRIM(@Description)),N'') IS NULL THROW 52101,'Description is required.',1;
    IF @RelevantDate>CONVERT(DATE,SYSUTCDATETIME()) THROW 52102,'Relevant date cannot be in the future.',1;
    IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@Actor AND IsActive=1) THROW 52104,'Active clinical user was not found.',1;
    -- Single-argument ISJSON supports older SQL Server engines; separately require an object root.
    IF @ClinicalFields IS NOT NULL AND (ISJSON(@ClinicalFields)<>1 OR
        LEFT(LTRIM(REPLACE(REPLACE(REPLACE(@ClinicalFields,NCHAR(9),N' '),NCHAR(10),N' '),NCHAR(13),N' ')),1)<>N'{')
        THROW 52107,'Clinical fields must be an object.',1;
    DECLARE
    @OnsetYear INT,
    @OnsetMonth INT,
    @OnsetDay INT,
    @ResolutionYear INT,
    @ResolutionMonth INT,
    @ResolutionDay INT,
    @ProcedureYear INT,
    @ProcedureMonth INT,
    @ProcedureDay INT,
    @LifeStageCode NVARCHAR(MAX),
    @ClinicalStatus NVARCHAR(MAX),
    @Notes NVARCHAR(MAX);
    SELECT @OnsetYear=OnsetYear,@OnsetMonth=OnsetMonth,@OnsetDay=OnsetDay,@ResolutionYear=ResolutionYear,@ResolutionMonth=ResolutionMonth,@ResolutionDay=ResolutionDay,@ProcedureYear=ProcedureYear,@ProcedureMonth=ProcedureMonth,@ProcedureDay=ProcedureDay,@LifeStageCode=LifeStageCode,@ClinicalStatus=ClinicalStatus,@Notes=Notes FROM OPENJSON(COALESCE(@ClinicalFields,N'{}')) WITH(
 OnsetYear INT '$.OnsetDate.Year',
 OnsetMonth INT '$.OnsetDate.Month',
 OnsetDay INT '$.OnsetDate.Day',
 ResolutionYear INT '$.ResolutionDate.Year',
 ResolutionMonth INT '$.ResolutionDate.Month',
 ResolutionDay INT '$.ResolutionDate.Day',
 ProcedureYear INT '$.ProcedureDate.Year',
 ProcedureMonth INT '$.ProcedureDate.Month',
 ProcedureDay INT '$.ProcedureDate.Day',
 LifeStageCode NVARCHAR(MAX) '$.LifeStageCode',
 ClinicalStatus NVARCHAR(MAX) '$.ClinicalStatus',
 Notes NVARCHAR(MAX) '$.Notes');
    SET @LifeStageCode=NULLIF(LTRIM(RTRIM(@LifeStageCode)),N'');
    SET @ClinicalStatus=NULLIF(LTRIM(RTRIM(@ClinicalStatus)),N'');
    SET @Notes=NULLIF(LTRIM(RTRIM(@Notes)),N'');
    IF dbo.HistoryPartialDateValid(@OnsetYear,@OnsetMonth,@OnsetDay)=0 THROW 52107,'Invalid clinical date precision.',1;
    IF @OnsetYear IS NOT NULL AND DATEFROMPARTS(@OnsetYear,COALESCE(@OnsetMonth,1),COALESCE(@OnsetDay,1))>CONVERT(DATE,SYSUTCDATETIME()) THROW 52102,'Clinical date cannot be in the future.',1;
    IF dbo.HistoryPartialDateValid(@ResolutionYear,@ResolutionMonth,@ResolutionDay)=0 THROW 52107,'Invalid clinical date precision.',1;
    IF @ResolutionYear IS NOT NULL AND DATEFROMPARTS(@ResolutionYear,COALESCE(@ResolutionMonth,1),COALESCE(@ResolutionDay,1))>CONVERT(DATE,SYSUTCDATETIME()) THROW 52102,'Clinical date cannot be in the future.',1;
    IF dbo.HistoryPartialDateValid(@ProcedureYear,@ProcedureMonth,@ProcedureDay)=0 THROW 52107,'Invalid clinical date precision.',1;
    IF @ProcedureYear IS NOT NULL AND DATEFROMPARTS(@ProcedureYear,COALESCE(@ProcedureMonth,1),COALESCE(@ProcedureDay,1))>CONVERT(DATE,SYSUTCDATETIME()) THROW 52102,'Clinical date cannot be in the future.',1;
    IF @LifeStageCode IS NOT NULL AND @LifeStageCode COLLATE Latin1_General_100_BIN2 NOT IN(N'N',N'I',N'C',N'T',N'A') THROW 52107,'Invalid life stage.',1;
    IF @ClinicalStatus IS NOT NULL AND @ClinicalStatus COLLATE Latin1_General_100_BIN2 NOT IN(N'Active',N'Resolved') THROW 52107,'Invalid clinical status.',1;
    IF DATALENGTH(@Notes)>2000 THROW 52107,'Notes exceed 1000 characters.',1;
    IF (@HistoryType=N'Medical' AND @ProcedureYear IS NOT NULL) OR (@HistoryType=N'Surgical' AND (@OnsetYear IS NOT NULL OR @ResolutionYear IS NOT NULL OR @LifeStageCode IS NOT NULL OR @ClinicalStatus IS NOT NULL)) THROW 52107,'Clinical fields do not apply to this history type. Clear prior fields before changing type.',1;
    IF @ClinicalStatus=N'Active' AND @ResolutionYear IS NOT NULL THROW 52107,'An Active condition cannot have a resolution date.',1;
    IF @OnsetYear IS NOT NULL AND @ResolutionYear IS NOT NULL AND
       DATEFROMPARTS(@ResolutionYear,COALESCE(@ResolutionMonth,12),COALESCE(@ResolutionDay,DAY(EOMONTH(DATEFROMPARTS(@ResolutionYear,COALESCE(@ResolutionMonth,12),1)))))
       < DATEFROMPARTS(@OnsetYear,COALESCE(@OnsetMonth,1),COALESCE(@OnsetDay,1)) THROW 52107,'Resolution cannot be before onset.',1;
 BEGIN TRANSACTION;
 DECLARE @PatientId BIGINT=(SELECT PatientId FROM dbo.Patient WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND IsDeleted=0);
 IF @PatientId IS NULL THROW 52103,'Patient was not found.',1;
 DECLARE @HistoryUid UNIQUEIDENTIFIER=NEWID();
 INSERT dbo.PatientClinicalHistory(HistoryUid,PatientUid,HistoryType,Description,RelevantDate,CreatedBy,OnsetYear,OnsetMonth,OnsetDay,ResolutionYear,ResolutionMonth,ResolutionDay,ProcedureYear,ProcedureMonth,ProcedureDay,LifeStageCode,ClinicalStatus,Notes)
 VALUES(@HistoryUid,@PatientUid,@HistoryType,LTRIM(RTRIM(@Description)),@RelevantDate,@Actor,@OnsetYear,@OnsetMonth,@OnsetDay,@ResolutionYear,@ResolutionMonth,@ResolutionDay,@ProcedureYear,@ProcedureMonth,@ProcedureDay,@LifeStageCode,@ClinicalStatus,@Notes);
 DECLARE @NewValue NVARCHAR(MAX)=(SELECT h.PatientUid,h.HistoryUid,h.HistoryType,h.Description,h.RelevantDate,h.Status,h.OnsetYear,h.OnsetMonth,h.OnsetDay,h.ResolutionYear,h.ResolutionMonth,h.ResolutionDay,h.ProcedureYear,h.ProcedureMonth,h.ProcedureDay,h.LifeStageCode,h.ClinicalStatus,h.Notes FROM dbo.PatientClinicalHistory h WHERE h.HistoryUid=@HistoryUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt)
 VALUES(@Actor,@PatientId,N'Create',N'PatientClinicalHistory',CONVERT(NVARCHAR(100),@HistoryUid),@NewValue,SYSUTCDATETIME());
 COMMIT;
 EXEC dbo.PatientClinicalHistory_Get @PatientUid,@HistoryUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientClinicalHistory_Update
 @PatientUid UNIQUEIDENTIFIER,@HistoryUid UNIQUEIDENTIFIER,@HistoryType NVARCHAR(20),@Description NVARCHAR(1000),@RelevantDate DATE=NULL,@ExpectedRowVersion BINARY(8),@Actor BIGINT,@ClinicalFields NVARCHAR(MAX)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
    IF @HistoryType NOT IN(N'Medical',N'Surgical') THROW 52100,'History type is invalid.',1;
    IF NULLIF(LTRIM(RTRIM(@Description)),N'') IS NULL THROW 52101,'Description is required.',1;
    IF @RelevantDate>CONVERT(DATE,SYSUTCDATETIME()) THROW 52102,'Relevant date cannot be in the future.',1;
    IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@Actor AND IsActive=1) THROW 52104,'Active clinical user was not found.',1;
 BEGIN TRANSACTION;
 DECLARE @PatientId BIGINT,@CurrentVersion BINARY(8),@Status NVARCHAR(20),@OldValue NVARCHAR(MAX),@PriorFields NVARCHAR(MAX),@OldType NVARCHAR(20);
 SELECT @PatientId=p.PatientId,@CurrentVersion=h.RowVersion,@Status=h.Status,@OldType=h.HistoryType,
 @OldValue=(SELECT h.PatientUid,h.HistoryUid,h.HistoryType,h.Description,h.RelevantDate,h.Status,h.OnsetYear,h.OnsetMonth,h.OnsetDay,h.ResolutionYear,h.ResolutionMonth,h.ResolutionDay,h.ProcedureYear,h.ProcedureMonth,h.ProcedureDay,h.LifeStageCode,h.ClinicalStatus,h.Notes FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER),
 @PriorFields=(SELECT h.OnsetYear AS [OnsetDate.Year],h.OnsetMonth AS [OnsetDate.Month],h.OnsetDay AS [OnsetDate.Day],h.ResolutionYear AS [ResolutionDate.Year],h.ResolutionMonth AS [ResolutionDate.Month],h.ResolutionDay AS [ResolutionDate.Day],h.ProcedureYear AS [ProcedureDate.Year],h.ProcedureMonth AS [ProcedureDate.Month],h.ProcedureDay AS [ProcedureDate.Day],h.LifeStageCode,h.ClinicalStatus,h.Notes FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)
 FROM dbo.PatientClinicalHistory h WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Patient p ON p.PatientUid=h.PatientUid AND p.IsDeleted=0
 WHERE h.PatientUid=@PatientUid AND h.HistoryUid=@HistoryUid;
 IF @PatientId IS NULL BEGIN ROLLBACK; RETURN; END;
 IF @ExpectedRowVersion IS NULL OR @ExpectedRowVersion<>@CurrentVersion THROW 52105,'History was changed by another user.',1;
 IF @Status<>N'Active' THROW 52106,'Archived history cannot be edited.',1;
 IF @OldType<>@HistoryType AND EXISTS(SELECT 1 FROM dbo.PatientClinicalHistory WHERE HistoryUid=@HistoryUid AND
 (OnsetYear IS NOT NULL OR ResolutionYear IS NOT NULL OR ProcedureYear IS NOT NULL OR LifeStageCode IS NOT NULL OR ClinicalStatus IS NOT NULL))
 THROW 52107,'Clear and save prior clinical fields before changing history type.',1;
 IF @ClinicalFields IS NULL SET @ClinicalFields=@PriorFields;
    -- Single-argument ISJSON supports older SQL Server engines; separately require an object root.
    IF @ClinicalFields IS NOT NULL AND (ISJSON(@ClinicalFields)<>1 OR
        LEFT(LTRIM(REPLACE(REPLACE(REPLACE(@ClinicalFields,NCHAR(9),N' '),NCHAR(10),N' '),NCHAR(13),N' ')),1)<>N'{')
        THROW 52107,'Clinical fields must be an object.',1;
    DECLARE
    @OnsetYear INT,
    @OnsetMonth INT,
    @OnsetDay INT,
    @ResolutionYear INT,
    @ResolutionMonth INT,
    @ResolutionDay INT,
    @ProcedureYear INT,
    @ProcedureMonth INT,
    @ProcedureDay INT,
    @LifeStageCode NVARCHAR(MAX),
    @ClinicalStatus NVARCHAR(MAX),
    @Notes NVARCHAR(MAX);
    SELECT @OnsetYear=OnsetYear,@OnsetMonth=OnsetMonth,@OnsetDay=OnsetDay,@ResolutionYear=ResolutionYear,@ResolutionMonth=ResolutionMonth,@ResolutionDay=ResolutionDay,@ProcedureYear=ProcedureYear,@ProcedureMonth=ProcedureMonth,@ProcedureDay=ProcedureDay,@LifeStageCode=LifeStageCode,@ClinicalStatus=ClinicalStatus,@Notes=Notes FROM OPENJSON(COALESCE(@ClinicalFields,N'{}')) WITH(
 OnsetYear INT '$.OnsetDate.Year',
 OnsetMonth INT '$.OnsetDate.Month',
 OnsetDay INT '$.OnsetDate.Day',
 ResolutionYear INT '$.ResolutionDate.Year',
 ResolutionMonth INT '$.ResolutionDate.Month',
 ResolutionDay INT '$.ResolutionDate.Day',
 ProcedureYear INT '$.ProcedureDate.Year',
 ProcedureMonth INT '$.ProcedureDate.Month',
 ProcedureDay INT '$.ProcedureDate.Day',
 LifeStageCode NVARCHAR(MAX) '$.LifeStageCode',
 ClinicalStatus NVARCHAR(MAX) '$.ClinicalStatus',
 Notes NVARCHAR(MAX) '$.Notes');
    SET @LifeStageCode=NULLIF(LTRIM(RTRIM(@LifeStageCode)),N'');
    SET @ClinicalStatus=NULLIF(LTRIM(RTRIM(@ClinicalStatus)),N'');
    SET @Notes=NULLIF(LTRIM(RTRIM(@Notes)),N'');
    IF dbo.HistoryPartialDateValid(@OnsetYear,@OnsetMonth,@OnsetDay)=0 THROW 52107,'Invalid clinical date precision.',1;
    IF @OnsetYear IS NOT NULL AND DATEFROMPARTS(@OnsetYear,COALESCE(@OnsetMonth,1),COALESCE(@OnsetDay,1))>CONVERT(DATE,SYSUTCDATETIME()) THROW 52102,'Clinical date cannot be in the future.',1;
    IF dbo.HistoryPartialDateValid(@ResolutionYear,@ResolutionMonth,@ResolutionDay)=0 THROW 52107,'Invalid clinical date precision.',1;
    IF @ResolutionYear IS NOT NULL AND DATEFROMPARTS(@ResolutionYear,COALESCE(@ResolutionMonth,1),COALESCE(@ResolutionDay,1))>CONVERT(DATE,SYSUTCDATETIME()) THROW 52102,'Clinical date cannot be in the future.',1;
    IF dbo.HistoryPartialDateValid(@ProcedureYear,@ProcedureMonth,@ProcedureDay)=0 THROW 52107,'Invalid clinical date precision.',1;
    IF @ProcedureYear IS NOT NULL AND DATEFROMPARTS(@ProcedureYear,COALESCE(@ProcedureMonth,1),COALESCE(@ProcedureDay,1))>CONVERT(DATE,SYSUTCDATETIME()) THROW 52102,'Clinical date cannot be in the future.',1;
    IF @LifeStageCode IS NOT NULL AND @LifeStageCode COLLATE Latin1_General_100_BIN2 NOT IN(N'N',N'I',N'C',N'T',N'A') THROW 52107,'Invalid life stage.',1;
    IF @ClinicalStatus IS NOT NULL AND @ClinicalStatus COLLATE Latin1_General_100_BIN2 NOT IN(N'Active',N'Resolved') THROW 52107,'Invalid clinical status.',1;
    IF DATALENGTH(@Notes)>2000 THROW 52107,'Notes exceed 1000 characters.',1;
    IF (@HistoryType=N'Medical' AND @ProcedureYear IS NOT NULL) OR (@HistoryType=N'Surgical' AND (@OnsetYear IS NOT NULL OR @ResolutionYear IS NOT NULL OR @LifeStageCode IS NOT NULL OR @ClinicalStatus IS NOT NULL)) THROW 52107,'Clinical fields do not apply to this history type. Clear prior fields before changing type.',1;
    IF @ClinicalStatus=N'Active' AND @ResolutionYear IS NOT NULL THROW 52107,'An Active condition cannot have a resolution date.',1;
    IF @OnsetYear IS NOT NULL AND @ResolutionYear IS NOT NULL AND
       DATEFROMPARTS(@ResolutionYear,COALESCE(@ResolutionMonth,12),COALESCE(@ResolutionDay,DAY(EOMONTH(DATEFROMPARTS(@ResolutionYear,COALESCE(@ResolutionMonth,12),1)))))
       < DATEFROMPARTS(@OnsetYear,COALESCE(@OnsetMonth,1),COALESCE(@OnsetDay,1)) THROW 52107,'Resolution cannot be before onset.',1;
 UPDATE dbo.PatientClinicalHistory SET HistoryType=@HistoryType,Description=LTRIM(RTRIM(@Description)),RelevantDate=@RelevantDate,
 OnsetYear=@OnsetYear,OnsetMonth=@OnsetMonth,OnsetDay=@OnsetDay,ResolutionYear=@ResolutionYear,ResolutionMonth=@ResolutionMonth,ResolutionDay=@ResolutionDay,ProcedureYear=@ProcedureYear,ProcedureMonth=@ProcedureMonth,ProcedureDay=@ProcedureDay,LifeStageCode=@LifeStageCode,ClinicalStatus=@ClinicalStatus,Notes=@Notes,UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@Actor
 WHERE PatientUid=@PatientUid AND HistoryUid=@HistoryUid AND RowVersion=@ExpectedRowVersion;
 DECLARE @NewValue NVARCHAR(MAX)=(SELECT h.PatientUid,h.HistoryUid,h.HistoryType,h.Description,h.RelevantDate,h.Status,h.OnsetYear,h.OnsetMonth,h.OnsetDay,h.ResolutionYear,h.ResolutionMonth,h.ResolutionDay,h.ProcedureYear,h.ProcedureMonth,h.ProcedureDay,h.LifeStageCode,h.ClinicalStatus,h.Notes FROM dbo.PatientClinicalHistory h WHERE h.HistoryUid=@HistoryUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
 INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
 VALUES(@Actor,@PatientId,N'Update',N'PatientClinicalHistory',CONVERT(NVARCHAR(100),@HistoryUid),@OldValue,@NewValue,SYSUTCDATETIME());
 COMMIT;
 EXEC dbo.PatientClinicalHistory_Get @PatientUid,@HistoryUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientClinicalHistory_Archive
    @PatientUid UNIQUEIDENTIFIER,
    @HistoryUid UNIQUEIDENTIFIER,
    @ExpectedRowVersion BINARY(8),
    @Actor BIGINT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@Actor AND IsActive=1) THROW 52104,'Active clinical user was not found.',1;
    DECLARE @OldValue NVARCHAR(MAX), @NewValue NVARCHAR(MAX), @PatientId BIGINT, @CurrentVersion BINARY(8), @Status NVARCHAR(20);
    BEGIN TRANSACTION;
    SELECT @PatientId = p.PatientId, @CurrentVersion = h.RowVersion, @Status = h.Status, @OldValue=(SELECT h.PatientUid,h.HistoryUid,h.HistoryType,h.Description,h.RelevantDate,h.Status,h.OnsetYear,h.OnsetMonth,h.OnsetDay,h.ResolutionYear,h.ResolutionMonth,h.ResolutionDay,h.ProcedureYear,h.ProcedureMonth,h.ProcedureDay,h.LifeStageCode,h.ClinicalStatus,h.Notes FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)
    FROM dbo.PatientClinicalHistory AS h WITH (UPDLOCK, HOLDLOCK)
    JOIN dbo.Patient AS p ON p.PatientUid = h.PatientUid AND p.IsDeleted = 0
    WHERE h.PatientUid = @PatientUid AND h.HistoryUid = @HistoryUid;
    IF @PatientId IS NULL BEGIN ROLLBACK; RETURN; END;
    IF @ExpectedRowVersion IS NULL OR @CurrentVersion <> @ExpectedRowVersion BEGIN ROLLBACK; THROW 52105, 'History was changed by another user.', 1; END;
    IF @Status <> N'Active' BEGIN ROLLBACK; THROW 52106, 'History is already archived.', 1; END;
    UPDATE dbo.PatientClinicalHistory SET Status = N'Archived', UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @Actor
    WHERE PatientUid = @PatientUid AND HistoryUid = @HistoryUid AND RowVersion = @ExpectedRowVersion;
    SET @NewValue=(SELECT h.PatientUid,h.HistoryUid,h.HistoryType,h.Description,h.RelevantDate,h.Status,h.OnsetYear,h.OnsetMonth,h.OnsetDay,h.ResolutionYear,h.ResolutionMonth,h.ResolutionDay,h.ProcedureYear,h.ProcedureMonth,h.ProcedureDay,h.LifeStageCode,h.ClinicalStatus,h.Notes FROM dbo.PatientClinicalHistory h WHERE h.HistoryUid=@HistoryUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
    INSERT dbo.AuditLog(UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
    VALUES(@Actor, @PatientId, N'Archive', N'PatientClinicalHistory', CONVERT(NVARCHAR(100), @HistoryUid), @OldValue, @NewValue, SYSUTCDATETIME());
    COMMIT;
    EXEC dbo.PatientClinicalHistory_Get @PatientUid, @HistoryUid;
END;
GO
