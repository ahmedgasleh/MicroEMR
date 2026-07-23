IF OBJECT_ID(N'dbo.PatientVital', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PatientVital
    (
        PatientVitalId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PatientVital PRIMARY KEY,
        PatientVitalUid UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PatientVital_Uid DEFAULT NEWSEQUENTIALID(),
        PatientUid UNIQUEIDENTIFIER NOT NULL,
        RecordedAt DATETIME2(0) NOT NULL,
        BloodPressureSystolic INT NULL,
        BloodPressureDiastolic INT NULL,
        HeartRate INT NULL,
        RespiratoryRate INT NULL,
        TemperatureCelsius DECIMAL(5,2) NULL,
        OxygenSaturation INT NULL,
        HeightCm DECIMAL(6,2) NULL,
        WeightKg DECIMAL(6,2) NULL,
        Bmi DECIMAL(5,2) NULL,
        Notes NVARCHAR(1000) NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_PatientVital_CreatedAt DEFAULT SYSUTCDATETIME(),
        CreatedBy BIGINT NULL,
        UpdatedAt DATETIME2(0) NULL,
        UpdatedBy BIGINT NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT UQ_PatientVital_Uid UNIQUE (PatientVitalUid)
    );
    CREATE INDEX IX_PatientVital_PatientUid_RecordedAt ON dbo.PatientVital(PatientUid, RecordedAt DESC);
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientVital_GetByPatientUid @PatientUid UNIQUEIDENTIFIER AS
BEGIN
    SET NOCOUNT ON;
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

CREATE OR ALTER PROCEDURE dbo.PatientVital_GetByUid @PatientUid UNIQUEIDENTIFIER, @PatientVitalUid UNIQUEIDENTIFIER AS
BEGIN
    SET NOCOUNT ON;
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
    @Notes NVARCHAR(1000) = NULL, @CreatedBy BIGINT = NULL AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @PatientId BIGINT, @PatientVitalUid UNIQUEIDENTIFIER = NEWID(), @Bmi DECIMAL(5,2) = NULL;
    SELECT @PatientId = PatientId FROM dbo.Patient WHERE PatientUid = @PatientUid AND IsDeleted = 0;
    IF @PatientId IS NULL THROW 51081, 'The requested patient was not found.', 1;
    IF @RecordedAt IS NULL THROW 51081, 'Recorded date and time are required.', 1;
    IF @HeightCm IS NOT NULL AND @HeightCm <= 0 THROW 51081, 'Height must be greater than zero.', 1;
    IF @WeightKg IS NOT NULL AND @WeightKg <= 0 THROW 51081, 'Weight must be greater than zero.', 1;
    IF @HeightCm > 0 AND @WeightKg > 0 SET @Bmi = ROUND(@WeightKg / POWER(@HeightCm / 100.0, 2), 2);
    BEGIN TRANSACTION;
    INSERT dbo.PatientVital(PatientVitalUid, PatientUid, RecordedAt, BloodPressureSystolic, BloodPressureDiastolic,
        HeartRate, RespiratoryRate, TemperatureCelsius, OxygenSaturation, HeightCm, WeightKg, Bmi, Notes, CreatedBy)
    VALUES(@PatientVitalUid, @PatientUid, @RecordedAt, @BloodPressureSystolic, @BloodPressureDiastolic,
        @HeartRate, @RespiratoryRate, @TemperatureCelsius, @OxygenSaturation, @HeightCm, @WeightKg, @Bmi,
        NULLIF(LTRIM(RTRIM(@Notes)), N''), @CreatedBy);
    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
        INSERT dbo.AuditLog(UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
        VALUES(@CreatedBy, @PatientId, N'Create', N'PatientVital', CONVERT(NVARCHAR(100), @PatientVitalUid), NULL, N'Vitals created', SYSUTCDATETIME());
    COMMIT;
    EXEC dbo.PatientVital_GetByUid @PatientUid, @PatientVitalUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientVital_Update
    @PatientUid UNIQUEIDENTIFIER, @PatientVitalUid UNIQUEIDENTIFIER, @RecordedAt DATETIME2(0),
    @BloodPressureSystolic INT = NULL, @BloodPressureDiastolic INT = NULL,
    @HeartRate INT = NULL, @RespiratoryRate INT = NULL,
    @TemperatureCelsius DECIMAL(5,2) = NULL, @OxygenSaturation INT = NULL,
    @HeightCm DECIMAL(6,2) = NULL, @WeightKg DECIMAL(6,2) = NULL,
    @Notes NVARCHAR(1000) = NULL, @UpdatedBy BIGINT = NULL AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @PatientId BIGINT, @Bmi DECIMAL(5,2) = NULL;
    IF @RecordedAt IS NULL THROW 51081, 'Recorded date and time are required.', 1;
    IF @HeightCm IS NOT NULL AND @HeightCm <= 0 THROW 51081, 'Height must be greater than zero.', 1;
    IF @WeightKg IS NOT NULL AND @WeightKg <= 0 THROW 51081, 'Weight must be greater than zero.', 1;
    IF @HeightCm > 0 AND @WeightKg > 0 SET @Bmi = ROUND(@WeightKg / POWER(@HeightCm / 100.0, 2), 2);
    SELECT @PatientId = p.PatientId FROM dbo.PatientVital pv JOIN dbo.Patient p ON p.PatientUid = pv.PatientUid
      WHERE pv.PatientUid = @PatientUid AND pv.PatientVitalUid = @PatientVitalUid AND p.IsDeleted = 0;
    IF @PatientId IS NULL RETURN;
    BEGIN TRANSACTION;
    UPDATE dbo.PatientVital SET RecordedAt=@RecordedAt, BloodPressureSystolic=@BloodPressureSystolic,
        BloodPressureDiastolic=@BloodPressureDiastolic, HeartRate=@HeartRate, RespiratoryRate=@RespiratoryRate,
        TemperatureCelsius=@TemperatureCelsius, OxygenSaturation=@OxygenSaturation, HeightCm=@HeightCm,
        WeightKg=@WeightKg, Bmi=@Bmi, Notes=NULLIF(LTRIM(RTRIM(@Notes)), N''),
        UpdatedAt=SYSUTCDATETIME(), UpdatedBy=@UpdatedBy
    WHERE PatientUid=@PatientUid AND PatientVitalUid=@PatientVitalUid;
    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
        INSERT dbo.AuditLog(UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
        VALUES(@UpdatedBy, @PatientId, N'Update', N'PatientVital', CONVERT(NVARCHAR(100), @PatientVitalUid), NULL, N'Vitals updated', SYSUTCDATETIME());
    COMMIT;
    EXEC dbo.PatientVital_GetByUid @PatientUid, @PatientVitalUid;
END;
GO
