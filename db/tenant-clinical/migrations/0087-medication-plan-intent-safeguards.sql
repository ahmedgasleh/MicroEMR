-- Independent plan intent. Legacy NULL is preserved without UPDATE/backfill.
SET XACT_ABORT ON;
GO
ALTER TABLE dbo.PatientMedication ADD LongTermIntent bit NULL;
GO
CREATE OR ALTER PROCEDURE dbo.PatientMedication_GetByPatientUid
    @PatientUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        pm.MedicationUid AS MedicationUid,
        pm.PatientUid AS PatientUid,
        pm.MedicationName AS MedicationName,
        pm.Strength AS Strength,
        pm.DosageForm AS DosageForm,
        pm.Route AS Route,
        pm.Directions AS Directions,
        pm.Frequency AS Frequency,
        pm.StartDate AS StartDate,
        pm.EndDate AS EndDate,
        pm.Indication AS Indication,
        pm.PrescriberName AS PrescriberName,
        pm.Notes AS Notes,
        pm.MedicationStatus AS MedicationStatus,
        pm.CreatedBy AS CreatedBy,
        COALESCE(pm.CreatedByDisplayName, au.DisplayName) AS CreatedByDisplayName,
        pm.CreatedAt AS CreatedAt,
        pm.UpdatedAt AS UpdatedAt,
        pm.LongTermIntent AS LongTermIntent,pm.Classification AS Classification,
        pm.RowVersion AS RowVersion
    FROM dbo.PatientMedication AS pm
    LEFT JOIN dbo.ApplicationUser AS au
        ON au.UserId = pm.CreatedBy
    WHERE pm.PatientUid = @PatientUid
    ORDER BY
        CASE WHEN pm.MedicationStatus = N'Active' THEN 0 ELSE 1 END,
        pm.MedicationName ASC,
        pm.CreatedAt DESC;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientMedication_GetByUid
    @MedicationUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        pm.MedicationUid AS MedicationUid,
        pm.PatientUid AS PatientUid,
        pm.MedicationName AS MedicationName,
        pm.Strength AS Strength,
        pm.DosageForm AS DosageForm,
        pm.Route AS Route,
        pm.Directions AS Directions,
        pm.Frequency AS Frequency,
        pm.StartDate AS StartDate,
        pm.EndDate AS EndDate,
        pm.Indication AS Indication,
        pm.PrescriberName AS PrescriberName,
        pm.Notes AS Notes,
        pm.MedicationStatus AS MedicationStatus,
        pm.CreatedBy AS CreatedBy,
        COALESCE(pm.CreatedByDisplayName, au.DisplayName) AS CreatedByDisplayName,
        pm.CreatedAt AS CreatedAt,
        pm.UpdatedAt AS UpdatedAt,
        pm.LongTermIntent AS LongTermIntent,pm.Classification AS Classification,
        pm.RowVersion AS RowVersion
    FROM dbo.PatientMedication AS pm
    LEFT JOIN dbo.ApplicationUser AS au
        ON au.UserId = pm.CreatedBy
    WHERE pm.MedicationUid = @MedicationUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientMedication_Create
    @PatientUid UNIQUEIDENTIFIER,
    @MedicationName NVARCHAR(200),
    @Strength NVARCHAR(100) = NULL,
    @DosageForm NVARCHAR(100) = NULL,
    @Route NVARCHAR(100) = NULL,
    @Directions NVARCHAR(500) = NULL,
    @Frequency NVARCHAR(100) = NULL,
    @StartDate DATE = NULL,
    @EndDate DATE = NULL,
    @Indication NVARCHAR(300) = NULL,
    @PrescriberName NVARCHAR(200) = NULL,
    @Notes NVARCHAR(1000) = NULL,
    @CreatedBy BIGINT = NULL,
    @CreatedByDisplayName NVARCHAR(200) = NULL,
    @Classification NVARCHAR(20) = NULL,@PlanFields nvarchar(max)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @PlanFields IS NOT NULL AND (ISJSON(@PlanFields)<>1 OR LEFT(LTRIM(@PlanFields),1)<>N'{') THROW 52902,'Plan fields must be an object.',1;
    IF EXISTS(SELECT 1 FROM OPENJSON(COALESCE(@PlanFields,N'{}')) WHERE [key]=N'LongTermIntent' AND [type] NOT IN(0,3)) THROW 52902,'Long-term intent must be a boolean or null.',1;
    DECLARE @LongTermIntent bit=CASE JSON_VALUE(@PlanFields,'$.LongTermIntent') WHEN N'true' THEN 1 WHEN N'false' THEN 0 END;
    DECLARE @PatientId BIGINT;
    DECLARE @NewValue nvarchar(max);
    SET @Classification=NULLIF(@Classification,N'');
    IF @Classification=N'Unclassified' SET @Classification=NULL;
    IF @Classification IS NOT NULL AND @Classification COLLATE Latin1_General_100_BIN2 NOT IN(N'Ongoing',N'Acute')
        THROW 52900,'Invalid medication classification.',1;
    IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@CreatedBy AND IsActive=1)
        THROW 52901,'Active clinical actor required.',1;
    IF NULLIF(LTRIM(RTRIM(@MedicationName)),N'') IS NULL THROW 52902,'Medication name required.',1;
    BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @MedicationUid UNIQUEIDENTIFIER = NEWID();

    SELECT @PatientId = p.PatientId
    FROM dbo.Patient AS p WITH (UPDLOCK,HOLDLOCK)
    WHERE p.PatientUid = @PatientUid
        AND p.IsDeleted = CONVERT(BIT, 0);

    IF @PatientId IS NULL
    BEGIN
        THROW 51061, 'The requested patient was not found.', 1;
    END;

    IF @EndDate IS NOT NULL
        AND @StartDate IS NOT NULL
        AND @EndDate < @StartDate
    BEGIN
        THROW 51062, 'End date cannot be before start date.', 1;
    END;

    INSERT INTO dbo.PatientMedication
    (
        MedicationUid,
        PatientUid,
        MedicationName,
        Strength,
        DosageForm,
        Route,
        Directions,
        Frequency,
        StartDate,
        EndDate,
        Indication,
        PrescriberName,
        Notes,
        Classification,LongTermIntent,
        MedicationStatus,
        CreatedBy,
        CreatedByDisplayName,
        CreatedAt
    )
    VALUES
    (
        @MedicationUid,
        @PatientUid,
        LTRIM(RTRIM(@MedicationName)),
        NULLIF(LTRIM(RTRIM(@Strength)), N''),
        NULLIF(LTRIM(RTRIM(@DosageForm)), N''),
        NULLIF(LTRIM(RTRIM(@Route)), N''),
        NULLIF(LTRIM(RTRIM(@Directions)), N''),
        NULLIF(LTRIM(RTRIM(@Frequency)), N''),
        @StartDate,
        @EndDate,
        NULLIF(LTRIM(RTRIM(@Indication)), N''),
        NULLIF(LTRIM(RTRIM(@PrescriberName)), N''),
        NULLIF(LTRIM(RTRIM(@Notes)), N''),
        @Classification,@LongTermIntent,
        N'Active',
        @CreatedBy,
        NULLIF(LTRIM(RTRIM(@CreatedByDisplayName)), N''),
        SYSUTCDATETIME()
    );

    SELECT @NewValue=(SELECT MedicationName,Strength,DosageForm,Route,Directions,Frequency,StartDate,EndDate,
        Indication,PrescriberName,Notes,MedicationStatus,Classification,LongTermIntent FROM dbo.PatientMedication WHERE MedicationUid=@MedicationUid
        FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
    INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
    VALUES(@CreatedBy,@PatientId,N'Create',N'PatientMedication',CONVERT(nvarchar(100),@MedicationUid),NULL,@NewValue,SYSUTCDATETIME());
    COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT>0 ROLLBACK;
        THROW;
    END CATCH;

    EXEC dbo.PatientMedication_GetByUid
        @MedicationUid = @MedicationUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientMedication_Update
    @PatientUid UNIQUEIDENTIFIER, @MedicationUid UNIQUEIDENTIFIER,
    @MedicationName NVARCHAR(200), @Strength NVARCHAR(100) = NULL,
    @DosageForm NVARCHAR(100) = NULL, @Route NVARCHAR(100) = NULL,
    @Directions NVARCHAR(500) = NULL, @Frequency NVARCHAR(100) = NULL,
    @StartDate DATE = NULL, @EndDate DATE = NULL,
    @Indication NVARCHAR(300) = NULL, @PrescriberName NVARCHAR(200) = NULL,
    @MedicationStatus NVARCHAR(30), @Notes NVARCHAR(1000) = NULL,
    @UpdatedBy BIGINT = NULL, @RowVersion BINARY(8), @Classification NVARCHAR(20) = NULL,@PlanFields nvarchar(max)=NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF @PlanFields IS NOT NULL AND (ISJSON(@PlanFields)<>1 OR LEFT(LTRIM(@PlanFields),1)<>N'{') THROW 52902,'Plan fields must be an object.',1;
    IF EXISTS(SELECT 1 FROM OPENJSON(COALESCE(@PlanFields,N'{}')) WHERE [key]=N'LongTermIntent' AND [type] NOT IN(0,3)) THROW 52902,'Long-term intent must be a boolean or null.',1;
    DECLARE @LongTermIntent bit=CASE JSON_VALUE(@PlanFields,'$.LongTermIntent') WHEN N'true' THEN 1 WHEN N'false' THEN 0 END;
    DECLARE @PatientId BIGINT,@OldValue nvarchar(max),@NewValue nvarchar(max);
    SET @Classification=NULLIF(@Classification,N'');
    IF @Classification=N'Unclassified' SET @Classification=NULL;
    IF @Classification IS NOT NULL AND @Classification COLLATE Latin1_General_100_BIN2 NOT IN(N'Ongoing',N'Acute')
        THROW 52900,'Invalid medication classification.',1;
    IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@UpdatedBy AND IsActive=1)
        THROW 52901,'Active clinical actor required.',1;
    IF NULLIF(LTRIM(RTRIM(@MedicationName)),N'') IS NULL THROW 52902,'Medication name required.',1;
    IF @StartDate IS NOT NULL AND @EndDate IS NOT NULL AND @EndDate < @StartDate
        THROW 51056, 'End date cannot be before start date.', 1;
    BEGIN TRY
    BEGIN TRANSACTION;
    SELECT @PatientId = p.PatientId,
        @OldValue=(SELECT pm.MedicationName,pm.Strength,pm.DosageForm,pm.Route,pm.Directions,pm.Frequency,pm.StartDate,pm.EndDate,
            pm.Indication,pm.PrescriberName,pm.Notes,pm.MedicationStatus,pm.Classification,pm.LongTermIntent
            FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)
    FROM dbo.PatientMedication AS pm WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN dbo.Patient AS p ON p.PatientUid = pm.PatientUid
    WHERE pm.PatientUid = @PatientUid AND pm.MedicationUid = @MedicationUid AND p.IsDeleted = 0;
    IF @PatientId IS NULL BEGIN ROLLBACK TRANSACTION; RETURN; END;
    UPDATE dbo.PatientMedication SET
        LongTermIntent=CASE WHEN @PlanFields IS NULL THEN LongTermIntent ELSE @LongTermIntent END,Classification = @Classification,
        MedicationName = LTRIM(RTRIM(@MedicationName)), Strength = NULLIF(LTRIM(RTRIM(@Strength)), N''),
        DosageForm = NULLIF(LTRIM(RTRIM(@DosageForm)), N''), Route = NULLIF(LTRIM(RTRIM(@Route)), N''),
        Directions = NULLIF(LTRIM(RTRIM(@Directions)), N''), Frequency = NULLIF(LTRIM(RTRIM(@Frequency)), N''),
        StartDate = @StartDate, EndDate = @EndDate, Indication = NULLIF(LTRIM(RTRIM(@Indication)), N''),
        PrescriberName = NULLIF(LTRIM(RTRIM(@PrescriberName)), N''), MedicationStatus = LTRIM(RTRIM(@MedicationStatus)),
        Notes = NULLIF(LTRIM(RTRIM(@Notes)), N''), UpdatedBy = @UpdatedBy, UpdatedAt = SYSUTCDATETIME()
    WHERE PatientUid = @PatientUid AND MedicationUid = @MedicationUid AND RowVersion = @RowVersion;
    IF @@ROWCOUNT = 0 BEGIN ROLLBACK TRANSACTION; THROW 51057, 'The medication was changed by another user.', 1; END;
    SELECT @NewValue=(SELECT MedicationName,Strength,DosageForm,Route,Directions,Frequency,StartDate,EndDate,
        Indication,PrescriberName,Notes,MedicationStatus,Classification,LongTermIntent FROM dbo.PatientMedication WHERE MedicationUid=@MedicationUid
        FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
    INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
    VALUES(@UpdatedBy,@PatientId,N'Update',N'PatientMedication',CONVERT(nvarchar(100),@MedicationUid),@OldValue,@NewValue,SYSUTCDATETIME());
    COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT>0 ROLLBACK;
        THROW;
    END CATCH;
    EXEC dbo.PatientMedication_GetByUid @MedicationUid = @MedicationUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PatientMedication_Discontinue
 @PatientUid uniqueidentifier,@MedicationUid uniqueidentifier,@DiscontinueReason nvarchar(500)=NULL,@DiscontinuedBy bigint=NULL,@RowVersion varbinary(max)
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @RowVersion IS NULL OR DATALENGTH(@RowVersion)<>8 THROW 51057,'A reviewed version is required.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId=@DiscontinuedBy AND IsActive=1) THROW 52901,'Active clinical actor required.',1;
 BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @PatientId bigint,@Status nvarchar(30),@Current binary(8),@Old nvarchar(max),@New nvarchar(max),@Start date,@End date;
 SELECT @PatientId=p.PatientId,@Status=m.MedicationStatus,@Current=m.RowVersion,@Start=m.StartDate,@End=m.EndDate
 FROM dbo.PatientMedication m WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Patient p WITH(UPDLOCK,HOLDLOCK) ON p.PatientUid=m.PatientUid AND p.IsDeleted=0
 WHERE m.PatientUid=@PatientUid AND m.MedicationUid=@MedicationUid;
 IF @PatientId IS NULL BEGIN ROLLBACK;RETURN;END;
 IF @Current<>@RowVersion THROW 51057,'The medication was changed by another user.',1;
 IF @Status<>N'Discontinued'
 BEGIN
  IF @End IS NULL AND @Start>CONVERT(date,SYSUTCDATETIME()) THROW 52902,'Review the future treatment start/end dates before discontinuing.',1;
  SELECT @Old=(SELECT * FROM dbo.PatientMedication WHERE PatientUid=@PatientUid AND MedicationUid=@MedicationUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
  UPDATE dbo.PatientMedication SET MedicationStatus=N'Discontinued',EndDate=COALESCE(EndDate,CONVERT(date,SYSUTCDATETIME())),UpdatedBy=@DiscontinuedBy,UpdatedAt=SYSUTCDATETIME()
  WHERE PatientUid=@PatientUid AND MedicationUid=@MedicationUid AND RowVersion=@RowVersion;
  IF @@ROWCOUNT<>1 THROW 51057,'The medication was changed by another user.',1;
  SELECT @New=(SELECT m.*,@DiscontinueReason DiscontinueReason FROM dbo.PatientMedication m WHERE PatientUid=@PatientUid AND MedicationUid=@MedicationUid FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER);
  INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
  VALUES(@DiscontinuedBy,@PatientId,N'Discontinue',N'PatientMedication',CONVERT(nvarchar(100),@MedicationUid),@Old,@New,SYSUTCDATETIME());
 END;
 COMMIT;
 EXEC dbo.PatientMedication_GetByUid @MedicationUid;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK;THROW;END CATCH;
END;
GO