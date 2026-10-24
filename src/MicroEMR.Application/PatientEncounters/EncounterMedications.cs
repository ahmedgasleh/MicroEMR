using System.ComponentModel.DataAnnotations;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PatientEncounters.Repositories;
using MicroEMR.Application.PatientMedications.Contracts;
using MicroEMR.Application.PatientMedications.Repositories;
using MicroEMR.Application.ReadAudit;
using MicroEMR.Core.PatientMedications;

namespace MicroEMR.Application.PatientEncounters;

public sealed class SaveEncounterMedicationRequest
{
    public Guid RequestUid { get; set; }
    [Required] public string Action { get; set; } = "";
    [Required] public string EncounterRowVersion { get; set; } = "";
    public Guid? MedicationUid { get; set; }
    public string? MedicationRowVersion { get; set; }
    public CreatePatientMedicationRequest? Medication { get; set; }
    [StringLength(500)] public string? DiscontinueReason { get; set; }
}

public sealed record EncounterMedicationsResponse(Guid PatientUid, Guid EncounterUid, string RowVersion,
    string Status, bool CanManage, IReadOnlyList<PatientMedicationListItemResponse> Items);

public interface IEncounterMedicationRepository
{
    Task<PatientMedicationDetailsResponse?> SaveAsync(Guid patient, Guid encounter, SaveEncounterMedicationRequest request,
        long actor, CancellationToken token = default);
}

public interface IEncounterMedicationService
{
    Task<EncounterMedicationsResponse?> GetAsync(Guid patient, Guid encounter, string correlation, CancellationToken token = default);
    Task<PatientMedicationDetailsResponse?> SaveAsync(Guid patient, Guid encounter, SaveEncounterMedicationRequest request, CancellationToken token = default);
}

public sealed class EncounterMedicationConflictException(string message) : Exception(message);

public sealed class EncounterMedicationService(IEncounterMedicationRepository repository, IPatientEncounterRepository encounters,
    IPatientMedicationRepository medications, ICurrentUserPermissionService permissions,
    IAuthenticatedClinicalUserAccessor actor, IPatientChartReadAuditService audit) : IEncounterMedicationService
{
    private async Task<IReadOnlySet<string>> Access(bool write, CancellationToken token)
    {
        var access = await permissions.GetEffectivePermissionsAsync(token);
        if (!access.Contains(PermissionKeys.EncountersView) || !access.Contains(PermissionKeys.PatientsView) ||
            write && (!access.Contains(PermissionKeys.EncountersEdit) || !access.Contains(PermissionKeys.ClinicalDataManage)))
            throw new UnauthorizedAccessException("Encounter medication access is restricted.");
        return access;
    }

    private async Task<Contracts.PatientEncounterDetailsResponse?> Owned(Guid patient, Guid encounter, CancellationToken token)
    {
        if (patient == Guid.Empty || encounter == Guid.Empty) throw new ArgumentException("Patient and encounter are required.");
        var row = await encounters.GetByUidAsync(encounter, token);
        if (row is not null && (row.PatientUid != patient || row.EncounterUid != encounter))
            throw new UnauthorizedAccessException("Encounter context is unavailable.");
        return row;
    }

    public async Task<EncounterMedicationsResponse?> GetAsync(Guid patient, Guid encounter, string correlation, CancellationToken token = default)
    {
        var access = await Access(false, token);
        await actor.GetRequiredUserIdAsync(token);
        var context = await Owned(patient, encounter, token);
        if (context is null) return null;
        await audit.RecordOpenedAsync(patient, correlation, token);
        var rows = await medications.GetByPatientUidAsync(patient, token);
        if (rows.Any(x => x.PatientUid != patient)) throw new UnauthorizedAccessException("Medication context is unavailable.");
        return new(patient, encounter, context.RowVersion, context.Status,
            context.Status == "Open" && access.Contains(PermissionKeys.EncountersEdit) && access.Contains(PermissionKeys.ClinicalDataManage), rows);
    }

    public async Task<PatientMedicationDetailsResponse?> SaveAsync(Guid patient, Guid encounter, SaveEncounterMedicationRequest request, CancellationToken token = default)
    {
        await Access(true, token);
        Validate(request);
        var user = await actor.GetRequiredUserIdAsync(token);
        var context = await Owned(patient, encounter, token);
        if (context is null) return null;
        if (context.Status != "Open") throw new EncounterMedicationConflictException("Medications can only be managed from an open encounter.");
        // Repository rechecks ownership, both versions, source state and signing under one transaction.
        var saved = await repository.SaveAsync(patient, encounter, request, user, token);
        if (saved is not null && (saved.PatientUid != patient || request.Action != "Create" && saved.MedicationUid != request.MedicationUid))
            throw new UnauthorizedAccessException("Medication context is unavailable.");
        return saved;
    }

    public static void Validate(SaveEncounterMedicationRequest request)
    {
        if (request.RequestUid == Guid.Empty || request.Action is not ("Create" or "Update" or "Discontinue"))
            throw new ArgumentException("Select a medication action and provide a stable request identifier.");
        static void Version(string? value)
        {
            try { if (Convert.FromBase64String(value ?? "").Length != 8) throw new FormatException(); }
            catch (FormatException) { throw new EncounterMedicationConflictException("Reload the encounter and medication before saving."); }
        }
        Version(request.EncounterRowVersion);
        if (request.Action == "Create")
        {
            if (request.MedicationUid is not null) throw new ArgumentException("Selecting an existing medication must not create another record.");
        }
        else
        {
            if (request.MedicationUid is null || request.MedicationUid == Guid.Empty) throw new ArgumentException("Select an existing medication.");
            Version(request.MedicationRowVersion);
        }
        if (request.DiscontinueReason?.Length > 500) throw new ArgumentException("Discontinuation reason must be 500 characters or fewer.");
        if (request.Action == "Discontinue") return;
        var input = request.Medication ?? throw new ArgumentException("Medication details are required.");
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(input, new(input), errors, true)) throw new ArgumentException(errors[0].ErrorMessage);
        if (string.IsNullOrWhiteSpace(input.MedicationName)) throw new ArgumentException("Medication name is required.");
        input.ComplianceAssessment?.EnsureValid();
        input.Classification = MedicationClassifications.Normalize(input.Classification);
        input.MedicationName = input.MedicationName.Trim();
    }
}
