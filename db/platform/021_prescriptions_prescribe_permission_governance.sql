/*
    Step 27P: govern Prescriptions.Prescribe through the existing access-profile
    and user-override permission architecture. Apply once after platform 020.
*/
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID(N'dbo.AccessProfilePermission',N'U') IS NULL
   OR OBJECT_ID(N'dbo.UserPermissionOverride',N'U') IS NULL
   OR OBJECT_ID(N'dbo.AccessProfile',N'U') IS NULL
   OR OBJECT_ID(N'dbo.AccessManagementAdministrator') IS NULL
    THROW 51510,'Platform permission-governance prerequisites are missing.',1;
GO

ALTER TABLE dbo.UserPermissionOverride DROP CONSTRAINT CK_UserPermissionOverride_Key;
GO
ALTER TABLE dbo.UserPermissionOverride WITH CHECK ADD CONSTRAINT CK_UserPermissionOverride_Key CHECK (PermissionKey IN
(N'Patients.View',N'Patients.Edit',N'Scheduling.View',N'Scheduling.Manage',N'Encounters.View',N'Encounters.Edit',N'Encounters.Sign',N'Documents.View',N'Documents.Manage',N'Templates.Use',N'Templates.Manage',N'ClinicalData.Manage',N'Prescriptions.Prescribe',N'Referrals.View',N'Referrals.Manage',N'Results.View',N'Results.Review',N'Tasks.View',N'Tasks.Manage',N'Reports.View',N'Reports.Export',N'ClinicSettings.Manage',N'Users.View',N'Users.Manage',N'Users.ManageAccess'));
GO
ALTER TABLE dbo.UserPermissionOverride CHECK CONSTRAINT CK_UserPermissionOverride_Key;
GO

CREATE OR ALTER PROCEDURE dbo.AccessProfile_SeedDefaults @TenantUid UNIQUEIDENTIFIER AS
BEGIN SET NOCOUNT ON;DECLARE @Profiles TABLE(Name NVARCHAR(100),Description NVARCHAR(500),Permissions NVARCHAR(MAX));INSERT @Profiles VALUES
(N'Clinic Administrator',N'Broad clinic administration and clinical access.',N'Patients.View,Patients.Edit,Scheduling.View,Scheduling.Manage,Encounters.View,Encounters.Edit,Encounters.Sign,Documents.View,Documents.Manage,Templates.Use,Templates.Manage,ClinicalData.Manage,Referrals.View,Referrals.Manage,Results.View,Results.Review,Tasks.View,Tasks.Manage,Reports.View,Reports.Export,ClinicSettings.Manage,Users.View,Users.Manage,Users.ManageAccess'),
(N'Physician',N'Clinical care including encounter signing.',N'Patients.View,Patients.Edit,Scheduling.View,Encounters.View,Encounters.Edit,Encounters.Sign,Documents.View,Documents.Manage,Templates.Use,ClinicalData.Manage,Prescriptions.Prescribe,Referrals.View,Referrals.Manage,Results.View,Results.Review,Tasks.View,Tasks.Manage,Reports.View'),
(N'Nurse',N'Clinical care without encounter signing.',N'Patients.View,Patients.Edit,Scheduling.View,Encounters.View,Encounters.Edit,Documents.View,Documents.Manage,Templates.Use,ClinicalData.Manage,Referrals.View,Results.View,Results.Review,Tasks.View,Tasks.Manage'),
(N'Medical Assistant',N'Conservative clinical support access.',N'Patients.View,Patients.Edit,Scheduling.View,Encounters.View,Documents.View,Templates.Use,ClinicalData.Manage,Referrals.View,Results.View,Tasks.View,Tasks.Manage'),
(N'Reception / Scheduling',N'Patient demographics and appointment management.',N'Patients.View,Patients.Edit,Scheduling.View,Scheduling.Manage'),
(N'Read Only',N'Broad read-only clinical access.',N'Patients.View,Scheduling.View,Encounters.View,Documents.View,Templates.Use,Referrals.View,Results.View,Tasks.View,Reports.View');DECLARE @Name NVARCHAR(100),@Description NVARCHAR(500),@Keys NVARCHAR(MAX),@Uid UNIQUEIDENTIFIER;DECLARE profiles CURSOR LOCAL FAST_FORWARD FOR SELECT Name,Description,Permissions FROM @Profiles;OPEN profiles;FETCH NEXT FROM profiles INTO @Name,@Description,@Keys;WHILE @@FETCH_STATUS=0 BEGIN SELECT @Uid=AccessProfileUid FROM dbo.AccessProfile WHERE TenantUid=@TenantUid AND Name=@Name;IF @Uid IS NULL BEGIN SET @Uid=NEWID();INSERT dbo.AccessProfile(AccessProfileUid,TenantUid,Name,Description,IsBuiltIn,IsActive,CreatedBy)VALUES(@Uid,@TenantUid,@Name,@Description,1,1,N'system-default');END;INSERT dbo.AccessProfilePermission SELECT @Uid,LTRIM(RTRIM(value)) FROM STRING_SPLIT(@Keys,N',') k WHERE NOT EXISTS(SELECT 1 FROM dbo.AccessProfilePermission p WHERE p.AccessProfileUid=@Uid AND p.PermissionKey=LTRIM(RTRIM(k.value)));SET @Uid=NULL;FETCH NEXT FROM profiles INTO @Name,@Description,@Keys;END;CLOSE profiles;DEALLOCATE profiles;END;
GO

INSERT dbo.AccessProfilePermission(AccessProfileUid,PermissionKey)
SELECT p.AccessProfileUid,N'Prescriptions.Prescribe'
FROM dbo.AccessProfile p
WHERE p.IsBuiltIn=1 AND p.Name=N'Physician'
  AND NOT EXISTS(SELECT 1 FROM dbo.AccessProfilePermission pp WHERE pp.AccessProfileUid=p.AccessProfileUid AND pp.PermissionKey=N'Prescriptions.Prescribe');
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
    IF EXISTS(SELECT 1 FROM @Submitted WHERE PermissionKey NOT IN(N'Patients.View',N'Patients.Edit',N'Scheduling.View',N'Scheduling.Manage',N'Encounters.View',N'Encounters.Edit',N'Encounters.Sign',N'Documents.View',N'Documents.Manage',N'Templates.Use',N'Templates.Manage',N'ClinicalData.Manage',N'Prescriptions.Prescribe',N'Referrals.View',N'Referrals.Manage',N'Results.View',N'Results.Review',N'Tasks.View',N'Tasks.Manage',N'Reports.View',N'Reports.Export',N'ClinicSettings.Manage',N'Users.View',N'Users.Manage',N'Users.ManageAccess')) THROW 51403,'Unknown permission.',1;
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
    SELECT @Current=RowVersion FROM dbo.UserTenantMembership WITH(UPDLOCK,HOLDLOCK)
    WHERE TenantUid=@TenantUid AND UserId=@UserId;
    IF @Current IS NULL THROW 51501,'Membership not found.',1;
    IF @Current<>@ExpectedMembershipRowVersion THROW 51502,'Access configuration changed.',1;
    IF @OverrideState NOT IN ('Inherit','Allow','Deny') THROW 51503,'Invalid override state.',1;
    IF @PermissionKey NOT IN
    (N'Patients.View',N'Patients.Edit',N'Scheduling.View',N'Scheduling.Manage',N'Encounters.View',N'Encounters.Edit',N'Encounters.Sign',N'Documents.View',N'Documents.Manage',N'Templates.Use',N'Templates.Manage',N'ClinicalData.Manage',N'Prescriptions.Prescribe',N'Referrals.View',N'Referrals.Manage',N'Results.View',N'Results.Review',N'Tasks.View',N'Tasks.Manage',N'Reports.View',N'Reports.Export',N'ClinicSettings.Manage',N'Users.View',N'Users.Manage',N'Users.ManageAccess') THROW 51503,'Unknown permission.',1;
    SELECT @Old=OverrideState FROM dbo.UserPermissionOverride WHERE TenantUid=@TenantUid AND UserId=@UserId AND PermissionKey=@PermissionKey;
    IF @OverrideState='Inherit' DELETE dbo.UserPermissionOverride WHERE TenantUid=@TenantUid AND UserId=@UserId AND PermissionKey=@PermissionKey;
    ELSE MERGE dbo.UserPermissionOverride AS t USING(SELECT @TenantUid TenantUid,@UserId UserId,@PermissionKey PermissionKey) s
      ON t.TenantUid=s.TenantUid AND t.UserId=s.UserId AND t.PermissionKey=s.PermissionKey
      WHEN MATCHED THEN UPDATE SET OverrideState=@OverrideState,UpdatedBy=@ActorUserId,UpdatedAt=SYSUTCDATETIME()
      WHEN NOT MATCHED THEN INSERT(TenantUid,UserId,PermissionKey,OverrideState,CreatedBy,UpdatedBy) VALUES(@TenantUid,@UserId,@PermissionKey,@OverrideState,@ActorUserId,@ActorUserId);
    UPDATE dbo.UserTenantMembership SET UpdatedAt=SYSUTCDATETIME() WHERE TenantUid=@TenantUid AND UserId=@UserId;
    IF NOT EXISTS(SELECT 1 FROM dbo.AccessManagementAdministrator(@TenantUid)) THROW 51602,'This override would remove the final access administrator.',1;
    INSERT dbo.PlatformAuditEvent VALUES(NEWID(),@ActorUserId,N'TenantAdmin',N'UserPermissionOverrideChanged',@TenantUid,@UserId,N'Succeeded',SYSUTCDATETIME(),NEWID(),
      CONCAT(N'{"permission":"',STRING_ESCAPE(@PermissionKey,'json'),N'","old":"',COALESCE(@Old,'Inherit'),N'","new":"',@OverrideState,N'"}'));
    COMMIT;
END;
GO
