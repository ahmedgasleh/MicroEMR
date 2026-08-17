using MicroEMR.Application.ClinicalUsers;

namespace MicroEMR.Application.ReadAudit;

public static class ReadAuditActions
{
    public const string PatientChartOpened = "PatientChartOpened";
    public const string EncounterViewed = "EncounterViewed";
    public const string PatientDocumentViewed = "PatientDocumentViewed";
}

public static class ReadAuditResourceTypes
{
    public const string PatientChart = "PatientChart";
    public const string Encounter = "Encounter";
    public const string PatientDocument = "PatientDocument";
}

public interface IReadAuditRepository
{
    Task<Guid> RecordPatientChartOpenedAsync(
        Guid patientUid,
        long clinicalUserId,
        string requestCorrelationId,
        string sourceApplication,
        CancellationToken cancellationToken = default);

    Task<Guid> RecordStructuredReadAsync(
        string eventType,
        string resourceType,
        Guid resourceUid,
        Guid patientUid,
        long clinicalUserId,
        string requestCorrelationId,
        string sourceApplication,
        CancellationToken cancellationToken = default);
}

public interface IPatientChartReadAuditService
{
    Task<Guid> RecordOpenedAsync(
        Guid patientUid,
        string requestCorrelationId,
        CancellationToken cancellationToken = default);
}

public sealed class PatientChartReadAuditService(
    IReadAuditRepository repository,
    IAuthenticatedClinicalUserAccessor actor) : IPatientChartReadAuditService
{
    public async Task<Guid> RecordOpenedAsync(
        Guid patientUid,
        string requestCorrelationId,
        CancellationToken cancellationToken = default)
    {
        if (patientUid == Guid.Empty) throw new ArgumentException("A patient is required.", nameof(patientUid));
        if (string.IsNullOrWhiteSpace(requestCorrelationId))
            throw new ArgumentException("A request correlation identifier is required.", nameof(requestCorrelationId));

        var clinicalUserId = await actor.GetRequiredUserIdAsync(cancellationToken);
        return await repository.RecordPatientChartOpenedAsync(
            patientUid, clinicalUserId, requestCorrelationId.Trim(), "MicroEMR.Api", cancellationToken);
    }
}
