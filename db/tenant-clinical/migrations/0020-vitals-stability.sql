CREATE OR ALTER PROCEDURE dbo.PatientVital_GetByPatientUid
    @PatientUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Patient WHERE PatientUid = @PatientUid AND IsDeleted = 0)
        THROW 51401, 'The requested patient was not found.', 1;

    SELECT pv.PatientVitalUid, pv.PatientUid, pv.RecordedAt, pv.BloodPressureSystolic,
        pv.BloodPressureDiastolic, pv.HeartRate, pv.RespiratoryRate, pv.TemperatureCelsius,
        pv.OxygenSaturation, pv.HeightCm, pv.WeightKg, pv.Bmi, pv.Notes, pv.CreatedAt,
        pv.CreatedBy, cu.DisplayName CreatedByDisplayName, pv.UpdatedAt, pv.UpdatedBy,
        uu.DisplayName UpdatedByDisplayName, pv.RowVersion
    FROM dbo.PatientVital pv
    LEFT JOIN dbo.ApplicationUser cu ON cu.UserId = pv.CreatedBy
    LEFT JOIN dbo.ApplicationUser uu ON uu.UserId = pv.UpdatedBy
    WHERE pv.PatientUid = @PatientUid
    ORDER BY pv.RecordedAt DESC, pv.CreatedAt DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientVital_GetByUid
    @PatientUid UNIQUEIDENTIFIER,
    @PatientVitalUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Patient WHERE PatientUid = @PatientUid AND IsDeleted = 0)
        THROW 51401, 'The requested patient was not found.', 1;

    SELECT pv.PatientVitalUid, pv.PatientUid, pv.RecordedAt, pv.BloodPressureSystolic,
        pv.BloodPressureDiastolic, pv.HeartRate, pv.RespiratoryRate, pv.TemperatureCelsius,
        pv.OxygenSaturation, pv.HeightCm, pv.WeightKg, pv.Bmi, pv.Notes, pv.CreatedAt,
        pv.CreatedBy, cu.DisplayName CreatedByDisplayName, pv.UpdatedAt, pv.UpdatedBy,
        uu.DisplayName UpdatedByDisplayName, pv.RowVersion
    FROM dbo.PatientVital pv
    LEFT JOIN dbo.ApplicationUser cu ON cu.UserId = pv.CreatedBy
    LEFT JOIN dbo.ApplicationUser uu ON uu.UserId = pv.UpdatedBy
    WHERE pv.PatientUid = @PatientUid AND pv.PatientVitalUid = @PatientVitalUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientVital_Create
    @PatientUid UNIQUEIDENTIFIER, @RecordedAt DATETIME2(0),
    @BloodPressureSystolic INT = NULL, @BloodPressureDiastolic INT = NULL,
    @HeartRate INT = NULL, @RespiratoryRate INT = NULL,
    @TemperatureCelsius DECIMAL(5,2) = NULL, @OxygenSaturation INT = NULL,
    @HeightCm DECIMAL(6,2) = NULL, @WeightKg DECIMAL(6,2) = NULL,
    @Notes NVARCHAR(1000) = NULL, @CreatedBy BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @PatientId BIGINT, @PatientVitalUid UNIQUEIDENTIFIER = NEWID(), @Bmi DECIMAL(5,2) = NULL;

    SELECT @PatientId = PatientId FROM dbo.Patient WHERE PatientUid = @PatientUid AND IsDeleted = 0;
    IF @PatientId IS NULL THROW 51401, 'The requested patient was not found.', 1;
    IF @RecordedAt IS NULL THROW 51403, 'Recorded date and time are required.', 1;
    IF @BloodPressureSystolic IS NOT NULL AND @BloodPressureSystolic NOT BETWEEN 40 AND 300 THROW 51403, 'Systolic blood pressure is outside the accepted range.', 1;
    IF @BloodPressureDiastolic IS NOT NULL AND @BloodPressureDiastolic NOT BETWEEN 20 AND 200 THROW 51403, 'Diastolic blood pressure is outside the accepted range.', 1;
    IF @HeartRate IS NOT NULL AND @HeartRate NOT BETWEEN 20 AND 250 THROW 51403, 'Heart rate is outside the accepted range.', 1;
    IF @RespiratoryRate IS NOT NULL AND @RespiratoryRate NOT BETWEEN 5 AND 80 THROW 51403, 'Respiratory rate is outside the accepted range.', 1;
    IF @TemperatureCelsius IS NOT NULL AND @TemperatureCelsius NOT BETWEEN 25 AND 45 THROW 51403, 'Temperature is outside the accepted range.', 1;
    IF @OxygenSaturation IS NOT NULL AND @OxygenSaturation NOT BETWEEN 0 AND 100 THROW 51403, 'Oxygen saturation must be between 0 and 100.', 1;
    IF @HeightCm IS NOT NULL AND @HeightCm NOT BETWEEN 20 AND 260 THROW 51403, 'Height is outside the accepted range.', 1;
    IF @WeightKg IS NOT NULL AND @WeightKg NOT BETWEEN 1 AND 500 THROW 51403, 'Weight is outside the accepted range.', 1;
    IF @HeightCm IS NOT NULL AND @WeightKg IS NOT NULL
        SET @Bmi = ROUND(@WeightKg / POWER(@HeightCm / 100.0, 2), 2);

    BEGIN TRANSACTION;
    INSERT dbo.PatientVital(PatientVitalUid, PatientUid, RecordedAt, BloodPressureSystolic, BloodPressureDiastolic,
        HeartRate, RespiratoryRate, TemperatureCelsius, OxygenSaturation, HeightCm, WeightKg, Bmi, Notes, CreatedBy)
    VALUES(@PatientVitalUid, @PatientUid, @RecordedAt, @BloodPressureSystolic, @BloodPressureDiastolic,
        @HeartRate, @RespiratoryRate, @TemperatureCelsius, @OxygenSaturation, @HeightCm, @WeightKg, @Bmi,
        NULLIF(LTRIM(RTRIM(@Notes)), N''), @CreatedBy);
    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
        INSERT dbo.AuditLog(UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
        VALUES(@CreatedBy, @PatientId, N'Create', N'PatientVital', CONVERT(NVARCHAR(100), @PatientVitalUid), NULL, N'Vitals created', SYSUTCDATETIME());
    COMMIT TRANSACTION;

    EXEC dbo.PatientVital_GetByUid @PatientUid, @PatientVitalUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientVital_Update
    @PatientUid UNIQUEIDENTIFIER, @PatientVitalUid UNIQUEIDENTIFIER, @RecordedAt DATETIME2(0),
    @BloodPressureSystolic INT = NULL, @BloodPressureDiastolic INT = NULL,
    @HeartRate INT = NULL, @RespiratoryRate INT = NULL,
    @TemperatureCelsius DECIMAL(5,2) = NULL, @OxygenSaturation INT = NULL,
    @HeightCm DECIMAL(6,2) = NULL, @WeightKg DECIMAL(6,2) = NULL,
    @Notes NVARCHAR(1000) = NULL, @UpdatedBy BIGINT, @RowVersion BINARY(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @PatientId BIGINT, @Bmi DECIMAL(5,2) = NULL;

    SELECT @PatientId = PatientId FROM dbo.Patient WHERE PatientUid = @PatientUid AND IsDeleted = 0;
    IF @PatientId IS NULL THROW 51401, 'The requested patient was not found.', 1;
    IF @RecordedAt IS NULL THROW 51403, 'Recorded date and time are required.', 1;
    IF @BloodPressureSystolic IS NOT NULL AND @BloodPressureSystolic NOT BETWEEN 40 AND 300 THROW 51403, 'Systolic blood pressure is outside the accepted range.', 1;
    IF @BloodPressureDiastolic IS NOT NULL AND @BloodPressureDiastolic NOT BETWEEN 20 AND 200 THROW 51403, 'Diastolic blood pressure is outside the accepted range.', 1;
    IF @HeartRate IS NOT NULL AND @HeartRate NOT BETWEEN 20 AND 250 THROW 51403, 'Heart rate is outside the accepted range.', 1;
    IF @RespiratoryRate IS NOT NULL AND @RespiratoryRate NOT BETWEEN 5 AND 80 THROW 51403, 'Respiratory rate is outside the accepted range.', 1;
    IF @TemperatureCelsius IS NOT NULL AND @TemperatureCelsius NOT BETWEEN 25 AND 45 THROW 51403, 'Temperature is outside the accepted range.', 1;
    IF @OxygenSaturation IS NOT NULL AND @OxygenSaturation NOT BETWEEN 0 AND 100 THROW 51403, 'Oxygen saturation must be between 0 and 100.', 1;
    IF @HeightCm IS NOT NULL AND @HeightCm NOT BETWEEN 20 AND 260 THROW 51403, 'Height is outside the accepted range.', 1;
    IF @WeightKg IS NOT NULL AND @WeightKg NOT BETWEEN 1 AND 500 THROW 51403, 'Weight is outside the accepted range.', 1;
    IF @HeightCm IS NOT NULL AND @WeightKg IS NOT NULL
        SET @Bmi = ROUND(@WeightKg / POWER(@HeightCm / 100.0, 2), 2);

    BEGIN TRANSACTION;
    UPDATE dbo.PatientVital
    SET RecordedAt = @RecordedAt, BloodPressureSystolic = @BloodPressureSystolic,
        BloodPressureDiastolic = @BloodPressureDiastolic, HeartRate = @HeartRate,
        RespiratoryRate = @RespiratoryRate, TemperatureCelsius = @TemperatureCelsius,
        OxygenSaturation = @OxygenSaturation, HeightCm = @HeightCm, WeightKg = @WeightKg,
        Bmi = @Bmi, Notes = NULLIF(LTRIM(RTRIM(@Notes)), N''),
        UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UpdatedBy
    WHERE PatientUid = @PatientUid AND PatientVitalUid = @PatientVitalUid AND RowVersion = @RowVersion;

    IF @@ROWCOUNT = 0
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.PatientVital WHERE PatientUid = @PatientUid AND PatientVitalUid = @PatientVitalUid)
            THROW 51402, 'The vital record was changed by another user.', 1;
        ROLLBACK TRANSACTION;
        RETURN;
    END;

    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
        INSERT dbo.AuditLog(UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
        VALUES(@UpdatedBy, @PatientId, N'Update', N'PatientVital', CONVERT(NVARCHAR(100), @PatientVitalUid), NULL, N'Vitals updated', SYSUTCDATETIME());
    COMMIT TRANSACTION;

    EXEC dbo.PatientVital_GetByUid @PatientUid, @PatientVitalUid;
END;
GO
