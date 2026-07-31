/*
    Patient demographic update stored procedures.
*/

IF OBJECT_ID(N'dbo.Patient', N'U') IS NULL
BEGIN
    THROW 51030, 'Required table dbo.Patient was not found.', 1;
END;
GO

IF COL_LENGTH('dbo.Patient', 'MiddleName') IS NULL
    ALTER TABLE dbo.Patient ADD MiddleName NVARCHAR(100) NULL;
IF COL_LENGTH('dbo.Patient', 'PreferredName') IS NULL
    ALTER TABLE dbo.Patient ADD PreferredName NVARCHAR(100) NULL;
IF COL_LENGTH('dbo.Patient', 'SexAtBirth') IS NULL
BEGIN
    ALTER TABLE dbo.Patient ADD SexAtBirth NVARCHAR(20) NULL;
    IF COL_LENGTH('dbo.Patient', 'Sex') IS NOT NULL
        EXEC(N'UPDATE dbo.Patient SET SexAtBirth = Sex WHERE SexAtBirth IS NULL;');
END;
IF COL_LENGTH('dbo.Patient', 'GenderIdentity') IS NULL
    ALTER TABLE dbo.Patient ADD GenderIdentity NVARCHAR(50) NULL;
IF COL_LENGTH('dbo.Patient', 'AlternatePhoneNumber') IS NULL
    ALTER TABLE dbo.Patient ADD AlternatePhoneNumber NVARCHAR(30) NULL;
IF COL_LENGTH('dbo.Patient', 'CountryCode') IS NULL
    ALTER TABLE dbo.Patient ADD CountryCode CHAR(2) NOT NULL
        CONSTRAINT DF_Patient_CountryCode DEFAULT 'CA' WITH VALUES;
IF COL_LENGTH('dbo.Patient', 'RowVersion') IS NULL
    ALTER TABLE dbo.Patient ADD RowVersion ROWVERSION;
GO

CREATE OR ALTER PROCEDURE dbo.Patient_GetByUid
    @PatientUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        PatientUid, ChartNumber, HealthCardNumber, HealthCardVersion,
        FirstName, MiddleName, LastName, DateOfBirth, SexAtBirth,
        GenderIdentity, PreferredName, PhoneNumber, AlternatePhoneNumber,
        Email, AddressLine1, AddressLine2, City, Province, PostalCode,
        CountryCode, IsActive, CreatedAt, UpdatedAt, RowVersion
    FROM dbo.Patient
    WHERE PatientUid = @PatientUid
      AND IsDeleted = CONVERT(BIT, 0);
END;
GO

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

    IF NULLIF(LTRIM(RTRIM(@FirstName)), N'') IS NULL
       OR NULLIF(LTRIM(RTRIM(@LastName)), N'') IS NULL
        THROW 51022, 'Patient first and last names are required.', 1;

    DECLARE @PatientUid UNIQUEIDENTIFIER = NEWID();
    DECLARE @ChartNumber NVARCHAR(50) =
        N'P-' + UPPER(LEFT(REPLACE(CONVERT(NVARCHAR(36), @PatientUid), N'-', N''), 16));

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
CREATE OR ALTER PROCEDURE dbo.Patient_Search
    @SearchText NVARCHAR(200) = NULL,
    @DateOfBirth DATE = NULL,
    @PageNumber INT = 1,
    @PageSize INT = 25,
    @IncludeInactive BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET @PageNumber = CASE WHEN @PageNumber < 1 THEN 1 ELSE @PageNumber END;
    SET @PageSize = CASE WHEN @PageSize < 1 THEN 25 WHEN @PageSize > 100 THEN 100 ELSE @PageSize END;
    DECLARE @Term NVARCHAR(200) = NULLIF(LTRIM(RTRIM(@SearchText)), N'');

    ;WITH Matches AS
    (
        SELECT p.*, COUNT(*) OVER() AS TotalRows,
            CASE WHEN p.ChartNumber = @Term THEN 0 WHEN p.HealthCardNumber = @Term THEN 1
                 WHEN p.LastName = @Term OR p.FirstName = @Term THEN 2 ELSE 3 END AS MatchRank
        FROM dbo.Patient p
        WHERE p.IsDeleted = 0
          AND (@IncludeInactive = 1 OR p.IsActive = 1)
          AND (@DateOfBirth IS NULL OR p.DateOfBirth = @DateOfBirth)
          AND (@Term IS NULL OR p.FirstName LIKE N'%' + @Term + N'%' OR p.LastName LIKE N'%' + @Term + N'%'
               OR p.PreferredName LIKE N'%' + @Term + N'%' OR p.ChartNumber LIKE N'%' + @Term + N'%'
               OR p.HealthCardNumber LIKE N'%' + @Term + N'%' OR p.PhoneNumber LIKE N'%' + @Term + N'%'
               OR CONCAT(p.FirstName,N' ',p.LastName) LIKE N'%' + @Term + N'%')
    )
    SELECT PatientUid,ChartNumber,FirstName,MiddleName,LastName,PreferredName,DateOfBirth,SexAtBirth,
           HealthCardNumber,HealthCardVersion,PhoneNumber,Email,IsActive,CONVERT(INT,TotalRows) TotalRows
    FROM Matches ORDER BY MatchRank,LastName,FirstName,COALESCE(UpdatedAt,CreatedAt) DESC
    OFFSET (@PageNumber-1)*@PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END;
GO
