namespace MicroEMR.Application.Cdm;

public interface ICdmEnrollmentRepository
{
    Task<IReadOnlyList<CdmEnrollmentResponse>> ListAsync(Guid patientUid, CancellationToken token);
    Task<CdmEnrollmentResponse?> GetAsync(Guid patientUid, Guid enrollmentUid, CancellationToken token);
    Task<CdmEnrollmentResponse> CreateAsync(Guid patientUid, Guid problemUid, CdmProgramMetadata program, long actor, CancellationToken token);
    Task<CdmEnrollmentResponse?> InactivateAsync(Guid patientUid, Guid enrollmentUid, byte[] rowVersion, string? reason, long actor, CancellationToken token);
}

public interface ICdmEnrollmentService
{
    Task<CdmSummaryResponse> GetSummaryAsync(Guid patientUid, CancellationToken token);
    Task<CdmEnrollmentResponse?> GetAsync(Guid patientUid, Guid enrollmentUid, CancellationToken token);
    Task<CdmEnrollmentResponse> CreateAsync(Guid patientUid, CreateCdmEnrollmentRequest request, long actor, CancellationToken token);
    Task<CdmEnrollmentResponse?> InactivateAsync(Guid patientUid, Guid enrollmentUid, InactivateCdmEnrollmentRequest request, long actor, CancellationToken token);
}

public sealed class CdmEnrollmentService(ICdmProgramRegistry registry, ICdmEnrollmentRepository repository) : ICdmEnrollmentService
{
    public async Task<CdmSummaryResponse> GetSummaryAsync(Guid patientUid, CancellationToken token) =>
        new(registry.Programs, await repository.ListAsync(patientUid, token));
    public Task<CdmEnrollmentResponse?> GetAsync(Guid patientUid, Guid enrollmentUid, CancellationToken token) => repository.GetAsync(patientUid, enrollmentUid, token);

    public Task<CdmEnrollmentResponse> CreateAsync(Guid patientUid, CreateCdmEnrollmentRequest request, long actor, CancellationToken token)
    {
        var program = registry.Find(request.ProgramKey, request.ProgramVersion) ??
            throw new CdmEnrollmentValidationException("The requested CDM program is not registered and approved.");
        return repository.CreateAsync(patientUid, request.PatientProblemUid, program, actor, token);
    }

    public Task<CdmEnrollmentResponse?> InactivateAsync(Guid patientUid, Guid enrollmentUid, InactivateCdmEnrollmentRequest request, long actor, CancellationToken token)
    {
        byte[] rowVersion;
        try { rowVersion = Convert.FromBase64String(request.RowVersion); }
        catch (FormatException) { throw new CdmEnrollmentValidationException("A valid row version is required."); }
        return repository.InactivateAsync(patientUid, enrollmentUid, rowVersion, request.Reason, actor, token);
    }
}
