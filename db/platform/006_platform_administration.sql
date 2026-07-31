USE MicroEMR_Platform;
GO

IF OBJECT_ID(N'dbo.PlatformAuditEvent', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PlatformAuditEvent
    (
        PlatformAuditEventUid UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PlatformAuditEvent PRIMARY KEY,
        ActorUserId NVARCHAR(450) NULL, ActorType VARCHAR(30) NOT NULL,
        Action NVARCHAR(100) NOT NULL, TargetTenantUid UNIQUEIDENTIFIER NULL,
        TargetUserId NVARCHAR(450) NULL, Outcome VARCHAR(30) NOT NULL,
        OccurredAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_PlatformAuditEvent_Occurred DEFAULT SYSUTCDATETIME(),
        CorrelationId UNIQUEIDENTIFIER NOT NULL, DetailsJson NVARCHAR(2000) NULL,
        CONSTRAINT CK_PlatformAuditEvent_Outcome CHECK (Outcome IN ('Succeeded','Failed'))
    );
    CREATE INDEX IX_PlatformAuditEvent_TenantTime ON dbo.PlatformAuditEvent(TargetTenantUid, OccurredAtUtc DESC);
END;
GO

IF COL_LENGTH('dbo.Tenant', 'RowVersion') IS NULL ALTER TABLE dbo.Tenant ADD RowVersion ROWVERSION NOT NULL;
IF COL_LENGTH('dbo.TenantDatabase', 'RowVersion') IS NULL ALTER TABLE dbo.TenantDatabase ADD RowVersion ROWVERSION NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.UserTenantMembership') AND name=N'UX_UserTenantMembership_ActiveDefault')
    CREATE UNIQUE INDEX UX_UserTenantMembership_ActiveDefault ON dbo.UserTenantMembership(UserId)
    WHERE IsDefaultTenant=1 AND MembershipStatus='Active';
GO

CREATE OR ALTER PROCEDURE dbo.PlatformTenant_List AS
BEGIN
 SET NOCOUNT ON;
 SELECT t.TenantUid,t.TenantKey,t.DisplayName,t.TenantStatus,t.DefaultTimeZoneId,d.DatabaseStatus,d.CurrentSchemaVersion,d.LastMigrationAt
 FROM dbo.Tenant t LEFT JOIN dbo.TenantDatabase d ON d.TenantUid=t.TenantUid ORDER BY t.TenantKey;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PlatformTenant_GetByUid @TenantUid UNIQUEIDENTIFIER AS
BEGIN SET NOCOUNT ON;
 SELECT t.TenantUid,t.TenantKey,t.DisplayName,t.TenantStatus,t.DefaultTimeZoneId,t.CreatedAt,t.ActivatedAt,t.SuspendedAt,
 d.DatabaseServerKey,d.DatabaseName,d.DatabaseStatus,d.CurrentSchemaVersion,d.LastMigrationAt,d.UpdatedAt
 FROM dbo.Tenant t LEFT JOIN dbo.TenantDatabase d ON d.TenantUid=t.TenantUid WHERE t.TenantUid=@TenantUid;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PlatformTenant_GetByKey @TenantKey NVARCHAR(50) AS
BEGIN SET NOCOUNT ON;
 SELECT t.TenantUid,t.TenantKey,t.DisplayName,t.TenantStatus,t.DefaultTimeZoneId,t.CreatedAt,t.ActivatedAt,t.SuspendedAt,
 d.DatabaseServerKey,d.DatabaseName,d.DatabaseStatus,d.CurrentSchemaVersion,d.LastMigrationAt,d.UpdatedAt
 FROM dbo.Tenant t LEFT JOIN dbo.TenantDatabase d ON d.TenantUid=t.TenantUid WHERE t.TenantKey=LOWER(LTRIM(RTRIM(@TenantKey)));
END;
GO

CREATE OR ALTER PROCEDURE dbo.PlatformTenant_Create
 @TenantUid UNIQUEIDENTIFIER,@TenantKey NVARCHAR(50),@DisplayName NVARCHAR(200),@DefaultTimeZoneId NVARCHAR(100),@ActorUserId NVARCHAR(450)
AS
BEGIN SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @TenantUid='00000000-0000-0000-0000-000000000000' OR NULLIF(LTRIM(RTRIM(@TenantKey)),N'') IS NULL OR NULLIF(LTRIM(RTRIM(@DisplayName)),N'') IS NULL OR NULLIF(LTRIM(RTRIM(@DefaultTimeZoneId)),N'') IS NULL THROW 51200,'Invalid tenant metadata.',1;
 BEGIN TRANSACTION;
 IF EXISTS(SELECT 1 FROM dbo.Tenant WITH(UPDLOCK,HOLDLOCK) WHERE TenantUid=@TenantUid OR TenantKey=LOWER(LTRIM(RTRIM(@TenantKey)))) THROW 51201,'Duplicate tenant.',1;
 INSERT dbo.Tenant(TenantUid,TenantKey,DisplayName,TenantStatus,DefaultTimeZoneId,CreatedAt) VALUES(@TenantUid,LOWER(LTRIM(RTRIM(@TenantKey))),LTRIM(RTRIM(@DisplayName)),'Provisioning',LTRIM(RTRIM(@DefaultTimeZoneId)),SYSUTCDATETIME());
 INSERT dbo.PlatformAuditEvent VALUES(NEWID(),NULLIF(LTRIM(RTRIM(@ActorUserId)),N''),'LocalCli','TenantCreated',@TenantUid,NULL,'Succeeded',SYSUTCDATETIME(),NEWID(),N'{"status":"Provisioning"}');
 COMMIT;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PlatformTenantDatabase_UpsertProvisioning
 @TenantUid UNIQUEIDENTIFIER,@DatabaseServerKey NVARCHAR(100),@DatabaseName SYSNAME,@SecretReference NVARCHAR(500),@ActorUserId NVARCHAR(450)
AS
BEGIN SET NOCOUNT ON; SET XACT_ABORT ON;
 IF NULLIF(LTRIM(RTRIM(@DatabaseServerKey)),N'') IS NULL OR NULLIF(LTRIM(RTRIM(@DatabaseName)),N'') IS NULL OR NULLIF(LTRIM(RTRIM(@SecretReference)),N'') IS NULL THROW 51200,'Invalid assignment metadata.',1;
 BEGIN TRANSACTION;
 DECLARE @TenantStatus VARCHAR(30); SELECT @TenantStatus=TenantStatus FROM dbo.Tenant WITH(UPDLOCK,HOLDLOCK) WHERE TenantUid=@TenantUid;
 IF @TenantStatus IS NULL THROW 51202,'Tenant not found.',1; IF @TenantStatus='Archived' THROW 51205,'Archived tenant.',1;
 IF EXISTS(SELECT 1 FROM dbo.TenantDatabase WHERE TenantUid=@TenantUid AND DatabaseStatus='Active') THROW 51203,'Active assignment cannot be overwritten.',1;
 IF EXISTS(SELECT 1 FROM dbo.TenantDatabase WHERE TenantUid=@TenantUid)
  UPDATE dbo.TenantDatabase SET DatabaseServerKey=LTRIM(RTRIM(@DatabaseServerKey)),DatabaseName=LTRIM(RTRIM(@DatabaseName)),SecretReference=LTRIM(RTRIM(@SecretReference)),DatabaseStatus='Provisioning',CurrentSchemaVersion=NULL,UpdatedAt=SYSUTCDATETIME() WHERE TenantUid=@TenantUid;
 ELSE INSERT dbo.TenantDatabase(TenantUid,DatabaseServerKey,DatabaseName,SecretReference,DatabaseStatus,CreatedAt) VALUES(@TenantUid,LTRIM(RTRIM(@DatabaseServerKey)),LTRIM(RTRIM(@DatabaseName)),LTRIM(RTRIM(@SecretReference)),'Provisioning',SYSUTCDATETIME());
 INSERT dbo.PlatformAuditEvent VALUES(NEWID(),NULLIF(LTRIM(RTRIM(@ActorUserId)),N''),'LocalCli','DatabaseAssignmentChanged',@TenantUid,NULL,'Succeeded',SYSUTCDATETIME(),NEWID(),N'{"status":"Provisioning"}'); COMMIT;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PlatformTenant_SetStatus @TenantUid UNIQUEIDENTIFIER,@NewStatus VARCHAR(30),@ActorUserId NVARCHAR(450) AS
BEGIN SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRANSACTION;
 DECLARE @Old VARCHAR(30); SELECT @Old=TenantStatus FROM dbo.Tenant WITH(UPDLOCK,HOLDLOCK) WHERE TenantUid=@TenantUid;
 IF @Old IS NULL THROW 51202,'Tenant not found.',1;
 IF NOT ((@Old='Provisioning' AND @NewStatus IN('Active','Suspended','Archived')) OR (@Old='Active' AND @NewStatus IN('Suspended','Archived')) OR (@Old='Suspended' AND @NewStatus IN('Active','Archived'))) THROW 51205,'Invalid transition.',1;
 IF @NewStatus='Active' AND NOT EXISTS(SELECT 1 FROM dbo.TenantDatabase WHERE TenantUid=@TenantUid AND DatabaseStatus='Active' AND NULLIF(CurrentSchemaVersion,N'') IS NOT NULL) THROW 51204,'Database not ready.',1;
 UPDATE dbo.Tenant SET TenantStatus=@NewStatus,ActivatedAt=CASE WHEN @NewStatus='Active' THEN COALESCE(ActivatedAt,SYSUTCDATETIME()) ELSE ActivatedAt END,SuspendedAt=CASE WHEN @NewStatus='Suspended' THEN SYSUTCDATETIME() WHEN @NewStatus='Active' THEN NULL ELSE SuspendedAt END WHERE TenantUid=@TenantUid;
 INSERT dbo.PlatformAuditEvent VALUES(NEWID(),NULLIF(LTRIM(RTRIM(@ActorUserId)),N''),'LocalCli',CONCAT('Tenant',@NewStatus),@TenantUid,NULL,'Succeeded',SYSUTCDATETIME(),NEWID(),CONCAT(N'{"previousStatus":"',@Old,N'","newStatus":"',@NewStatus,N'"}')); COMMIT;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PlatformMembership_Add @UserId NVARCHAR(450),@TenantUid UNIQUEIDENTIFIER,@IsDefaultTenant BIT,@ActorUserId NVARCHAR(450) AS
BEGIN SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRANSACTION;
 IF EXISTS(SELECT 1 FROM dbo.UserTenantMembership WITH(UPDLOCK,HOLDLOCK) WHERE UserId=@UserId AND TenantUid=@TenantUid) THROW 51301,'Membership exists.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.Tenant WHERE TenantUid=@TenantUid AND TenantStatus='Active') THROW 51302,'Tenant inactive.',1;
 IF @IsDefaultTenant=1 AND EXISTS(SELECT 1 FROM dbo.UserTenantMembership WITH(UPDLOCK,HOLDLOCK) WHERE UserId=@UserId AND MembershipStatus='Active' AND IsDefaultTenant=1) THROW 51304,'Default exists.',1;
 INSERT dbo.UserTenantMembership(UserId,TenantUid,MembershipStatus,IsDefaultTenant,CreatedAt) VALUES(LTRIM(RTRIM(@UserId)),@TenantUid,'Active',@IsDefaultTenant,SYSUTCDATETIME());
 INSERT dbo.PlatformAuditEvent VALUES(NEWID(),@ActorUserId,'LocalCli','MembershipAdded',@TenantUid,@UserId,'Succeeded',SYSUTCDATETIME(),NEWID(),N'{}'); COMMIT;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PlatformMembership_SetStatus @UserId NVARCHAR(450),@TenantUid UNIQUEIDENTIFIER,@MembershipStatus VARCHAR(30),@ActorUserId NVARCHAR(450) AS
BEGIN SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRANSACTION;
 UPDATE dbo.UserTenantMembership WITH(UPDLOCK) SET MembershipStatus=@MembershipStatus,IsDefaultTenant=CASE WHEN @MembershipStatus='Active' THEN IsDefaultTenant ELSE 0 END,UpdatedAt=SYSUTCDATETIME() WHERE UserId=@UserId AND TenantUid=@TenantUid; IF @@ROWCOUNT<>1 THROW 51303,'Membership not found.',1;
 INSERT dbo.PlatformAuditEvent VALUES(NEWID(),@ActorUserId,'LocalCli','MembershipStatusChanged',@TenantUid,@UserId,'Succeeded',SYSUTCDATETIME(),NEWID(),CONCAT(N'{"status":"',@MembershipStatus,N'"}')); COMMIT;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PlatformMembership_SetDefault @UserId NVARCHAR(450),@TenantUid UNIQUEIDENTIFIER,@IsDefaultTenant BIT,@ActorUserId NVARCHAR(450) AS
BEGIN SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRANSACTION;
 IF @IsDefaultTenant=1 UPDATE dbo.UserTenantMembership WITH(UPDLOCK,HOLDLOCK) SET IsDefaultTenant=0,UpdatedAt=SYSUTCDATETIME() WHERE UserId=@UserId AND IsDefaultTenant=1;
 UPDATE dbo.UserTenantMembership SET IsDefaultTenant=@IsDefaultTenant,UpdatedAt=SYSUTCDATETIME() WHERE UserId=@UserId AND TenantUid=@TenantUid AND MembershipStatus='Active'; IF @@ROWCOUNT<>1 THROW 51303,'Active membership not found.',1;
 INSERT dbo.PlatformAuditEvent VALUES(NEWID(),@ActorUserId,'LocalCli','DefaultMembershipChanged',@TenantUid,@UserId,'Succeeded',SYSUTCDATETIME(),NEWID(),CONCAT(N'{"isDefault":',CASE WHEN @IsDefaultTenant=1 THEN N'true' ELSE N'false' END,N'}')); COMMIT;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PlatformTenantRole_Add @UserId NVARCHAR(450),@TenantUid UNIQUEIDENTIFIER,@RoleName NVARCHAR(100),@ActorUserId NVARCHAR(450) AS
BEGIN SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRANSACTION;
 IF NOT EXISTS(SELECT 1 FROM dbo.UserTenantMembership WITH(UPDLOCK,HOLDLOCK) WHERE UserId=@UserId AND TenantUid=@TenantUid) THROW 51303,'Membership not found.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.UserTenantRole WHERE UserId=@UserId AND TenantUid=@TenantUid AND RoleName=@RoleName) INSERT dbo.UserTenantRole(UserId,TenantUid,RoleName) VALUES(@UserId,@TenantUid,@RoleName);
 INSERT dbo.PlatformAuditEvent VALUES(NEWID(),@ActorUserId,'LocalCli','TenantRoleAdded',@TenantUid,@UserId,'Succeeded',SYSUTCDATETIME(),NEWID(),CONCAT(N'{"role":"',STRING_ESCAPE(@RoleName,'json'),N'"}')); COMMIT;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PlatformTenantRole_Remove @UserId NVARCHAR(450),@TenantUid UNIQUEIDENTIFIER,@RoleName NVARCHAR(100),@ActorUserId NVARCHAR(450) AS
BEGIN SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRANSACTION; DELETE dbo.UserTenantRole WHERE UserId=@UserId AND TenantUid=@TenantUid AND RoleName=@RoleName;
 INSERT dbo.PlatformAuditEvent VALUES(NEWID(),@ActorUserId,'LocalCli','TenantRoleRemoved',@TenantUid,@UserId,'Succeeded',SYSUTCDATETIME(),NEWID(),CONCAT(N'{"role":"',STRING_ESCAPE(@RoleName,'json'),N'"}')); COMMIT; END;
GO

CREATE OR ALTER PROCEDURE dbo.PlatformMembership_ListByUser @UserId NVARCHAR(450) AS
BEGIN SET NOCOUNT ON; SELECT m.UserId,m.TenantUid,t.TenantKey,t.DisplayName,m.MembershipStatus,m.IsDefaultTenant,r.RoleName FROM dbo.UserTenantMembership m JOIN dbo.Tenant t ON t.TenantUid=m.TenantUid LEFT JOIN dbo.UserTenantRole r ON r.UserId=m.UserId AND r.TenantUid=m.TenantUid WHERE m.UserId=@UserId ORDER BY t.TenantKey,r.RoleName; END;
GO
CREATE OR ALTER PROCEDURE dbo.PlatformMembership_ListByTenant @TenantUid UNIQUEIDENTIFIER AS
BEGIN SET NOCOUNT ON; SELECT m.UserId,m.TenantUid,t.TenantKey,t.DisplayName,m.MembershipStatus,m.IsDefaultTenant,r.RoleName FROM dbo.UserTenantMembership m JOIN dbo.Tenant t ON t.TenantUid=m.TenantUid LEFT JOIN dbo.UserTenantRole r ON r.UserId=m.UserId AND r.TenantUid=m.TenantUid WHERE m.TenantUid=@TenantUid ORDER BY m.UserId,r.RoleName; END;
GO
