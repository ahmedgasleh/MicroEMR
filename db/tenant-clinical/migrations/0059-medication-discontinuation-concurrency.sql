-- Step 38: preserve historical medication SQL; require the reviewed version for discontinuation.
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
        pm.RowVersion AS RowVersion
    FROM dbo.PatientMedication AS pm
    LEFT JOIN dbo.ApplicationUser AS au
        ON au.UserId = pm.CreatedBy
    WHERE pm.PatientUid = @PatientUid AND pm.MedicationUid = @MedicationUid;
END;
GO
