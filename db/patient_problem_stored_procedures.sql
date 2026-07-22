/* Patient Problem List schema and stored procedures. Safe to re-run. */
IF OBJECT_ID(N'dbo.Patient', N'U') IS NULL THROW 51070, 'Required table dbo.Patient was not found.', 1;
GO

IF OBJECT_ID(N'dbo.PatientProblem', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PatientProblem
    (
        PatientProblemId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        PatientProblemUid UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PatientProblem_Uid DEFAULT NEWSEQUENTIALID(),
        PatientUid UNIQUEIDENTIFIER NOT NULL,
        ProblemName NVARCHAR(200) NOT NULL,
        ProblemDescription NVARCHAR(1000) NULL,
        OnsetDate DATE NULL,
        ProblemStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_PatientProblem_Status DEFAULT N'Active',
        ResolvedAt DATETIME2(0) NULL,
        ResolvedBy BIGINT NULL,
        ResolutionReason NVARCHAR(500) NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_PatientProblem_CreatedAt DEFAULT SYSUTCDATETIME(),
        CreatedBy BIGINT NULL,
        UpdatedAt DATETIME2(0) NULL,
        UpdatedBy BIGINT NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT UQ_PatientProblem_Uid UNIQUE (PatientProblemUid),
        CONSTRAINT CK_PatientProblem_Status CHECK (ProblemStatus IN (N'Active', N'Resolved'))
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PatientProblem') AND name = N'IX_PatientProblem_PatientUid_Status')
    CREATE INDEX IX_PatientProblem_PatientUid_Status ON dbo.PatientProblem(PatientUid, ProblemStatus);
GO

CREATE OR ALTER PROCEDURE dbo.PatientProblem_GetByPatientUid
    @PatientUid UNIQUEIDENTIFIER, @StatusFilter NVARCHAR(50) = N'Active'
AS
BEGIN
    SET NOCOUNT ON;
    SET @StatusFilter = CASE WHEN @StatusFilter IN (N'Active', N'Resolved', N'All') THEN @StatusFilter ELSE N'Active' END;
    SELECT pp.PatientProblemUid, pp.PatientUid, pp.ProblemName, pp.ProblemDescription, pp.OnsetDate,
        pp.ProblemStatus, pp.ResolvedAt, pp.ResolvedBy, resolvedUser.DisplayName AS ResolvedByDisplayName,
        pp.ResolutionReason, pp.CreatedAt, pp.CreatedBy, createdUser.DisplayName AS CreatedByDisplayName,
        pp.UpdatedAt, pp.UpdatedBy, pp.RowVersion
    FROM dbo.PatientProblem pp
    LEFT JOIN dbo.ApplicationUser createdUser ON createdUser.UserId = pp.CreatedBy
    LEFT JOIN dbo.ApplicationUser resolvedUser ON resolvedUser.UserId = pp.ResolvedBy
    WHERE pp.PatientUid = @PatientUid AND (@StatusFilter = N'All' OR pp.ProblemStatus = @StatusFilter)
    ORDER BY CASE WHEN pp.ProblemStatus = N'Active' THEN 0 ELSE 1 END, COALESCE(pp.UpdatedAt, pp.CreatedAt) DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientProblem_GetByUid
    @PatientUid UNIQUEIDENTIFIER, @PatientProblemUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT pp.PatientProblemUid, pp.PatientUid, pp.ProblemName, pp.ProblemDescription, pp.OnsetDate,
        pp.ProblemStatus, pp.ResolvedAt, pp.ResolvedBy, resolvedUser.DisplayName AS ResolvedByDisplayName,
        pp.ResolutionReason, pp.CreatedAt, pp.CreatedBy, createdUser.DisplayName AS CreatedByDisplayName,
        pp.UpdatedAt, pp.UpdatedBy, pp.RowVersion
    FROM dbo.PatientProblem pp
    LEFT JOIN dbo.ApplicationUser createdUser ON createdUser.UserId = pp.CreatedBy
    LEFT JOIN dbo.ApplicationUser resolvedUser ON resolvedUser.UserId = pp.ResolvedBy
    WHERE pp.PatientUid = @PatientUid AND pp.PatientProblemUid = @PatientProblemUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientProblem_Create
    @PatientUid UNIQUEIDENTIFIER, @ProblemName NVARCHAR(200), @ProblemDescription NVARCHAR(1000) = NULL,
    @OnsetDate DATE = NULL, @CreatedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @PatientId BIGINT, @PatientProblemUid UNIQUEIDENTIFIER = NEWID();
    SELECT @PatientId = PatientId FROM dbo.Patient WHERE PatientUid = @PatientUid AND IsDeleted = 0;
    IF @PatientId IS NULL THROW 51071, 'The requested patient was not found.', 1;
    IF NULLIF(LTRIM(RTRIM(@ProblemName)), N'') IS NULL THROW 51071, 'Problem name is required.', 1;
    BEGIN TRANSACTION;
    INSERT dbo.PatientProblem(PatientProblemUid, PatientUid, ProblemName, ProblemDescription, OnsetDate, ProblemStatus, CreatedAt, CreatedBy)
    VALUES(@PatientProblemUid, @PatientUid, LTRIM(RTRIM(@ProblemName)), NULLIF(LTRIM(RTRIM(@ProblemDescription)), N''), @OnsetDate, N'Active', SYSUTCDATETIME(), @CreatedBy);
    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
        INSERT dbo.AuditLog(UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
        VALUES(@CreatedBy, @PatientId, N'Create', N'PatientProblem', CONVERT(NVARCHAR(100), @PatientProblemUid), NULL, N'Problem created', SYSUTCDATETIME());
    COMMIT;
    EXEC dbo.PatientProblem_GetByUid @PatientUid, @PatientProblemUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientProblem_Update
    @PatientUid UNIQUEIDENTIFIER, @PatientProblemUid UNIQUEIDENTIFIER, @ProblemName NVARCHAR(200),
    @ProblemDescription NVARCHAR(1000) = NULL, @OnsetDate DATE = NULL, @UpdatedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @PatientId BIGINT, @Status NVARCHAR(50);
    IF NULLIF(LTRIM(RTRIM(@ProblemName)), N'') IS NULL THROW 51071, 'Problem name is required.', 1;
    BEGIN TRANSACTION;
    SELECT @PatientId = p.PatientId, @Status = pp.ProblemStatus
    FROM dbo.PatientProblem pp WITH (UPDLOCK, HOLDLOCK) JOIN dbo.Patient p ON p.PatientUid = pp.PatientUid
    WHERE pp.PatientUid = @PatientUid AND pp.PatientProblemUid = @PatientProblemUid AND p.IsDeleted = 0;
    IF @PatientId IS NULL BEGIN ROLLBACK; RETURN; END;
    IF @Status <> N'Active' BEGIN ROLLBACK; THROW 51072, 'Resolved problems cannot be edited.', 1; END;
    UPDATE dbo.PatientProblem SET ProblemName = LTRIM(RTRIM(@ProblemName)),
        ProblemDescription = NULLIF(LTRIM(RTRIM(@ProblemDescription)), N''), OnsetDate = @OnsetDate,
        UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UpdatedBy
    WHERE PatientUid = @PatientUid AND PatientProblemUid = @PatientProblemUid;
    IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
        INSERT dbo.AuditLog(UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
        VALUES(@UpdatedBy, @PatientId, N'Update', N'PatientProblem', CONVERT(NVARCHAR(100), @PatientProblemUid), NULL, N'Problem updated', SYSUTCDATETIME());
    COMMIT;
    EXEC dbo.PatientProblem_GetByUid @PatientUid, @PatientProblemUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientProblem_Resolve
    @PatientUid UNIQUEIDENTIFIER, @PatientProblemUid UNIQUEIDENTIFIER,
    @ResolutionReason NVARCHAR(500) = NULL, @ResolvedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @PatientId BIGINT, @Status NVARCHAR(50);
    BEGIN TRANSACTION;
    SELECT @PatientId = p.PatientId, @Status = pp.ProblemStatus
    FROM dbo.PatientProblem pp WITH (UPDLOCK, HOLDLOCK) JOIN dbo.Patient p ON p.PatientUid = pp.PatientUid
    WHERE pp.PatientUid = @PatientUid AND pp.PatientProblemUid = @PatientProblemUid AND p.IsDeleted = 0;
    IF @PatientId IS NULL BEGIN ROLLBACK; RETURN; END;
    IF @Status <> N'Resolved'
    BEGIN
        UPDATE dbo.PatientProblem SET ProblemStatus = N'Resolved', ResolvedAt = SYSUTCDATETIME(), ResolvedBy = @ResolvedBy,
            ResolutionReason = NULLIF(LTRIM(RTRIM(@ResolutionReason)), N''), UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @ResolvedBy
        WHERE PatientUid = @PatientUid AND PatientProblemUid = @PatientProblemUid;
        IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NOT NULL
            INSERT dbo.AuditLog(UserId, PatientId, ActionName, EntityName, EntityId, OldValue, NewValue, CreatedAt)
            VALUES(@ResolvedBy, @PatientId, N'Resolve', N'PatientProblem', CONVERT(NVARCHAR(100), @PatientProblemUid), N'Active', N'Resolved', SYSUTCDATETIME());
    END;
    COMMIT;
    EXEC dbo.PatientProblem_GetByUid @PatientUid, @PatientProblemUid;
END;
GO
