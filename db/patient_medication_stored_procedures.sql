/*
    Patient Medications API stored procedures.
*/

IF OBJECT_ID(N'dbo.Patient', N'U') IS NULL
BEGIN
    THROW 51060, 'Required table dbo.Patient was not found.', 1;
END;
GO

IF OBJECT_ID(N'dbo.PatientMedication', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PatientMedication
    (
        PatientMedicationId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        MedicationUid UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_PatientMedication_MedicationUid DEFAULT NEWSEQUENTIALID(),
        PatientUid UNIQUEIDENTIFIER NOT NULL,
        MedicationName NVARCHAR(200) NOT NULL,
        Strength NVARCHAR(100) NULL,
        DosageForm NVARCHAR(100) NULL,
        Route NVARCHAR(100) NULL,
        Directions NVARCHAR(500) NULL,
        Frequency NVARCHAR(100) NULL,
        StartDate DATE NULL,
        EndDate DATE NULL,
        Indication NVARCHAR(300) NULL,
        PrescriberName NVARCHAR(200) NULL,
        Notes NVARCHAR(1000) NULL,
        MedicationStatus NVARCHAR(30) NOT NULL
            CONSTRAINT DF_PatientMedication_MedicationStatus DEFAULT N'Active',
        CreatedBy BIGINT NULL,
        CreatedByDisplayName NVARCHAR(200) NULL,
        CreatedAt DATETIME2(0) NOT NULL
            CONSTRAINT DF_PatientMedication_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedBy BIGINT NULL,
        UpdatedAt DATETIME2(0) NULL,
        RowVersion ROWVERSION NOT NULL
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.PatientMedication')
        AND name = N'UQ_PatientMedication_MedicationUid'
)
BEGIN
    ALTER TABLE dbo.PatientMedication
        ADD CONSTRAINT UQ_PatientMedication_MedicationUid UNIQUE (MedicationUid);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.PatientMedication')
        AND name = N'IX_PatientMedication_PatientUid_MedicationName'
)
BEGIN
    CREATE INDEX IX_PatientMedication_PatientUid_MedicationName
    ON dbo.PatientMedication (PatientUid, MedicationName);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.PatientMedication')
        AND name = N'IX_PatientMedication_PatientUid_MedicationStatus'
)
BEGIN
    CREATE INDEX IX_PatientMedication_PatientUid_MedicationStatus
    ON dbo.PatientMedication (PatientUid, MedicationStatus);
END;
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
        pm.UpdatedAt AS UpdatedAt
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
    @CreatedByDisplayName NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @PatientId BIGINT;
    DECLARE @MedicationUid UNIQUEIDENTIFIER = NEWID();

    SELECT @PatientId = p.PatientId
    FROM dbo.Patient AS p
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

    BEGIN TRANSACTION;

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
        N'Active',
        @CreatedBy,
        NULLIF(LTRIM(RTRIM(@CreatedByDisplayName)), N''),
        SYSUTCDATETIME()
    );

    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
    BEGIN
        INSERT INTO dbo.AuditLog
        (
            UserId,
            PatientId,
            ActionName,
            EntityName,
            EntityId,
            OldValue,
            NewValue,
            CreatedAt
        )
        VALUES
        (
            @CreatedBy,
            @PatientId,
            N'Create',
            N'PatientMedication',
            CONVERT(NVARCHAR(100), @MedicationUid),
            NULL,
            N'Medication created',
            SYSUTCDATETIME()
        );
    END;

    COMMIT TRANSACTION;

    EXEC dbo.PatientMedication_GetByUid
        @MedicationUid = @MedicationUid;
END;
GO
