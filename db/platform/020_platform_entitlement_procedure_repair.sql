USE MicroEMR_Platform;
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
