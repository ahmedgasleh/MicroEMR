/*
    Step 35P: repair initial access-profile resolution during tenant membership
    creation. Apply once after platform migration 021.
*/
USE MicroEMR_Platform;
GO

SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID(N'dbo.AccessProfile', N'U') IS NULL
   OR OBJECT_ID(N'dbo.UserTenantAccessProfile', N'U') IS NULL
    THROW 51410, 'Access-profile provisioning prerequisites are missing.', 1;
GO

CREATE OR ALTER PROCEDURE dbo.PlatformMembership_CreateWithInitialRole
    @UserId NVARCHAR(450),
    @TenantUid UNIQUEIDENTIFIER,
    @RoleName NVARCHAR(100),
    @ActorUserId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @UserId = LTRIM(RTRIM(@UserId));
    SET @RoleName = LTRIM(RTRIM(@RoleName));
    SET @ActorUserId = LTRIM(RTRIM(@ActorUserId));

    IF @UserId IS NULL OR LEN(@UserId) = 0
        THROW 51303, 'User is required.', 1;
    IF @ActorUserId IS NULL OR LEN(@ActorUserId) = 0
        THROW 51303, 'Actor is required.', 1;

    DECLARE @ProfileName NVARCHAR(100) = CASE @RoleName
        WHEN N'ClinicAdministrator' THEN N'Clinic Administrator'
        WHEN N'Physician' THEN N'Physician'
        WHEN N'Nurse' THEN N'Nurse'
        WHEN N'MedicalAssistant' THEN N'Medical Assistant'
        WHEN N'Scheduler' THEN N'Reception / Scheduling'
        ELSE NULL
    END;
    DECLARE @ProfileUid UNIQUEIDENTIFIER;

    IF @ProfileName IS NULL
        THROW 51310, 'Invalid tenant role.', 1;

    BEGIN TRANSACTION;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.Tenant WITH (UPDLOCK, HOLDLOCK)
        WHERE TenantUid = @TenantUid AND TenantStatus = N'Active'
    )
    BEGIN
        ROLLBACK;
        THROW 51302, 'Tenant is not active.', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.UserTenantMembership WITH (UPDLOCK, HOLDLOCK)
        WHERE UserId = @UserId AND TenantUid = @TenantUid
    )
    BEGIN
        ROLLBACK;
        THROW 51301, 'Membership exists.', 1;
    END;

    SELECT @ProfileUid = AccessProfileUid
    FROM dbo.AccessProfile WITH (UPDLOCK, HOLDLOCK)
    WHERE TenantUid = @TenantUid
      AND Name = @ProfileName
      AND IsActive = 1;

    IF @ProfileUid IS NULL
    BEGIN
        ROLLBACK;
        THROW 51401, 'Default access profile unavailable.', 1;
    END;

    INSERT dbo.UserTenantMembership
        (UserId, TenantUid, MembershipStatus, IsDefaultTenant, CreatedAt)
    VALUES
        (@UserId, @TenantUid, N'Active', 0, SYSUTCDATETIME());

    INSERT dbo.UserTenantRole(UserId, TenantUid, RoleName)
    VALUES (@UserId, @TenantUid, @RoleName);

    INSERT dbo.UserTenantAccessProfile
        (UserId, TenantUid, AccessProfileUid, AssignedBy)
    VALUES
        (@UserId, @TenantUid, @ProfileUid, @ActorUserId);

    INSERT dbo.PlatformAuditEvent
    (
        PlatformAuditEventUid, ActorUserId, ActorType, Action, TargetTenantUid,
        TargetUserId, Outcome, OccurredAtUtc, CorrelationId, DetailsJson
    )
    VALUES
    (
        NEWID(), @ActorUserId, N'TenantAdmin', N'TenantUserCreated', @TenantUid,
        @UserId, N'Succeeded', SYSUTCDATETIME(), NEWID(),
        CONCAT
        (
            N'{"membershipStatus":"Active","initialRole":"',
            STRING_ESCAPE(@RoleName, 'json'),
            N'","accessProfile":"',
            STRING_ESCAPE(@ProfileName, 'json'),
            N'"}'
        )
    );

    COMMIT;
END;
GO
