namespace MicroEMR.Application.Tenancy;

public interface IUserTenantMembershipRepository
{
    Task<IReadOnlyList<UserTenantMembershipInfo>> GetActiveMembershipsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<UserTenantMembershipInfo?> GetMembershipAsync(
        string userId,
        Guid tenantUid,
        CancellationToken cancellationToken = default);
}
