SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.ClinicProfile', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ClinicProfile
    (
        ClinicProfileId TINYINT NOT NULL
            CONSTRAINT PK_ClinicProfile PRIMARY KEY
            CONSTRAINT CK_ClinicProfile_Singleton CHECK (ClinicProfileId = 1),
        LegalName NVARCHAR(200) NULL,
        Phone NVARCHAR(50) NULL,
        Fax NVARCHAR(50) NULL,
        Email NVARCHAR(254) NULL,
        AddressLine1 NVARCHAR(200) NULL,
        AddressLine2 NVARCHAR(200) NULL,
        City NVARCHAR(100) NULL,
        ProvinceState NVARCHAR(100) NULL,
        PostalCode NVARCHAR(30) NULL,
        Country NVARCHAR(100) NULL,
        DefaultAppointmentDurationMinutes INT NULL,
        UpdatedAtUtc DATETIME2(7) NOT NULL,
        UpdatedBy BIGINT NOT NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_ClinicProfile_DefaultDuration
            CHECK (DefaultAppointmentDurationMinutes IS NULL OR
                   DefaultAppointmentDurationMinutes BETWEEN 5 AND 240),
        CONSTRAINT FK_ClinicProfile_UpdatedBy
            FOREIGN KEY (UpdatedBy) REFERENCES dbo.ApplicationUser(UserId)
    );
END;
GO

CREATE OR ALTER PROCEDURE dbo.ClinicProfile_Get
AS
BEGIN
    SET NOCOUNT ON;

    SELECT LegalName, Phone, Fax, Email, AddressLine1, AddressLine2, City,
           ProvinceState, PostalCode, Country, DefaultAppointmentDurationMinutes,
           UpdatedAtUtc, UpdatedBy, RowVersion
    FROM dbo.ClinicProfile
    WHERE ClinicProfileId = 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ClinicProfile_Save
    @LegalName NVARCHAR(200) = NULL,
    @Phone NVARCHAR(50) = NULL,
    @Fax NVARCHAR(50) = NULL,
    @Email NVARCHAR(254) = NULL,
    @AddressLine1 NVARCHAR(200) = NULL,
    @AddressLine2 NVARCHAR(200) = NULL,
    @City NVARCHAR(100) = NULL,
    @ProvinceState NVARCHAR(100) = NULL,
    @PostalCode NVARCHAR(30) = NULL,
    @Country NVARCHAR(100) = NULL,
    @DefaultAppointmentDurationMinutes INT = NULL,
    @UpdatedBy BIGINT,
    @ExpectedRowVersion BINARY(8) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @UpdatedBy IS NULL THROW 51802, 'An authenticated clinical user is required.', 1;
    IF @DefaultAppointmentDurationMinutes IS NOT NULL AND
       @DefaultAppointmentDurationMinutes NOT BETWEEN 5 AND 240
        THROW 51803, 'Default appointment duration is invalid.', 1;

    SET @LegalName = NULLIF(LTRIM(RTRIM(@LegalName)), N'');
    SET @Phone = NULLIF(LTRIM(RTRIM(@Phone)), N'');
    SET @Fax = NULLIF(LTRIM(RTRIM(@Fax)), N'');
    SET @Email = NULLIF(LTRIM(RTRIM(@Email)), N'');
    SET @AddressLine1 = NULLIF(LTRIM(RTRIM(@AddressLine1)), N'');
    SET @AddressLine2 = NULLIF(LTRIM(RTRIM(@AddressLine2)), N'');
    SET @City = NULLIF(LTRIM(RTRIM(@City)), N'');
    SET @ProvinceState = NULLIF(LTRIM(RTRIM(@ProvinceState)), N'');
    SET @PostalCode = NULLIF(LTRIM(RTRIM(@PostalCode)), N'');
    SET @Country = NULLIF(LTRIM(RTRIM(@Country)), N'');

    BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @OldValue NVARCHAR(MAX);
    SELECT @OldValue = (SELECT LegalName, Phone, Fax, Email, AddressLine1, AddressLine2,
        City, ProvinceState, PostalCode, Country, DefaultAppointmentDurationMinutes
        FROM dbo.ClinicProfile WITH (UPDLOCK, HOLDLOCK)
        WHERE ClinicProfileId = 1 FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);

    IF EXISTS (SELECT 1 FROM dbo.ClinicProfile WITH (UPDLOCK, HOLDLOCK) WHERE ClinicProfileId = 1)
    BEGIN
        IF @ExpectedRowVersion IS NULL OR NOT EXISTS
           (SELECT 1 FROM dbo.ClinicProfile WHERE ClinicProfileId = 1 AND RowVersion = @ExpectedRowVersion)
            THROW 51801, 'Clinic configuration has changed.', 1;

        UPDATE dbo.ClinicProfile
        SET LegalName=@LegalName, Phone=@Phone, Fax=@Fax, Email=@Email,
            AddressLine1=@AddressLine1, AddressLine2=@AddressLine2, City=@City,
            ProvinceState=@ProvinceState, PostalCode=@PostalCode, Country=@Country,
            DefaultAppointmentDurationMinutes=@DefaultAppointmentDurationMinutes,
            UpdatedAtUtc=SYSUTCDATETIME(), UpdatedBy=@UpdatedBy
        WHERE ClinicProfileId=1;
    END
    ELSE
    BEGIN
        IF @ExpectedRowVersion IS NOT NULL THROW 51801, 'Clinic configuration has changed.', 1;
        INSERT dbo.ClinicProfile
            (ClinicProfileId, LegalName, Phone, Fax, Email, AddressLine1, AddressLine2,
             City, ProvinceState, PostalCode, Country, DefaultAppointmentDurationMinutes,
             UpdatedAtUtc, UpdatedBy)
        VALUES
            (1, @LegalName, @Phone, @Fax, @Email, @AddressLine1, @AddressLine2,
             @City, @ProvinceState, @PostalCode, @Country, @DefaultAppointmentDurationMinutes,
             SYSUTCDATETIME(), @UpdatedBy);
    END;

    DECLARE @NewValue NVARCHAR(MAX);
    SELECT @NewValue = (SELECT LegalName, Phone, Fax, Email, AddressLine1, AddressLine2,
        City, ProvinceState, PostalCode, Country, DefaultAppointmentDurationMinutes
        FROM dbo.ClinicProfile WHERE ClinicProfileId = 1 FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);

    INSERT dbo.AuditLog(UserId, ActionName, EntityName, EntityId, OldValue, NewValue)
    VALUES(@UpdatedBy, CASE WHEN @OldValue IS NULL THEN N'Create' ELSE N'Update' END,
           N'ClinicProfile', N'1', @OldValue, @NewValue);

    COMMIT TRANSACTION;
    EXEC dbo.ClinicProfile_Get;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
