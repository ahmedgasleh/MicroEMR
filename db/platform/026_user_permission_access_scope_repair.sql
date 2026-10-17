SET XACT_ABORT ON;
GO

-- Repair the access display without changing stored overrides or permission enforcement.
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

    -- Scope both sources before combining keys. A FULL JOIN against unscoped
    -- memberships/overrides can return another user's override for the same key.
    WITH ProfilePermissions AS
    (
        SELECT pp.PermissionKey
        FROM dbo.UserTenantMembership m
        JOIN dbo.UserTenantAccessProfile a ON a.UserId=m.UserId AND a.TenantUid=m.TenantUid
        JOIN dbo.AccessProfile p ON p.AccessProfileUid=a.AccessProfileUid AND p.TenantUid=m.TenantUid AND p.IsActive=1
        JOIN dbo.AccessProfilePermission pp ON pp.AccessProfileUid=p.AccessProfileUid
        WHERE m.TenantUid=@TenantUid AND m.UserId=@UserId
    ), UserOverrides AS
    (
        SELECT o.PermissionKey,o.OverrideState,o.RowVersion
        FROM dbo.UserPermissionOverride o
        JOIN dbo.UserTenantMembership m ON m.TenantUid=o.TenantUid AND m.UserId=o.UserId
        WHERE o.TenantUid=@TenantUid AND o.UserId=@UserId
    ), PermissionKeys AS
    (
        SELECT PermissionKey FROM ProfilePermissions
        UNION
        SELECT PermissionKey FROM UserOverrides
    )
    SELECT k.PermissionKey,
           CAST(CASE WHEN pp.PermissionKey IS NULL THEN 0 ELSE 1 END AS BIT) ProfileAllowed,
           o.OverrideState,o.RowVersion
    FROM PermissionKeys k
    LEFT JOIN ProfilePermissions pp ON pp.PermissionKey=k.PermissionKey
    LEFT JOIN UserOverrides o ON o.PermissionKey=k.PermissionKey
    ORDER BY k.PermissionKey;
END;
GO
