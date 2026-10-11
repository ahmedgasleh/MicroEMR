using System.Text.Json.Serialization;
using MicroEMR.Application.PatientEncounters.Contracts;
using MicroEMR.Application.PatientEncounters.Services;

namespace MicroEMR.Application.PatientEncounters;

// The browser supplies content, location and concurrency only. Identity is never bindable.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SaveEncounterContributionRequest
{
    public Guid? ContributionUid { get; set; }
    public string SectionKey { get; set; } = string.Empty;
    public string? FieldKey { get; set; }
    public string Content { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class EncounterContributionResponse
{
    public Guid ContributionUid { get; set; }
    public Guid EncounterUid { get; set; }
    public Guid PatientUid { get; set; }
    public string SectionKey { get; set; } = string.Empty;
    public string? FieldKey { get; set; }
    public string SectionTitle { get; set; } = string.Empty;
    public string? FieldLabel { get; set; }
    public string ValueKind { get; set; } = "Narrative";
    public string? PriorValueJson { get; set; }
    public long OriginalContributorId { get; set; }
    public string OriginalContributorDisplayName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public List<EncounterContributionRevisionResponse> Revisions { get; set; } = [];
    public string Content => Revisions.LastOrDefault()?.Content ?? string.Empty;
}

public sealed record EncounterContributionRevisionResponse(
    int RevisionNumber, string Content, long EditorId, string EditorDisplayName, DateTime RevisedAtUtc);

public interface IEncounterContributionRepository
{
    Task<bool> SaveAsync(Guid patientUid, Guid encounterUid, SaveEncounterContributionRequest request,
        long actorId, CancellationToken token = default);
}

public interface IEncounterContributionService
{
    Task<bool> SaveAsync(Guid patientUid, Guid encounterUid, SaveEncounterContributionRequest request,
        long actorId, CancellationToken token = default);
}

public sealed class EncounterContributionService(
    IPatientEncounterService encounters, IEncounterContributionRepository repository) : IEncounterContributionService
{
    public async Task<bool> SaveAsync(Guid patientUid, Guid encounterUid, SaveEncounterContributionRequest request,
        long actorId, CancellationToken token = default)
    {
        if (patientUid == Guid.Empty || encounterUid == Guid.Empty || request.ContributionUid == Guid.Empty)
            throw new ArgumentException("Valid note and contribution identifiers are required.");
        if (actorId <= 0) throw new UnauthorizedAccessException("A clinical actor is required.");
        if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Length > 32000)
            throw new ArgumentException("Contribution text is required and must not exceed 32000 characters.");
        byte[] version;
        try { version = Convert.FromBase64String(request.RowVersion); }
        catch (FormatException) { throw new ArgumentException("A valid RowVersion is required."); }
        if (version.Length != 8) throw new ArgumentException("A valid RowVersion is required.");
        var encounter = await encounters.GetByUidAsync(encounterUid, token);
        if (encounter is null || encounter.PatientUid != patientUid) return false;
        if (!string.Equals(encounter.Status, "Open", StringComparison.OrdinalIgnoreCase))
            throw new EncounterNoteNotEditableException("Only open encounter contributions can be edited.");
        var validSection = encounter.TemplateVersionUid.HasValue
            ? encounter.TemplateDefinition?.Sections?.Any(s => s.Key == request.SectionKey) == true
            : request.SectionKey is "subjective" or "objective" or "assessment" or "plan" or "notes";
        if (request.FieldKey is not null && encounter.TemplateDefinition?.Sections?
            .SingleOrDefault(s => s.Key == request.SectionKey)?.Fields?
            .Any(f => f.Key == request.FieldKey && f.Type is "Text" or "TextArea") != true)
            throw new ArgumentException("The narrative field does not belong to this encounter section.");
        if (!validSection) throw new ArgumentException("The section does not belong to the encounter.");
        if (request.ContributionUid.HasValue && !encounter.Contributions.Any(c =>
            c.ContributionUid == request.ContributionUid && c.SectionKey == request.SectionKey
                && c.FieldKey == request.FieldKey && c.ValueKind == "Narrative")) return false;
        return await repository.SaveAsync(patientUid, encounterUid, request, actorId, token);
    }
}
