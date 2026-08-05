SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

CREATE OR ALTER PROCEDURE dbo.PlatformMembership_ReplaceRoles
    @UserId NVARCHAR(450), @TenantUid UNIQUEIDENTIFIER, @RoleNames NVARCHAR(1000),
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
        IF @Status<>'Active' THROW 51305,'Membership is not active.',1;
        IF @ExpectedRowVersion IS NULL OR @CurrentRowVersion<>@ExpectedRowVersion THROW 51307,'Membership has changed.',1;

        DECLARE @Roles TABLE(RoleName NVARCHAR(100) PRIMARY KEY);
        INSERT @Roles(RoleName)
        SELECT DISTINCT LTRIM(RTRIM(value)) FROM STRING_SPLIT(@RoleNames,',') WHERE LTRIM(RTRIM(value))<>N'';
        IF NOT EXISTS(SELECT 1 FROM @Roles) THROW 51310,'At least one role is required.',1;
        IF EXISTS(SELECT 1 FROM @Roles WHERE RoleName NOT IN
            (N'Physician',N'Nurse',N'MedicalAssistant',N'Scheduler',N'ClinicAdministrator'))
            THROW 51310,'Unknown role.',1;

        IF EXISTS(SELECT 1 FROM dbo.UserTenantRole WHERE UserId=@UserId AND TenantUid=@TenantUid AND RoleName=N'ClinicAdministrator')
           AND NOT EXISTS(SELECT 1 FROM @Roles WHERE RoleName=N'ClinicAdministrator')
        BEGIN
            IF @UserId=@ActorUserId THROW 51309,'Current administrator cannot remove own role.',1;
            IF NOT EXISTS(
                SELECT 1 FROM dbo.UserTenantMembership m
                JOIN dbo.UserTenantRole r ON r.UserId=m.UserId AND r.TenantUid=m.TenantUid
                WHERE m.TenantUid=@TenantUid AND m.MembershipStatus='Active'
                  AND r.RoleName=N'ClinicAdministrator' AND m.UserId<>@UserId)
                THROW 51308,'Last active administrator cannot lose role.',1;
        END;

        DELETE r FROM dbo.UserTenantRole r WHERE r.UserId=@UserId AND r.TenantUid=@TenantUid
          AND NOT EXISTS(SELECT 1 FROM @Roles x WHERE x.RoleName=r.RoleName);
        INSERT dbo.UserTenantRole(UserId,TenantUid,RoleName)
        SELECT @UserId,@TenantUid,x.RoleName FROM @Roles x
        WHERE NOT EXISTS(SELECT 1 FROM dbo.UserTenantRole r WHERE r.UserId=@UserId AND r.TenantUid=@TenantUid AND r.RoleName=x.RoleName);
        UPDATE dbo.UserTenantMembership SET UpdatedAt=SYSUTCDATETIME() WHERE UserId=@UserId AND TenantUid=@TenantUid;
        DECLARE @Details NVARCHAR(MAX)=(SELECT RoleName AS [role] FROM @Roles ORDER BY RoleName FOR JSON PATH,ROOT('roles'));
        INSERT dbo.PlatformAuditEvent
            (PlatformAuditEventUid,ActorUserId,ActorType,Action,TargetTenantUid,TargetUserId,Outcome,OccurredAtUtc,CorrelationId,DetailsJson)
        VALUES(NEWID(),@ActorUserId,'TenantAdmin','TenantRolesReplaced',@TenantUid,@UserId,'Succeeded',SYSUTCDATETIME(),NEWID(),@Details);
        COMMIT;
        SELECT r.RoleName,m.UpdatedAt,m.RowVersion FROM dbo.UserTenantMembership m
        JOIN dbo.UserTenantRole r ON r.UserId=m.UserId AND r.TenantUid=m.TenantUid
        WHERE m.UserId=@UserId AND m.TenantUid=@TenantUid ORDER BY r.RoleName;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO
