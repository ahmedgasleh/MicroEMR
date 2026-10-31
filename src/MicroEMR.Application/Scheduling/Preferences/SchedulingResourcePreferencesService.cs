using System.Text.Json;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.Scheduling.Repositories;

namespace MicroEMR.Application.Scheduling.Preferences;

public sealed class SchedulingResourcePreferencesService(
    ISchedulingResourcePreferencesRepository repository, ISchedulingReadRepository resources,
    IAuthenticatedClinicalUserAccessor currentUser, ILogger<SchedulingResourcePreferencesService> logger)
    : ISchedulingResourcePreferencesService
{
    public async Task<SchedulingResourcePreferencesResponse> GetAsync(CancellationToken token = default)
    {
        // Identity failures must not be converted into another user's defaults or write authority.
        var userId = await currentUser.GetRequiredUserIdAsync(token);
        try
        {
            var data = await repository.GetAsync(userId, token);
            if (data is null) return new([], false, null);
            var saved = Normalize(JsonSerializer.Deserialize<List<Guid>>(data.ResourceUidsJson));
            var active = await ActiveResources(token);
            var effective = saved.Where(active.Contains).ToArray();
            return new(effective, true, data.RowVersion, saved.Count - effective.Length);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error)
        {
            logger.LogWarning(error, "Scheduling resource defaults unavailable; retaining initial resource selection.");
            return new([], false, null, IsAvailable: false);
        }
    }

    public async Task<SchedulingResourcePreferencesResponse> SaveAsync(
        SaveSchedulingResourcePreferencesRequest request, CancellationToken token = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync(token);
        ArgumentNullException.ThrowIfNull(request);
        var selected = Normalize(request.ResourceUids);
        if (request.RowVersion is not null)
        {
            if (!Convert.TryFromBase64String(request.RowVersion, new byte[8], out var length) || length != 8)
                throw new ArgumentException("The resource defaults row version is invalid.");
        }
        var active = await ActiveResources(token);
        if (selected.Any(uid => !active.Contains(uid)))
            throw new ArgumentException("Choose only available scheduling resources in the current tenant.");
        var saved = await repository.SaveAsync(userId, JsonSerializer.Serialize(selected), request.RowVersion, token);
        return new(selected, true, saved.RowVersion);
    }

    private async Task<HashSet<Guid>> ActiveResources(CancellationToken token) =>
        (await resources.GetActiveResourcesAsync(token))
            .Where(resource => resource.IsActive && resource.ResourceType is "Provider" or "Room" or "Equipment")
            .Select(resource => resource.ResourceUid).ToHashSet();

    private static List<Guid> Normalize(List<Guid>? selected)
    {
        if (selected is null || selected.Count is < 1 or > 200 || selected.Contains(Guid.Empty))
            throw new ArgumentException("Select between 1 and 200 scheduling resources.");
        return selected.Distinct().ToList(); // Preserve the first occurrence and the user's chosen order.
    }
}
