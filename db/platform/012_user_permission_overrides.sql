SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.UserPermissionOverride', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserPermissionOverride
    (
        UserPermissionOverrideUid UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_UserPermissionOverride_Uid DEFAULT NEWSEQUENTIALID(),
        TenantUid UNIQUEIDENTIFIER NOT NULL,
        UserId NVARCHAR(450) NOT NULL,
        PermissionKey NVARCHAR(100) NOT NULL,
        OverrideState VARCHAR(5) NOT NULL,
        CreatedBy NVARCHAR(450) NOT NULL,
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_UserPermissionOverride_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedBy NVARCHAR(450) NOT NULL,
        UpdatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_UserPermissionOverride_UpdatedAt DEFAULT SYSUTCDATETIME(),
        RowVersion ROWVERSION NOT NULL,
        OverrideIdentityHash AS CONVERT(BINARY(32), HASHBYTES('SHA2_256',
            CONCAT(CONVERT(NVARCHAR(36), TenantUid), N'|', UserId, N'|', PermissionKey))) PERSISTED,
        CONSTRAINT PK_UserPermissionOverride PRIMARY KEY CLUSTERED (UserPermissionOverrideUid),
        CONSTRAINT UQ_UserPermissionOverride_IdentityHash UNIQUE NONCLUSTERED (OverrideIdentityHash),
        CONSTRAINT FK_UserPermissionOverride_Membership FOREIGN KEY (UserId, TenantUid)
            REFERENCES dbo.UserTenantMembership(UserId, TenantUid),
        CONSTRAINT CK_UserPermissionOverride_State CHECK (OverrideState IN ('Allow', 'Deny')),
        CONSTRAINT CK_UserPermissionOverride_Key CHECK (PermissionKey IN
        (N'Patients.View',N'Patients.Edit',N'Scheduling.View',N'Scheduling.Manage',N'Encounters.View',N'Encounters.Edit',N'Encounters.Sign',N'Documents.View',N'Documents.Manage',N'Templates.Use',N'Templates.Manage',N'ClinicalData.Manage',N'Referrals.View',N'Referrals.Manage',N'Results.View',N'Results.Review',N'Tasks.View',N'Tasks.Manage',N'Reports.View',N'Reports.Export',N'ClinicSettings.Manage',N'Users.View',N'Users.Manage',N'Users.ManageAccess'))
    );
    CREATE INDEX IX_UserPermissionOverride_TenantUser ON dbo.UserPermissionOverride(TenantUid, UserPermissionOverrideUid)
        INCLUDE(UserId, PermissionKey, OverrideState, RowVersion);
END;
GO

CREATE OR ALTER PROCEDURE dbo.AccessProfile_GetEffective
    @TenantUid UNIQUEIDENTIFIER,
    @UserId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT m.MembershipStatus, effective.PermissionKey
    FROM dbo.UserTenantMembership m
    OUTER APPLY
    (
        SELECT pp.PermissionKey
        FROM dbo.UserTenantAccessProfile a
        JOIN dbo.AccessProfile p ON p.AccessProfileUid=a.AccessProfileUid AND p.TenantUid=m.TenantUid AND p.IsActive=1
        JOIN dbo.AccessProfilePermission pp ON pp.AccessProfileUid=p.AccessProfileUid
        WHERE a.UserId=m.UserId AND a.TenantUid=m.TenantUid
          AND NOT EXISTS (SELECT 1 FROM dbo.UserPermissionOverride o WHERE o.TenantUid=m.TenantUid AND o.UserId=m.UserId AND o.PermissionKey=pp.PermissionKey AND o.OverrideState='Deny')
        UNION
        SELECT o.PermissionKey FROM dbo.UserPermissionOverride o
        WHERE o.TenantUid=m.TenantUid AND o.UserId=m.UserId AND o.OverrideState='Allow'
    ) effective
    WHERE m.TenantUid=@TenantUid AND m.UserId=@UserId
    ORDER BY effective.PermissionKey;
END;
GO

CREATE OR ALTER PROCEDURE dbo.UserPermissionAccess_Get
    @TenantUid UNIQUEIDENTIFIER,
    @UserId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT m.MembershipStatus,a.AccessProfileUid,p.Name,m.RowVersion
    FROM dbo.UserTenantMembership m
    LEFT JOIN dbo.UserTenantAccessProfile a ON a.UserId=m.UserId AND a.TenantUid=m.TenantUid
    LEFT JOIN dbo.AccessProfile p ON p.AccessProfileUid=a.AccessProfileUid AND p.TenantUid=m.TenantUid
    WHERE m.TenantUid=@TenantUid AND m.UserId=@UserId;

    SELECT COALESCE(pp.PermissionKey,o.PermissionKey) PermissionKey, CAST(CASE WHEN pp.PermissionKey IS NULL THEN 0 ELSE 1 END AS BIT) ProfileAllowed, o.OverrideState, o.RowVersion
    FROM dbo.UserTenantMembership m
    LEFT JOIN dbo.UserTenantAccessProfile a ON a.UserId=m.UserId AND a.TenantUid=m.TenantUid
    LEFT JOIN dbo.AccessProfilePermission pp ON pp.AccessProfileUid=a.AccessProfileUid
    FULL JOIN dbo.UserPermissionOverride o ON o.TenantUid=@TenantUid AND o.UserId=@UserId AND o.PermissionKey=pp.PermissionKey
    WHERE (m.TenantUid=@TenantUid AND m.UserId=@UserId) OR o.UserPermissionOverrideUid IS NOT NULL;
END;
GO

CREATE OR ALTER PROCEDURE dbo.UserPermissionOverride_Set
    @TenantUid UNIQUEIDENTIFIER,@UserId NVARCHAR(450),@PermissionKey NVARCHAR(100),
    @OverrideState VARCHAR(7),@ExpectedMembershipRowVersion BINARY(8),@ActorUserId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRANSACTION;
    DECLARE @Current BINARY(8),@Old VARCHAR(5);
    SELECT @Current=RowVersion FROM dbo.UserTenantMembership WITH(UPDLOCK,HOLDLOCK)
    WHERE TenantUid=@TenantUid AND UserId=@UserId;
    IF @Current IS NULL THROW 51501,'Membership not found.',1;
    IF @Current<>@ExpectedMembershipRowVersion THROW 51502,'Access configuration changed.',1;
    IF @OverrideState NOT IN ('Inherit','Allow','Deny') THROW 51503,'Invalid override state.',1;
    IF @PermissionKey NOT IN
    (N'Patients.View',N'Patients.Edit',N'Scheduling.View',N'Scheduling.Manage',N'Encounters.View',N'Encounters.Edit',N'Encounters.Sign',N'Documents.View',N'Documents.Manage',N'Templates.Use',N'Templates.Manage',N'ClinicalData.Manage',N'Referrals.View',N'Referrals.Manage',N'Results.View',N'Results.Review',N'Tasks.View',N'Tasks.Manage',N'Reports.View',N'Reports.Export',N'ClinicSettings.Manage',N'Users.View',N'Users.Manage',N'Users.ManageAccess') THROW 51503,'Unknown permission.',1;
    IF @PermissionKey=N'Users.ManageAccess' AND @OverrideState='Deny'
       AND EXISTS(SELECT 1 FROM dbo.UserTenantRole WHERE TenantUid=@TenantUid AND UserId=@UserId AND RoleName=N'ClinicAdministrator')
       AND 1=(SELECT COUNT(DISTINCT r.UserId) FROM dbo.UserTenantRole r JOIN dbo.UserTenantMembership m ON m.TenantUid=r.TenantUid AND m.UserId=r.UserId WHERE r.TenantUid=@TenantUid AND r.RoleName=N'ClinicAdministrator' AND m.MembershipStatus=N'Active')
       THROW 51504,'The last active clinic administrator cannot be denied access management.',1;
    SELECT @Old=OverrideState FROM dbo.UserPermissionOverride WHERE TenantUid=@TenantUid AND UserId=@UserId AND PermissionKey=@PermissionKey;
    IF @OverrideState='Inherit' DELETE dbo.UserPermissionOverride WHERE TenantUid=@TenantUid AND UserId=@UserId AND PermissionKey=@PermissionKey;
    ELSE MERGE dbo.UserPermissionOverride AS t USING(SELECT @TenantUid TenantUid,@UserId UserId,@PermissionKey PermissionKey) s
      ON t.TenantUid=s.TenantUid AND t.UserId=s.UserId AND t.PermissionKey=s.PermissionKey
      WHEN MATCHED THEN UPDATE SET OverrideState=@OverrideState,UpdatedBy=@ActorUserId,UpdatedAt=SYSUTCDATETIME()
      WHEN NOT MATCHED THEN INSERT(TenantUid,UserId,PermissionKey,OverrideState,CreatedBy,UpdatedBy) VALUES(@TenantUid,@UserId,@PermissionKey,@OverrideState,@ActorUserId,@ActorUserId);
    UPDATE dbo.UserTenantMembership SET UpdatedAt=SYSUTCDATETIME() WHERE TenantUid=@TenantUid AND UserId=@UserId;
    INSERT dbo.PlatformAuditEvent VALUES(NEWID(),@ActorUserId,N'TenantAdmin',N'UserPermissionOverrideChanged',@TenantUid,@UserId,N'Succeeded',SYSUTCDATETIME(),NEWID(),
      CONCAT(N'{"permission":"',STRING_ESCAPE(@PermissionKey,'json'),N'","old":"',COALESCE(@Old,'Inherit'),N'","new":"',@OverrideState,N'"}'));
    COMMIT;
END;
GO
