using System.ComponentModel.DataAnnotations;
using MicroEMR.Application.AccessProfiles;

namespace MicroEMR.Application.PatientRiskFactors;

public interface IPatientRiskFactorRepository
{
    Task<IReadOnlyList<PatientRiskFactorResponse>> ListAsync(Guid patientUid, string status, CancellationToken token = default);
    Task<PatientRiskFactorResponse?> GetAsync(Guid patientUid, Guid riskFactorUid, CancellationToken token = default);
    Task<PatientRiskFactorResponse> CreateAsync(Guid patientUid, CreatePatientRiskFactorRequest request, long actor, CancellationToken token = default);
    Task<PatientRiskFactorResponse?> UpdateAsync(Guid patientUid, Guid riskFactorUid, UpdatePatientRiskFactorRequest request, long actor, CancellationToken token = default);
    Task<PatientRiskFactorResponse?> ArchiveAsync(Guid patientUid, Guid riskFactorUid, string rowVersion, long actor, CancellationToken token = default);
}
public interface IPatientRiskFactorService : IPatientRiskFactorRepository;

public sealed class PatientRiskFactorService(IPatientRiskFactorRepository repository,
    ICurrentUserPermissionService permissions) : IPatientRiskFactorService
{
    private async Task Access(Guid patient, bool write, long actor, CancellationToken token)
    {
        if (patient == Guid.Empty) throw new ArgumentException("A patient is required.");
        var effective = await permissions.GetEffectivePermissionsAsync(token);
        if (!effective.Contains(PermissionKeys.PatientsView) || (write && !effective.Contains(PermissionKeys.ClinicalDataManage)))
            throw new UnauthorizedAccessException("Risk factors access is restricted.");
        if (write && actor <= 0) throw new UnauthorizedAccessException("An active clinical actor is required.");
    }
    public async Task<IReadOnlyList<PatientRiskFactorResponse>> ListAsync(Guid patientUid, string status, CancellationToken token = default)
    {
        await Access(patientUid, false, 0, token);
        var rows = await repository.ListAsync(patientUid, status is "Archived" or "All" ? status : "Active", token);
        if (rows.Any(x => x.PatientUid != patientUid)) throw new UnauthorizedAccessException("Risk factor ownership is invalid.");
        return rows;
    }
    public async Task<PatientRiskFactorResponse?> GetAsync(Guid patientUid, Guid riskFactorUid, CancellationToken token = default)
    {
        await Access(patientUid, false, 0, token);
        return Owned(await repository.GetAsync(patientUid, riskFactorUid, token), patientUid, riskFactorUid);
    }
    public async Task<PatientRiskFactorResponse> CreateAsync(Guid patientUid, CreatePatientRiskFactorRequest request, long actor, CancellationToken token = default)
    {
        await Access(patientUid, true, actor, token);
        Validate(request);
        var row = await repository.CreateAsync(patientUid, request, actor, token);
        if (row.PatientUid != patientUid) throw new UnauthorizedAccessException("Risk factor ownership is invalid.");
        return row;
    }
    public async Task<PatientRiskFactorResponse?> UpdateAsync(Guid patientUid, Guid riskFactorUid, UpdatePatientRiskFactorRequest request, long actor, CancellationToken token = default)
    {
        await Access(patientUid, true, actor, token);
        Validate(request); ValidateVersion(request.RowVersion);
        return Owned(await repository.UpdateAsync(patientUid, riskFactorUid, request, actor, token), patientUid, riskFactorUid);
    }
    public async Task<PatientRiskFactorResponse?> ArchiveAsync(Guid patientUid, Guid riskFactorUid, string rowVersion, long actor, CancellationToken token = default)
    {
        await Access(patientUid, true, actor, token);
        ValidateVersion(rowVersion);
        return Owned(await repository.ArchiveAsync(patientUid, riskFactorUid, rowVersion, actor, token), patientUid, riskFactorUid);
    }
    private static PatientRiskFactorResponse? Owned(PatientRiskFactorResponse? row, Guid patient, Guid uid) =>
        row is null || (row.PatientUid == patient && row.RiskFactorUid == uid) ? row : throw new UnauthorizedAccessException("Risk factor ownership is invalid.");
    private static void Validate(object request)
    {
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new(request), results, true)) throw new ArgumentException(string.Join(" ", results.Select(x => x.ErrorMessage)));
    }
    public static byte[] ValidateVersion(string? value) => value is not null && Convert.TryFromBase64String(value, new byte[8], out var size) && size == 8
        ? Convert.FromBase64String(value) : throw new ArgumentException("A valid risk factor row version is required.");
}
