using MicroEMR.Application.Tenancy;

namespace MicroEMR.Auth.Services.Tenancy;

public enum TenantMembershipResolutionStatus
{
    None,
    Resolved,
    SelectionRequired
}

public sealed record TenantMembershipResolutionResult(
    TenantMembershipResolutionStatus Status,
    UserTenantMembershipInfo? Membership,
    IReadOnlyList<UserTenantMembershipInfo> AvailableMemberships);
