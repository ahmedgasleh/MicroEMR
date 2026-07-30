using System.Security.Claims;

namespace MicroEMR.Application.Security;

public static class ClaimsPrincipalTenantExtensions
{
    public static Guid? GetTenantUid(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return Guid.TryParse(
            principal.FindFirst(MicroEmrClaimTypes.TenantId)?.Value,
            out var tenantUid)
            ? tenantUid
            : null;
    }

    public static string? GetTenantKey(this ClaimsPrincipal principal) =>
        principal.FindFirst(MicroEmrClaimTypes.TenantKey)?.Value;

    public static string? GetTenantDisplayName(this ClaimsPrincipal principal) =>
        principal.FindFirst(MicroEmrClaimTypes.TenantName)?.Value;

    public static IReadOnlyList<string> GetTenantRoles(
        this ClaimsPrincipal principal) =>
        principal.FindAll(MicroEmrClaimTypes.TenantRole)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToArray();
}
