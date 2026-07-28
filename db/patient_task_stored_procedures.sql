IF OBJECT_ID(N'dbo.PatientTask', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PatientTask
    (
        PatientTaskId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PatientTask PRIMARY KEY,
        PatientTaskUid UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PatientTask_Uid DEFAULT NEWSEQUENTIALID(),
        PatientUid UNIQUEIDENTIFIER NOT NULL,
        TaskTitle NVARCHAR(200) NOT NULL,
        TaskDescription NVARCHAR(1000) NULL,
        TaskType NVARCHAR(50) NOT NULL CONSTRAINT DF_PatientTask_Type DEFAULT N'General',
        TaskPriority NVARCHAR(50) NOT NULL CONSTRAINT DF_PatientTask_Priority DEFAULT N'Normal',
        TaskStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_PatientTask_Status DEFAULT N'Open',
        DueAt DATETIME2(0) NULL,
        AssignedTo BIGINT NULL,
        CompletedAt DATETIME2(0) NULL,
        CompletedBy BIGINT NULL,
        CompletionNote NVARCHAR(1000) NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_PatientTask_CreatedAt DEFAULT SYSUTCDATETIME(),
        CreatedBy BIGINT NULL,
        UpdatedAt DATETIME2(0) NULL,
        UpdatedBy BIGINT NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT UQ_PatientTask_Uid UNIQUE (PatientTaskUid),
        CONSTRAINT CK_PatientTask_Status CHECK (TaskStatus IN (N'Open', N'Completed'))
    );
    CREATE INDEX IX_PatientTask_PatientUid_Status ON dbo.PatientTask(PatientUid, TaskStatus);
    CREATE INDEX IX_PatientTask_AssignedTo_Status_DueAt ON dbo.PatientTask(AssignedTo, TaskStatus, DueAt);
    CREATE INDEX IX_PatientTask_DueAt_Status ON dbo.PatientTask(DueAt, TaskStatus);
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientTask_GetByUid
    @PatientUid UNIQUEIDENTIFIER,
    @PatientTaskUid UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.*, au.DisplayName AssignedToDisplayName, cu.DisplayName CreatedByDisplayName,
           uu.DisplayName UpdatedByDisplayName, xu.DisplayName CompletedByDisplayName
    FROM dbo.PatientTask t
    LEFT JOIN dbo.ApplicationUser au ON au.UserId = t.AssignedTo
    LEFT JOIN dbo.ApplicationUser cu ON cu.UserId = t.CreatedBy
    LEFT JOIN dbo.ApplicationUser uu ON uu.UserId = t.UpdatedBy
    LEFT JOIN dbo.ApplicationUser xu ON xu.UserId = t.CompletedBy
    WHERE t.PatientUid = @PatientUid AND t.PatientTaskUid = @PatientTaskUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientTask_GetByPatientUid
    @PatientUid UNIQUEIDENTIFIER,
    @StatusFilter NVARCHAR(50) = N'Open'
AS
BEGIN
    SET NOCOUNT ON;
    IF @StatusFilter NOT IN (N'Open', N'Completed', N'All') SET @StatusFilter = N'Open';
    SELECT t.*, au.DisplayName AssignedToDisplayName, cu.DisplayName CreatedByDisplayName,
           uu.DisplayName UpdatedByDisplayName, xu.DisplayName CompletedByDisplayName
    FROM dbo.PatientTask t
    LEFT JOIN dbo.ApplicationUser au ON au.UserId = t.AssignedTo
    LEFT JOIN dbo.ApplicationUser cu ON cu.UserId = t.CreatedBy
    LEFT JOIN dbo.ApplicationUser uu ON uu.UserId = t.UpdatedBy
    LEFT JOIN dbo.ApplicationUser xu ON xu.UserId = t.CompletedBy
    WHERE t.PatientUid = @PatientUid AND (@StatusFilter = N'All' OR t.TaskStatus = @StatusFilter)
    ORDER BY CASE WHEN t.TaskStatus = N'Open' THEN 0 ELSE 1 END,
             CASE WHEN t.TaskStatus = N'Open' AND t.DueAt IS NULL THEN 1 ELSE 0 END,
             CASE WHEN t.TaskStatus = N'Open' THEN t.DueAt END,
             CASE t.TaskPriority WHEN N'Urgent' THEN 1 WHEN N'High' THEN 2 WHEN N'Normal' THEN 3 ELSE 4 END,
             CASE WHEN t.TaskStatus = N'Completed' THEN t.CompletedAt END DESC, t.CreatedAt DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientTask_Create
    @PatientUid UNIQUEIDENTIFIER, @TaskTitle NVARCHAR(200), @TaskDescription NVARCHAR(1000) = NULL,
    @TaskType NVARCHAR(50) = N'General', @TaskPriority NVARCHAR(50) = N'Normal',
    @DueAt DATETIME2(0) = NULL, @AssignedTo BIGINT = NULL, @CreatedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Patient WHERE PatientUid = @PatientUid AND IsDeleted = 0) THROW 51300, 'Patient not found.', 1;
    IF NULLIF(LTRIM(RTRIM(@TaskTitle)), N'') IS NULL THROW 51301, 'Task title is required.', 1;
    IF @TaskType NOT IN (N'General',N'Follow-up',N'Call Patient',N'Review Result',N'Form',N'Referral',N'Booking') SET @TaskType = N'General';
    IF @TaskPriority NOT IN (N'Low',N'Normal',N'High',N'Urgent') SET @TaskPriority = N'Normal';
    DECLARE @Uid UNIQUEIDENTIFIER = NEWID();
    INSERT dbo.PatientTask(PatientTaskUid,PatientUid,TaskTitle,TaskDescription,TaskType,TaskPriority,DueAt,AssignedTo,CreatedBy)
    VALUES(@Uid,@PatientUid,LTRIM(RTRIM(@TaskTitle)),NULLIF(LTRIM(RTRIM(@TaskDescription)),N''),@TaskType,@TaskPriority,@DueAt,@AssignedTo,@CreatedBy);
    EXEC dbo.PatientTask_GetByUid @PatientUid, @Uid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientTask_Update
    @PatientUid UNIQUEIDENTIFIER, @PatientTaskUid UNIQUEIDENTIFIER, @TaskTitle NVARCHAR(200),
    @TaskDescription NVARCHAR(1000) = NULL, @TaskType NVARCHAR(50) = N'General',
    @TaskPriority NVARCHAR(50) = N'Normal', @DueAt DATETIME2(0) = NULL,
    @AssignedTo BIGINT = NULL, @UpdatedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM dbo.PatientTask WHERE PatientUid=@PatientUid AND PatientTaskUid=@PatientTaskUid AND TaskStatus=N'Completed') THROW 51302, 'Completed tasks cannot be edited.', 1;
    IF NULLIF(LTRIM(RTRIM(@TaskTitle)), N'') IS NULL THROW 51301, 'Task title is required.', 1;
    IF @TaskType NOT IN (N'General',N'Follow-up',N'Call Patient',N'Review Result',N'Form',N'Referral',N'Booking') SET @TaskType = N'General';
    IF @TaskPriority NOT IN (N'Low',N'Normal',N'High',N'Urgent') SET @TaskPriority = N'Normal';
    UPDATE dbo.PatientTask SET TaskTitle=LTRIM(RTRIM(@TaskTitle)),TaskDescription=NULLIF(LTRIM(RTRIM(@TaskDescription)),N''),
        TaskType=@TaskType,TaskPriority=@TaskPriority,DueAt=@DueAt,AssignedTo=@AssignedTo,UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@UpdatedBy
    WHERE PatientUid=@PatientUid AND PatientTaskUid=@PatientTaskUid;
    IF @@ROWCOUNT = 0 RETURN;
    EXEC dbo.PatientTask_GetByUid @PatientUid, @PatientTaskUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientTask_Complete
    @PatientUid UNIQUEIDENTIFIER, @PatientTaskUid UNIQUEIDENTIFIER,
    @CompletionNote NVARCHAR(1000) = NULL, @CompletedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.PatientTask SET TaskStatus=N'Completed',CompletedAt=SYSUTCDATETIME(),CompletedBy=@CompletedBy,
        CompletionNote=NULLIF(LTRIM(RTRIM(@CompletionNote)),N''),UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@CompletedBy
    WHERE PatientUid=@PatientUid AND PatientTaskUid=@PatientTaskUid AND TaskStatus=N'Open';
    IF @@ROWCOUNT = 0 AND NOT EXISTS (SELECT 1 FROM dbo.PatientTask WHERE PatientUid=@PatientUid AND PatientTaskUid=@PatientTaskUid) RETURN;
    EXEC dbo.PatientTask_GetByUid @PatientUid, @PatientTaskUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientTask_Reopen
    @PatientUid UNIQUEIDENTIFIER, @PatientTaskUid UNIQUEIDENTIFIER, @UpdatedBy BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.PatientTask SET TaskStatus=N'Open',CompletedAt=NULL,CompletedBy=NULL,CompletionNote=NULL,
        UpdatedAt=SYSUTCDATETIME(),UpdatedBy=@UpdatedBy
    WHERE PatientUid=@PatientUid AND PatientTaskUid=@PatientTaskUid AND TaskStatus=N'Completed';
    IF @@ROWCOUNT = 0 AND NOT EXISTS (SELECT 1 FROM dbo.PatientTask WHERE PatientUid=@PatientUid AND PatientTaskUid=@PatientTaskUid) RETURN;
    EXEC dbo.PatientTask_GetByUid @PatientUid, @PatientTaskUid;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PatientTask_GetOpenForDashboard @AssignedTo BIGINT = NULL, @MaxRows INT = 10
AS
BEGIN
    SET NOCOUNT ON;
    SET @MaxRows = CASE WHEN @MaxRows BETWEEN 1 AND 50 THEN @MaxRows ELSE 10 END;
    SELECT TOP (@MaxRows) t.PatientTaskUid,t.PatientUid,
        LTRIM(RTRIM(CONCAT(p.FirstName,N' ',p.LastName))) PatientDisplayName,p.ChartNumber,p.DateOfBirth,
        t.TaskTitle,t.TaskDescription,t.TaskType,t.TaskPriority,t.TaskStatus,t.DueAt,t.CreatedAt
    FROM dbo.PatientTask t INNER JOIN dbo.Patient p ON p.PatientUid=t.PatientUid AND p.IsDeleted=0
    WHERE t.TaskStatus=N'Open' AND (@AssignedTo IS NULL OR t.AssignedTo=@AssignedTo OR t.AssignedTo IS NULL)
    ORDER BY CASE WHEN t.DueAt < SYSUTCDATETIME() THEN 0 ELSE 1 END,
        CASE WHEN t.DueAt IS NULL THEN 1 ELSE 0 END,t.DueAt,
        CASE t.TaskPriority WHEN N'Urgent' THEN 1 WHEN N'High' THEN 2 WHEN N'Normal' THEN 3 ELSE 4 END,t.CreatedAt DESC;
END;
GO
