using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using MicroEMR.Auth.Services.Tenancy;
using Xunit;

namespace MicroEMR.Auth.Tests;

public sealed class PendingTenantSelectionStoreTests
{
    [Fact]
    public async Task Take_IsSingleUse()
    {
        var store = CreateStore();
        var pending = Selection("selection-1", "user-1");
        await store.StoreAsync(pending);

        Assert.NotNull(await store.TakeAsync(pending.SelectionId));
        Assert.Null(await store.TakeAsync(pending.SelectionId));
    }

    [Fact]
    public async Task ConcurrentTake_AllowsExactlyOneConsumer()
    {
        var store = CreateStore();
        var pending = Selection("selection-2", "user-1");
        await store.StoreAsync(pending);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => store.TakeAsync(pending.SelectionId)));
        Assert.Single(results, result => result is not null);
    }

    [Fact]
    public async Task IndependentSelections_DoNotOverwriteEachOther()
    {
        var store = CreateStore();
        await store.StoreAsync(Selection("tab-a", "user-1"));
        await store.StoreAsync(Selection("tab-b", "user-1"));
        Assert.NotNull(await store.TakeAsync("tab-a"));
        Assert.NotNull(await store.TakeAsync("tab-b"));
    }

    private static DistributedPendingTenantSelectionStore CreateStore()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        return new DistributedPendingTenantSelectionStore(
            services.BuildServiceProvider().GetRequiredService<IDistributedCache>());
    }

    private static PendingTenantSelection Selection(string id, string userId) =>
        new(id, userId, "/connect/authorize?client_id=web", DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(5), [Guid.NewGuid()]);
}
