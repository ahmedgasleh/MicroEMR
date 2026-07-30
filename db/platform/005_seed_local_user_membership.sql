USE MicroEMR_Platform;
GO

-- In SSMS, replace every occurrence of IDENTITY-USER-ID-HERE with Id from
-- MicroEMR_Auth.dbo.AspNetUsers, then execute this entire script.
-- The ID is used directly against UserId columns so this also works when the
-- SSMS connection has Always Encrypted parameterization enabled.

INSERT INTO dbo.UserTenantMembership
(
    UserId,
    TenantUid,
    MembershipStatus,
    IsDefaultTenant,
    CreatedAt
)
SELECT
    N'IDENTITY-USER-ID-HERE',
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
        WHERE membership.UserId = N'IDENTITY-USER-ID-HERE'
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
    N'IDENTITY-USER-ID-HERE',
    tenant.TenantUid,
    N'ClinicAdministrator',
    SYSUTCDATETIME()
FROM dbo.Tenant AS tenant
INNER JOIN dbo.UserTenantMembership AS membership
    ON membership.TenantUid = tenant.TenantUid
    AND membership.UserId = N'IDENTITY-USER-ID-HERE'
WHERE tenant.TenantKey = N'local-dev'
    AND NOT EXISTS
    (
        SELECT 1
        FROM dbo.UserTenantRole AS role
        WHERE role.UserId = membership.UserId
            AND role.TenantUid = membership.TenantUid
            AND role.RoleName = N'ClinicAdministrator'
    );

SELECT
    membership.UserId,
    tenant.TenantUid,
    tenant.TenantKey,
    tenant.DisplayName AS TenantDisplayName,
    membership.MembershipStatus,
    membership.IsDefaultTenant,
    role.RoleName
FROM dbo.UserTenantMembership AS membership
INNER JOIN dbo.Tenant AS tenant
    ON tenant.TenantUid = membership.TenantUid
LEFT JOIN dbo.UserTenantRole AS role
    ON role.UserId = membership.UserId
    AND role.TenantUid = membership.TenantUid
WHERE membership.UserId = N'IDENTITY-USER-ID-HERE'
    AND tenant.TenantKey = N'local-dev';
GO
