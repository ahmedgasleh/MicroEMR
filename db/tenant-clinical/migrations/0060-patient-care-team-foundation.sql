CREATE TABLE dbo.PatientProviderRelationshipType
(
    RelationshipTypeId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PatientProviderRelationshipType PRIMARY KEY,
    Code NVARCHAR(30) NOT NULL CONSTRAINT UQ_PatientProviderRelationshipType_Code UNIQUE,
    DisplayName NVARCHAR(100) NOT NULL,
    DisplayOrder INT NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_PatientProviderRelationshipType_IsActive DEFAULT 1,
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_PatientProviderRelationshipType_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2(0) NULL
);
GO

INSERT dbo.PatientProviderRelationshipType(Code, DisplayName, DisplayOrder)
SELECT v.Code, v.DisplayName, v.DisplayOrder
FROM (VALUES
    (N'REFERRING', N'Referring Physician', 10),
    (N'ATTENDING', N'Attending Physician', 20),
    (N'PCP', N'Primary Care Physician', 30),
    (N'CONSULTING', N'Consulting Physician', 40),
    (N'MRP', N'Most Responsible Physician', 50),
    (N'OTHER', N'Other', 60)
) v(Code, DisplayName, DisplayOrder)
WHERE NOT EXISTS (SELECT 1 FROM dbo.PatientProviderRelationshipType t WHERE t.Code = v.Code);
GO

CREATE TABLE dbo.PatientProviderRelationship
(
    RelationshipId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PatientProviderRelationship PRIMARY KEY,
    RelationshipUid UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PatientProviderRelationship_Uid DEFAULT NEWID(),
    PatientId BIGINT NOT NULL,
    ProviderId BIGINT NOT NULL,
    RelationshipTypeId INT NOT NULL,
    IsPrimary BIT NOT NULL CONSTRAINT DF_PatientProviderRelationship_IsPrimary DEFAULT 0,
    StartDate DATE NOT NULL,
    EndDate DATE NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_PatientProviderRelationship_IsActive DEFAULT 1,
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_PatientProviderRelationship_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy BIGINT NOT NULL,
    UpdatedAt DATETIME2(0) NULL,
    UpdatedBy BIGINT NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT UQ_PatientProviderRelationship_Uid UNIQUE(RelationshipUid),
    CONSTRAINT FK_PatientProviderRelationship_Patient FOREIGN KEY(PatientId) REFERENCES dbo.Patient(PatientId),
    CONSTRAINT FK_PatientProviderRelationship_Provider FOREIGN KEY(ProviderId) REFERENCES dbo.Provider(ProviderId),
    CONSTRAINT FK_PatientProviderRelationship_Type FOREIGN KEY(RelationshipTypeId) REFERENCES dbo.PatientProviderRelationshipType(RelationshipTypeId),
    CONSTRAINT FK_PatientProviderRelationship_CreatedBy FOREIGN KEY(CreatedBy) REFERENCES dbo.ApplicationUser(UserId),
    CONSTRAINT FK_PatientProviderRelationship_UpdatedBy FOREIGN KEY(UpdatedBy) REFERENCES dbo.ApplicationUser(UserId),
    CONSTRAINT CK_PatientProviderRelationship_Dates CHECK(EndDate IS NULL OR StartDate <= EndDate),
    CONSTRAINT CK_PatientProviderRelationship_ActiveEnd CHECK(IsActive = 0 OR EndDate IS NULL)
);
GO

CREATE UNIQUE INDEX UX_PatientProviderRelationship_ActiveAssignment
    ON dbo.PatientProviderRelationship(PatientId, ProviderId, RelationshipTypeId) WHERE IsActive = 1;
CREATE UNIQUE INDEX UX_PatientProviderRelationship_PrimaryRole
    ON dbo.PatientProviderRelationship(PatientId, RelationshipTypeId) WHERE IsActive = 1 AND IsPrimary = 1;
CREATE INDEX IX_PatientProviderRelationship_Patient ON dbo.PatientProviderRelationship(PatientId, IsActive);
GO

CREATE OR ALTER PROCEDURE dbo.PatientProviderRelationshipType_ListActive AS
BEGIN
    SET NOCOUNT ON;
    SELECT RelationshipTypeId, Code, DisplayName, DisplayOrder
    FROM dbo.PatientProviderRelationshipType WHERE IsActive = 1 ORDER BY DisplayOrder, Code;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientProviderRelationship_List @PatientUid UNIQUEIDENTIFIER AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.RelationshipUid, p.PatientUid, v.ProviderUid, v.DisplayName ProviderDisplayName,
        t.Code RelationshipTypeCode, t.DisplayName RelationshipTypeDisplayName,
        r.IsPrimary, r.StartDate, r.EndDate, r.IsActive,
        r.CreatedAt, r.CreatedBy, r.UpdatedAt, r.UpdatedBy, r.RowVersion
    FROM dbo.PatientProviderRelationship r
    JOIN dbo.Patient p ON p.PatientId = r.PatientId
    JOIN dbo.Provider v ON v.ProviderId = r.ProviderId
    JOIN dbo.PatientProviderRelationshipType t ON t.RelationshipTypeId = r.RelationshipTypeId
    WHERE p.PatientUid = @PatientUid AND p.IsDeleted = 0
    ORDER BY r.IsActive DESC, t.DisplayOrder, r.StartDate DESC, r.RelationshipId DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientProviderRelationship_Get @PatientUid UNIQUEIDENTIFIER, @RelationshipUid UNIQUEIDENTIFIER AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.RelationshipUid, p.PatientUid, v.ProviderUid, v.DisplayName ProviderDisplayName,
        t.Code RelationshipTypeCode, t.DisplayName RelationshipTypeDisplayName,
        r.IsPrimary, r.StartDate, r.EndDate, r.IsActive,
        r.CreatedAt, r.CreatedBy, r.UpdatedAt, r.UpdatedBy, r.RowVersion
    FROM dbo.PatientProviderRelationship r
    JOIN dbo.Patient p ON p.PatientId = r.PatientId
    JOIN dbo.Provider v ON v.ProviderId = r.ProviderId
    JOIN dbo.PatientProviderRelationshipType t ON t.RelationshipTypeId = r.RelationshipTypeId
    WHERE p.PatientUid = @PatientUid AND p.IsDeleted = 0 AND r.RelationshipUid = @RelationshipUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientProviderRelationship_Create
    @PatientUid UNIQUEIDENTIFIER, @ProviderUid UNIQUEIDENTIFIER, @RelationshipTypeCode NVARCHAR(30),
    @IsPrimary BIT, @StartDate DATE, @ActorUserId BIGINT AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    DECLARE @PatientId BIGINT, @ProviderId BIGINT, @TypeId INT, @RelationshipUid UNIQUEIDENTIFIER = NEWID();
    SELECT @PatientId = PatientId FROM dbo.Patient WHERE PatientUid = @PatientUid AND IsActive = 1 AND IsDeleted = 0;
    SELECT @ProviderId = ProviderId FROM dbo.Provider WHERE ProviderUid = @ProviderUid AND IsActive = 1;
    SELECT @TypeId = RelationshipTypeId FROM dbo.PatientProviderRelationshipType WHERE Code = @RelationshipTypeCode AND IsActive = 1;
    IF @PatientId IS NULL OR @ProviderId IS NULL OR @TypeId IS NULL THROW 51800, 'Active patient, provider, and relationship type are required.', 1;
    IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId = @ActorUserId AND IsActive = 1) THROW 51801, 'Active actor is required.', 1;
    IF EXISTS(SELECT 1 FROM dbo.PatientProviderRelationship WITH(UPDLOCK, HOLDLOCK)
        WHERE PatientId = @PatientId AND ProviderId = @ProviderId AND RelationshipTypeId = @TypeId AND IsActive = 1)
        THROW 51802, 'Active relationship already exists.', 1;
    IF @IsPrimary = 1 AND EXISTS(SELECT 1 FROM dbo.PatientProviderRelationship WITH(UPDLOCK, HOLDLOCK)
        WHERE PatientId = @PatientId AND RelationshipTypeId = @TypeId AND IsActive = 1 AND IsPrimary = 1)
        THROW 51803, 'A primary relationship already exists for this role.', 1;
    INSERT dbo.PatientProviderRelationship(RelationshipUid, PatientId, ProviderId, RelationshipTypeId, IsPrimary, StartDate, CreatedBy)
    VALUES(@RelationshipUid, @PatientId, @ProviderId, @TypeId, @IsPrimary, @StartDate, @ActorUserId);
    INSERT dbo.AuditLog(UserId, PatientId, ActionName, EntityName, EntityId, NewValue, CreatedAt)
    VALUES(@ActorUserId, @PatientId, N'CareTeamRelationshipAdded', N'PatientProviderRelationship', CONVERT(NVARCHAR(36), @RelationshipUid),
        CONCAT(N'ProviderUid=', @ProviderUid, N';Role=', @RelationshipTypeCode, N';Primary=', @IsPrimary, N';StartDate=', CONVERT(NVARCHAR(10), @StartDate, 23)), SYSUTCDATETIME());
    COMMIT;
    EXEC dbo.PatientProviderRelationship_Get @PatientUid, @RelationshipUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientProviderRelationship_Update
    @PatientUid UNIQUEIDENTIFIER, @RelationshipUid UNIQUEIDENTIFIER, @IsPrimary BIT,
    @StartDate DATE, @ExpectedRowVersion BINARY(8), @ActorUserId BIGINT AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    DECLARE @PatientId BIGINT, @TypeId INT, @Current BINARY(8), @OldValue NVARCHAR(MAX);
    SELECT @PatientId = r.PatientId, @TypeId = r.RelationshipTypeId, @Current = r.RowVersion,
        @OldValue = CONCAT(N'Primary=', r.IsPrimary, N';StartDate=', CONVERT(NVARCHAR(10), r.StartDate, 23))
    FROM dbo.PatientProviderRelationship r WITH(UPDLOCK, HOLDLOCK)
    JOIN dbo.Patient p ON p.PatientId = r.PatientId AND p.PatientUid = @PatientUid AND p.IsActive = 1 AND p.IsDeleted = 0
    WHERE r.RelationshipUid = @RelationshipUid AND r.IsActive = 1;
    IF @PatientId IS NULL THROW 51804, 'Active relationship not found.', 1;
    IF @Current <> @ExpectedRowVersion THROW 51805, 'Relationship changed.', 1;
    IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId = @ActorUserId AND IsActive = 1) THROW 51801, 'Active actor is required.', 1;
    IF @IsPrimary = 1 AND EXISTS(SELECT 1 FROM dbo.PatientProviderRelationship WITH(UPDLOCK, HOLDLOCK)
        WHERE PatientId = @PatientId AND RelationshipTypeId = @TypeId AND IsActive = 1 AND IsPrimary = 1 AND RelationshipUid <> @RelationshipUid)
        THROW 51803, 'A primary relationship already exists for this role.', 1;
    UPDATE dbo.PatientProviderRelationship SET IsPrimary = @IsPrimary, StartDate = @StartDate,
        UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @ActorUserId WHERE RelationshipUid = @RelationshipUid;
    INSERT dbo.AuditLog(UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
    VALUES(@ActorUserId, @PatientId, N'CareTeamRelationshipUpdated', N'PatientProviderRelationship', CONVERT(NVARCHAR(36), @RelationshipUid),
        @OldValue, CONCAT(N'Primary=', @IsPrimary, N';StartDate=', CONVERT(NVARCHAR(10), @StartDate, 23)), SYSUTCDATETIME());
    COMMIT;
    EXEC dbo.PatientProviderRelationship_Get @PatientUid, @RelationshipUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientProviderRelationship_End
    @PatientUid UNIQUEIDENTIFIER, @RelationshipUid UNIQUEIDENTIFIER, @EndDate DATE,
    @ExpectedRowVersion BINARY(8), @ActorUserId BIGINT AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    DECLARE @PatientId BIGINT, @StartDate DATE, @Current BINARY(8);
    SELECT @PatientId = r.PatientId, @StartDate = r.StartDate, @Current = r.RowVersion
    FROM dbo.PatientProviderRelationship r WITH(UPDLOCK, HOLDLOCK)
    JOIN dbo.Patient p ON p.PatientId = r.PatientId AND p.PatientUid = @PatientUid AND p.IsDeleted = 0
    WHERE r.RelationshipUid = @RelationshipUid AND r.IsActive = 1;
    IF @PatientId IS NULL THROW 51804, 'Active relationship not found.', 1;
    IF @Current <> @ExpectedRowVersion THROW 51805, 'Relationship changed.', 1;
    IF @EndDate < @StartDate THROW 51806, 'End date cannot precede start date.', 1;
    IF NOT EXISTS(SELECT 1 FROM dbo.ApplicationUser WHERE UserId = @ActorUserId AND IsActive = 1) THROW 51801, 'Active actor is required.', 1;
    UPDATE dbo.PatientProviderRelationship SET EndDate = @EndDate, IsActive = 0,
        UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @ActorUserId WHERE RelationshipUid = @RelationshipUid;
    INSERT dbo.AuditLog(UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
    VALUES(@ActorUserId, @PatientId, N'CareTeamRelationshipEnded', N'PatientProviderRelationship', CONVERT(NVARCHAR(36), @RelationshipUid),
        N'Status=Active', CONCAT(N'Status=Inactive;EndDate=', CONVERT(NVARCHAR(10), @EndDate, 23)), SYSUTCDATETIME());
    COMMIT;
    EXEC dbo.PatientProviderRelationship_Get @PatientUid, @RelationshipUid;
END;
GO
