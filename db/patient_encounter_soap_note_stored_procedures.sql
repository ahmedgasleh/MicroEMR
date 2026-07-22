IF COL_LENGTH('dbo.PatientEncounter', 'SubjectiveNote') IS NULL
    ALTER TABLE dbo.PatientEncounter ADD SubjectiveNote NVARCHAR(MAX) NULL;
GO
IF COL_LENGTH('dbo.PatientEncounter', 'ObjectiveNote') IS NULL
    ALTER TABLE dbo.PatientEncounter ADD ObjectiveNote NVARCHAR(MAX) NULL;
GO
IF COL_LENGTH('dbo.PatientEncounter', 'AssessmentNote') IS NULL
    ALTER TABLE dbo.PatientEncounter ADD AssessmentNote NVARCHAR(MAX) NULL;
GO
IF COL_LENGTH('dbo.PatientEncounter', 'PlanNote') IS NULL
    ALTER TABLE dbo.PatientEncounter ADD PlanNote NVARCHAR(MAX) NULL;
GO

IF OBJECT_ID(N'dbo.PatientEncounter_GetByUid', N'P') IS NOT NULL
    DROP PROCEDURE dbo.PatientEncounter_GetByUid;
GO

CREATE PROCEDURE dbo.PatientEncounter_GetByUid
    @EncounterUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT pe.EncounterUid, pe.PatientUid,
        COALESCE(pe.EncounterDateUtc, pe.EncounterDate, pe.CreatedAt) AS EncounterDateUtc,
        COALESCE(NULLIF(pe.EncounterType, N''), N'Office Visit') AS EncounterType,
        pe.ReasonForVisit, pe.LocationName, pe.ProviderName, pe.EncounterStatus,
        pe.CreatedBy, COALESCE(pe.CreatedByDisplayName, au.DisplayName) AS CreatedByDisplayName,
        pe.CreatedAt, pe.UpdatedAt, pe.EncounterNotes,
        pe.SubjectiveNote, pe.ObjectiveNote, pe.AssessmentNote, pe.PlanNote,
        pe.SignedAt, pe.SignedBy, signedUser.DisplayName AS SignedByDisplayName, pe.RowVersion
    FROM dbo.PatientEncounter AS pe
    LEFT JOIN dbo.ApplicationUser AS au ON au.UserId = pe.CreatedBy
    LEFT JOIN dbo.ApplicationUser AS signedUser ON signedUser.UserId = pe.SignedBy
    WHERE pe.EncounterUid = @EncounterUid;
END;
GO

IF OBJECT_ID(N'dbo.PatientEncounter_UpdateSoapNote', N'P') IS NOT NULL
    DROP PROCEDURE dbo.PatientEncounter_UpdateSoapNote;
GO

CREATE PROCEDURE dbo.PatientEncounter_UpdateSoapNote
    @PatientUid UNIQUEIDENTIFIER, @EncounterUid UNIQUEIDENTIFIER,
    @SubjectiveNote NVARCHAR(MAX) = NULL, @ObjectiveNote NVARCHAR(MAX) = NULL,
    @AssessmentNote NVARCHAR(MAX) = NULL, @PlanNote NVARCHAR(MAX) = NULL,
    @UpdatedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @PatientId BIGINT;
    DECLARE @EncounterStatus NVARCHAR(30);
    DECLARE @EncounterFound BIT;
    SET @EncounterFound = 0;
    BEGIN TRANSACTION;
    SELECT @PatientId = pe.PatientId, @EncounterStatus = pe.EncounterStatus, @EncounterFound = 1
    FROM dbo.PatientEncounter AS pe WITH (UPDLOCK, HOLDLOCK)
    WHERE pe.PatientUid = @PatientUid AND pe.EncounterUid = @EncounterUid;
    IF @EncounterFound = 0 BEGIN ROLLBACK TRANSACTION; RETURN; END;
    IF ISNULL(@EncounterStatus, N'') <> N'Open'
    BEGIN ROLLBACK TRANSACTION; THROW 51071, 'The encounter note cannot be edited in its current status.', 1; END;
    UPDATE dbo.PatientEncounter
    SET SubjectiveNote = NULLIF(@SubjectiveNote, N''), ObjectiveNote = NULLIF(@ObjectiveNote, N''),
        AssessmentNote = NULLIF(@AssessmentNote, N''), PlanNote = NULLIF(@PlanNote, N''),
        UpdatedBy = @UpdatedBy, UpdatedAt = SYSUTCDATETIME()
    WHERE PatientUid = @PatientUid AND EncounterUid = @EncounterUid;
    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
        INSERT dbo.AuditLog (UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
        VALUES (@UpdatedBy, @PatientId, N'UpdateNote', N'PatientEncounter', CONVERT(NVARCHAR(100), @EncounterUid),
                NULL, N'Encounter SOAP note updated', SYSUTCDATETIME());
    EXEC dbo.PatientEncounterHistory_Create @EncounterUid, @PatientUid, N'NoteUpdated',
        N'Encounter SOAP note updated.', NULL, @EncounterStatus, NULL, @UpdatedBy, 0;
    COMMIT TRANSACTION;
    EXEC dbo.PatientEncounter_GetByUid @EncounterUid = @EncounterUid;
END;
GO
