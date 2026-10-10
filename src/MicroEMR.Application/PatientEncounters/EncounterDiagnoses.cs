using System.ComponentModel.DataAnnotations;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PatientEncounters.Repositories;
using MicroEMR.Application.ReadAudit;

namespace MicroEMR.Application.PatientEncounters;

public sealed class EncounterDiagnosisInput
{
    public Guid? DiagnosisUid { get; set; }
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    public DateTime? OnsetDate { get; set; }
}
public sealed class SaveEncounterDiagnosesRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    public bool SaveToCpp { get; set; }
    [Required] public IReadOnlyList<EncounterDiagnosisInput> Diagnoses { get; set; } = [];
}
public sealed record EncounterDiagnosis(Guid DiagnosisUid, string Name, string? Description, DateTime? OnsetDate, Guid? PatientProblemUid);
public sealed record EncounterDiagnosesResponse(Guid PatientUid, Guid EncounterUid, string RowVersion, string Status,
    IReadOnlyList<EncounterDiagnosis> Diagnoses)
{
    public bool CanEdit { get; init; }
    public bool CanSaveToCpp { get; init; }
}
public sealed class EncounterDiagnosisConflictException(string message) : Exception(message);
public interface IEncounterDiagnosisRepository
{
    Task<EncounterDiagnosesResponse?> GetAsync(Guid patient, Guid encounter, CancellationToken token = default);
    Task<EncounterDiagnosesResponse?> SaveAsync(Guid patient, Guid encounter, SaveEncounterDiagnosesRequest request, long actor, CancellationToken token = default);
}
public interface IEncounterDiagnosisService
{
    Task<EncounterDiagnosesResponse?> GetAsync(Guid patient, Guid encounter, string correlation, CancellationToken token = default);
    Task<EncounterDiagnosesResponse?> SaveAsync(Guid patient, Guid encounter, SaveEncounterDiagnosesRequest request, CancellationToken token = default);
}
public sealed class EncounterDiagnosisService(IEncounterDiagnosisRepository repository, IPatientEncounterRepository encounters,
    ICurrentUserPermissionService permissions, IAuthenticatedClinicalUserAccessor actor, IPatientChartReadAuditService audit) : IEncounterDiagnosisService
{
    public async Task<EncounterDiagnosesResponse?> GetAsync(Guid patient, Guid encounter, string correlation, CancellationToken token = default)
    {
        ValidateIds(patient,encounter);
        var access = await Access(token);
        await actor.GetRequiredUserIdAsync(token);
        if (await Owned(patient,encounter,token) is null) return null;
        await audit.RecordOpenedAsync(patient,correlation,token);
        var result = await repository.GetAsync(patient,encounter,token);
        return AuthorizeResponse(result,patient,encounter,access);
    }
    public async Task<EncounterDiagnosesResponse?> SaveAsync(Guid patient, Guid encounter, SaveEncounterDiagnosesRequest request, CancellationToken token = default)
    {
        ValidateIds(patient,encounter);
        var access = await Access(token);
        if (!access.Contains(PermissionKeys.EncountersEdit) || request.SaveToCpp &&
            (!access.Contains(PermissionKeys.ClinicalDataManage) || !access.Contains(PermissionKeys.PatientsView)))
            throw new UnauthorizedAccessException("The selected diagnosis save destination is restricted.");
        Validate(request);
        var user = await actor.GetRequiredUserIdAsync(token);
        var record = await Owned(patient,encounter,token);
        if (record is null) return null;
        if (record.Status != "Open") throw new EncounterDiagnosisConflictException("Diagnoses can only be edited in an open encounter.");
        // One repository operation owns the transaction across encounter diagnoses and authoritative Problems.
        return AuthorizeResponse(await repository.SaveAsync(patient,encounter,request,user,token),patient,encounter,access);
    }
    private async Task<IReadOnlySet<string>> Access(CancellationToken token)
    {
        var access = await permissions.GetEffectivePermissionsAsync(token);
        if (!access.Contains(PermissionKeys.EncountersView)) throw new UnauthorizedAccessException("Encounter access is restricted.");
        return access;
    }
    private async Task<Contracts.PatientEncounterDetailsResponse?> Owned(Guid patient, Guid encounter, CancellationToken token)
    {
        var record = await encounters.GetByUidAsync(encounter,token);
        if (record is not null && (record.PatientUid != patient || record.EncounterUid != encounter))
            throw new UnauthorizedAccessException("Encounter context is unavailable.");
        return record;
    }
    private static EncounterDiagnosesResponse? AuthorizeResponse(EncounterDiagnosesResponse? result, Guid patient, Guid encounter, IReadOnlySet<string> access)
    {
        if (result is null) return null;
        if (result.PatientUid != patient || result.EncounterUid != encounter) throw new UnauthorizedAccessException("Encounter context is unavailable.");
        var edit = result.Status == "Open" && access.Contains(PermissionKeys.EncountersEdit);
        return result with { CanEdit = edit, CanSaveToCpp = edit && access.Contains(PermissionKeys.ClinicalDataManage) && access.Contains(PermissionKeys.PatientsView) };
    }
    private static void ValidateIds(Guid patient, Guid encounter)
    {
        if (patient == Guid.Empty || encounter == Guid.Empty) throw new ArgumentException("Patient and encounter are required.");
    }
    public static void Validate(SaveEncounterDiagnosesRequest request)
    {
        try { if (Convert.FromBase64String(request.RowVersion).Length != 8) throw new FormatException(); }
        catch (Exception error) when (error is FormatException or ArgumentNullException) { throw new ArgumentException("Reload the encounter before saving diagnoses."); }
        if (request.Diagnoses is null || request.Diagnoses.Count > 50) throw new ArgumentException("Provide up to 50 diagnoses.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<Guid>();
        foreach (var row in request.Diagnoses)
        {
            if (row is null || string.IsNullOrWhiteSpace(row.Name) || row.Name.Trim().Length > 200 || row.Description?.Length > 1000)
                throw new ArgumentException("Each diagnosis needs a name up to 200 characters and details up to 1000 characters.");
            row.Name = row.Name.Trim(); row.Description = string.IsNullOrWhiteSpace(row.Description) ? null : row.Description.Trim();
            row.OnsetDate = row.OnsetDate?.Date;
            if (!names.Add(row.Name) || row.DiagnosisUid.HasValue && (row.DiagnosisUid == Guid.Empty || !ids.Add(row.DiagnosisUid.Value)))
                throw new ArgumentException("Diagnosis names and identifiers must be distinct.");
        }
    }
}
