-- Medication-list classification only. NULL means unknown; deliberately no clinical data backfill.
-- Apply through the tenant migration runner, never against the platform database.
SET XACT_ABORT ON;
GO
ALTER TABLE dbo.PatientMedication ADD Classification nvarchar(20) NULL
 CONSTRAINT CK_PatientMedication_Classification CHECK (Classification COLLATE Latin1_General_100_BIN2 IN(N'Ongoing',N'Acute'));
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
        pm.Classification AS Classification,
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
        pm.Classification AS Classification,
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
    @Classification NVARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

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
        Classification,
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
        @Classification,
        N'Active',
        @CreatedBy,
        NULLIF(LTRIM(RTRIM(@CreatedByDisplayName)), N''),
        SYSUTCDATETIME()
    );

    SELECT @NewValue=(SELECT MedicationName,Strength,DosageForm,Route,Directions,Frequency,StartDate,EndDate,
        Indication,PrescriberName,Notes,MedicationStatus,Classification FROM dbo.PatientMedication WHERE MedicationUid=@MedicationUid
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
    @UpdatedBy BIGINT = NULL, @RowVersion BINARY(8), @Classification NVARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
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
            pm.Indication,pm.PrescriberName,pm.Notes,pm.MedicationStatus,pm.Classification
            FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)
    FROM dbo.PatientMedication AS pm WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN dbo.Patient AS p ON p.PatientUid = pm.PatientUid
    WHERE pm.PatientUid = @PatientUid AND pm.MedicationUid = @MedicationUid AND p.IsDeleted = 0;
    IF @PatientId IS NULL BEGIN ROLLBACK TRANSACTION; RETURN; END;
    UPDATE dbo.PatientMedication SET
        Classification = @Classification,
        MedicationName = LTRIM(RTRIM(@MedicationName)), Strength = NULLIF(LTRIM(RTRIM(@Strength)), N''),
        DosageForm = NULLIF(LTRIM(RTRIM(@DosageForm)), N''), Route = NULLIF(LTRIM(RTRIM(@Route)), N''),
        Directions = NULLIF(LTRIM(RTRIM(@Directions)), N''), Frequency = NULLIF(LTRIM(RTRIM(@Frequency)), N''),
        StartDate = @StartDate, EndDate = @EndDate, Indication = NULLIF(LTRIM(RTRIM(@Indication)), N''),
        PrescriberName = NULLIF(LTRIM(RTRIM(@PrescriberName)), N''), MedicationStatus = LTRIM(RTRIM(@MedicationStatus)),
        Notes = NULLIF(LTRIM(RTRIM(@Notes)), N''), UpdatedBy = @UpdatedBy, UpdatedAt = SYSUTCDATETIME()
    WHERE PatientUid = @PatientUid AND MedicationUid = @MedicationUid AND RowVersion = @RowVersion;
    IF @@ROWCOUNT = 0 BEGIN ROLLBACK TRANSACTION; THROW 51057, 'The medication was changed by another user.', 1; END;
    SELECT @NewValue=(SELECT MedicationName,Strength,DosageForm,Route,Directions,Frequency,StartDate,EndDate,
        Indication,PrescriberName,Notes,MedicationStatus,Classification FROM dbo.PatientMedication WHERE MedicationUid=@MedicationUid
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
    @PatientUid UNIQUEIDENTIFIER,
    @MedicationUid UNIQUEIDENTIFIER,
    @DiscontinueReason NVARCHAR(500) = NULL,
    @DiscontinuedBy BIGINT = NULL,
    @RowVersion BINARY(8)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @PatientId BIGINT, @CurrentStatus NVARCHAR(30), @CurrentRowVersion BINARY(8);
    BEGIN TRANSACTION;
    SELECT @PatientId = p.PatientId, @CurrentStatus = pm.MedicationStatus, @CurrentRowVersion = pm.RowVersion
    FROM dbo.PatientMedication AS pm WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN dbo.Patient AS p ON p.PatientUid = pm.PatientUid
    WHERE pm.PatientUid = @PatientUid AND pm.MedicationUid = @MedicationUid AND p.IsDeleted = 0;
    IF @PatientId IS NULL BEGIN ROLLBACK TRANSACTION; RETURN; END;
    IF @RowVersion IS NULL OR @CurrentRowVersion <> @RowVersion
    BEGIN ROLLBACK TRANSACTION; THROW 51057, 'The medication was changed by another user.', 1; END;
    IF @CurrentStatus <> N'Discontinued'
    BEGIN
        UPDATE dbo.PatientMedication
        SET MedicationStatus = N'Discontinued', EndDate = COALESCE(EndDate, CONVERT(DATE, SYSUTCDATETIME())),
            UpdatedBy = @DiscontinuedBy, UpdatedAt = SYSUTCDATETIME()
        WHERE PatientUid = @PatientUid AND MedicationUid = @MedicationUid AND RowVersion = @RowVersion;
        IF @@ROWCOUNT = 0 BEGIN ROLLBACK TRANSACTION; THROW 51057, 'The medication was changed by another user.', 1; END;
        IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
            INSERT dbo.AuditLog (UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
            VALUES (@DiscontinuedBy, @PatientId, N'Discontinue', N'PatientMedication',
                CONVERT(NVARCHAR(100), @MedicationUid), @CurrentStatus,
                COALESCE(NULLIF(LTRIM(RTRIM(@DiscontinueReason)), N''), N'Medication discontinued'), SYSUTCDATETIME());
    END;
    COMMIT TRANSACTION;
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
        pm.Classification AS Classification,
        pm.RowVersion AS RowVersion
    FROM dbo.PatientMedication AS pm
    LEFT JOIN dbo.ApplicationUser AS au
        ON au.UserId = pm.CreatedBy
    WHERE pm.PatientUid = @PatientUid AND pm.MedicationUid = @MedicationUid;
END;
GO

