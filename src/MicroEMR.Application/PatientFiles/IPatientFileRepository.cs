namespace MicroEMR.Application.PatientFiles;

public interface IPatientFileRepository
{
    Task<IReadOnlyList<PatientFile>> GetByPatientUidAsync(Guid patientUid, CancellationToken cancellationToken = default);
    Task<PatientFile?> GetByUidAsync(Guid patientUid, Guid fileUid, CancellationToken cancellationToken = default);
    Task<PatientFile> CreateAsync(Guid patientUid, CreatePatientFileMetadata metadata,
        long uploadedBy, CancellationToken cancellationToken = default);
    Task<PatientFile> ArchiveAsync(Guid patientUid,Guid fileUid,string rowVersion,long actor,CancellationToken cancellationToken=default);
    Task<PatientFile> RestoreAsync(Guid patientUid,Guid fileUid,string rowVersion,long actor,CancellationToken cancellationToken=default);
}

public interface IPatientFileStorage
{
    Task SaveAsync(Stream content, string storageKey, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
