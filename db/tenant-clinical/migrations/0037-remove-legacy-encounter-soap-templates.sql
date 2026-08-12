SET XACT_ABORT ON;
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
        THROW 51041, 'The requested patient was not found.', 1;

    BEGIN TRANSACTION;

    INSERT dbo.PatientEncounter
    (
        EncounterUid, PatientId, PatientUid, EncounterDateUtc, EncounterType,
        ReasonForVisit, LocationName, ProviderName, EncounterStatus, Status,
        CreatedBy, CreatedByDisplayName, CreatedAt
    )
    VALUES
    (
        @EncounterUid, @PatientId, @PatientUid, @EncounterDateUtc,
        LTRIM(RTRIM(@EncounterType)), NULLIF(LTRIM(RTRIM(@ReasonForVisit)), N''),
        NULLIF(LTRIM(RTRIM(@LocationName)), N''), NULLIF(LTRIM(RTRIM(@ProviderName)), N''),
        N'Open', N'Open', @CreatedBy, NULLIF(LTRIM(RTRIM(@CreatedByDisplayName)), N''), SYSUTCDATETIME()
    );

    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
        INSERT dbo.AuditLog(UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
        VALUES(@CreatedBy, @PatientId, N'Create', N'PatientEncounter', CONVERT(NVARCHAR(100), @EncounterUid),
               NULL, N'Encounter created', SYSUTCDATETIME());

    EXEC dbo.PatientEncounterHistory_Create @EncounterUid, @PatientUid, N'Created', N'Encounter created.',
        NULL, N'Open', NULL, @CreatedBy, 0;

    COMMIT TRANSACTION;
    EXEC dbo.PatientEncounter_GetByUid @EncounterUid = @EncounterUid;
END;
GO

DROP PROCEDURE IF EXISTS dbo.EncounterSoapTemplate_SetActive;
DROP PROCEDURE IF EXISTS dbo.EncounterSoapTemplate_Update;
DROP PROCEDURE IF EXISTS dbo.EncounterSoapTemplate_Create;
DROP PROCEDURE IF EXISTS dbo.EncounterSoapTemplate_GetAll;
DROP PROCEDURE IF EXISTS dbo.EncounterSoapTemplate_GetByUid;
GO

DROP TABLE IF EXISTS dbo.EncounterSoapTemplate;
GO
