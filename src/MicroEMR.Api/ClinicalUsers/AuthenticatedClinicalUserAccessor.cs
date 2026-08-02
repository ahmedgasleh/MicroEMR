using System.Security.Claims;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.Tenancy;

namespace MicroEMR.Api.ClinicalUsers;

public sealed class AuthenticatedClinicalUserAccessor(
    IHttpContextAccessor httpContextAccessor,
    ITenantContext tenantContext,
    IClinicalUserRepository clinicalUsers) : IAuthenticatedClinicalUserAccessor
{
    private long? _resolvedUserId;

    public async Task<long> GetRequiredUserIdAsync(
        CancellationToken cancellationToken = default)
    {
        if (_resolvedUserId.HasValue) return _resolvedUserId.Value;

        var context = httpContextAccessor.HttpContext
            ?? throw new ClinicalUserResolutionException("The authenticated request context is unavailable.");
        if (context.User.Identity?.IsAuthenticated != true)
            throw new ClinicalUserResolutionException("Authentication is required for this clinical operation.");

        var subject = context.User.FindFirstValue("sub")
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(subject))
            throw new ClinicalUserResolutionException("The authenticated account has no subject identifier.");
        if (tenantContext.TenantUid == Guid.Empty)
            throw new ClinicalUserResolutionException("A tenant context is required for this clinical operation.");

        var clinicalUser = await clinicalUsers.GetByAuthSubjectIdAsync(subject, cancellationToken);
        if (clinicalUser is null || !clinicalUser.IsActive)
            throw new ClinicalUserResolutionException(
                "The authenticated account is not provisioned as an active clinical user in this tenant.");

        _resolvedUserId = clinicalUser.UserId;
        return clinicalUser.UserId;
    }
}
