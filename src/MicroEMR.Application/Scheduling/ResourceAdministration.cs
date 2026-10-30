using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace MicroEMR.Application.Scheduling;

public sealed record SchedulingResourceItem(Guid ResourceUid, string ResourceType, string DisplayName,
    string? ColorCode, int SortOrder, bool IsActive, Guid? ProviderUid, string? ProviderDisplayName, string RowVersion);
public sealed record SchedulingProviderChoice(Guid ProviderUid, string DisplayName);
public sealed class SaveSchedulingResourceRequest
{
    [Required, StringLength(50)] public string ResourceType { get; set; } = "Provider";
    [Required, StringLength(200)] public string DisplayName { get; set; } = "";
    [StringLength(20)] public string? ColorCode { get; set; }
    public int SortOrder { get; set; }
    public Guid? ProviderUid { get; set; }
    public string? RowVersion { get; set; }
}
public sealed record SchedulingResourceVersionRequest([Required] string RowVersion);
public sealed class SchedulingResourceConflictException(string message) : Exception(message);
public interface ISchedulingResourceAdministrationRepository
{
    Task<IReadOnlyList<SchedulingResourceItem>> ListAsync(string status, CancellationToken token = default);
    Task<SchedulingResourceItem?> GetAsync(Guid uid, CancellationToken token = default);
    Task<IReadOnlyList<SchedulingProviderChoice>> ProvidersAsync(CancellationToken token = default);
    Task<SchedulingResourceItem?> SaveAsync(Guid? uid, SaveSchedulingResourceRequest request, long actor, CancellationToken token = default);
    Task<SchedulingResourceItem?> SetActiveAsync(Guid uid, bool active, string version, long actor, CancellationToken token = default);
}
public sealed class SchedulingResourceAdministrationService(ISchedulingResourceAdministrationRepository repository)
{
    public Task<IReadOnlyList<SchedulingResourceItem>> ListAsync(string status, CancellationToken token = default)
    {
        if (status is not ("Active" or "Inactive" or "All")) throw new ArgumentException("Select Active, Inactive, or All.");
        return repository.ListAsync(status, token);
    }
    public Task<SchedulingResourceItem?> GetAsync(Guid uid, CancellationToken token = default) => repository.GetAsync(Required(uid), token);
    public Task<IReadOnlyList<SchedulingProviderChoice>> ProvidersAsync(CancellationToken token = default) => repository.ProvidersAsync(token);
    public Task<SchedulingResourceItem?> SaveAsync(Guid? uid, SaveSchedulingResourceRequest request, long actor, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (uid.HasValue) { Required(uid.Value); Version(request.RowVersion); }
        if (actor <= 0) throw new ArgumentException("An active clinical actor is required.");
        if (request.ResourceType is not ("Provider" or "Room" or "Equipment")) throw new ArgumentException("Select Provider, Room, or Equipment.");
        request.DisplayName = request.DisplayName?.Trim() ?? "";
        if (request.DisplayName.Length is < 1 or > 200) throw new ArgumentException("Display name is required and must be at most 200 characters.");
        request.ColorCode = string.IsNullOrWhiteSpace(request.ColorCode) ? null : request.ColorCode.Trim();
        if (request.ColorCode is not null && !Regex.IsMatch(request.ColorCode, "^#[0-9a-fA-F]{6}$")) throw new ArgumentException("Use a six-digit colour such as #0d6efd, or leave it blank.");
        if (request.ProviderUid == Guid.Empty) throw new ArgumentException("Select an existing clinical Provider.");
        if (request.ResourceType != "Provider" && request.ProviderUid.HasValue) throw new ArgumentException("Rooms and equipment cannot link a clinical Provider.");
        if (!uid.HasValue && request.ResourceType == "Provider" && !request.ProviderUid.HasValue) throw new ArgumentException("Select an existing clinical Provider.");
        return repository.SaveAsync(uid, request, actor, token);
    }
    public Task<SchedulingResourceItem?> SetActiveAsync(Guid uid, bool active, string version, long actor, CancellationToken token = default)
    {
        Required(uid); Version(version);
        if (actor <= 0) throw new ArgumentException("An active clinical actor is required.");
        return repository.SetActiveAsync(uid, active, version, actor, token);
    }
    private static Guid Required(Guid uid) => uid == Guid.Empty ? throw new ArgumentException("Resource identifier is required.") : uid;
    private static void Version(string? version)
    {
        try { if (string.IsNullOrWhiteSpace(version) || Convert.FromBase64String(version).Length != 8) throw new FormatException(); }
        catch (FormatException) { throw new ArgumentException("Reload the resource before saving; its version is missing or invalid."); }
    }
}
