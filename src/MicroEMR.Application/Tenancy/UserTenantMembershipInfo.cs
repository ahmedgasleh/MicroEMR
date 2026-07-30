namespace MicroEMR.Application.Tenancy;

public sealed record UserTenantMembershipInfo(
    string UserId,
    Guid TenantUid,
    string TenantKey,
    string TenantDisplayName,
    string MembershipStatus,
    bool IsDefaultTenant,
    IReadOnlyCollection<string> Roles);
