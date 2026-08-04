namespace MicroEMR.Application.TenantUserAdministration;

public sealed record TenantMembershipLifecycleResult(
    string MembershipStatus, DateTimeOffset UpdatedAt, string RowVersion);

public interface ITenantMembershipLifecycleRepository
{
    Task<TenantMembershipLifecycleResult> DeactivateAsync(string authUserId, Guid tenantUid,
        string rowVersion, string actorAuthUserId, CancellationToken cancellationToken = default);
    Task<TenantMembershipLifecycleResult> ActivateAsync(string authUserId, Guid tenantUid,
        string rowVersion, string actorAuthUserId, CancellationToken cancellationToken = default);
}

public interface IAuthenticatedSubjectAccessor
{
    string GetRequiredSubject();
}

public class TenantMembershipLifecycleException(string message, Exception? inner = null) : Exception(message, inner);
public sealed class TenantMembershipNotFoundException(string message, Exception? inner = null) : TenantMembershipLifecycleException(message, inner);
public sealed class TenantMembershipTransitionException(string message, Exception? inner = null) : TenantMembershipLifecycleException(message, inner);
public sealed class TenantMembershipConcurrencyException(string message, Exception? inner = null) : TenantMembershipLifecycleException(message, inner);
public sealed class TenantMembershipSelfDeactivationException(string message, Exception? inner = null) : TenantMembershipLifecycleException(message, inner);
public sealed class TenantMembershipLastAdministratorException(string message, Exception? inner = null) : TenantMembershipLifecycleException(message, inner);
