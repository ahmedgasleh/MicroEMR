using MicroEMR.Web.Models.PatientEncounters;

namespace MicroEMR.Web.Services.PatientEncounters;

public interface IPatientEncounterApiClient
{
    Task<IReadOnlyList<PatientEncounterListItemResponse>>
        GetByPatientUidAsync(
            Guid patientUid,
            CancellationToken cancellationToken = default);

    Task<PatientEncounterDetailsResponse?> GetByUidAsync(
        Guid encounterUid,
        CancellationToken cancellationToken = default);

    Task<PatientEncounterDetailsResponse> CreateAsync(
        Guid patientUid,
        CreatePatientEncounterRequest request,
        CancellationToken cancellationToken = default);

    Task<PatientEncounterDetailsResponse?> UpdateNoteAsync(
        Guid patientUid,
        Guid encounterUid,
        UpdateEncounterNoteRequest request,
        CancellationToken cancellationToken = default);

    Task<PatientEncounterDetailsResponse?> SignEncounterAsync(
        Guid patientUid,
        Guid encounterUid,
        CancellationToken cancellationToken = default);
}
