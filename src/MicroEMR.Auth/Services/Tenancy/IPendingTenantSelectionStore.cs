namespace MicroEMR.Auth.Services.Tenancy;

public interface IPendingTenantSelectionStore
{
    Task StoreAsync(PendingTenantSelection selection, CancellationToken cancellationToken = default);
    Task<PendingTenantSelection?> GetAsync(string selectionId, CancellationToken cancellationToken = default);
    Task<PendingTenantSelection?> TakeAsync(string selectionId, CancellationToken cancellationToken = default);
    Task StoreContinuationAsync(TenantSelectionContinuation continuation, CancellationToken cancellationToken = default);
    Task<TenantSelectionContinuation?> TakeContinuationAsync(string continuationId, CancellationToken cancellationToken = default);
    Task RemoveAsync(string selectionId, CancellationToken cancellationToken = default);
}
