USE MicroEMR_Platform;
GO

IF OBJECT_ID(N'dbo.PlatformEntitlement', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PlatformEntitlement
    (
        PlatformEntitlementUid UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT PK_PlatformEntitlement PRIMARY KEY
            CONSTRAINT DF_PlatformEntitlement_Uid DEFAULT NEWSEQUENTIALID(),
        EntitlementKey NVARCHAR(100) COLLATE Latin1_General_100_BIN2 NOT NULL,
        DisplayName NVARCHAR(150) NOT NULL,
        Description NVARCHAR(500) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_PlatformEntitlement_IsActive DEFAULT 1,
        CreatedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_PlatformEntitlement_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(450) NOT NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT UQ_PlatformEntitlement_Key UNIQUE (EntitlementKey),
        CONSTRAINT CK_PlatformEntitlement_Key CHECK
        (
            EntitlementKey = N'SecurityAudit.View'
            AND EntitlementKey NOT LIKE N'%*%'
        ),
        CONSTRAINT CK_PlatformEntitlement_Text CHECK
        (
            LEN(LTRIM(RTRIM(DisplayName))) > 0
            AND LEN(LTRIM(RTRIM(Description))) > 0
            AND LEN(LTRIM(RTRIM(CreatedBy))) > 0
        )
    );
END;
GO

IF OBJECT_ID(N'dbo.UserPlatformEntitlement', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserPlatformEntitlement
    (
        UserPlatformEntitlementUid UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT PK_UserPlatformEntitlement PRIMARY KEY
            CONSTRAINT DF_UserPlatformEntitlement_Uid DEFAULT NEWSEQUENTIALID(),
        UserId NVARCHAR(450) NOT NULL,
        PlatformEntitlementUid UNIQUEIDENTIFIER NOT NULL,
        AssignedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_UserPlatformEntitlement_AssignedAtUtc DEFAULT SYSUTCDATETIME(),
        AssignedBy NVARCHAR(450) NOT NULL,
        RevokedAtUtc DATETIME2(7) NULL,
        RevokedBy NVARCHAR(450) NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_UserPlatformEntitlement_Entitlement FOREIGN KEY (PlatformEntitlementUid)
            REFERENCES dbo.PlatformEntitlement(PlatformEntitlementUid),
        CONSTRAINT CK_UserPlatformEntitlement_User CHECK (LEN(LTRIM(RTRIM(UserId))) > 0),
        CONSTRAINT CK_UserPlatformEntitlement_AssignedBy CHECK (LEN(LTRIM(RTRIM(AssignedBy))) > 0),
        CONSTRAINT CK_UserPlatformEntitlement_Revocation CHECK
        (
            (RevokedAtUtc IS NULL AND RevokedBy IS NULL)
            OR
            (RevokedAtUtc IS NOT NULL AND RevokedAtUtc >= AssignedAtUtc
                AND RevokedBy IS NOT NULL AND LEN(LTRIM(RTRIM(RevokedBy))) > 0)
        )
    );

    CREATE UNIQUE INDEX UX_UserPlatformEntitlement_Active
        ON dbo.UserPlatformEntitlement(UserId, PlatformEntitlementUid)
        WHERE RevokedAtUtc IS NULL;
    CREATE INDEX IX_UserPlatformEntitlement_UserHistory
        ON dbo.UserPlatformEntitlement(UserId, AssignedAtUtc DESC);
END;
GO

IF OBJECT_ID(N'dbo.PlatformAuthorizationState', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PlatformAuthorizationState
    (
        UserId NVARCHAR(450) NOT NULL CONSTRAINT PK_PlatformAuthorizationState PRIMARY KEY NONCLUSTERED,
        AuthorizationVersion BIGINT NOT NULL,
        UpdatedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_PlatformAuthorizationState_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_PlatformAuthorizationState_User CHECK (LEN(LTRIM(RTRIM(UserId))) > 0),
        CONSTRAINT CK_PlatformAuthorizationState_Version CHECK (AuthorizationVersion > 0)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.PlatformEntitlement WHERE EntitlementKey = N'SecurityAudit.View')
BEGIN
    INSERT dbo.PlatformEntitlement
    (
        PlatformEntitlementUid, EntitlementKey, DisplayName, Description,
        IsActive, CreatedAtUtc, CreatedBy
    )
    VALUES
    (
        '88C2CD7A-6076-4E93-A06E-6D838C275F25', N'SecurityAudit.View',
        N'View security audit', N'View governed platform security-denial audit events.',
        1, SYSUTCDATETIME(), N'platform-migration-018'
    );
END;
GO

CREATE OR ALTER PROCEDURE dbo.PlatformEntitlement_GetActiveForUser
    @UserId NVARCHAR(451)
AS
BEGIN
    SET NOCOUNT ON;
    IF @UserId IS NULL OR LEN(LTRIM(RTRIM(@UserId))) = 0 OR LEN(@UserId) > 450
        THROW 52001, 'A valid user identifier is required.', 1;

    SELECT e.EntitlementKey
    FROM dbo.UserPlatformEntitlement a
    INNER JOIN dbo.PlatformEntitlement e
        ON e.PlatformEntitlementUid = a.PlatformEntitlementUid
    WHERE a.UserId = LTRIM(RTRIM(@UserId))
      AND a.RevokedAtUtc IS NULL
      AND e.IsActive = 1
      AND e.EntitlementKey = N'SecurityAudit.View'
    ORDER BY e.EntitlementKey;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PlatformAuthorization_GetVersionForUser
    @UserId NVARCHAR(451)
AS
BEGIN
    SET NOCOUNT ON;
    IF @UserId IS NULL OR LEN(LTRIM(RTRIM(@UserId))) = 0 OR LEN(@UserId) > 450
        THROW 52001, 'A valid user identifier is required.', 1;

    SELECT COALESCE
    (
        (SELECT AuthorizationVersion
         FROM dbo.PlatformAuthorizationState
         WHERE UserId = LTRIM(RTRIM(@UserId))),
        CONVERT(BIGINT, 0)
    ) AS AuthorizationVersion;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PlatformEntitlement_AssignToUser
    @UserId NVARCHAR(451),
    @EntitlementKey NVARCHAR(101),
    @ActorUserId NVARCHAR(451),
    @CorrelationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @UserId = LTRIM(RTRIM(@UserId));
    SET @EntitlementKey = LTRIM(RTRIM(@EntitlementKey));
    SET @ActorUserId = LTRIM(RTRIM(@ActorUserId));
    IF @UserId IS NULL OR LEN(@UserId) = 0 OR LEN(@UserId) > 450
        THROW 52001, 'A valid user identifier is required.', 1;
    IF @EntitlementKey IS NULL OR LEN(@EntitlementKey) = 0 OR LEN(@EntitlementKey) > 100
        THROW 52002, 'A valid entitlement key is required.', 1;
    IF @ActorUserId IS NULL OR LEN(@ActorUserId) = 0 OR LEN(@ActorUserId) > 450
        THROW 52003, 'A valid actor identifier is required.', 1;
    IF @CorrelationId IS NULL OR @CorrelationId = '00000000-0000-0000-0000-000000000000'
        THROW 52004, 'A valid correlation identifier is required.', 1;

    DECLARE @EntitlementUid UNIQUEIDENTIFIER;
    DECLARE @AssignmentUid UNIQUEIDENTIFIER = NEWID();
    DECLARE @Now DATETIME2(7) = SYSUTCDATETIME();
    DECLARE @LockResource NVARCHAR(100) = CONCAT
    (
        N'PlatformEntitlement|',
        CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', CONCAT(@UserId, N'|', @EntitlementKey)), 2)
    );
    DECLARE @LockResult INT;

    BEGIN TRANSACTION;
    EXEC @LockResult = sys.sp_getapplock @Resource = @LockResource, @LockMode = 'Exclusive',
        @LockOwner = 'Transaction', @LockTimeout = 10000;
    IF @LockResult < 0
        THROW 52010, 'The entitlement assignment lock could not be acquired.', 1;

    SELECT @EntitlementUid = PlatformEntitlementUid
    FROM dbo.PlatformEntitlement WITH (UPDLOCK, HOLDLOCK)
    WHERE EntitlementKey = @EntitlementKey AND IsActive = 1;
    IF @EntitlementUid IS NULL
        THROW 52005, 'The entitlement is unknown or inactive.', 1;
    IF EXISTS
    (
        SELECT 1 FROM dbo.UserPlatformEntitlement WITH (UPDLOCK, HOLDLOCK)
        WHERE UserId = @UserId AND PlatformEntitlementUid = @EntitlementUid AND RevokedAtUtc IS NULL
    )
        THROW 52006, 'The entitlement is already assigned.', 1;

    INSERT dbo.UserPlatformEntitlement
    (
        UserPlatformEntitlementUid, UserId, PlatformEntitlementUid,
        AssignedAtUtc, AssignedBy, RevokedAtUtc, RevokedBy
    )
    VALUES (@AssignmentUid, @UserId, @EntitlementUid, @Now, @ActorUserId, NULL, NULL);

    UPDATE dbo.PlatformAuthorizationState WITH (UPDLOCK, SERIALIZABLE)
    SET AuthorizationVersion = AuthorizationVersion + 1, UpdatedAtUtc = @Now
    WHERE UserId = @UserId;
    IF @@ROWCOUNT = 0
        INSERT dbo.PlatformAuthorizationState(UserId, AuthorizationVersion, UpdatedAtUtc)
        VALUES (@UserId, 1, @Now);

    INSERT dbo.PlatformAuditEvent
    (
        PlatformAuditEventUid, ActorUserId, ActorType, Action, TargetTenantUid,
        TargetUserId, Outcome, OccurredAtUtc, CorrelationId, DetailsJson
    )
    VALUES
    (
        NEWID(), @ActorUserId, N'PlatformAdminTool', N'PlatformEntitlementAssigned', NULL,
        @UserId, N'Succeeded', @Now, @CorrelationId,
        CONCAT(N'{"entitlement":"', STRING_ESCAPE(@EntitlementKey, 'json'), N'"}')
    );

    COMMIT;
    SELECT @AssignmentUid AS UserPlatformEntitlementUid,
        (SELECT AuthorizationVersion FROM dbo.PlatformAuthorizationState WHERE UserId = @UserId) AS AuthorizationVersion;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PlatformEntitlement_RevokeFromUser
    @UserId NVARCHAR(451),
    @EntitlementKey NVARCHAR(101),
    @ActorUserId NVARCHAR(451),
    @CorrelationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @UserId = LTRIM(RTRIM(@UserId));
    SET @EntitlementKey = LTRIM(RTRIM(@EntitlementKey));
    SET @ActorUserId = LTRIM(RTRIM(@ActorUserId));
    IF @UserId IS NULL OR LEN(@UserId) = 0 OR LEN(@UserId) > 450
        THROW 52001, 'A valid user identifier is required.', 1;
    IF @EntitlementKey IS NULL OR LEN(@EntitlementKey) = 0 OR LEN(@EntitlementKey) > 100
        THROW 52002, 'A valid entitlement key is required.', 1;
    IF @ActorUserId IS NULL OR LEN(@ActorUserId) = 0 OR LEN(@ActorUserId) > 450
        THROW 52003, 'A valid actor identifier is required.', 1;
    IF @CorrelationId IS NULL OR @CorrelationId = '00000000-0000-0000-0000-000000000000'
        THROW 52004, 'A valid correlation identifier is required.', 1;

    DECLARE @EntitlementUid UNIQUEIDENTIFIER;
    DECLARE @AssignmentUid UNIQUEIDENTIFIER;
    DECLARE @Now DATETIME2(7) = SYSUTCDATETIME();
    DECLARE @LockResource NVARCHAR(100) = CONCAT
    (
        N'PlatformEntitlement|',
        CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', CONCAT(@UserId, N'|', @EntitlementKey)), 2)
    );
    DECLARE @LockResult INT;

    BEGIN TRANSACTION;
    EXEC @LockResult = sys.sp_getapplock @Resource = @LockResource, @LockMode = 'Exclusive',
        @LockOwner = 'Transaction', @LockTimeout = 10000;
    IF @LockResult < 0
        THROW 52010, 'The entitlement revocation lock could not be acquired.', 1;

    SELECT @EntitlementUid = PlatformEntitlementUid
    FROM dbo.PlatformEntitlement WITH (UPDLOCK, HOLDLOCK)
    WHERE EntitlementKey = @EntitlementKey;
    IF @EntitlementUid IS NULL
        THROW 52005, 'The entitlement is unknown.', 1;

    SELECT @AssignmentUid = UserPlatformEntitlementUid
    FROM dbo.UserPlatformEntitlement WITH (UPDLOCK, HOLDLOCK)
    WHERE UserId = @UserId AND PlatformEntitlementUid = @EntitlementUid AND RevokedAtUtc IS NULL;
    IF @AssignmentUid IS NULL
        THROW 52007, 'The entitlement is not actively assigned.', 1;

    UPDATE dbo.UserPlatformEntitlement
    SET RevokedAtUtc = @Now, RevokedBy = @ActorUserId
    WHERE UserPlatformEntitlementUid = @AssignmentUid AND RevokedAtUtc IS NULL;
    IF @@ROWCOUNT <> 1
        THROW 52008, 'The entitlement assignment changed concurrently.', 1;

    UPDATE dbo.PlatformAuthorizationState WITH (UPDLOCK, SERIALIZABLE)
    SET AuthorizationVersion = AuthorizationVersion + 1, UpdatedAtUtc = @Now
    WHERE UserId = @UserId;
    IF @@ROWCOUNT <> 1
        THROW 52009, 'The platform authorization state is missing.', 1;

    INSERT dbo.PlatformAuditEvent
    (
        PlatformAuditEventUid, ActorUserId, ActorType, Action, TargetTenantUid,
        TargetUserId, Outcome, OccurredAtUtc, CorrelationId, DetailsJson
    )
    VALUES
    (
        NEWID(), @ActorUserId, N'PlatformAdminTool', N'PlatformEntitlementRevoked', NULL,
        @UserId, N'Succeeded', @Now, @CorrelationId,
        CONCAT(N'{"entitlement":"', STRING_ESCAPE(@EntitlementKey, 'json'), N'"}')
    );

    COMMIT;
    SELECT @AssignmentUid AS UserPlatformEntitlementUid,
        (SELECT AuthorizationVersion FROM dbo.PlatformAuthorizationState WHERE UserId = @UserId) AS AuthorizationVersion;
END;
GO
