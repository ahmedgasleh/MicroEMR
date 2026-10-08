using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Application.PatientCpp;

public sealed class CppPrintRequest
{
    public Guid ClinicianUid { get; set; }
    [Required] public List<string> Categories { get; set; } = [];
}
public sealed record CppPrintClinician(Guid ClinicianUid, string DisplayName, string ProviderType);
public sealed record CppPrintOptions(IReadOnlyList<CppDisplayCategory> Categories, IReadOnlyList<CppPrintClinician> Clinicians);
public interface ICppPrintService
{
    Task<CppPrintOptions> GetOptionsAsync(CancellationToken token = default);
    Task<byte[]?> PrintAsync(Guid patientUid, CppPrintRequest request, string correlation, CancellationToken token = default);
}
public static class CppPrintSelection
{
    public static void Validate(IReadOnlyList<string>? categories)
    {
        if (categories is null || categories.Count == 0 || categories.Count > CppDisplayCatalog.Categories.Count
            || categories.Distinct(StringComparer.Ordinal).Count() != categories.Count
            || categories.Any(x => !CppDisplayCatalog.Categories.Any(c => c.Key == x)))
            throw new ArgumentException("Select one or more supported CPP categories without duplicates.");
    }
}

public sealed class CppPrintUnavailableException(string message) : InvalidOperationException(message);
