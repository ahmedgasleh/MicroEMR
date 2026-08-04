SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH('dbo.UserTenantMembership', 'RowVersion') IS NULL
    ALTER TABLE dbo.UserTenantMembership ADD RowVersion ROWVERSION NOT NULL;
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_UserTenantMembership_MembershipStatus')
    ALTER TABLE dbo.UserTenantMembership DROP CONSTRAINT CK_UserTenantMembership_MembershipStatus;
GO

ALTER TABLE dbo.UserTenantMembership ADD CONSTRAINT CK_UserTenantMembership_MembershipStatus
    CHECK (MembershipStatus IN ('Invited', 'Active', 'Inactive', 'Suspended', 'Revoked'));
GO

CREATE OR ALTER PROCEDURE dbo.PlatformMembership_ListByTenant @TenantUid UNIQUEIDENTIFIER AS
BEGIN
    SET NOCOUNT ON;
    SELECT m.UserId,m.TenantUid,t.TenantKey,t.DisplayName,m.MembershipStatus,m.IsDefaultTenant,
           r.RoleName,m.UpdatedAt,m.RowVersion
    FROM dbo.UserTenantMembership m
    JOIN dbo.Tenant t ON t.TenantUid=m.TenantUid
    LEFT JOIN dbo.UserTenantRole r ON r.UserId=m.UserId AND r.TenantUid=m.TenantUid
    WHERE m.TenantUid=@TenantUid
    ORDER BY m.UserId,r.RoleName;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PlatformMembership_Deactivate
    @UserId NVARCHAR(450), @TenantUid UNIQUEIDENTIFIER,
    @ExpectedRowVersion BINARY(8), @ActorUserId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @Status VARCHAR(30), @CurrentRowVersion BINARY(8);
        SELECT @Status=MembershipStatus,@CurrentRowVersion=RowVersion
        FROM dbo.UserTenantMembership WITH(UPDLOCK,HOLDLOCK)
        WHERE UserId=@UserId AND TenantUid=@TenantUid;
        IF @Status IS NULL THROW 51303,'Membership not found.',1;
        IF @UserId=@ActorUserId THROW 51306,'Current administrator membership cannot be deactivated.',1;
        IF @Status<>'Active' THROW 51305,'Membership is not active.',1;
        IF @ExpectedRowVersion IS NULL OR @CurrentRowVersion<>@ExpectedRowVersion
            THROW 51307,'Membership has changed.',1;
        IF EXISTS(SELECT 1 FROM dbo.UserTenantRole WHERE UserId=@UserId AND TenantUid=@TenantUid AND RoleName=N'ClinicAdministrator')
           AND NOT EXISTS(
               SELECT 1 FROM dbo.UserTenantMembership m
               JOIN dbo.UserTenantRole r ON r.UserId=m.UserId AND r.TenantUid=m.TenantUid
               WHERE m.TenantUid=@TenantUid AND m.MembershipStatus='Active'
                 AND r.RoleName=N'ClinicAdministrator' AND m.UserId<>@UserId)
            THROW 51308,'The last active clinic administrator cannot be deactivated.',1;
        UPDATE dbo.UserTenantMembership
        SET MembershipStatus='Inactive',IsDefaultTenant=0,UpdatedAt=SYSUTCDATETIME()
        WHERE UserId=@UserId AND TenantUid=@TenantUid;
        INSERT dbo.PlatformAuditEvent
            (PlatformAuditEventUid,ActorUserId,ActorType,Action,TargetTenantUid,TargetUserId,Outcome,OccurredAtUtc,CorrelationId,DetailsJson)
        VALUES(NEWID(),@ActorUserId,'TenantAdmin','MembershipDeactivated',@TenantUid,@UserId,
               'Succeeded',SYSUTCDATETIME(),NEWID(),N'{"previousStatus":"Active","newStatus":"Inactive"}');
        COMMIT;
        SELECT MembershipStatus,UpdatedAt,RowVersion FROM dbo.UserTenantMembership
        WHERE UserId=@UserId AND TenantUid=@TenantUid;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PlatformMembership_Activate
    @UserId NVARCHAR(450), @TenantUid UNIQUEIDENTIFIER,
    @ExpectedRowVersion BINARY(8), @ActorUserId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @Status VARCHAR(30), @CurrentRowVersion BINARY(8);
        SELECT @Status=MembershipStatus,@CurrentRowVersion=RowVersion
        FROM dbo.UserTenantMembership WITH(UPDLOCK,HOLDLOCK)
        WHERE UserId=@UserId AND TenantUid=@TenantUid;
        IF @Status IS NULL THROW 51303,'Membership not found.',1;
        IF @Status<>'Inactive' THROW 51305,'Membership is not inactive.',1;
        IF @ExpectedRowVersion IS NULL OR @CurrentRowVersion<>@ExpectedRowVersion
            THROW 51307,'Membership has changed.',1;
        UPDATE dbo.UserTenantMembership
        SET MembershipStatus='Active',UpdatedAt=SYSUTCDATETIME()
        WHERE UserId=@UserId AND TenantUid=@TenantUid;
        INSERT dbo.PlatformAuditEvent
            (PlatformAuditEventUid,ActorUserId,ActorType,Action,TargetTenantUid,TargetUserId,Outcome,OccurredAtUtc,CorrelationId,DetailsJson)
        VALUES(NEWID(),@ActorUserId,'TenantAdmin','MembershipActivated',@TenantUid,@UserId,
               'Succeeded',SYSUTCDATETIME(),NEWID(),N'{"previousStatus":"Inactive","newStatus":"Active"}');
        COMMIT;
        SELECT MembershipStatus,UpdatedAt,RowVersion FROM dbo.UserTenantMembership
        WHERE UserId=@UserId AND TenantUid=@TenantUid;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO
