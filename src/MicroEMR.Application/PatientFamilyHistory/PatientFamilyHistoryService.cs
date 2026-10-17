using System.ComponentModel.DataAnnotations;
using MicroEMR.Application.AccessProfiles;

namespace MicroEMR.Application.PatientFamilyHistory;

public interface IPatientFamilyHistoryRepository
{
    Task<IReadOnlyList<PatientFamilyHistoryResponse>> ListAsync(Guid patientUid, string status, CancellationToken token = default);
    Task<PatientFamilyHistoryResponse?> GetAsync(Guid patientUid, Guid familyHistoryUid, CancellationToken token = default);
    Task<PatientFamilyHistoryResponse> CreateAsync(Guid patientUid, CreatePatientFamilyHistoryRequest request, long actor, CancellationToken token = default);
    Task<PatientFamilyHistoryResponse?> UpdateAsync(Guid patientUid, Guid familyHistoryUid, UpdatePatientFamilyHistoryRequest request, long actor, CancellationToken token = default);
    Task<PatientFamilyHistoryResponse?> ArchiveAsync(Guid patientUid, Guid familyHistoryUid, string rowVersion, long actor, CancellationToken token = default);
}
public interface IPatientFamilyHistoryService : IPatientFamilyHistoryRepository;

public sealed class PatientFamilyHistoryService(IPatientFamilyHistoryRepository repository,
    ICurrentUserPermissionService permissions) : IPatientFamilyHistoryService
{
    private async Task Access(Guid patient, bool write, long actor, CancellationToken token)
    {
        if (patient == Guid.Empty) throw new ArgumentException("A patient is required.");
        var effective = await permissions.GetEffectivePermissionsAsync(token);
        if (!effective.Contains(PermissionKeys.PatientsView) || (write && !effective.Contains(PermissionKeys.ClinicalDataManage)))
            throw new UnauthorizedAccessException("Family medical history access is restricted.");
        if (write && actor <= 0) throw new UnauthorizedAccessException("An active clinical actor is required.");
    }
    public async Task<IReadOnlyList<PatientFamilyHistoryResponse>> ListAsync(Guid patientUid, string status, CancellationToken token = default)
    {
        await Access(patientUid, false, 0, token);
        var rows = await repository.ListAsync(patientUid, status is "Archived" or "All" ? status : "Active", token);
        if (rows.Any(x => x.PatientUid != patientUid)) throw new UnauthorizedAccessException("Family history ownership is invalid.");
        return rows;
    }
    public async Task<PatientFamilyHistoryResponse?> GetAsync(Guid patientUid, Guid familyHistoryUid, CancellationToken token = default)
    {
        await Access(patientUid, false, 0, token);
        return Owned(await repository.GetAsync(patientUid, familyHistoryUid, token), patientUid, familyHistoryUid);
    }
    public async Task<PatientFamilyHistoryResponse> CreateAsync(Guid patientUid, CreatePatientFamilyHistoryRequest request, long actor, CancellationToken token = default)
    {
        await Access(patientUid, true, actor, token);
        Validate(request);
        var row = await repository.CreateAsync(patientUid, request, actor, token);
        if (row.PatientUid != patientUid) throw new UnauthorizedAccessException("Family history ownership is invalid.");
        return row;
    }
    public async Task<PatientFamilyHistoryResponse?> UpdateAsync(Guid patientUid, Guid familyHistoryUid, UpdatePatientFamilyHistoryRequest request, long actor, CancellationToken token = default)
    {
        await Access(patientUid, true, actor, token);
        Validate(request); ValidateVersion(request.RowVersion);
        return Owned(await repository.UpdateAsync(patientUid, familyHistoryUid, request, actor, token), patientUid, familyHistoryUid);
    }
    public async Task<PatientFamilyHistoryResponse?> ArchiveAsync(Guid patientUid, Guid familyHistoryUid, string rowVersion, long actor, CancellationToken token = default)
    {
        await Access(patientUid, true, actor, token);
        ValidateVersion(rowVersion);
        return Owned(await repository.ArchiveAsync(patientUid, familyHistoryUid, rowVersion, actor, token), patientUid, familyHistoryUid);
    }
    private static PatientFamilyHistoryResponse? Owned(PatientFamilyHistoryResponse? row, Guid patient, Guid uid) =>
        row is null || (row.PatientUid == patient && row.FamilyHistoryUid == uid) ? row : throw new UnauthorizedAccessException("Family history ownership is invalid.");
    private static void Validate(object request)
    {
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new(request), results, true)) throw new ArgumentException(string.Join(" ", results.Select(x => x.ErrorMessage)));
    }
    public static byte[] ValidateVersion(string? value) => value is not null && Convert.TryFromBase64String(value, new byte[8], out var size) && size == 8
        ? Convert.FromBase64String(value) : throw new ArgumentException("A valid family history row version is required.");
}
