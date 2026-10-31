using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MicroEMR.Application.Scheduling.Preferences;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SaveSchedulingResourcePreferencesRequest
{
    [Required, MinLength(1), MaxLength(200)] public List<Guid> ResourceUids { get; set; } = [];
    [StringLength(12)] public string? RowVersion { get; set; }
}

public sealed record SchedulingResourcePreferencesResponse(
    IReadOnlyList<Guid> ResourceUids, bool HasSavedDefaults, string? RowVersion,
    int UnavailableResourceCount = 0, bool IsAvailable = true);

public sealed record SchedulingResourcePreferenceData(string ResourceUidsJson, string RowVersion);

public interface ISchedulingResourcePreferencesRepository
{
    Task<SchedulingResourcePreferenceData?> GetAsync(long userId, CancellationToken token = default);
    Task<SchedulingResourcePreferenceData> SaveAsync(long userId, string resourceUidsJson,
        string? expectedRowVersion, CancellationToken token = default);
}

public interface ISchedulingResourcePreferencesService
{
    Task<SchedulingResourcePreferencesResponse> GetAsync(CancellationToken token = default);
    Task<SchedulingResourcePreferencesResponse> SaveAsync(SaveSchedulingResourcePreferencesRequest request,
        CancellationToken token = default);
}

public sealed class SchedulingResourcePreferencesConcurrencyException()
    : Exception("Resource defaults changed in another session. Restore My Defaults before saving again.");
