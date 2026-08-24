SET XACT_ABORT ON;
GO

CREATE TABLE dbo.PatientImmunization
(
    PatientImmunizationId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PatientImmunization PRIMARY KEY,
    ImmunizationUid UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PatientImmunization_Uid DEFAULT NEWSEQUENTIALID(),
    PatientUid UNIQUEIDENTIFIER NOT NULL,
    VaccineName NVARCHAR(200) NOT NULL,
    AdministrationDate DATE NOT NULL,
    DoseNumber INT NULL,
    Route NVARCHAR(100) NULL,
    Site NVARCHAR(100) NULL,
    LotNumber NVARCHAR(100) NULL,
    SourceType NVARCHAR(30) NOT NULL,
    SourceDescription NVARCHAR(500) NULL,
    AdministeredByName NVARCHAR(200) NULL,
    EncounterUid UNIQUEIDENTIFIER NULL,
    Notes NVARCHAR(1000) NULL,
    Status NVARCHAR(30) NOT NULL CONSTRAINT DF_PatientImmunization_Status DEFAULT N'Completed',
    CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_PatientImmunization_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    CreatedBy BIGINT NOT NULL,
    UpdatedAtUtc DATETIME2(0) NULL,
    UpdatedBy BIGINT NULL,
    EnteredInErrorAtUtc DATETIME2(0) NULL,
    EnteredInErrorBy BIGINT NULL,
    EnteredInErrorReason NVARCHAR(500) NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT UQ_PatientImmunization_Uid UNIQUE (ImmunizationUid),
    CONSTRAINT FK_PatientImmunization_Patient FOREIGN KEY (PatientUid) REFERENCES dbo.Patient(PatientUid),
    CONSTRAINT FK_PatientImmunization_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.ApplicationUser(UserId),
    CONSTRAINT FK_PatientImmunization_UpdatedBy FOREIGN KEY (UpdatedBy) REFERENCES dbo.ApplicationUser(UserId),
    CONSTRAINT FK_PatientImmunization_EnteredInErrorBy FOREIGN KEY (EnteredInErrorBy) REFERENCES dbo.ApplicationUser(UserId),
    CONSTRAINT CK_PatientImmunization_SourceType CHECK (SourceType IN (N'ClinicAdministered', N'HistoricalExternal')),
    CONSTRAINT CK_PatientImmunization_Status CHECK (Status IN (N'Completed', N'EnteredInError')),
    CONSTRAINT CK_PatientImmunization_DoseNumber CHECK (DoseNumber IS NULL OR DoseNumber > 0),
    CONSTRAINT CK_PatientImmunization_ClinicAdministrator CHECK (SourceType <> N'ClinicAdministered' OR NULLIF(LTRIM(RTRIM(AdministeredByName)), N'') IS NOT NULL),
    CONSTRAINT CK_PatientImmunization_ErrorMetadata CHECK
        ((Status = N'Completed' AND EnteredInErrorAtUtc IS NULL AND EnteredInErrorBy IS NULL AND EnteredInErrorReason IS NULL)
         OR (Status = N'EnteredInError' AND EnteredInErrorAtUtc IS NOT NULL AND EnteredInErrorBy IS NOT NULL AND NULLIF(LTRIM(RTRIM(EnteredInErrorReason)), N'') IS NOT NULL))
);
GO

CREATE INDEX IX_PatientImmunization_Patient_Status_Date
    ON dbo.PatientImmunization(PatientUid, Status, AdministrationDate DESC);
GO

CREATE OR ALTER PROCEDURE dbo.PatientImmunization_ListByPatient
    @PatientUid UNIQUEIDENTIFIER,
    @Status NVARCHAR(30) = N'All'
AS
BEGIN
    SET NOCOUNT ON;
    IF @Status NOT IN (N'Completed', N'EnteredInError', N'All') SET @Status = N'All';
    SELECT i.ImmunizationUid, i.PatientUid, i.VaccineName, i.AdministrationDate, i.DoseNumber,
        i.Route, i.Site, i.LotNumber, i.SourceType, i.SourceDescription, i.AdministeredByName,
        i.EncounterUid, i.Notes, i.Status, i.CreatedAtUtc, i.CreatedBy,
        cu.DisplayName AS CreatedByDisplayName, i.UpdatedAtUtc, i.UpdatedBy,
        uu.DisplayName AS UpdatedByDisplayName, i.EnteredInErrorAtUtc, i.EnteredInErrorBy,
        eu.DisplayName AS EnteredInErrorByDisplayName, i.EnteredInErrorReason, i.RowVersion
    FROM dbo.PatientImmunization AS i
    LEFT JOIN dbo.ApplicationUser AS cu ON cu.UserId = i.CreatedBy
    LEFT JOIN dbo.ApplicationUser AS uu ON uu.UserId = i.UpdatedBy
    LEFT JOIN dbo.ApplicationUser AS eu ON eu.UserId = i.EnteredInErrorBy
    WHERE i.PatientUid = @PatientUid AND (@Status = N'All' OR i.Status = @Status)
    ORDER BY i.AdministrationDate DESC, i.CreatedAtUtc DESC, i.ImmunizationUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientImmunization_GetByUid
    @PatientUid UNIQUEIDENTIFIER,
    @ImmunizationUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT i.ImmunizationUid, i.PatientUid, i.VaccineName, i.AdministrationDate, i.DoseNumber,
        i.Route, i.Site, i.LotNumber, i.SourceType, i.SourceDescription, i.AdministeredByName,
        i.EncounterUid, i.Notes, i.Status, i.CreatedAtUtc, i.CreatedBy,
        cu.DisplayName AS CreatedByDisplayName, i.UpdatedAtUtc, i.UpdatedBy,
        uu.DisplayName AS UpdatedByDisplayName, i.EnteredInErrorAtUtc, i.EnteredInErrorBy,
        eu.DisplayName AS EnteredInErrorByDisplayName, i.EnteredInErrorReason, i.RowVersion
    FROM dbo.PatientImmunization AS i
    LEFT JOIN dbo.ApplicationUser AS cu ON cu.UserId = i.CreatedBy
    LEFT JOIN dbo.ApplicationUser AS uu ON uu.UserId = i.UpdatedBy
    LEFT JOIN dbo.ApplicationUser AS eu ON eu.UserId = i.EnteredInErrorBy
    WHERE i.PatientUid = @PatientUid AND i.ImmunizationUid = @ImmunizationUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientImmunization_Create
    @PatientUid UNIQUEIDENTIFIER, @VaccineName NVARCHAR(200), @AdministrationDate DATE,
    @DoseNumber INT = NULL, @Route NVARCHAR(100) = NULL, @Site NVARCHAR(100) = NULL,
    @LotNumber NVARCHAR(100) = NULL, @SourceType NVARCHAR(30), @SourceDescription NVARCHAR(500) = NULL,
    @AdministeredByName NVARCHAR(200) = NULL, @EncounterUid UNIQUEIDENTIFIER = NULL,
    @Notes NVARCHAR(1000) = NULL, @Actor BIGINT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF NULLIF(LTRIM(RTRIM(@VaccineName)), N'') IS NULL THROW 52300, 'Vaccine name is required.', 1;
    IF @AdministrationDate IS NULL OR @AdministrationDate > CONVERT(DATE, SYSUTCDATETIME()) THROW 52301, 'Administration date is invalid.', 1;
    IF @DoseNumber IS NOT NULL AND @DoseNumber <= 0 THROW 52302, 'Dose number must be positive.', 1;
    IF @SourceType NOT IN (N'ClinicAdministered', N'HistoricalExternal') THROW 52303, 'Source type is invalid.', 1;
    IF @SourceType = N'ClinicAdministered' AND NULLIF(LTRIM(RTRIM(@AdministeredByName)), N'') IS NULL THROW 52304, 'Administered by is required.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.ApplicationUser WHERE UserId = @Actor AND IsActive = 1) THROW 52306, 'Active clinical actor was not found.', 1;
    DECLARE @PatientId BIGINT = (SELECT PatientId FROM dbo.Patient WHERE PatientUid = @PatientUid AND IsDeleted = 0);
    IF @PatientId IS NULL THROW 52305, 'Patient was not found.', 1;
    IF @EncounterUid IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.PatientEncounter WHERE EncounterUid = @EncounterUid AND PatientUid = @PatientUid) THROW 52307, 'Encounter was not found for patient.', 1;
    DECLARE @ImmunizationUid UNIQUEIDENTIFIER = NEWID();
    BEGIN TRANSACTION;
    INSERT dbo.PatientImmunization(ImmunizationUid,PatientUid,VaccineName,AdministrationDate,DoseNumber,Route,Site,LotNumber,SourceType,SourceDescription,AdministeredByName,EncounterUid,Notes,Status,CreatedBy)
    VALUES(@ImmunizationUid,@PatientUid,LTRIM(RTRIM(@VaccineName)),@AdministrationDate,@DoseNumber,NULLIF(LTRIM(RTRIM(@Route)),N''),NULLIF(LTRIM(RTRIM(@Site)),N''),NULLIF(LTRIM(RTRIM(@LotNumber)),N''),@SourceType,NULLIF(LTRIM(RTRIM(@SourceDescription)),N''),NULLIF(LTRIM(RTRIM(@AdministeredByName)),N''),@EncounterUid,NULLIF(LTRIM(RTRIM(@Notes)),N''),N'Completed',@Actor);
    INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt)
    VALUES(@Actor,@PatientId,N'ImmunizationCreated',N'PatientImmunization',CONVERT(NVARCHAR(100),@ImmunizationUid),
        (SELECT @PatientUid AS PatientUid,@ImmunizationUid AS ImmunizationUid,LTRIM(RTRIM(@VaccineName)) AS VaccineName,@AdministrationDate AS AdministrationDate,@SourceType AS SourceType,N'Completed' AS Status FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),SYSUTCDATETIME());
    COMMIT;
    EXEC dbo.PatientImmunization_GetByUid @PatientUid,@ImmunizationUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientImmunization_Update
    @PatientUid UNIQUEIDENTIFIER, @ImmunizationUid UNIQUEIDENTIFIER,
    @VaccineName NVARCHAR(200), @AdministrationDate DATE, @DoseNumber INT = NULL,
    @Route NVARCHAR(100) = NULL, @Site NVARCHAR(100) = NULL, @LotNumber NVARCHAR(100) = NULL,
    @SourceType NVARCHAR(30), @SourceDescription NVARCHAR(500) = NULL,
    @AdministeredByName NVARCHAR(200) = NULL, @EncounterUid UNIQUEIDENTIFIER = NULL,
    @Notes NVARCHAR(1000) = NULL, @ExpectedRowVersion BINARY(8), @Actor BIGINT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF NULLIF(LTRIM(RTRIM(@VaccineName)), N'') IS NULL THROW 52300, 'Vaccine name is required.', 1;
    IF @AdministrationDate IS NULL OR @AdministrationDate > CONVERT(DATE, SYSUTCDATETIME()) THROW 52301, 'Administration date is invalid.', 1;
    IF @DoseNumber IS NOT NULL AND @DoseNumber <= 0 THROW 52302, 'Dose number must be positive.', 1;
    IF @SourceType NOT IN (N'ClinicAdministered', N'HistoricalExternal') THROW 52303, 'Source type is invalid.', 1;
    IF @SourceType = N'ClinicAdministered' AND NULLIF(LTRIM(RTRIM(@AdministeredByName)), N'') IS NULL THROW 52304, 'Administered by is required.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.ApplicationUser WHERE UserId = @Actor AND IsActive = 1) THROW 52306, 'Active clinical actor was not found.', 1;
    IF @EncounterUid IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.PatientEncounter WHERE EncounterUid=@EncounterUid AND PatientUid=@PatientUid) THROW 52307, 'Encounter was not found for patient.', 1;
    DECLARE @PatientId BIGINT,@CurrentVersion BINARY(8),@Status NVARCHAR(30),@OldValue NVARCHAR(MAX);
    BEGIN TRANSACTION;
    SELECT @PatientId=p.PatientId,@CurrentVersion=i.RowVersion,@Status=i.Status,
        @OldValue=(SELECT i.PatientUid,i.ImmunizationUid,i.VaccineName,i.AdministrationDate,i.SourceType,i.Status FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)
    FROM dbo.PatientImmunization i WITH (UPDLOCK,HOLDLOCK) JOIN dbo.Patient p ON p.PatientUid=i.PatientUid AND p.IsDeleted=0
    WHERE i.PatientUid=@PatientUid AND i.ImmunizationUid=@ImmunizationUid;
    IF @PatientId IS NULL BEGIN ROLLBACK; RETURN; END;
    IF @CurrentVersion<>@ExpectedRowVersion BEGIN ROLLBACK; THROW 52308, 'Immunization was changed by another user.', 1; END;
    IF @Status<>N'Completed' BEGIN ROLLBACK; THROW 52309, 'Entered-in-error immunizations cannot be edited.', 1; END;
    UPDATE dbo.PatientImmunization SET VaccineName=LTRIM(RTRIM(@VaccineName)),AdministrationDate=@AdministrationDate,DoseNumber=@DoseNumber,
        Route=NULLIF(LTRIM(RTRIM(@Route)),N''),Site=NULLIF(LTRIM(RTRIM(@Site)),N''),LotNumber=NULLIF(LTRIM(RTRIM(@LotNumber)),N''),
        SourceType=@SourceType,SourceDescription=NULLIF(LTRIM(RTRIM(@SourceDescription)),N''),AdministeredByName=NULLIF(LTRIM(RTRIM(@AdministeredByName)),N''),
        EncounterUid=@EncounterUid,Notes=NULLIF(LTRIM(RTRIM(@Notes)),N''),UpdatedAtUtc=SYSUTCDATETIME(),UpdatedBy=@Actor
    WHERE PatientUid=@PatientUid AND ImmunizationUid=@ImmunizationUid AND RowVersion=@ExpectedRowVersion;
    DECLARE @NewValue NVARCHAR(MAX)=(SELECT @PatientUid AS PatientUid,@ImmunizationUid AS ImmunizationUid,LTRIM(RTRIM(@VaccineName)) AS VaccineName,@AdministrationDate AS AdministrationDate,@SourceType AS SourceType,N'Completed' AS Status FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
    INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
    VALUES(@Actor,@PatientId,N'ImmunizationUpdated',N'PatientImmunization',CONVERT(NVARCHAR(100),@ImmunizationUid),@OldValue,@NewValue,SYSUTCDATETIME());
    COMMIT;
    EXEC dbo.PatientImmunization_GetByUid @PatientUid,@ImmunizationUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientImmunization_MarkEnteredInError
    @PatientUid UNIQUEIDENTIFIER, @ImmunizationUid UNIQUEIDENTIFIER,
    @Reason NVARCHAR(500), @ExpectedRowVersion BINARY(8), @Actor BIGINT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF NULLIF(LTRIM(RTRIM(@Reason)),N'') IS NULL THROW 52310, 'Entered-in-error reason is required.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.ApplicationUser WHERE UserId = @Actor AND IsActive = 1) THROW 52306, 'Active clinical actor was not found.', 1;
    DECLARE @PatientId BIGINT,@CurrentVersion BINARY(8),@Status NVARCHAR(30);
    BEGIN TRANSACTION;
    SELECT @PatientId=p.PatientId,@CurrentVersion=i.RowVersion,@Status=i.Status
    FROM dbo.PatientImmunization i WITH (UPDLOCK,HOLDLOCK) JOIN dbo.Patient p ON p.PatientUid=i.PatientUid AND p.IsDeleted=0
    WHERE i.PatientUid=@PatientUid AND i.ImmunizationUid=@ImmunizationUid;
    IF @PatientId IS NULL BEGIN ROLLBACK; RETURN; END;
    IF @CurrentVersion<>@ExpectedRowVersion BEGIN ROLLBACK; THROW 52308, 'Immunization was changed by another user.', 1; END;
    IF @Status<>N'Completed' BEGIN ROLLBACK; THROW 52309, 'Immunization is already entered in error.', 1; END;
    UPDATE dbo.PatientImmunization SET Status=N'EnteredInError',UpdatedAtUtc=SYSUTCDATETIME(),UpdatedBy=@Actor,
        EnteredInErrorAtUtc=SYSUTCDATETIME(),EnteredInErrorBy=@Actor,EnteredInErrorReason=LTRIM(RTRIM(@Reason))
    WHERE PatientUid=@PatientUid AND ImmunizationUid=@ImmunizationUid AND RowVersion=@ExpectedRowVersion;
    INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,OldValue,NewValue,CreatedAt)
    VALUES(@Actor,@PatientId,N'ImmunizationEnteredInError',N'PatientImmunization',CONVERT(NVARCHAR(100),@ImmunizationUid),N'Status=Completed',
        (SELECT N'EnteredInError' AS Status,LTRIM(RTRIM(@Reason)) AS Reason FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),SYSUTCDATETIME());
    COMMIT;
    EXEC dbo.PatientImmunization_GetByUid @PatientUid,@ImmunizationUid;
END;
GO
