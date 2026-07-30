using System.Security.Claims;
using MicroEMR.Application.Security;
using MicroEMR.Application.Tenancy;
using MicroEMR.Auth.Data;

namespace MicroEMR.Auth.Services.Tenancy;

public sealed class TenantClaimEnricher : ITenantClaimEnricher
{
    private static readonly string[] TenantClaimTypes =
    [
        MicroEmrClaimTypes.TenantId,
        MicroEmrClaimTypes.TenantKey,
        MicroEmrClaimTypes.TenantName,
        MicroEmrClaimTypes.TenantRole
    ];

    private readonly IUserTenantResolver _resolver;
    private readonly ILogger<TenantClaimEnricher> _logger;

    public TenantClaimEnricher(
        IUserTenantResolver resolver,
        ILogger<TenantClaimEnricher> logger)
    {
        _resolver = resolver;
        _logger = logger;
    }

    public async Task<TenantClaimEnrichmentResult> EnrichAsync(
        ApplicationUser user,
        ClaimsIdentity identity,
        string traceIdentifier,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(identity);

        TenantMembershipResolutionResult resolution;

        try
        {
            resolution = await _resolver.ResolveAsync(user, cancellationToken);
        }
        catch (InvalidTenantMembershipDataException exception)
        {
            _logger.LogError(
                exception,
                "Invalid tenant membership data for user {UserId}. TraceIdentifier: {TraceIdentifier}",
                user.Id,
                traceIdentifier);

            return new TenantClaimEnrichmentResult(
                TenantClaimEnrichmentStatus.InvalidMembershipData,
                "Your account could not be assigned to a clinic.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Tenant membership resolution failed for user {UserId}. TraceIdentifier: {TraceIdentifier}",
                user.Id,
                traceIdentifier);

            return new TenantClaimEnrichmentResult(
                TenantClaimEnrichmentStatus.InvalidMembershipData,
                "Your account could not be assigned to a clinic.");
        }

        switch (resolution.Status)
        {
            case TenantMembershipResolutionStatus.Resolved
                when resolution.Membership is not null:
                ReplaceTenantClaims(identity, resolution.Membership);

                _logger.LogInformation(
                    "Tenant membership resolved for user {UserId} to tenant {TenantUid}. ResolutionStatus: {ResolutionStatus}. TraceIdentifier: {TraceIdentifier}",
                    user.Id,
                    resolution.Membership.TenantUid,
                    resolution.Status,
                    traceIdentifier);

                return new TenantClaimEnrichmentResult(
                    TenantClaimEnrichmentStatus.Resolved);

            case TenantMembershipResolutionStatus.None:
                _logger.LogWarning(
                    "No active tenant membership for user {UserId}. ResolutionStatus: {ResolutionStatus}. TraceIdentifier: {TraceIdentifier}",
                    user.Id,
                    resolution.Status,
                    traceIdentifier);

                return new TenantClaimEnrichmentResult(
                    TenantClaimEnrichmentStatus.NoActiveMembership,
                    "Your account is not assigned to an active clinic.");

            case TenantMembershipResolutionStatus.SelectionRequired:
                _logger.LogWarning(
                    "Tenant selection is required for user {UserId}. ResolutionStatus: {ResolutionStatus}. TraceIdentifier: {TraceIdentifier}",
                    user.Id,
                    resolution.Status,
                    traceIdentifier);

                return new TenantClaimEnrichmentResult(
                    TenantClaimEnrichmentStatus.SelectionRequired,
                    "Your account is assigned to multiple clinics and requires clinic selection.");

            default:
                _logger.LogError(
                    "Tenant resolver returned an invalid result for user {UserId}. ResolutionStatus: {ResolutionStatus}. TraceIdentifier: {TraceIdentifier}",
                    user.Id,
                    resolution.Status,
                    traceIdentifier);

                return new TenantClaimEnrichmentResult(
                    TenantClaimEnrichmentStatus.InvalidMembershipData,
                    "Your account could not be assigned to a clinic.");
        }
    }

    public TenantClaimEnrichmentResult EnrichFromValidatedMembership(
        ClaimsIdentity identity,
        UserTenantMembershipInfo membership)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(membership);
        ReplaceTenantClaims(identity, membership);
        return new TenantClaimEnrichmentResult(TenantClaimEnrichmentStatus.Resolved);
    }

    private static void ReplaceTenantClaims(
        ClaimsIdentity identity,
        UserTenantMembershipInfo membership)
    {
        foreach (var claim in identity.Claims
                     .Where(claim => TenantClaimTypes.Contains(claim.Type))
                     .ToArray())
        {
            identity.RemoveClaim(claim);
        }

        identity.AddClaim(new Claim(
            MicroEmrClaimTypes.TenantId,
            membership.TenantUid.ToString("D")));
        identity.AddClaim(new Claim(
            MicroEmrClaimTypes.TenantKey,
            membership.TenantKey));
        identity.AddClaim(new Claim(
            MicroEmrClaimTypes.TenantName,
            membership.TenantDisplayName));

        foreach (var role in membership.Roles
                     .Where(role => !string.IsNullOrWhiteSpace(role))
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(role => role, StringComparer.Ordinal))
        {
            identity.AddClaim(new Claim(
                MicroEmrClaimTypes.TenantRole,
                role));
        }
    }
}
