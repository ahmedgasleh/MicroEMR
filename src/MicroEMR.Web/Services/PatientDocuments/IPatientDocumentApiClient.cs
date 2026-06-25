using MicroEMR.Web.Models.PatientDocuments;

namespace MicroEMR.Web.Services.PatientDocuments;

public interface IPatientDocumentApiClient
{
    Task<IReadOnlyList<PatientDocumentListItemResponse>>
        GetByPatientUidAsync (
            Guid patientUid,
            CancellationToken cancellationToken = default );

    Task<PatientDocumentDetailsResponse?> GetByUidAsync (
        Guid documentUid,
        CancellationToken cancellationToken = default );
}