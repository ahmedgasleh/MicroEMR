SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.PatientEncounterAddendum', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PatientEncounterAddendum
    (
        EncounterAddendumId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        EncounterAddendumUid UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_PatientEncounterAddendum_Uid DEFAULT NEWSEQUENTIALID(),
        EncounterUid UNIQUEIDENTIFIER NOT NULL,
        PatientUid UNIQUEIDENTIFIER NOT NULL,
        AddendumText NVARCHAR(MAX) NOT NULL,
        CreatedAt DATETIME2(0) NOT NULL
            CONSTRAINT DF_PatientEncounterAddendum_CreatedAt DEFAULT SYSUTCDATETIME(),
        CreatedBy BIGINT NULL
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.PatientEncounterAddendum')
        AND name = N'IX_PatientEncounterAddendum_EncounterUid_CreatedAt'
)
    CREATE INDEX IX_PatientEncounterAddendum_EncounterUid_CreatedAt
        ON dbo.PatientEncounterAddendum (EncounterUid, CreatedAt);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.PatientEncounterAddendum')
        AND name = N'IX_PatientEncounterAddendum_PatientUid_CreatedAt'
)
    CREATE INDEX IX_PatientEncounterAddendum_PatientUid_CreatedAt
        ON dbo.PatientEncounterAddendum (PatientUid, CreatedAt);
GO

CREATE OR ALTER PROCEDURE dbo.PatientEncounterAddendum_GetByEncounterUid
    @PatientUid UNIQUEIDENTIFIER,
    @EncounterUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        addendum.EncounterAddendumUid,
        addendum.EncounterUid,
        addendum.PatientUid,
        addendum.AddendumText,
        addendum.CreatedAt,
        addendum.CreatedBy,
        users.DisplayName AS CreatedByDisplayName
    FROM dbo.PatientEncounterAddendum AS addendum
    LEFT JOIN dbo.ApplicationUser AS users ON users.UserId = addendum.CreatedBy
    WHERE addendum.PatientUid = @PatientUid
        AND addendum.EncounterUid = @EncounterUid
    ORDER BY addendum.CreatedAt ASC, addendum.EncounterAddendumId ASC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientEncounterAddendum_Create
    @PatientUid UNIQUEIDENTIFIER,
    @EncounterUid UNIQUEIDENTIFIER,
    @AddendumText NVARCHAR(MAX),
    @CreatedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NULLIF(LTRIM(RTRIM(@AddendumText)), N'') IS NULL
        THROW 51074, 'Addendum text is required.', 1;

    DECLARE @EncounterStatus NVARCHAR(30);
    DECLARE @EncounterAddendumUid UNIQUEIDENTIFIER = NEWID();

    BEGIN TRANSACTION;

    SELECT @EncounterStatus = EncounterStatus
    FROM dbo.PatientEncounter WITH (UPDLOCK, HOLDLOCK)
    WHERE PatientUid = @PatientUid AND EncounterUid = @EncounterUid;

    IF @EncounterStatus IS NULL
    BEGIN
        ROLLBACK TRANSACTION;
        RETURN;
    END;

    IF @EncounterStatus <> N'Signed'
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51075, 'Addendums can only be added to signed encounters.', 1;
    END;

    INSERT INTO dbo.PatientEncounterAddendum
        (EncounterAddendumUid, EncounterUid, PatientUid, AddendumText, CreatedBy)
    VALUES
        (@EncounterAddendumUid, @EncounterUid, @PatientUid,
         LTRIM(RTRIM(@AddendumText)), @CreatedBy);

    EXEC dbo.PatientEncounterHistory_Create
        @EncounterUid = @EncounterUid,
        @PatientUid = @PatientUid,
        @ActionType = N'AddendumAdded',
        @ActionDescription = N'Encounter addendum added.',
        @CreatedBy = @CreatedBy,
        @ReturnResult = 0;

    COMMIT TRANSACTION;

    SELECT
        addendum.EncounterAddendumUid,
        addendum.EncounterUid,
        addendum.PatientUid,
        addendum.AddendumText,
        addendum.CreatedAt,
        addendum.CreatedBy,
        users.DisplayName AS CreatedByDisplayName
    FROM dbo.PatientEncounterAddendum AS addendum
    LEFT JOIN dbo.ApplicationUser AS users ON users.UserId = addendum.CreatedBy
    WHERE addendum.EncounterAddendumUid = @EncounterAddendumUid;
END;
GO
