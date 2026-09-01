/*
    Forward-only repair for platform databases where migration 013 was recorded or
    partially deployed without dbo.AccessManagementAdministrator.

    Apply once after platform migration 023. Do not replay migration 013.
*/
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID(N'dbo.UserTenantMembership', N'U') IS NULL
   OR OBJECT_ID(N'dbo.UserPermissionOverride', N'U') IS NULL
   OR OBJECT_ID(N'dbo.UserTenantAccessProfile', N'U') IS NULL
   OR OBJECT_ID(N'dbo.AccessProfile', N'U') IS NULL
   OR OBJECT_ID(N'dbo.AccessProfilePermission', N'U') IS NULL
    THROW 52400, 'Access-management repair prerequisites are missing.', 1;

IF OBJECT_ID(N'dbo.AccessProfile_ReplacePermissions', N'P') IS NULL
   OR OBJECT_ID(N'dbo.UserPermissionOverride_Set', N'P') IS NULL
    THROW 52401, 'Platform permission procedures are missing; apply platform migrations through 023 first.', 1;

IF OBJECT_DEFINITION(OBJECT_ID(N'dbo.AccessProfile_ReplacePermissions', N'P')) NOT LIKE N'%Providers.Manage%'
   OR OBJECT_DEFINITION(OBJECT_ID(N'dbo.UserPermissionOverride_Set', N'P')) NOT LIKE N'%Providers.Manage%'
    THROW 52402, 'Platform permission procedures are not at migration 023; apply migration 023 first.', 1;
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

IF OBJECT_ID(N'dbo.AccessManagementAdministrator', N'IF') IS NULL
    THROW 52403, 'Access-management administrator function repair failed.', 1;
GO
