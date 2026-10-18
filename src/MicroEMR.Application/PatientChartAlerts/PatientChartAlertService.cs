using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Application.PatientChartAlerts;

public interface IPatientChartAlertService
{
    Task<IReadOnlyList<PatientChartAlertResponse>> ListAsync(Guid patientUid, string status = "Active", CancellationToken token = default);
    Task<PatientChartAlertResponse?> GetAsync(Guid patientUid, Guid uid, CancellationToken token = default);
    Task<PatientChartAlertResponse?> CreateAsync(Guid patientUid, CreatePatientChartAlertRequest request, long actor, CancellationToken token = default);
    Task<PatientChartAlertResponse?> UpdateAsync(Guid patientUid, Guid uid, UpdatePatientChartAlertRequest request, long actor, CancellationToken token = default);
    Task<PatientChartAlertResponse?> ResolveAsync(Guid patientUid, Guid uid, ResolvePatientChartAlertRequest request, long actor, CancellationToken token = default);
}
public sealed class PatientChartAlertService(IPatientChartAlertRepository repository) : IPatientChartAlertService
{
    public async Task<IReadOnlyList<PatientChartAlertResponse>> ListAsync(Guid patientUid, string status = "Active", CancellationToken token = default)
    {
        var rows = await repository.GetByPatientUidAsync(patientUid, status, token);
        if (rows.Any(x => x.PatientUid != patientUid)) throw new UnauthorizedAccessException("Alert patient context is invalid.");
        return rows;
    }
    public async Task<PatientChartAlertResponse?> GetAsync(Guid patientUid, Guid uid, CancellationToken token = default)
    {
        var row = await repository.GetByUidAsync(patientUid, uid, token);
        if (row is not null && row.PatientUid != patientUid) throw new UnauthorizedAccessException("Alert patient context is invalid.");
        return row;
    }
    public Task<PatientChartAlertResponse?> CreateAsync(Guid patientUid, CreatePatientChartAlertRequest request, long actor, CancellationToken token = default)
    { Validate(request); return repository.CreateAsync(patientUid, request, actor, token); }
    public Task<PatientChartAlertResponse?> UpdateAsync(Guid patientUid, Guid uid, UpdatePatientChartAlertRequest request, long actor, CancellationToken token = default)
    { Validate(request); _ = Version(request.RowVersion); return repository.UpdateAsync(patientUid, uid, request, actor, token); }
    public Task<PatientChartAlertResponse?> ResolveAsync(Guid patientUid, Guid uid, ResolvePatientChartAlertRequest request, long actor, CancellationToken token = default)
    { Validate(request); _ = Version(request.RowVersion); return repository.ResolveAsync(patientUid, uid, request, actor, token); }
    private static void Validate(object request)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), errors, true)) throw new ChartAlertValidationException(errors[0].ErrorMessage!);
        if (request is SavePatientChartAlertRequest save && string.IsNullOrWhiteSpace(save.AlertTitle)) throw new ChartAlertValidationException("Alert title is required.");
    }
    public static byte[] Version(string? value)
    {
        try { var bytes = Convert.FromBase64String(value ?? ""); return bytes.Length == 8 ? bytes : throw new FormatException(); }
        catch (FormatException) { throw new ChartAlertValidationException("Reload before saving: an exact eight-byte RowVersion is required."); }
    }
}
