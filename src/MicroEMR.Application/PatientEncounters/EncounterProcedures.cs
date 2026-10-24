using System.ComponentModel.DataAnnotations;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PatientClinicalHistory;
using MicroEMR.Application.PatientEncounters.Repositories;
using MicroEMR.Application.ReadAudit;

namespace MicroEMR.Application.PatientEncounters;

public sealed class SaveEncounterProcedureRequest
{
    public Guid HistoryUid { get; set; }
    public bool IsNew { get; set; }
    [Required] public string EncounterRowVersion { get; set; } = "";
    public string? HistoryRowVersion { get; set; }
    [Required, StringLength(1000)] public string Description { get; set; } = "";
    public ClinicalPartialDate? ProcedureDate { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
}

public sealed record EncounterProceduresResponse(Guid PatientUid, Guid EncounterUid, string RowVersion,
    string Status, bool CanManage, IReadOnlyList<PatientClinicalHistoryResponse> Items);

public interface IEncounterProcedureRepository
{
    Task<PatientClinicalHistoryResponse?> SaveAsync(Guid patient, Guid encounter, SaveEncounterProcedureRequest request,
        long actor, CancellationToken token = default);
}

public interface IEncounterProcedureService
{
    Task<EncounterProceduresResponse?> GetAsync(Guid patient, Guid encounter, string correlation, CancellationToken token = default);
    Task<PatientClinicalHistoryResponse?> SaveAsync(Guid patient, Guid encounter, SaveEncounterProcedureRequest request, CancellationToken token = default);
}

public sealed class EncounterProcedureConflictException(string message) : Exception(message);

public sealed class EncounterProcedureService(IEncounterProcedureRepository repository, IPatientEncounterRepository encounters,
    IPatientClinicalHistoryRepository history, ICurrentUserPermissionService permissions,
    IAuthenticatedClinicalUserAccessor actor, IPatientChartReadAuditService audit) : IEncounterProcedureService
{
    private async Task<IReadOnlySet<string>> Access(bool write, CancellationToken token)
    {
        var access = await permissions.GetEffectivePermissionsAsync(token);
        if (!access.Contains(PermissionKeys.EncountersView) || !access.Contains(PermissionKeys.PatientsView) ||
            write && (!access.Contains(PermissionKeys.EncountersEdit) || !access.Contains(PermissionKeys.ClinicalDataManage)))
            throw new UnauthorizedAccessException("Encounter procedure access is restricted.");
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

    public async Task<EncounterProceduresResponse?> GetAsync(Guid patient, Guid encounter, string correlation, CancellationToken token = default)
    {
        var access = await Access(false, token);
        await actor.GetRequiredUserIdAsync(token);
        var context = await Owned(patient, encounter, token);
        if (context is null) return null;
        await audit.RecordOpenedAsync(patient, correlation, token);
        var rows = await history.ListAsync(patient, "Active", token);
        if (rows.Any(x => x.PatientUid != patient)) throw new UnauthorizedAccessException("Procedure context is unavailable.");
        return new(patient, encounter, context.RowVersion, context.Status,
            context.Status == "Open" && access.Contains(PermissionKeys.EncountersEdit) && access.Contains(PermissionKeys.ClinicalDataManage),
            rows.Where(x => x.Status == "Active" && x.HistoryType is "Procedure" or "Surgical").ToArray());
    }

    public async Task<PatientClinicalHistoryResponse?> SaveAsync(Guid patient, Guid encounter, SaveEncounterProcedureRequest request, CancellationToken token = default)
    {
        await Access(true, token);
        Validate(request);
        var user = await actor.GetRequiredUserIdAsync(token);
        var context = await Owned(patient, encounter, token);
        if (context is null) return null;
        if (context.Status != "Open") throw new EncounterProcedureConflictException("Procedures can only be managed from an open encounter.");
        // The stored procedure independently locks/rechecks the context and both versions through commit.
        var saved = await repository.SaveAsync(patient, encounter, request, user, token);
        if (saved is not null && (saved.PatientUid != patient || saved.HistoryUid != request.HistoryUid || saved.HistoryType is not ("Procedure" or "Surgical")))
            throw new UnauthorizedAccessException("Procedure context is unavailable.");
        return saved;
    }

    public static void Validate(SaveEncounterProcedureRequest request)
    {
        if (request.HistoryUid == Guid.Empty) throw new ArgumentException("A stable procedure identifier is required.");
        static void Version(string? value)
        {
            try { if (Convert.FromBase64String(value ?? "").Length != 8) throw new FormatException(); }
            catch (FormatException) { throw new EncounterProcedureConflictException("Reload the encounter and procedure before saving."); }
        }
        Version(request.EncounterRowVersion);
        if (!request.IsNew) Version(request.HistoryRowVersion);
        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length > 1000 || request.Notes?.Length > 1000)
            throw new ArgumentException("A procedure description is required; description and notes allow up to 1000 characters.");
        var fields = new HistoryClinicalFields { ProcedureDate = request.ProcedureDate, Notes = request.Notes };
        var errors = fields.Validate("Procedure").ToArray();
        if (errors.Length > 0) throw new ArgumentException(errors[0].ErrorMessage);
        request.Description = request.Description.Trim();
        request.Notes = fields.Notes;
    }
}
