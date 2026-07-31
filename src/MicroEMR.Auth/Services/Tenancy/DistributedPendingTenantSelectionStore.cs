using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace MicroEMR.Auth.Services.Tenancy;

public sealed class DistributedPendingTenantSelectionStore : IPendingTenantSelectionStore
{
    private const string SelectionPrefix = "tenant-selection:";
    private const string ContinuationPrefix = "tenant-continuation:";
    private readonly IDistributedCache _cache;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public DistributedPendingTenantSelectionStore(IDistributedCache cache) => _cache = cache;

    public Task StoreAsync(PendingTenantSelection selection, CancellationToken cancellationToken = default) =>
        StoreValueAsync(SelectionPrefix + selection.SelectionId, selection, selection.ExpiresAt, cancellationToken);

    public Task<PendingTenantSelection?> GetAsync(string selectionId, CancellationToken cancellationToken = default) =>
        GetValueAsync<PendingTenantSelection>(SelectionPrefix + selectionId, cancellationToken);

    public Task<PendingTenantSelection?> TakeAsync(string selectionId, CancellationToken cancellationToken = default) =>
        TakeValueAsync<PendingTenantSelection>(SelectionPrefix + selectionId, cancellationToken);

    public Task StoreContinuationAsync(TenantSelectionContinuation continuation, CancellationToken cancellationToken = default) =>
        StoreValueAsync(ContinuationPrefix + continuation.ContinuationId, continuation, continuation.ExpiresAt, cancellationToken);

    public Task<TenantSelectionContinuation?> TakeContinuationAsync(string continuationId, CancellationToken cancellationToken = default) =>
        TakeValueAsync<TenantSelectionContinuation>(ContinuationPrefix + continuationId, cancellationToken);

    public Task RemoveAsync(string selectionId, CancellationToken cancellationToken = default) =>
        _cache.RemoveAsync(SelectionPrefix + selectionId, cancellationToken);

    private async Task StoreValueAsync<T>(string key, T value, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        await _cache.SetStringAsync(key, JsonSerializer.Serialize(value), new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = expiresAt
        }, cancellationToken);
    }

    private async Task<T?> GetValueAsync<T>(string key, CancellationToken cancellationToken)
    {
        var json = await _cache.GetStringAsync(key, cancellationToken);
        return json is null ? default : JsonSerializer.Deserialize<T>(json);
    }

    private async Task<T?> TakeValueAsync<T>(string key, CancellationToken cancellationToken)
    {
        var gate = _locks.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var value = await GetValueAsync<T>(key, cancellationToken);
            if (value is not null)
            {
                await _cache.RemoveAsync(key, cancellationToken);
            }
            return value;
        }
        finally
        {
            gate.Release();
        }
    }
}
