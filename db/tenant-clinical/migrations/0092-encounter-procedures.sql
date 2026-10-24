-- Forward-only Procedure kind; existing Medical/Surgical rows and versions remain unchanged.
SET XACT_ABORT ON;
GO
ALTER TABLE dbo.PatientClinicalHistory DROP CONSTRAINT CK_PatientClinicalHistory_Type;
ALTER TABLE dbo.PatientClinicalHistory WITH CHECK ADD CONSTRAINT CK_PatientClinicalHistory_Type
 CHECK(HistoryType IN(N'Medical',N'Surgical',N'Procedure'));
ALTER TABLE dbo.PatientClinicalHistory DROP CONSTRAINT CK_History_KindFields;
ALTER TABLE dbo.PatientClinicalHistory WITH CHECK ADD CONSTRAINT CK_History_KindFields CHECK(
 (HistoryType=N'Medical' AND ProcedureYear IS NULL) OR
 (HistoryType IN(N'Surgical',N'Procedure') AND OnsetYear IS NULL AND ResolutionYear IS NULL AND LifeStageCode IS NULL AND ClinicalStatus IS NULL));
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
 @PatientUid UNIQUEIDENTIFIER,@HistoryType NVARCHAR(20),@Description NVARCHAR(1000),@RelevantDate DATE=NULL,@Actor BIGINT,@ClinicalFields NVARCHAR(MAX)=NULL,@HistoryUid UNIQUEIDENTIFIER=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
    IF @HistoryType NOT IN(N'Medical',N'Surgical',N'Procedure') THROW 52100,'History type is invalid.',1;
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
    IF (@HistoryType=N'Medical' AND @ProcedureYear IS NOT NULL) OR (@HistoryType IN(N'Surgical',N'Procedure') AND (@OnsetYear IS NOT NULL OR @ResolutionYear IS NOT NULL OR @LifeStageCode IS NOT NULL OR @ClinicalStatus IS NOT NULL)) THROW 52107,'Clinical fields do not apply to this history type. Clear prior fields before changing type.',1;
    IF @ClinicalStatus=N'Active' AND @ResolutionYear IS NOT NULL THROW 52107,'An Active condition cannot have a resolution date.',1;
    IF @OnsetYear IS NOT NULL AND @ResolutionYear IS NOT NULL AND
       DATEFROMPARTS(@ResolutionYear,COALESCE(@ResolutionMonth,12),COALESCE(@ResolutionDay,DAY(EOMONTH(DATEFROMPARTS(@ResolutionYear,COALESCE(@ResolutionMonth,12),1)))))
       < DATEFROMPARTS(@OnsetYear,COALESCE(@OnsetMonth,1),COALESCE(@OnsetDay,1)) THROW 52107,'Resolution cannot be before onset.',1;
 BEGIN TRANSACTION;
 DECLARE @PatientId BIGINT=(SELECT PatientId FROM dbo.Patient WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND IsDeleted=0);
 IF @PatientId IS NULL THROW 52103,'Patient was not found.',1;
 SET @HistoryUid=COALESCE(@HistoryUid,NEWID());
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
    IF @HistoryType NOT IN(N'Medical',N'Surgical',N'Procedure') THROW 52100,'History type is invalid.',1;
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
    IF (@HistoryType=N'Medical' AND @ProcedureYear IS NOT NULL) OR (@HistoryType IN(N'Surgical',N'Procedure') AND (@OnsetYear IS NOT NULL OR @ResolutionYear IS NOT NULL OR @LifeStageCode IS NOT NULL OR @ClinicalStatus IS NOT NULL)) THROW 52107,'Clinical fields do not apply to this history type. Clear prior fields before changing type.',1;
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

-- Atomic encounter context + existing longitudinal History mutation + provenance.
CREATE OR ALTER PROCEDURE dbo.PatientEncounterProcedure_Save
 @PatientUid uniqueidentifier,@EncounterUid uniqueidentifier,@ExpectedEncounterRowVersion binary(8),
 @HistoryUid uniqueidentifier,@IsNew bit,@ExpectedHistoryRowVersion varbinary(max)=NULL,
 @Description nvarchar(1000),@ClinicalFields nvarchar(max)=NULL,@Actor bigint
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @PatientId bigint,@State nvarchar(30),@EncounterVersion binary(8),@Kind nvarchar(20),
 @SourcePatient uniqueidentifier,@SourceState nvarchar(20),@SourceVersion binary(8),@RelevantDate date;
 SELECT @PatientId=PatientId FROM dbo.Patient WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@PatientUid AND IsDeleted=0;
 IF @PatientId IS NULL BEGIN ROLLBACK; RETURN; END;
 SELECT @State=EncounterStatus,@EncounterVersion=RowVersion FROM dbo.PatientEncounter WITH(UPDLOCK,HOLDLOCK)
 WHERE PatientUid=@PatientUid AND EncounterUid=@EncounterUid;
 IF @State IS NULL BEGIN ROLLBACK; RETURN; END;
 IF @State<>N'Open' THROW 53101,'Procedures can only be managed from an open encounter.',1;
 IF @ExpectedEncounterRowVersion IS NULL OR @EncounterVersion<>@ExpectedEncounterRowVersion THROW 53102,'The encounter changed. Reload before saving.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@Actor AND IsActive=1) THROW 52104,'Active clinical user was not found.',1;
 IF @HistoryUid IS NULL THROW 53102,'A stable procedure identifier is required.',1;
 DECLARE @CreateDescription nvarchar(500)=N'Procedure '+CONVERT(nvarchar(36),@HistoryUid)+N' '+
   CONVERT(nvarchar(64),HASHBYTES('SHA2_256',CONCAT(@Description,N'|',COALESCE(@ClinicalFields,N''))),2);
 SELECT @SourcePatient=PatientUid,@Kind=HistoryType,@SourceState=Status,@SourceVersion=RowVersion,@RelevantDate=RelevantDate
 FROM dbo.PatientClinicalHistory WITH(UPDLOCK,HOLDLOCK) WHERE HistoryUid=@HistoryUid;
 IF @IsNew=1
 BEGIN
  IF @SourcePatient IS NOT NULL
  BEGIN
   -- A replay of the same actor/context/payload returns the committed identity, never a second record.
   IF @SourcePatient<>@PatientUid OR @Kind<>N'Procedure' OR @SourceState<>N'Active' OR NOT EXISTS(
     SELECT 1 FROM dbo.PatientEncounterHistory WHERE EncounterUid=@EncounterUid AND PatientUid=@PatientUid
       AND CreatedBy=@Actor AND ActionType=N'CppProcedureCreated' AND ActionDescription=@CreateDescription)
     THROW 53102,'That procedure identifier has already been used. Reload before saving.',1;
   COMMIT;
   EXEC dbo.PatientClinicalHistory_Get @PatientUid,@HistoryUid;
   RETURN;
  END;
  EXEC dbo.PatientClinicalHistory_Create @PatientUid=@PatientUid,@HistoryType=N'Procedure',@Description=@Description,
    @Actor=@Actor,@ClinicalFields=@ClinicalFields,@HistoryUid=@HistoryUid;
  EXEC dbo.PatientEncounterHistory_Create @EncounterUid,@PatientUid,N'CppProcedureCreated',@CreateDescription,N'Open',N'Open',NULL,@Actor,0;
 END
 ELSE
 BEGIN
  IF @SourcePatient IS NULL OR @SourcePatient<>@PatientUid BEGIN ROLLBACK; RETURN; END;
  IF @Kind NOT IN(N'Procedure',N'Surgical') THROW 53102,'Select an existing procedure or surgical history record.',1;
  IF @SourceState<>N'Active' THROW 52106,'History is archived.',1;
  IF @ExpectedHistoryRowVersion IS NULL OR DATALENGTH(@ExpectedHistoryRowVersion)<>8 OR @SourceVersion<>@ExpectedHistoryRowVersion
    THROW 52105,'History was changed by another user.',1;
  EXEC dbo.PatientClinicalHistory_Update @PatientUid=@PatientUid,@HistoryUid=@HistoryUid,@HistoryType=@Kind,
    @Description=@Description,@RelevantDate=@RelevantDate,@ExpectedRowVersion=@ExpectedHistoryRowVersion,@Actor=@Actor,@ClinicalFields=@ClinicalFields;
  DECLARE @UpdateDescription nvarchar(500)=N'Procedure '+CONVERT(nvarchar(36),@HistoryUid);
  EXEC dbo.PatientEncounterHistory_Create @EncounterUid,@PatientUid,N'CppProcedureUpdated',@UpdateDescription,N'Open',N'Open',NULL,@Actor,0;
 END;
 COMMIT;
 END TRY
 BEGIN CATCH
  IF @@TRANCOUNT>0 ROLLBACK;
  THROW;
 END CATCH;
END;
GO
