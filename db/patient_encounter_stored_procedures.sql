/*
    Patient Encounters API stored procedures.

    This script supports both a fresh dbo.PatientEncounter table and the
    older prototype table shape in db/initial.sql.
*/

IF OBJECT_ID(N'dbo.Patient', N'U') IS NULL
BEGIN
    THROW 51040, 'Required table dbo.Patient was not found.', 1;
END;
GO

IF OBJECT_ID(N'dbo.PatientEncounter', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PatientEncounter
    (
        PatientEncounterId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        EncounterUid UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_PatientEncounter_EncounterUid DEFAULT NEWSEQUENTIALID(),
        AppointmentUid UNIQUEIDENTIFIER NULL,
        PatientId BIGINT NULL,
        PatientUid UNIQUEIDENTIFIER NOT NULL,
        EncounterDateUtc DATETIME2(0) NOT NULL,
        EncounterType NVARCHAR(100) NOT NULL,
        ReasonForVisit NVARCHAR(500) NULL,
        LocationName NVARCHAR(200) NULL,
        ProviderName NVARCHAR(200) NULL,
        EncounterStatus NVARCHAR(30) NOT NULL
            CONSTRAINT DF_PatientEncounter_EncounterStatus DEFAULT N'Open',
        Status NVARCHAR(50) NULL,
        CreatedBy BIGINT NULL,
        CreatedByDisplayName NVARCHAR(200) NULL,
        CreatedAt DATETIME2(0) NOT NULL
            CONSTRAINT DF_PatientEncounter_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedBy BIGINT NULL,
        UpdatedAt DATETIME2(0) NULL,
        RowVersion ROWVERSION NOT NULL
    );
END;
GO

IF COL_LENGTH('dbo.PatientEncounter', 'AppointmentUid') IS NULL
BEGIN
    ALTER TABLE dbo.PatientEncounter
        ADD AppointmentUid UNIQUEIDENTIFIER NULL;
END;
GO

IF COL_LENGTH('dbo.PatientEncounter', 'EncounterUid') IS NULL
BEGIN
    ALTER TABLE dbo.PatientEncounter
        ADD EncounterUid UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_PatientEncounter_EncounterUid_Missing DEFAULT NEWID()
            WITH VALUES;
END;
GO

IF COL_LENGTH('dbo.PatientEncounter', 'PatientUid') IS NULL
BEGIN
    ALTER TABLE dbo.PatientEncounter
        ADD PatientUid UNIQUEIDENTIFIER NULL;
END;
GO

IF COL_LENGTH('dbo.PatientEncounter', 'PatientId') IS NULL
BEGIN
    ALTER TABLE dbo.PatientEncounter
        ADD PatientId BIGINT NULL;
END;
GO

IF COL_LENGTH('dbo.PatientEncounter', 'EncounterDate') IS NULL
BEGIN
    ALTER TABLE dbo.PatientEncounter
        ADD EncounterDate DATETIME2 NULL;
END;
GO

IF COL_LENGTH('dbo.PatientEncounter', 'ChiefComplaint') IS NULL
BEGIN
    ALTER TABLE dbo.PatientEncounter
        ADD ChiefComplaint NVARCHAR(500) NULL;
END;
GO

UPDATE pe
SET PatientUid = p.PatientUid
FROM dbo.PatientEncounter AS pe
INNER JOIN dbo.Patient AS p
    ON p.PatientId = pe.PatientId
WHERE pe.PatientUid IS NULL;
GO

IF COL_LENGTH('dbo.PatientEncounter', 'EncounterDateUtc') IS NULL
BEGIN
    ALTER TABLE dbo.PatientEncounter
        ADD EncounterDateUtc DATETIME2(0) NULL;
END;
GO

UPDATE dbo.PatientEncounter
SET EncounterDateUtc = EncounterDate
WHERE EncounterDateUtc IS NULL
    AND COL_LENGTH('dbo.PatientEncounter', 'EncounterDate') IS NOT NULL;
GO

UPDATE dbo.PatientEncounter
SET EncounterDateUtc = CreatedAt
WHERE EncounterDateUtc IS NULL
    AND CreatedAt IS NOT NULL;
GO

UPDATE dbo.PatientEncounter
SET EncounterDateUtc = SYSUTCDATETIME()
WHERE EncounterDateUtc IS NULL;
GO

UPDATE dbo.PatientEncounter
SET EncounterType = N'Office Visit'
WHERE EncounterType IS NULL
    OR LTRIM(RTRIM(EncounterType)) = N'';
GO

IF COL_LENGTH('dbo.PatientEncounter', 'ReasonForVisit') IS NULL
BEGIN
    ALTER TABLE dbo.PatientEncounter
        ADD ReasonForVisit NVARCHAR(500) NULL;
END;
GO

UPDATE dbo.PatientEncounter
SET ReasonForVisit = ChiefComplaint
WHERE ReasonForVisit IS NULL
    AND COL_LENGTH('dbo.PatientEncounter', 'ChiefComplaint') IS NOT NULL;
GO

IF COL_LENGTH('dbo.PatientEncounter', 'LocationName') IS NULL
BEGIN
    ALTER TABLE dbo.PatientEncounter
        ADD LocationName NVARCHAR(200) NULL;
END;
GO

IF COL_LENGTH('dbo.PatientEncounter', 'ProviderName') IS NULL
BEGIN
    ALTER TABLE dbo.PatientEncounter
        ADD ProviderName NVARCHAR(200) NULL;
END;
GO

IF COL_LENGTH('dbo.PatientEncounter', 'EncounterStatus') IS NULL
BEGIN
    ALTER TABLE dbo.PatientEncounter
        ADD EncounterStatus NVARCHAR(30) NULL;
END;
GO

IF COL_LENGTH('dbo.PatientEncounter', 'Status') IS NULL
BEGIN
    ALTER TABLE dbo.PatientEncounter
        ADD Status NVARCHAR(50) NULL;
END;
GO

UPDATE dbo.PatientEncounter
SET EncounterStatus = COALESCE(NULLIF(Status, N''), N'Open')
WHERE EncounterStatus IS NULL;
GO

IF COL_LENGTH('dbo.PatientEncounter', 'CreatedByDisplayName') IS NULL
BEGIN
    ALTER TABLE dbo.PatientEncounter
        ADD CreatedByDisplayName NVARCHAR(200) NULL;
END;
GO

IF COL_LENGTH('dbo.PatientEncounter', 'UpdatedBy') IS NULL
BEGIN
    ALTER TABLE dbo.PatientEncounter
        ADD UpdatedBy BIGINT NULL;
END;
GO

IF COL_LENGTH('dbo.PatientEncounter', 'UpdatedAt') IS NULL
BEGIN
    ALTER TABLE dbo.PatientEncounter
        ADD UpdatedAt DATETIME2(0) NULL;
END;
GO

IF COL_LENGTH('dbo.PatientEncounter', 'RowVersion') IS NULL
BEGIN
    ALTER TABLE dbo.PatientEncounter
        ADD RowVersion ROWVERSION;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.PatientEncounter')
        AND name = N'UQ_PatientEncounter_EncounterUid'
)
BEGIN
    ALTER TABLE dbo.PatientEncounter
        ADD CONSTRAINT UQ_PatientEncounter_EncounterUid UNIQUE (EncounterUid);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.PatientEncounter')
        AND name = N'UX_PatientEncounter_AppointmentUid'
)
BEGIN
    CREATE UNIQUE INDEX UX_PatientEncounter_AppointmentUid
    ON dbo.PatientEncounter (AppointmentUid)
    WHERE AppointmentUid IS NOT NULL;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.PatientEncounter')
        AND name = N'IX_PatientEncounter_PatientUid_EncounterDateUtc'
)
BEGIN
    CREATE INDEX IX_PatientEncounter_PatientUid_EncounterDateUtc
    ON dbo.PatientEncounter (PatientUid, EncounterDateUtc DESC);
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientEncounter_GetByPatientUid
    @PatientUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        pe.EncounterUid AS EncounterUid,
        pe.PatientUid AS PatientUid,
        COALESCE(pe.EncounterDateUtc, pe.EncounterDate, pe.CreatedAt) AS EncounterDateUtc,
        COALESCE(NULLIF(pe.EncounterType, N''), N'Office Visit') AS EncounterType,
        pe.ReasonForVisit AS ReasonForVisit,
        pe.LocationName AS LocationName,
        pe.ProviderName AS ProviderName,
        pe.EncounterStatus AS EncounterStatus,
        pe.CreatedBy AS CreatedBy,
        COALESCE(pe.CreatedByDisplayName, au.DisplayName) AS CreatedByDisplayName,
        pe.CreatedAt AS CreatedAt,
        pe.UpdatedAt AS UpdatedAt
    FROM dbo.PatientEncounter AS pe
    LEFT JOIN dbo.ApplicationUser AS au
        ON au.UserId = pe.CreatedBy
    WHERE pe.PatientUid = @PatientUid
    ORDER BY
        COALESCE(pe.EncounterDateUtc, pe.EncounterDate, pe.CreatedAt) DESC,
        pe.CreatedAt DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientEncounter_GetByUid
    @EncounterUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        pe.EncounterUid AS EncounterUid,
        pe.PatientUid AS PatientUid,
        COALESCE(pe.EncounterDateUtc, pe.EncounterDate, pe.CreatedAt) AS EncounterDateUtc,
        COALESCE(NULLIF(pe.EncounterType, N''), N'Office Visit') AS EncounterType,
        pe.ReasonForVisit AS ReasonForVisit,
        pe.LocationName AS LocationName,
        pe.ProviderName AS ProviderName,
        pe.EncounterStatus AS EncounterStatus,
        pe.CreatedBy AS CreatedBy,
        COALESCE(pe.CreatedByDisplayName, au.DisplayName) AS CreatedByDisplayName,
        pe.CreatedAt AS CreatedAt,
        pe.UpdatedAt AS UpdatedAt,
        pe.RowVersion AS RowVersion
    FROM dbo.PatientEncounter AS pe
    LEFT JOIN dbo.ApplicationUser AS au
        ON au.UserId = pe.CreatedBy
    WHERE pe.EncounterUid = @EncounterUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientEncounter_Create
    @PatientUid UNIQUEIDENTIFIER,
    @EncounterDateUtc DATETIME2(0),
    @EncounterType NVARCHAR(100),
    @ReasonForVisit NVARCHAR(500) = NULL,
    @LocationName NVARCHAR(200) = NULL,
    @ProviderName NVARCHAR(200) = NULL,
    @CreatedBy BIGINT = NULL,
    @CreatedByDisplayName NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @PatientId BIGINT;
    DECLARE @EncounterUid UNIQUEIDENTIFIER = NEWID();

    SELECT @PatientId = p.PatientId
    FROM dbo.Patient AS p
    WHERE p.PatientUid = @PatientUid
        AND p.IsDeleted = CONVERT(BIT, 0);

    IF @PatientId IS NULL
    BEGIN
        THROW 51041, 'The requested patient was not found.', 1;
    END;

    BEGIN TRANSACTION;

    INSERT INTO dbo.PatientEncounter
    (
        EncounterUid,
        PatientId,
        PatientUid,
        EncounterDateUtc,
        EncounterType,
        ReasonForVisit,
        LocationName,
        ProviderName,
        EncounterStatus,
        Status,
        CreatedBy,
        CreatedByDisplayName,
        CreatedAt
    )
    VALUES
    (
        @EncounterUid,
        @PatientId,
        @PatientUid,
        @EncounterDateUtc,
        LTRIM(RTRIM(@EncounterType)),
        NULLIF(LTRIM(RTRIM(@ReasonForVisit)), N''),
        NULLIF(LTRIM(RTRIM(@LocationName)), N''),
        NULLIF(LTRIM(RTRIM(@ProviderName)), N''),
        N'Open',
        N'Open',
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
            N'PatientEncounter',
            CONVERT(NVARCHAR(100), @EncounterUid),
            NULL,
            N'Encounter created',
            SYSUTCDATETIME()
        );
    END;

    COMMIT TRANSACTION;

    EXEC dbo.PatientEncounter_GetByUid
        @EncounterUid = @EncounterUid;
END;
GO

IF OBJECT_ID(N'dbo.PatientEncounter_StartFromAppointment', N'P') IS NOT NULL
BEGIN
    DROP PROCEDURE dbo.PatientEncounter_StartFromAppointment;
END;
GO

CREATE PROCEDURE dbo.PatientEncounter_StartFromAppointment
    @AppointmentUid UNIQUEIDENTIFIER,
    @CreatedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @PatientUid UNIQUEIDENTIFIER;
    DECLARE @PatientId BIGINT;
    DECLARE @AppointmentStatus NVARCHAR(30);
    DECLARE @AppointmentDateUtc DATETIME2(0);
    DECLARE @AppointmentType NVARCHAR(100);
    DECLARE @ReasonForVisit NVARCHAR(500);
    DECLARE @EncounterUid UNIQUEIDENTIFIER;
    DECLARE @WasCreated BIT;

    -- Avoid literal assignment here. Clients with Always Encrypted parameterization
    -- can rewrite scalar literals inside CREATE PROCEDURE into undeclared parameters.
    SET @WasCreated = CONVERT(BIT, @@ROWCOUNT - @@ROWCOUNT);

    BEGIN TRANSACTION;

    SELECT
        @PatientUid = appointment.PatientUid,
        @PatientId = patient.PatientId,
        @AppointmentStatus = appointment.AppointmentStatus,
        @AppointmentDateUtc = appointment.StartDateTimeUtc,
        @AppointmentType = appointment.AppointmentType,
        @ReasonForVisit = appointment.Reason
    FROM dbo.ScheduleAppointment AS appointment WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN dbo.Patient AS patient ON patient.PatientUid = appointment.PatientUid
    WHERE appointment.AppointmentUid = @AppointmentUid
        AND appointment.IsDeleted = 0
        AND patient.IsDeleted = 0;

    IF @PatientUid IS NULL
    BEGIN
        ROLLBACK TRANSACTION;
        RETURN;
    END;

    IF @AppointmentStatus = N'Cancelled'
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51069, 'Cancelled appointments cannot start encounters.', 1;
    END;

    IF @AppointmentStatus = N'Completed'
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51070, 'Completed appointments cannot start new encounters.', 1;
    END;

    SELECT @EncounterUid = EncounterUid
    FROM dbo.PatientEncounter WITH (UPDLOCK, HOLDLOCK)
    WHERE AppointmentUid = @AppointmentUid;

    IF @EncounterUid IS NULL
    BEGIN
        SET @EncounterUid = NEWID();
        SET @WasCreated = CONVERT(BIT, SIGN(@@TRANCOUNT));

        INSERT INTO dbo.PatientEncounter
        (
            EncounterUid, AppointmentUid, PatientId, PatientUid,
            EncounterDateUtc, EncounterType, ReasonForVisit,
            EncounterStatus, Status, CreatedBy, CreatedAt
        )
        VALUES
        (
            @EncounterUid, @AppointmentUid, @PatientId, @PatientUid,
            @AppointmentDateUtc,
            COALESCE(NULLIF(LTRIM(RTRIM(@AppointmentType)), N''), N'Scheduled Visit'),
            NULLIF(LTRIM(RTRIM(@ReasonForVisit)), N''),
            N'Open', N'Open', @CreatedBy, SYSUTCDATETIME()
        );

        IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
        BEGIN
            INSERT INTO dbo.AuditLog
                (UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
            VALUES
                (@CreatedBy, @PatientId, N'Create', N'PatientEncounter',
                 CONVERT(NVARCHAR(100), @EncounterUid), NULL,
                 N'Encounter started from appointment', SYSUTCDATETIME());
        END;
    END;

    COMMIT TRANSACTION;

    SELECT
        EncounterUid,
        PatientUid,
        AppointmentUid,
        EncounterDateUtc AS EncounterDate,
        EncounterType,
        ReasonForVisit,
        EncounterStatus AS Status,
        @WasCreated AS WasCreated
    FROM dbo.PatientEncounter
    WHERE EncounterUid = @EncounterUid;
END;
GO
