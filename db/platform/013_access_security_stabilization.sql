SET XACT_ABORT ON;
GO

CREATE OR ALTER FUNCTION dbo.AccessManagementAdministrator(@TenantUid UNIQUEIDENTIFIER)
RETURNS TABLE
AS RETURN
(
    SELECT m.UserId
    FROM dbo.UserTenantMembership m
    WHERE m.TenantUid=@TenantUid AND m.MembershipStatus=N'Active'
      AND
      (
          EXISTS(SELECT 1 FROM dbo.UserPermissionOverride o
                 WHERE o.TenantUid=m.TenantUid AND o.UserId=m.UserId
                   AND o.PermissionKey=N'Users.ManageAccess' AND o.OverrideState='Allow')
          OR
          (
              NOT EXISTS(SELECT 1 FROM dbo.UserPermissionOverride o
                         WHERE o.TenantUid=m.TenantUid AND o.UserId=m.UserId
                           AND o.PermissionKey=N'Users.ManageAccess' AND o.OverrideState='Deny')
              AND EXISTS
              (
                  SELECT 1
                  FROM dbo.UserTenantAccessProfile a
                  JOIN dbo.AccessProfile p ON p.AccessProfileUid=a.AccessProfileUid
                       AND p.TenantUid=m.TenantUid AND p.IsActive=1
                  JOIN dbo.AccessProfilePermission pp ON pp.AccessProfileUid=p.AccessProfileUid
                  WHERE a.TenantUid=m.TenantUid AND a.UserId=m.UserId
                    AND pp.PermissionKey=N'Users.ManageAccess'
              )
          )
      )
);
GO

CREATE OR ALTER PROCEDURE dbo.PlatformMembership_Deactivate
    @UserId NVARCHAR(450),@TenantUid UNIQUEIDENTIFIER,
    @ExpectedRowVersion BINARY(8),@ActorUserId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRANSACTION;
    DECLARE @LockResult INT,@Status VARCHAR(30),@Current BINARY(8);
    DECLARE @LockResource NVARCHAR(255)=CONCAT(N'MicroEMR:AccessAdmin:',@TenantUid);
    EXEC @LockResult=sys.sp_getapplock @Resource=@LockResource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
    IF @LockResult<0 THROW 51601,'Access administration is busy. Try again.',1;
    SELECT @Status=MembershipStatus,@Current=RowVersion FROM dbo.UserTenantMembership WITH(UPDLOCK,HOLDLOCK) WHERE TenantUid=@TenantUid AND UserId=@UserId;
    IF @Status IS NULL THROW 51303,'Membership not found.',1;
    IF @UserId=@ActorUserId THROW 51306,'Current administrator membership cannot be deactivated.',1;
    IF @Status<>'Active' THROW 51305,'Membership is not active.',1;
    IF @ExpectedRowVersion IS NULL OR @Current<>@ExpectedRowVersion THROW 51307,'Membership has changed.',1;
    UPDATE dbo.UserTenantMembership SET MembershipStatus='Inactive',IsDefaultTenant=0,UpdatedAt=SYSUTCDATETIME() WHERE TenantUid=@TenantUid AND UserId=@UserId;
    IF NOT EXISTS(SELECT 1 FROM dbo.AccessManagementAdministrator(@TenantUid)) THROW 51602,'The last user able to manage access cannot be deactivated.',1;
    INSERT dbo.PlatformAuditEvent VALUES(NEWID(),@ActorUserId,N'TenantAdmin',N'MembershipDeactivated',@TenantUid,@UserId,N'Succeeded',SYSUTCDATETIME(),NEWID(),N'{"previousStatus":"Active","newStatus":"Inactive"}');
    COMMIT;
    SELECT MembershipStatus,UpdatedAt,RowVersion FROM dbo.UserTenantMembership WHERE TenantUid=@TenantUid AND UserId=@UserId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.AccessProfile_AssignUser
    @TenantUid UNIQUEIDENTIFIER,@UserId NVARCHAR(450),@AccessProfileUid UNIQUEIDENTIFIER,
    @ExpectedMembershipRowVersion BINARY(8),@ActorUserId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRANSACTION;
    DECLARE @LockResult INT,@Current BINARY(8),@Old UNIQUEIDENTIFIER;
    DECLARE @LockResource NVARCHAR(255)=CONCAT(N'MicroEMR:AccessAdmin:',@TenantUid);
    EXEC @LockResult=sys.sp_getapplock @Resource=@LockResource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
    IF @LockResult<0 THROW 51601,'Access administration is busy. Try again.',1;
    SELECT @Current=RowVersion FROM dbo.UserTenantMembership WITH(UPDLOCK,HOLDLOCK) WHERE TenantUid=@TenantUid AND UserId=@UserId;
    IF @Current IS NULL THROW 51303,'Membership not found.',1;
    IF @Current<>@ExpectedMembershipRowVersion THROW 51307,'Membership changed.',1;
    IF NOT EXISTS(SELECT 1 FROM dbo.AccessProfile WHERE TenantUid=@TenantUid AND AccessProfileUid=@AccessProfileUid AND IsActive=1) THROW 51401,'Active profile not found.',1;
    SELECT @Old=AccessProfileUid FROM dbo.UserTenantAccessProfile WHERE TenantUid=@TenantUid AND UserId=@UserId;
    MERGE dbo.UserTenantAccessProfile AS t USING(SELECT @UserId UserId,@TenantUid TenantUid) s ON t.UserId=s.UserId AND t.TenantUid=s.TenantUid
    WHEN MATCHED THEN UPDATE SET AccessProfileUid=@AccessProfileUid,AssignedBy=@ActorUserId,AssignedAt=SYSUTCDATETIME()
    WHEN NOT MATCHED THEN INSERT(UserId,TenantUid,AccessProfileUid,AssignedBy) VALUES(@UserId,@TenantUid,@AccessProfileUid,@ActorUserId);
    UPDATE dbo.UserTenantMembership SET UpdatedAt=SYSUTCDATETIME() WHERE TenantUid=@TenantUid AND UserId=@UserId;
    IF NOT EXISTS(SELECT 1 FROM dbo.AccessManagementAdministrator(@TenantUid)) THROW 51602,'This profile assignment would remove the final access administrator.',1;
    INSERT dbo.PlatformAuditEvent VALUES(NEWID(),@ActorUserId,N'TenantAdmin',N'UserAccessProfileChanged',@TenantUid,@UserId,N'Succeeded',SYSUTCDATETIME(),NEWID(),CONCAT(N'{"old":"',COALESCE(CONVERT(NVARCHAR(36),@Old),N''),N'","new":"',@AccessProfileUid,N'"}'));
    COMMIT;
END;
GO

CREATE OR ALTER PROCEDURE dbo.AccessProfile_ReplacePermissions
    @TenantUid UNIQUEIDENTIFIER,@AccessProfileUid UNIQUEIDENTIFIER,@PermissionKeys NVARCHAR(MAX),
    @ExpectedRowVersion BINARY(8),@ActorUserId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRANSACTION;
    DECLARE @LockResult INT,@Current BINARY(8);
    DECLARE @LockResource NVARCHAR(255)=CONCAT(N'MicroEMR:AccessAdmin:',@TenantUid);
    EXEC @LockResult=sys.sp_getapplock @Resource=@LockResource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
    IF @LockResult<0 THROW 51601,'Access administration is busy. Try again.',1;
    SELECT @Current=RowVersion FROM dbo.AccessProfile WITH(UPDLOCK,HOLDLOCK) WHERE TenantUid=@TenantUid AND AccessProfileUid=@AccessProfileUid AND IsActive=1;
    IF @Current IS NULL THROW 51401,'Profile not found.',1;
    IF @Current<>@ExpectedRowVersion THROW 51402,'Profile changed.',1;
    DECLARE @Submitted TABLE(PermissionKey NVARCHAR(100) PRIMARY KEY);
    INSERT @Submitted SELECT DISTINCT LTRIM(RTRIM(value)) FROM STRING_SPLIT(@PermissionKeys,N',') WHERE LTRIM(RTRIM(value))<>N'';
    IF NOT EXISTS(SELECT 1 FROM @Submitted) THROW 51403,'At least one permission is required.',1;
    IF EXISTS(SELECT 1 FROM @Submitted WHERE PermissionKey NOT IN(N'Patients.View',N'Patients.Edit',N'Scheduling.View',N'Scheduling.Manage',N'Encounters.View',N'Encounters.Edit',N'Encounters.Sign',N'Documents.View',N'Documents.Manage',N'Templates.Use',N'Templates.Manage',N'ClinicalData.Manage',N'Referrals.View',N'Referrals.Manage',N'Results.View',N'Results.Review',N'Tasks.View',N'Tasks.Manage',N'Reports.View',N'Reports.Export',N'ClinicSettings.Manage',N'Users.View',N'Users.Manage',N'Users.ManageAccess')) THROW 51403,'Unknown permission.',1;
    DECLARE @Old NVARCHAR(MAX)=(SELECT PermissionKey FROM dbo.AccessProfilePermission WHERE AccessProfileUid=@AccessProfileUid ORDER BY PermissionKey FOR JSON PATH);
    DELETE dbo.AccessProfilePermission WHERE AccessProfileUid=@AccessProfileUid;
    INSERT dbo.AccessProfilePermission SELECT @AccessProfileUid,PermissionKey FROM @Submitted;
    UPDATE dbo.AccessProfile SET UpdatedBy=@ActorUserId,UpdatedAt=SYSUTCDATETIME() WHERE AccessProfileUid=@AccessProfileUid;
    IF NOT EXISTS(SELECT 1 FROM dbo.AccessManagementAdministrator(@TenantUid)) THROW 51602,'This profile change would remove the final access administrator.',1;
    DECLARE @New NVARCHAR(MAX)=(SELECT PermissionKey FROM dbo.AccessProfilePermission WHERE AccessProfileUid=@AccessProfileUid ORDER BY PermissionKey FOR JSON PATH);
    INSERT dbo.PlatformAuditEvent VALUES(NEWID(),@ActorUserId,N'TenantAdmin',N'AccessProfilePermissionsChanged',@TenantUid,NULL,N'Succeeded',SYSUTCDATETIME(),NEWID(),CONCAT(N'{"profileUid":"',@AccessProfileUid,N'","old":',@Old,N',"new":',@New,N'}'));
    COMMIT;
END;
GO

CREATE OR ALTER PROCEDURE dbo.UserPermissionOverride_Set
    @TenantUid UNIQUEIDENTIFIER,@UserId NVARCHAR(450),@PermissionKey NVARCHAR(100),
    @OverrideState VARCHAR(7),@ExpectedMembershipRowVersion BINARY(8),@ActorUserId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRANSACTION;
    DECLARE @LockResult INT,@Current BINARY(8),@Old VARCHAR(5);
    DECLARE @LockResource NVARCHAR(255)=CONCAT(N'MicroEMR:AccessAdmin:',@TenantUid);
    EXEC @LockResult=sys.sp_getapplock @Resource=@LockResource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
    IF @LockResult<0 THROW 51601,'Access administration is busy. Try again.',1;
    SELECT @Current=RowVersion FROM dbo.UserTenantMembership WITH(UPDLOCK,HOLDLOCK) WHERE TenantUid=@TenantUid AND UserId=@UserId;
    IF @Current IS NULL THROW 51501,'Membership not found.',1;
    IF @Current<>@ExpectedMembershipRowVersion THROW 51502,'Access configuration changed.',1;
    IF @OverrideState NOT IN ('Inherit','Allow','Deny') THROW 51503,'Invalid override state.',1;
    IF @PermissionKey NOT IN(N'Patients.View',N'Patients.Edit',N'Scheduling.View',N'Scheduling.Manage',N'Encounters.View',N'Encounters.Edit',N'Encounters.Sign',N'Documents.View',N'Documents.Manage',N'Templates.Use',N'Templates.Manage',N'ClinicalData.Manage',N'Referrals.View',N'Referrals.Manage',N'Results.View',N'Results.Review',N'Tasks.View',N'Tasks.Manage',N'Reports.View',N'Reports.Export',N'ClinicSettings.Manage',N'Users.View',N'Users.Manage',N'Users.ManageAccess') THROW 51503,'Unknown permission.',1;
    SELECT @Old=OverrideState FROM dbo.UserPermissionOverride WHERE TenantUid=@TenantUid AND UserId=@UserId AND PermissionKey=@PermissionKey;
    IF @OverrideState='Inherit' DELETE dbo.UserPermissionOverride WHERE TenantUid=@TenantUid AND UserId=@UserId AND PermissionKey=@PermissionKey;
    ELSE MERGE dbo.UserPermissionOverride AS t USING(SELECT @TenantUid TenantUid,@UserId UserId,@PermissionKey PermissionKey) s ON t.TenantUid=s.TenantUid AND t.UserId=s.UserId AND t.PermissionKey=s.PermissionKey
      WHEN MATCHED THEN UPDATE SET OverrideState=@OverrideState,UpdatedBy=@ActorUserId,UpdatedAt=SYSUTCDATETIME()
      WHEN NOT MATCHED THEN INSERT(TenantUid,UserId,PermissionKey,OverrideState,CreatedBy,UpdatedBy) VALUES(@TenantUid,@UserId,@PermissionKey,@OverrideState,@ActorUserId,@ActorUserId);
    UPDATE dbo.UserTenantMembership SET UpdatedAt=SYSUTCDATETIME() WHERE TenantUid=@TenantUid AND UserId=@UserId;
    IF NOT EXISTS(SELECT 1 FROM dbo.AccessManagementAdministrator(@TenantUid)) THROW 51602,'This override would remove the final access administrator.',1;
    INSERT dbo.PlatformAuditEvent VALUES(NEWID(),@ActorUserId,N'TenantAdmin',N'UserPermissionOverrideChanged',@TenantUid,@UserId,N'Succeeded',SYSUTCDATETIME(),NEWID(),CONCAT(N'{"permission":"',STRING_ESCAPE(@PermissionKey,'json'),N'","old":"',COALESCE(@Old,'Inherit'),N'","new":"',@OverrideState,N'"}'));
    COMMIT;
END;
GO
