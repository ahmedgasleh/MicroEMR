using System.Text.Json;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.ClinicalUsers;

namespace MicroEMR.Application.PatientCpp;

public interface ICppDisplayPreferencesService
{
    Task<CppDisplayPreferencesResponse> GetAsync(CancellationToken cancellationToken = default);
    Task<CppDisplayPreferencesResponse> SaveAsync(SaveCppDisplayPreferencesRequest request, CancellationToken cancellationToken = default);
}

public sealed class CppDisplayPreferencesService(
    ICppDisplayPreferencesRepository repository,
    IAuthenticatedClinicalUserAccessor currentUser,
    ILogger<CppDisplayPreferencesService> logger) : ICppDisplayPreferencesService
{
    private static readonly HashSet<string> CategoryKeys = CppDisplayCatalog.Categories.Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
    private static readonly HashSet<string> FieldKeys = CppDisplayCatalog.Categories.SelectMany(x => x.Fields).Select(x => x.Key).ToHashSet(StringComparer.Ordinal);

    public async Task<CppDisplayPreferencesResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        // Resolve outside fallback: identity failures must never become another user's defaults/write authority.
        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);
        CppDisplayPreferencesData? data;
        try
        {
            data = await repository.GetAsync(userId, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "CPP display preferences unavailable; retaining the default display.");
            return new([], [], null, IsAvailable: false);
        }
        if (data is null) return new([], [], null);

        try
        {
            var settings = JsonSerializer.Deserialize<SaveCppDisplayPreferencesRequest>(data.SettingsJson)
                ?? throw new ArgumentException("Missing settings.");
            var normalized = Normalize(settings);
            return new(normalized.HiddenCategories, normalized.HiddenFields, data.RowVersion);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            logger.LogWarning("Invalid CPP display preferences; retaining the default display.");
            // Keep concurrency token so Restore Defaults can repair this user's invalid configuration.
            return new([], [], data.RowVersion);
        }
    }

    public async Task<CppDisplayPreferencesResponse> SaveAsync(SaveCppDisplayPreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);
        var normalized = Normalize(request);
        ValidateRowVersion(request.RowVersion);
        var data = await repository.SaveAsync(userId, JsonSerializer.Serialize(normalized), request.RowVersion, cancellationToken);
        return new(normalized.HiddenCategories, normalized.HiddenFields, data.RowVersion);
    }

    private static SaveCppDisplayPreferencesRequest Normalize(SaveCppDisplayPreferencesRequest request)
    {
        if (request.HiddenCategories is null || request.HiddenFields is null ||
            request.HiddenCategories.Count > CategoryKeys.Count || request.HiddenFields.Count > FieldKeys.Count ||
            request.HiddenCategories.Any(x => x is null || !CategoryKeys.Contains(x)) ||
            request.HiddenFields.Any(x => x is null || !FieldKeys.Contains(x)))
            throw new ArgumentException("Choose only supported CPP categories and information fields.");
        return new()
        {
            HiddenCategories = request.HiddenCategories.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList(),
            HiddenFields = request.HiddenFields.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList()
        };
    }

    private static void ValidateRowVersion(string? value)
    {
        if (value is null) return;
        try
        {
            if (Convert.FromBase64String(value).Length == 8) return;
        }
        catch (FormatException) { }
        throw new ArgumentException("The preference row version is invalid.");
    }
}
