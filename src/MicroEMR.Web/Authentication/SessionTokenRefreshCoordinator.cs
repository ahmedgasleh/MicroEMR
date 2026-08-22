using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Runtime.ExceptionServices;
using System.Text;

namespace MicroEMR.Web.Authentication;

public interface ISessionTokenRefreshCoordinator
{
    Task<RefreshedTokenSet> RunOnceAsync(
        string refreshToken,
        Func<CancellationToken, Task<RefreshedTokenSet>> refresh,
        CancellationToken cancellationToken);
}

public sealed class SessionTokenRefreshCoordinator(TimeProvider timeProvider)
    : ISessionTokenRefreshCoordinator
{
    private static readonly TimeSpan CompletedEntryLifetime = TimeSpan.FromMinutes(2);
    private readonly ConcurrentDictionary<string, RefreshEntry> _entries = new(StringComparer.Ordinal);

    public async Task<RefreshedTokenSet> RunOnceAsync(
        string refreshToken,
        Func<CancellationToken, Task<RefreshedTokenSet>> refresh,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        ArgumentNullException.ThrowIfNull(refresh);

        RemoveExpiredEntries();

        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
        var entry = _entries.GetOrAdd(key, _ => new RefreshEntry());

        await entry.Gate.WaitAsync(cancellationToken);
        try
        {
            if (entry.Result is not null)
            {
                return entry.Result;
            }

            entry.Error?.Throw();

            try
            {
                entry.Result = await refresh(cancellationToken);
                return entry.Result;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                entry.Error = ExceptionDispatchInfo.Capture(exception);
                throw;
            }
            finally
            {
                entry.CompletedAt = timeProvider.GetUtcNow();
            }
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    private void RemoveExpiredEntries()
    {
        var cutoff = timeProvider.GetUtcNow() - CompletedEntryLifetime;
        foreach (var pair in _entries)
        {
            if (pair.Value.CompletedAt is { } completedAt && completedAt < cutoff)
            {
                _entries.TryRemove(pair);
            }
        }
    }

    private sealed class RefreshEntry
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public RefreshedTokenSet? Result { get; set; }
        public ExceptionDispatchInfo? Error { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
    }
}
