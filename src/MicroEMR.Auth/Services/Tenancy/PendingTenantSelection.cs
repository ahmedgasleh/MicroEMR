namespace MicroEMR.Auth.Services.Tenancy;

public sealed record PendingTenantSelection(
    string SelectionId,
    string UserId,
    string ReturnUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    IReadOnlyCollection<Guid> AllowedTenantUids);

public sealed record TenantSelectionContinuation(
    string ContinuationId,
    string UserId,
    string ReturnUrl,
    Guid SelectedTenantUid,
    DateTimeOffset ExpiresAt);
