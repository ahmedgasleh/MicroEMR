using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalUsers;

namespace MicroEMR.Application.PatientPrescriptions;

// These types are deliberately separate from the general prescription response.
public sealed record PrescriptionPrivateNoteDetail(Guid PatientUid, Guid PrescriptionUid,
    string? NoteText, string Status, string RowVersion, bool CanEdit);
public sealed record PrescriptionPrivateNoteRevision(Guid RevisionUid, int RevisionNumber,
    string Operation, string? NoteText, long ActorUserId, DateTime CreatedAtUtc);
public interface IPrescriptionPrivateNoteRequestContext
{
    string CorrelationId { get; }
}
public interface IPrescriptionPrivateNoteRepository
{
    Task<PrescriptionPrivateNoteDetail?> ReadAsync(Guid patient, Guid prescription, long actor,
        string correlation, bool canPrescribe, CancellationToken token);
    Task<IReadOnlyList<PrescriptionPrivateNoteRevision>?> HistoryAsync(Guid patient, Guid prescription,
        long actor, string correlation, CancellationToken token);
    Task RecordDeniedAsync(string subject, long? actor, Guid? patient, Guid? prescription,
        string correlation, int status, CancellationToken token);
}
public interface IPrescriptionPrivateNoteService
{
    string CorrelationId { get; }
    Task AuthorizeWriteAsync(long actor, CancellationToken token);
    Task<PrescriptionPrivateNoteDetail?> ReadAsync(Guid patient, Guid prescription, CancellationToken token);
    Task<IReadOnlyList<PrescriptionPrivateNoteRevision>?> HistoryAsync(Guid patient, Guid prescription, CancellationToken token);
}
public sealed class PrescriptionPrivateNoteAccessException : Exception
{
    public PrescriptionPrivateNoteAccessException() : base("Private prescription note access is required.") { }
}
public sealed class PrescriptionPrivateNoteService(IPrescriptionPrivateNoteRepository repository,
    ICurrentUserPermissionService permissions, IAuthenticatedClinicalUserAccessor actors,
    IPrescriptionPrivateNoteRequestContext context) : IPrescriptionPrivateNoteService
{
    public string CorrelationId => context.CorrelationId;
    private async Task<long> AuthorizeReadAsync(CancellationToken token)
    {
        if (!await permissions.HasPermissionAsync(PermissionKeys.PatientsView, token) ||
            !await permissions.HasPermissionAsync(PermissionKeys.PrescriptionsViewPrivateNotes, token))
            throw new PrescriptionPrivateNoteAccessException();
        return await actors.GetRequiredUserIdAsync(token);
    }
    public async Task AuthorizeWriteAsync(long actor, CancellationToken token)
    {
        if (actor != await AuthorizeReadAsync(token) ||
            !await permissions.HasPermissionAsync(PermissionKeys.PrescriptionsPrescribe, token))
            throw new PrescriptionPrivateNoteAccessException();
    }
    public async Task<PrescriptionPrivateNoteDetail?> ReadAsync(Guid patient, Guid prescription, CancellationToken token)
    {
        var actor = await AuthorizeReadAsync(token);
        return await repository.ReadAsync(patient, prescription, actor, CorrelationId,
            await permissions.HasPermissionAsync(PermissionKeys.PrescriptionsPrescribe, token), token);
    }
    public async Task<IReadOnlyList<PrescriptionPrivateNoteRevision>?> HistoryAsync(Guid patient, Guid prescription, CancellationToken token)
    {
        var actor = await AuthorizeReadAsync(token);
        return await repository.HistoryAsync(patient, prescription, actor, CorrelationId, token);
    }
}
