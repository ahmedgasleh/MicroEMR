USE MicroEMR_Platform;
GO

-- Supply the ASP.NET Identity user ID with sqlcmd's -v option.
-- Example: -v IdentityUserId="00000000-0000-0000-0000-000000000000"
IF N'$(IdentityUserId)' LIKE N'$(%'
BEGIN
    THROW 51001, 'Supply IdentityUserId using sqlcmd -v.', 1;
END;

IF NULLIF(LTRIM(RTRIM(N'$(IdentityUserId)')), N'') IS NULL
BEGIN
    THROW 51002, 'IdentityUserId must not be blank.', 1;
END;

INSERT INTO dbo.UserTenantMembership
(
    UserId,
    TenantUid,
    MembershipStatus,
    IsDefaultTenant,
    CreatedAt
)
SELECT
    N'$(IdentityUserId)',
    tenant.TenantUid,
    'Active',
    CONVERT(BIT, 1),
    SYSUTCDATETIME()
FROM dbo.Tenant AS tenant
WHERE tenant.TenantKey = N'local-dev'
    AND NOT EXISTS
    (
        SELECT 1
        FROM dbo.UserTenantMembership AS membership
        WHERE membership.UserId = N'$(IdentityUserId)'
            AND membership.TenantUid = tenant.TenantUid
    );

INSERT INTO dbo.UserTenantRole
(
    UserId,
    TenantUid,
    RoleName,
    CreatedAt
)
SELECT
    N'$(IdentityUserId)',
    tenant.TenantUid,
    N'ClinicAdministrator',
    SYSUTCDATETIME()
FROM dbo.Tenant AS tenant
INNER JOIN dbo.UserTenantMembership AS membership
    ON membership.TenantUid = tenant.TenantUid
    AND membership.UserId = N'$(IdentityUserId)'
WHERE tenant.TenantKey = N'local-dev'
    AND NOT EXISTS
    (
        SELECT 1
        FROM dbo.UserTenantRole AS role
        WHERE role.UserId = membership.UserId
            AND role.TenantUid = membership.TenantUid
            AND role.RoleName = N'ClinicAdministrator'
    );
GO
