namespace MicroEMR.Auth.Services.Tenancy;

public enum TenantClaimEnrichmentStatus
{
    Resolved,
    NoActiveMembership,
    SelectionRequired,
    InvalidMembershipData
}

public sealed record TenantClaimEnrichmentResult(
    TenantClaimEnrichmentStatus Status,
    string? ErrorDescription = null);
