CREATE OR ALTER PROCEDURE dbo.PlatformMembership_CreateWithInitialRole
    @UserId NVARCHAR(450),
    @TenantUid UNIQUEIDENTIFIER,
    @RoleName NVARCHAR(100),
    @ActorUserId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @UserId IS NULL OR LEN(LTRIM(RTRIM(@UserId)))=0 THROW 51303,'User is required.',1;
    IF @ActorUserId IS NULL OR LEN(LTRIM(RTRIM(@ActorUserId)))=0 THROW 51303,'Actor is required.',1;
    IF @RoleName NOT IN (N'Physician',N'Nurse',N'MedicalAssistant',N'Scheduler',N'ClinicAdministrator')
        THROW 51310,'Invalid tenant role.',1;
    BEGIN TRANSACTION;
    IF NOT EXISTS(SELECT 1 FROM dbo.Tenant WITH(UPDLOCK,HOLDLOCK) WHERE TenantUid=@TenantUid AND TenantStatus=N'Active')
    BEGIN ROLLBACK; THROW 51302,'Tenant is not active.',1; END;
    IF EXISTS(SELECT 1 FROM dbo.UserTenantMembership WITH(UPDLOCK,HOLDLOCK) WHERE UserId=@UserId AND TenantUid=@TenantUid)
    BEGIN ROLLBACK; THROW 51301,'Membership exists.',1; END;
    INSERT dbo.UserTenantMembership(UserId,TenantUid,MembershipStatus,IsDefaultTenant,CreatedAt)
        VALUES(LTRIM(RTRIM(@UserId)),@TenantUid,N'Active',0,SYSUTCDATETIME());
    INSERT dbo.UserTenantRole(UserId,TenantUid,RoleName)
        VALUES(LTRIM(RTRIM(@UserId)),@TenantUid,@RoleName);
    INSERT dbo.PlatformAuditEvent
        (PlatformAuditEventUid,ActorUserId,ActorType,Action,TargetTenantUid,TargetUserId,Outcome,OccurredAtUtc,CorrelationId,DetailsJson)
        VALUES(NEWID(),LTRIM(RTRIM(@ActorUserId)),N'TenantAdmin',N'TenantUserCreated',@TenantUid,LTRIM(RTRIM(@UserId)),
            N'Succeeded',SYSUTCDATETIME(),NEWID(),
            CONCAT(N'{"membershipStatus":"Active","initialRole":"',STRING_ESCAPE(@RoleName,'json'),N'"}'));
    COMMIT;
END;
