/*
    Patient demographic update stored procedures.
*/

IF OBJECT_ID(N'dbo.Patient', N'U') IS NULL
BEGIN
    THROW 51030, 'Required table dbo.Patient was not found.', 1;
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

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.Patient AS p
        WHERE p.PatientUid = @PatientUid
            AND p.IsDeleted = CONVERT(BIT, 0)
    )
    BEGIN
        THROW 51020, 'Patient was not found.', 1;
    END;

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
    BEGIN
        THROW 51021, 'This patient was updated by another user. Reload the patient and try again.', 1;
    END;

    EXEC dbo.Patient_GetByUid
        @PatientUid = @PatientUid;
END;
GO
