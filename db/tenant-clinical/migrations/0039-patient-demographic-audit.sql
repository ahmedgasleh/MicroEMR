CREATE OR ALTER PROCEDURE dbo.Patient_Create
    @HealthCardNumber NVARCHAR(50) = NULL,
    @HealthCardVersion NVARCHAR(10) = NULL,
    @FirstName NVARCHAR(100),
    @MiddleName NVARCHAR(100) = NULL,
    @LastName NVARCHAR(100),
    @DateOfBirth DATE,
    @SexAtBirth NVARCHAR(20) = NULL,
    @GenderIdentity NVARCHAR(50) = NULL,
    @PreferredName NVARCHAR(100) = NULL,
    @PhoneNumber NVARCHAR(30) = NULL,
    @AlternatePhoneNumber NVARCHAR(30) = NULL,
    @Email NVARCHAR(255) = NULL,
    @AddressLine1 NVARCHAR(255) = NULL,
    @AddressLine2 NVARCHAR(255) = NULL,
    @City NVARCHAR(100) = NULL,
    @Province NVARCHAR(50) = NULL,
    @PostalCode NVARCHAR(20) = NULL,
    @CountryCode CHAR(2) = 'CA',
    @CreatedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NULLIF(LTRIM(RTRIM(@FirstName)), N'') IS NULL
       OR NULLIF(LTRIM(RTRIM(@LastName)), N'') IS NULL
        THROW 51022, 'Patient first and last names are required.', 1;

    DECLARE @PatientUid UNIQUEIDENTIFIER = NEWID();
    DECLARE @PatientId BIGINT;
    DECLARE @ChartNumber NVARCHAR(50) =
        N'P-' + UPPER(LEFT(REPLACE(CONVERT(NVARCHAR(36), @PatientUid), N'-', N''), 16));

    BEGIN TRANSACTION;

    INSERT dbo.Patient
    (
        PatientUid, ChartNumber, HealthCardNumber, HealthCardVersion,
        FirstName, MiddleName, LastName, DateOfBirth, SexAtBirth,
        GenderIdentity, PreferredName, PhoneNumber, AlternatePhoneNumber,
        Email, AddressLine1, AddressLine2, City, Province, PostalCode,
        CountryCode, IsActive, IsDeleted, CreatedAt, CreatedBy
    )
    VALUES
    (
        @PatientUid, @ChartNumber, NULLIF(LTRIM(RTRIM(@HealthCardNumber)), N''),
        NULLIF(LTRIM(RTRIM(@HealthCardVersion)), N''), LTRIM(RTRIM(@FirstName)),
        NULLIF(LTRIM(RTRIM(@MiddleName)), N''), LTRIM(RTRIM(@LastName)),
        @DateOfBirth, NULLIF(LTRIM(RTRIM(@SexAtBirth)), N''),
        NULLIF(LTRIM(RTRIM(@GenderIdentity)), N''),
        NULLIF(LTRIM(RTRIM(@PreferredName)), N''),
        NULLIF(LTRIM(RTRIM(@PhoneNumber)), N''),
        NULLIF(LTRIM(RTRIM(@AlternatePhoneNumber)), N''),
        NULLIF(LTRIM(RTRIM(@Email)), N''), NULLIF(LTRIM(RTRIM(@AddressLine1)), N''),
        NULLIF(LTRIM(RTRIM(@AddressLine2)), N''), NULLIF(LTRIM(RTRIM(@City)), N''),
        NULLIF(LTRIM(RTRIM(@Province)), N''), NULLIF(LTRIM(RTRIM(@PostalCode)), N''),
        COALESCE(NULLIF(LTRIM(RTRIM(@CountryCode)), ''), 'CA'),
        CONVERT(BIT, 1), CONVERT(BIT, 0), SYSUTCDATETIME(), @CreatedBy
    );

    SET @PatientId = SCOPE_IDENTITY();

    DECLARE @NewValue NVARCHAR(MAX) =
    (
        SELECT
            p.ChartNumber, p.HealthCardNumber, p.HealthCardVersion,
            p.FirstName, p.MiddleName, p.LastName, p.PreferredName,
            p.DateOfBirth, p.SexAtBirth, p.GenderIdentity,
            p.PhoneNumber, p.AlternatePhoneNumber, p.Email,
            p.AddressLine1, p.AddressLine2, p.City, p.Province,
            p.PostalCode, p.CountryCode, p.IsActive
        FROM dbo.Patient AS p
        WHERE p.PatientId = @PatientId
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    );

    INSERT dbo.AuditLog
        (UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
    VALUES
        (@CreatedBy, @PatientId, N'Create', N'Patient', CONVERT(NVARCHAR(100), @PatientUid),
         NULL, @NewValue, SYSUTCDATETIME());

    COMMIT TRANSACTION;

    EXEC dbo.Patient_GetByUid @PatientUid = @PatientUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.Patient_UpdateDemographics
    @PatientUid UNIQUEIDENTIFIER,
    @FirstName NVARCHAR(100),
    @MiddleName NVARCHAR(100) = NULL,
    @LastName NVARCHAR(100),
    @PreferredName NVARCHAR(100) = NULL,
    @DateOfBirth DATE,
    @SexAtBirth NVARCHAR(20) = NULL,
    @GenderIdentity NVARCHAR(50) = NULL,
    @HealthCardNumber NVARCHAR(50) = NULL,
    @HealthCardVersion NVARCHAR(10) = NULL,
    @PhoneNumber NVARCHAR(30) = NULL,
    @AlternatePhoneNumber NVARCHAR(30) = NULL,
    @Email NVARCHAR(255) = NULL,
    @AddressLine1 NVARCHAR(255) = NULL,
    @AddressLine2 NVARCHAR(255) = NULL,
    @City NVARCHAR(100) = NULL,
    @Province NVARCHAR(50) = NULL,
    @PostalCode NVARCHAR(20) = NULL,
    @CountryCode CHAR(2),
    @IsActive BIT,
    @UpdatedBy BIGINT = NULL,
    @RowVersion VARBINARY(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @PatientId BIGINT;
    DECLARE @OldValue NVARCHAR(MAX);
    DECLARE @NewValue NVARCHAR(MAX);

    SELECT
        @PatientId = p.PatientId,
        @OldValue =
        (
            SELECT
                p.ChartNumber, p.HealthCardNumber, p.HealthCardVersion,
                p.FirstName, p.MiddleName, p.LastName, p.PreferredName,
                p.DateOfBirth, p.SexAtBirth, p.GenderIdentity,
                p.PhoneNumber, p.AlternatePhoneNumber, p.Email,
                p.AddressLine1, p.AddressLine2, p.City, p.Province,
                p.PostalCode, p.CountryCode, p.IsActive
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
        )
    FROM dbo.Patient AS p
    WHERE p.PatientUid = @PatientUid
      AND p.IsDeleted = CONVERT(BIT, 0);

    IF @PatientId IS NULL
        THROW 51020, 'Patient was not found.', 1;

    BEGIN TRANSACTION;

    UPDATE dbo.Patient
    SET
        FirstName = LTRIM(RTRIM(@FirstName)),
        MiddleName = NULLIF(LTRIM(RTRIM(@MiddleName)), N''),
        LastName = LTRIM(RTRIM(@LastName)),
        PreferredName = NULLIF(LTRIM(RTRIM(@PreferredName)), N''),
        DateOfBirth = @DateOfBirth,
        SexAtBirth = NULLIF(LTRIM(RTRIM(@SexAtBirth)), N''),
        GenderIdentity = NULLIF(LTRIM(RTRIM(@GenderIdentity)), N''),
        HealthCardNumber = NULLIF(LTRIM(RTRIM(@HealthCardNumber)), N''),
        HealthCardVersion = NULLIF(LTRIM(RTRIM(@HealthCardVersion)), N''),
        PhoneNumber = NULLIF(LTRIM(RTRIM(@PhoneNumber)), N''),
        AlternatePhoneNumber = NULLIF(LTRIM(RTRIM(@AlternatePhoneNumber)), N''),
        Email = NULLIF(LTRIM(RTRIM(@Email)), N''),
        AddressLine1 = NULLIF(LTRIM(RTRIM(@AddressLine1)), N''),
        AddressLine2 = NULLIF(LTRIM(RTRIM(@AddressLine2)), N''),
        City = NULLIF(LTRIM(RTRIM(@City)), N''),
        Province = NULLIF(LTRIM(RTRIM(@Province)), N''),
        PostalCode = NULLIF(LTRIM(RTRIM(@PostalCode)), N''),
        CountryCode = LTRIM(RTRIM(@CountryCode)),
        IsActive = @IsActive,
        UpdatedAt = SYSUTCDATETIME(),
        UpdatedBy = @UpdatedBy
    WHERE PatientUid = @PatientUid
      AND IsDeleted = CONVERT(BIT, 0)
      AND RowVersion = @RowVersion;

    IF @@ROWCOUNT = 0
        THROW 51021, 'This patient was updated by another user. Reload the patient and try again.', 1;

    SELECT @NewValue =
    (
        SELECT
            p.ChartNumber, p.HealthCardNumber, p.HealthCardVersion,
            p.FirstName, p.MiddleName, p.LastName, p.PreferredName,
            p.DateOfBirth, p.SexAtBirth, p.GenderIdentity,
            p.PhoneNumber, p.AlternatePhoneNumber, p.Email,
            p.AddressLine1, p.AddressLine2, p.City, p.Province,
            p.PostalCode, p.CountryCode, p.IsActive
        FROM dbo.Patient AS p
        WHERE p.PatientId = @PatientId
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    );

    INSERT dbo.AuditLog
        (UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
    VALUES
        (@UpdatedBy, @PatientId, N'UpdateDemographics', N'Patient',
         CONVERT(NVARCHAR(100), @PatientUid), @OldValue, @NewValue, SYSUTCDATETIME());

    COMMIT TRANSACTION;

    EXEC dbo.Patient_GetByUid @PatientUid = @PatientUid;
END;
GO
