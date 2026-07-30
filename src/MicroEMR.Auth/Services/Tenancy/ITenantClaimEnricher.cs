using System.Security.Claims;
using MicroEMR.Auth.Data;

namespace MicroEMR.Auth.Services.Tenancy;

public interface ITenantClaimEnricher
{
    Task<TenantClaimEnrichmentResult> EnrichAsync(
        ApplicationUser user,
        ClaimsIdentity identity,
        string traceIdentifier,
        CancellationToken cancellationToken = default);
}
