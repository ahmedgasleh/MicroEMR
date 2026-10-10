using MicroEMR.Application.ClinicalOutput;

namespace MicroEMR.Application.PatientEncounters.Chronology;

public sealed class EncounterChronologyRequest
{
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Direction { get; set; } = "Ascending";
    public string TimeZoneId { get; set; } = "UTC";
}

public sealed record ChronologyAttachment(string Kind, Guid SourceUid, Guid? ArtifactUid, string Identity, bool Available);
public sealed record EncounterChronologyEntry(string SourceType, Guid SourceUid, DateOnly ClinicalDate,
    DateTime? ClinicalDateTimeUtc, string Title, string Status, Guid? EncounterUid, string Content,
    ChronologyAttachment? Attachment = null, string? Warning = null);
public sealed record EncounterChronologyResponse(Guid PatientUid, string PatientName, string ChartNumber,
    string? HealthCardNumber, DateOnly DateOfBirth, EncounterChronologyRequest Criteria,
    IReadOnlyList<EncounterChronologyEntry> Entries, IReadOnlyList<string> Qualifications, DateTime GeneratedAtUtc)
{
    public bool IsComplete => Qualifications.Count == 0 && Entries.All(x => x.Warning is null);
}

public interface IEncounterChronologyService
{
    Task<EncounterChronologyResponse?> GetAsync(Guid patientUid, EncounterChronologyRequest request,
        string correlation, CancellationToken token = default);
    Task<ClinicalArtifactContent?> OpenEncounterPdfAsync(Guid patientUid, Guid encounterUid,
        string correlation, CancellationToken token = default);
}
