SET XACT_ABORT ON;
GO

CREATE TABLE dbo.PatientClinicalHistory
(
    HistoryId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PatientClinicalHistory PRIMARY KEY,
    HistoryUid UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PatientClinicalHistory_Uid DEFAULT NEWSEQUENTIALID(),
    PatientUid UNIQUEIDENTIFIER NOT NULL,
    HistoryType NVARCHAR(20) NOT NULL,
    Description NVARCHAR(1000) NOT NULL,
    RelevantDate DATE NULL,
    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_PatientClinicalHistory_Status DEFAULT N'Active',
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_PatientClinicalHistory_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy BIGINT NOT NULL,
    UpdatedAt DATETIME2(0) NULL,
    UpdatedBy BIGINT NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT UQ_PatientClinicalHistory_Uid UNIQUE (HistoryUid),
    CONSTRAINT FK_PatientClinicalHistory_Patient FOREIGN KEY (PatientUid) REFERENCES dbo.Patient(PatientUid),
    CONSTRAINT FK_PatientClinicalHistory_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.ApplicationUser(UserId),
    CONSTRAINT FK_PatientClinicalHistory_UpdatedBy FOREIGN KEY (UpdatedBy) REFERENCES dbo.ApplicationUser(UserId),
    CONSTRAINT CK_PatientClinicalHistory_Type CHECK (HistoryType IN (N'Medical', N'Surgical')),
    CONSTRAINT CK_PatientClinicalHistory_Status CHECK (Status IN (N'Active', N'Archived'))
);
GO

CREATE INDEX IX_PatientClinicalHistory_Patient_Status_Type
    ON dbo.PatientClinicalHistory(PatientUid, Status, HistoryType, RelevantDate DESC);
GO

CREATE OR ALTER PROCEDURE dbo.PatientClinicalHistory_List
    @PatientUid UNIQUEIDENTIFIER,
    @Status NVARCHAR(20) = N'Active'
AS
BEGIN
    SET NOCOUNT ON;
    IF @Status NOT IN (N'Active', N'Archived', N'All') SET @Status = N'Active';
    SELECT h.HistoryUid, h.PatientUid, h.HistoryType, h.Description, h.RelevantDate,
        h.Status, h.CreatedAt, h.CreatedBy, cu.DisplayName AS CreatedByDisplayName,
        h.UpdatedAt, h.UpdatedBy, uu.DisplayName AS UpdatedByDisplayName, h.RowVersion
    FROM dbo.PatientClinicalHistory AS h
    LEFT JOIN dbo.ApplicationUser AS cu ON cu.UserId = h.CreatedBy
    LEFT JOIN dbo.ApplicationUser AS uu ON uu.UserId = h.UpdatedBy
    WHERE h.PatientUid = @PatientUid AND (@Status = N'All' OR h.Status = @Status)
    ORDER BY CASE h.HistoryType WHEN N'Medical' THEN 0 ELSE 1 END,
        COALESCE(h.RelevantDate, CONVERT(DATE, h.CreatedAt)) DESC, h.CreatedAt DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientClinicalHistory_Get
    @PatientUid UNIQUEIDENTIFIER,
    @HistoryUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT h.HistoryUid, h.PatientUid, h.HistoryType, h.Description, h.RelevantDate,
        h.Status, h.CreatedAt, h.CreatedBy, cu.DisplayName AS CreatedByDisplayName,
        h.UpdatedAt, h.UpdatedBy, uu.DisplayName AS UpdatedByDisplayName, h.RowVersion
    FROM dbo.PatientClinicalHistory AS h
    LEFT JOIN dbo.ApplicationUser AS cu ON cu.UserId = h.CreatedBy
    LEFT JOIN dbo.ApplicationUser AS uu ON uu.UserId = h.UpdatedBy
    WHERE h.PatientUid = @PatientUid AND h.HistoryUid = @HistoryUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientClinicalHistory_Create
    @PatientUid UNIQUEIDENTIFIER,
    @HistoryType NVARCHAR(20),
    @Description NVARCHAR(1000),
    @RelevantDate DATE = NULL,
    @Actor BIGINT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF @HistoryType NOT IN (N'Medical', N'Surgical') THROW 52100, 'History type is invalid.', 1;
    IF NULLIF(LTRIM(RTRIM(@Description)), N'') IS NULL THROW 52101, 'Description is required.', 1;
    IF @RelevantDate > CONVERT(DATE, SYSUTCDATETIME()) THROW 52102, 'Relevant date cannot be in the future.', 1;
    DECLARE @PatientId BIGINT = (SELECT PatientId FROM dbo.Patient WHERE PatientUid = @PatientUid AND IsDeleted = 0);
    IF @PatientId IS NULL THROW 52103, 'Patient was not found.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.ApplicationUser WHERE UserId = @Actor AND IsActive = 1) THROW 52104, 'Active clinical user was not found.', 1;
    DECLARE @HistoryUid UNIQUEIDENTIFIER = NEWID();
    BEGIN TRANSACTION;
    INSERT dbo.PatientClinicalHistory(HistoryUid, PatientUid, HistoryType, Description, RelevantDate, CreatedBy)
    VALUES(@HistoryUid, @PatientUid, @HistoryType, LTRIM(RTRIM(@Description)), @RelevantDate, @Actor);
    INSERT dbo.AuditLog(UserId, PatientId, ActionName, EntityName, EntityId, NewValue, CreatedAt)
    VALUES(@Actor, @PatientId, N'Create', N'PatientClinicalHistory', CONVERT(NVARCHAR(100), @HistoryUid),
        (SELECT @HistoryType AS HistoryType, LTRIM(RTRIM(@Description)) AS Description, @RelevantDate AS RelevantDate, N'Active' AS Status FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), SYSUTCDATETIME());
    COMMIT;
    EXEC dbo.PatientClinicalHistory_Get @PatientUid, @HistoryUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientClinicalHistory_Update
    @PatientUid UNIQUEIDENTIFIER,
    @HistoryUid UNIQUEIDENTIFIER,
    @HistoryType NVARCHAR(20),
    @Description NVARCHAR(1000),
    @RelevantDate DATE = NULL,
    @ExpectedRowVersion BINARY(8),
    @Actor BIGINT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF @HistoryType NOT IN (N'Medical', N'Surgical') THROW 52100, 'History type is invalid.', 1;
    IF NULLIF(LTRIM(RTRIM(@Description)), N'') IS NULL THROW 52101, 'Description is required.', 1;
    IF @RelevantDate > CONVERT(DATE, SYSUTCDATETIME()) THROW 52102, 'Relevant date cannot be in the future.', 1;
    DECLARE @PatientId BIGINT, @OldValue NVARCHAR(MAX), @CurrentVersion BINARY(8), @Status NVARCHAR(20);
    BEGIN TRANSACTION;
    SELECT @PatientId = p.PatientId, @CurrentVersion = h.RowVersion, @Status = h.Status,
        @OldValue = (SELECT h.HistoryType, h.Description, h.RelevantDate, h.Status FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
    FROM dbo.PatientClinicalHistory AS h WITH (UPDLOCK, HOLDLOCK)
    JOIN dbo.Patient AS p ON p.PatientUid = h.PatientUid AND p.IsDeleted = 0
    WHERE h.PatientUid = @PatientUid AND h.HistoryUid = @HistoryUid;
    IF @PatientId IS NULL BEGIN ROLLBACK; RETURN; END;
    IF @CurrentVersion <> @ExpectedRowVersion BEGIN ROLLBACK; THROW 52105, 'History was changed by another user.', 1; END;
    IF @Status <> N'Active' BEGIN ROLLBACK; THROW 52106, 'Archived history cannot be edited.', 1; END;
    UPDATE dbo.PatientClinicalHistory SET HistoryType = @HistoryType,
        Description = LTRIM(RTRIM(@Description)), RelevantDate = @RelevantDate,
        UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @Actor
    WHERE PatientUid = @PatientUid AND HistoryUid = @HistoryUid AND RowVersion = @ExpectedRowVersion;
    DECLARE @NewValue NVARCHAR(MAX) = (SELECT @HistoryType AS HistoryType, LTRIM(RTRIM(@Description)) AS Description, @RelevantDate AS RelevantDate, N'Active' AS Status FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
    INSERT dbo.AuditLog(UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
    VALUES(@Actor, @PatientId, N'Update', N'PatientClinicalHistory', CONVERT(NVARCHAR(100), @HistoryUid), @OldValue, @NewValue, SYSUTCDATETIME());
    COMMIT;
    EXEC dbo.PatientClinicalHistory_Get @PatientUid, @HistoryUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientClinicalHistory_Archive
    @PatientUid UNIQUEIDENTIFIER,
    @HistoryUid UNIQUEIDENTIFIER,
    @ExpectedRowVersion BINARY(8),
    @Actor BIGINT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @PatientId BIGINT, @CurrentVersion BINARY(8), @Status NVARCHAR(20);
    BEGIN TRANSACTION;
    SELECT @PatientId = p.PatientId, @CurrentVersion = h.RowVersion, @Status = h.Status
    FROM dbo.PatientClinicalHistory AS h WITH (UPDLOCK, HOLDLOCK)
    JOIN dbo.Patient AS p ON p.PatientUid = h.PatientUid AND p.IsDeleted = 0
    WHERE h.PatientUid = @PatientUid AND h.HistoryUid = @HistoryUid;
    IF @PatientId IS NULL BEGIN ROLLBACK; RETURN; END;
    IF @CurrentVersion <> @ExpectedRowVersion BEGIN ROLLBACK; THROW 52105, 'History was changed by another user.', 1; END;
    IF @Status <> N'Active' BEGIN ROLLBACK; THROW 52106, 'History is already archived.', 1; END;
    UPDATE dbo.PatientClinicalHistory SET Status = N'Archived', UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @Actor
    WHERE PatientUid = @PatientUid AND HistoryUid = @HistoryUid AND RowVersion = @ExpectedRowVersion;
    INSERT dbo.AuditLog(UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
    VALUES(@Actor, @PatientId, N'Archive', N'PatientClinicalHistory', CONVERT(NVARCHAR(100), @HistoryUid), N'Status=Active', N'Status=Archived', SYSUTCDATETIME());
    COMMIT;
    EXEC dbo.PatientClinicalHistory_Get @PatientUid, @HistoryUid;
END;
GO
