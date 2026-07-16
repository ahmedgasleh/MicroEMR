/*
    Patient Allergies API stored procedures.
*/

IF OBJECT_ID(N'dbo.Patient', N'U') IS NULL
BEGIN
    THROW 51050, 'Required table dbo.Patient was not found.', 1;
END;
GO

IF OBJECT_ID(N'dbo.PatientAllergy', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PatientAllergy
    (
        PatientAllergyId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        AllergyUid UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_PatientAllergy_AllergyUid DEFAULT NEWSEQUENTIALID(),
        PatientUid UNIQUEIDENTIFIER NOT NULL,
        AllergenName NVARCHAR(200) NOT NULL,
        AllergenType NVARCHAR(100) NULL,
        Reaction NVARCHAR(500) NULL,
        Severity NVARCHAR(30) NULL,
        OnsetDate DATE NULL,
        Notes NVARCHAR(1000) NULL,
        AllergyStatus NVARCHAR(30) NOT NULL
            CONSTRAINT DF_PatientAllergy_AllergyStatus DEFAULT N'Active',
        CreatedBy BIGINT NULL,
        CreatedByDisplayName NVARCHAR(200) NULL,
        CreatedAt DATETIME2(0) NOT NULL
            CONSTRAINT DF_PatientAllergy_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedBy BIGINT NULL,
        UpdatedAt DATETIME2(0) NULL,
        RowVersion ROWVERSION NOT NULL
    );
END;
GO

IF COL_LENGTH('dbo.PatientAllergy', 'AllergyUid') IS NULL
BEGIN
    ALTER TABLE dbo.PatientAllergy
        ADD AllergyUid UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_PatientAllergy_AllergyUid_Missing DEFAULT NEWID()
            WITH VALUES;
END;
GO

IF COL_LENGTH('dbo.PatientAllergy', 'PatientUid') IS NULL
BEGIN
    ALTER TABLE dbo.PatientAllergy
        ADD PatientUid UNIQUEIDENTIFIER NULL;
END;
GO

IF COL_LENGTH('dbo.PatientAllergy', 'AllergenName') IS NULL
BEGIN
    ALTER TABLE dbo.PatientAllergy
        ADD AllergenName NVARCHAR(200) NOT NULL
            CONSTRAINT DF_PatientAllergy_AllergenName DEFAULT N'Unknown'
            WITH VALUES;
END;
GO

IF COL_LENGTH('dbo.PatientAllergy', 'AllergenType') IS NULL
BEGIN
    ALTER TABLE dbo.PatientAllergy
        ADD AllergenType NVARCHAR(100) NULL;
END;
GO

IF COL_LENGTH('dbo.PatientAllergy', 'Reaction') IS NULL
BEGIN
    ALTER TABLE dbo.PatientAllergy
        ADD Reaction NVARCHAR(500) NULL;
END;
GO

IF COL_LENGTH('dbo.PatientAllergy', 'Severity') IS NULL
BEGIN
    ALTER TABLE dbo.PatientAllergy
        ADD Severity NVARCHAR(30) NULL;
END;
GO

IF COL_LENGTH('dbo.PatientAllergy', 'OnsetDate') IS NULL
BEGIN
    ALTER TABLE dbo.PatientAllergy
        ADD OnsetDate DATE NULL;
END;
GO

IF COL_LENGTH('dbo.PatientAllergy', 'Notes') IS NULL
BEGIN
    ALTER TABLE dbo.PatientAllergy
        ADD Notes NVARCHAR(1000) NULL;
END;
GO

IF COL_LENGTH('dbo.PatientAllergy', 'AllergyStatus') IS NULL
BEGIN
    ALTER TABLE dbo.PatientAllergy
        ADD AllergyStatus NVARCHAR(30) NOT NULL
            CONSTRAINT DF_PatientAllergy_AllergyStatus_Missing DEFAULT N'Active'
            WITH VALUES;
END;
GO

IF COL_LENGTH('dbo.PatientAllergy', 'CreatedBy') IS NULL
BEGIN
    ALTER TABLE dbo.PatientAllergy
        ADD CreatedBy BIGINT NULL;
END;
GO

IF COL_LENGTH('dbo.PatientAllergy', 'CreatedByDisplayName') IS NULL
BEGIN
    ALTER TABLE dbo.PatientAllergy
        ADD CreatedByDisplayName NVARCHAR(200) NULL;
END;
GO

IF COL_LENGTH('dbo.PatientAllergy', 'CreatedAt') IS NULL
BEGIN
    ALTER TABLE dbo.PatientAllergy
        ADD CreatedAt DATETIME2(0) NOT NULL
            CONSTRAINT DF_PatientAllergy_CreatedAt_Missing DEFAULT SYSUTCDATETIME()
            WITH VALUES;
END;
GO

IF COL_LENGTH('dbo.PatientAllergy', 'UpdatedBy') IS NULL
BEGIN
    ALTER TABLE dbo.PatientAllergy
        ADD UpdatedBy BIGINT NULL;
END;
GO

IF COL_LENGTH('dbo.PatientAllergy', 'UpdatedAt') IS NULL
BEGIN
    ALTER TABLE dbo.PatientAllergy
        ADD UpdatedAt DATETIME2(0) NULL;
END;
GO

IF COL_LENGTH('dbo.PatientAllergy', 'RowVersion') IS NULL
BEGIN
    ALTER TABLE dbo.PatientAllergy
        ADD RowVersion ROWVERSION;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.PatientAllergy')
        AND name = N'UQ_PatientAllergy_AllergyUid'
)
BEGIN
    ALTER TABLE dbo.PatientAllergy
        ADD CONSTRAINT UQ_PatientAllergy_AllergyUid UNIQUE (AllergyUid);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.PatientAllergy')
        AND name = N'IX_PatientAllergy_PatientUid_AllergenName'
)
BEGIN
    CREATE INDEX IX_PatientAllergy_PatientUid_AllergenName
    ON dbo.PatientAllergy (PatientUid, AllergenName);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.PatientAllergy')
        AND name = N'IX_PatientAllergy_PatientUid_AllergyStatus'
)
BEGIN
    CREATE INDEX IX_PatientAllergy_PatientUid_AllergyStatus
    ON dbo.PatientAllergy (PatientUid, AllergyStatus);
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientAllergy_GetByPatientUid
    @PatientUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        pa.AllergyUid AS AllergyUid,
        pa.PatientUid AS PatientUid,
        pa.AllergenName AS AllergenName,
        pa.AllergenType AS AllergenType,
        pa.Reaction AS Reaction,
        pa.Severity AS Severity,
        pa.OnsetDate AS OnsetDate,
        pa.Notes AS Notes,
        pa.AllergyStatus AS AllergyStatus,
        pa.CreatedBy AS CreatedBy,
        COALESCE(pa.CreatedByDisplayName, au.DisplayName) AS CreatedByDisplayName,
        pa.CreatedAt AS CreatedAt,
        pa.UpdatedAt AS UpdatedAt
    FROM dbo.PatientAllergy AS pa
    LEFT JOIN dbo.ApplicationUser AS au
        ON au.UserId = pa.CreatedBy
    WHERE pa.PatientUid = @PatientUid
    ORDER BY
        CASE WHEN pa.AllergyStatus = N'Active' THEN 0 ELSE 1 END,
        pa.AllergenName ASC,
        pa.CreatedAt DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientAllergy_GetByUid
    @AllergyUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        pa.AllergyUid AS AllergyUid,
        pa.PatientUid AS PatientUid,
        pa.AllergenName AS AllergenName,
        pa.AllergenType AS AllergenType,
        pa.Reaction AS Reaction,
        pa.Severity AS Severity,
        pa.OnsetDate AS OnsetDate,
        pa.Notes AS Notes,
        pa.AllergyStatus AS AllergyStatus,
        pa.CreatedBy AS CreatedBy,
        COALESCE(pa.CreatedByDisplayName, au.DisplayName) AS CreatedByDisplayName,
        pa.CreatedAt AS CreatedAt,
        pa.UpdatedAt AS UpdatedAt,
        pa.RowVersion AS RowVersion
    FROM dbo.PatientAllergy AS pa
    LEFT JOIN dbo.ApplicationUser AS au
        ON au.UserId = pa.CreatedBy
    WHERE pa.AllergyUid = @AllergyUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientAllergy_Create
    @PatientUid UNIQUEIDENTIFIER,
    @AllergenName NVARCHAR(200),
    @AllergenType NVARCHAR(100) = NULL,
    @Reaction NVARCHAR(500) = NULL,
    @Severity NVARCHAR(30) = NULL,
    @OnsetDate DATE = NULL,
    @Notes NVARCHAR(1000) = NULL,
    @CreatedBy BIGINT = NULL,
    @CreatedByDisplayName NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @PatientId BIGINT;
    DECLARE @AllergyUid UNIQUEIDENTIFIER = NEWID();

    SELECT @PatientId = p.PatientId
    FROM dbo.Patient AS p
    WHERE p.PatientUid = @PatientUid
        AND p.IsDeleted = CONVERT(BIT, 0);

    IF @PatientId IS NULL
    BEGIN
        THROW 51051, 'The requested patient was not found.', 1;
    END;

    BEGIN TRANSACTION;

    INSERT INTO dbo.PatientAllergy
    (
        AllergyUid,
        PatientUid,
        AllergenName,
        AllergenType,
        Reaction,
        Severity,
        OnsetDate,
        Notes,
        AllergyStatus,
        CreatedBy,
        CreatedByDisplayName,
        CreatedAt
    )
    VALUES
    (
        @AllergyUid,
        @PatientUid,
        LTRIM(RTRIM(@AllergenName)),
        NULLIF(LTRIM(RTRIM(@AllergenType)), N''),
        NULLIF(LTRIM(RTRIM(@Reaction)), N''),
        NULLIF(LTRIM(RTRIM(@Severity)), N''),
        @OnsetDate,
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
            N'PatientAllergy',
            CONVERT(NVARCHAR(100), @AllergyUid),
            NULL,
            N'Allergy created',
            SYSUTCDATETIME()
        );
    END;

    COMMIT TRANSACTION;

    EXEC dbo.PatientAllergy_GetByUid
        @AllergyUid = @AllergyUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientAllergy_Update
    @PatientUid UNIQUEIDENTIFIER,
    @AllergyUid UNIQUEIDENTIFIER,
    @AllergenName NVARCHAR(200),
    @AllergenType NVARCHAR(100) = NULL,
    @Reaction NVARCHAR(500) = NULL,
    @Severity NVARCHAR(30) = NULL,
    @OnsetDate DATE = NULL,
    @AllergyStatus NVARCHAR(30),
    @Notes NVARCHAR(1000) = NULL,
    @UpdatedBy BIGINT = NULL,
    @RowVersion BINARY(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @PatientId BIGINT;

    BEGIN TRANSACTION;

    SELECT @PatientId = p.PatientId
    FROM dbo.PatientAllergy AS pa WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN dbo.Patient AS p ON p.PatientUid = pa.PatientUid
    WHERE pa.PatientUid = @PatientUid
        AND pa.AllergyUid = @AllergyUid
        AND p.IsDeleted = CONVERT(BIT, 0);

    IF @PatientId IS NULL
    BEGIN
        ROLLBACK TRANSACTION;
        RETURN;
    END;

    UPDATE dbo.PatientAllergy
    SET AllergenName = LTRIM(RTRIM(@AllergenName)),
        AllergenType = NULLIF(LTRIM(RTRIM(@AllergenType)), N''),
        Reaction = NULLIF(LTRIM(RTRIM(@Reaction)), N''),
        Severity = NULLIF(LTRIM(RTRIM(@Severity)), N''),
        OnsetDate = @OnsetDate,
        AllergyStatus = LTRIM(RTRIM(@AllergyStatus)),
        Notes = NULLIF(LTRIM(RTRIM(@Notes)), N''),
        UpdatedBy = @UpdatedBy,
        UpdatedAt = SYSUTCDATETIME()
    WHERE PatientUid = @PatientUid
        AND AllergyUid = @AllergyUid
        AND RowVersion = @RowVersion;

    IF @@ROWCOUNT = 0
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51052, 'The allergy was changed by another user.', 1;
    END;

    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
    BEGIN
        INSERT INTO dbo.AuditLog
            (UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
        VALUES
            (@UpdatedBy, @PatientId, N'Update', N'PatientAllergy',
             CONVERT(NVARCHAR(100), @AllergyUid), NULL,
             N'Allergy updated', SYSUTCDATETIME());
    END;

    COMMIT TRANSACTION;

    EXEC dbo.PatientAllergy_GetByUid @AllergyUid = @AllergyUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientAllergy_Resolve
    @PatientUid UNIQUEIDENTIFIER, @AllergyUid UNIQUEIDENTIFIER,
    @ResolveReason NVARCHAR(500) = NULL, @ResolvedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @PatientId BIGINT, @CurrentStatus NVARCHAR(30);
    BEGIN TRANSACTION;
    SELECT @PatientId = p.PatientId, @CurrentStatus = pa.AllergyStatus
    FROM dbo.PatientAllergy AS pa WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN dbo.Patient AS p ON p.PatientUid = pa.PatientUid
    WHERE pa.PatientUid = @PatientUid AND pa.AllergyUid = @AllergyUid AND p.IsDeleted = 0;
    IF @PatientId IS NULL BEGIN ROLLBACK TRANSACTION; RETURN; END;
    IF @CurrentStatus <> N'Resolved'
    BEGIN
        UPDATE dbo.PatientAllergy SET AllergyStatus = N'Resolved', UpdatedBy = @ResolvedBy,
            UpdatedAt = SYSUTCDATETIME()
        WHERE PatientUid = @PatientUid AND AllergyUid = @AllergyUid;
        IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
            INSERT dbo.AuditLog (UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
            VALUES (@ResolvedBy, @PatientId, N'Resolve', N'PatientAllergy', CONVERT(NVARCHAR(100), @AllergyUid),
                @CurrentStatus, COALESCE(NULLIF(LTRIM(RTRIM(@ResolveReason)), N''), N'Allergy resolved'), SYSUTCDATETIME());
    END;
    COMMIT TRANSACTION;
    EXEC dbo.PatientAllergy_GetByUid @AllergyUid = @AllergyUid;
END;
GO
