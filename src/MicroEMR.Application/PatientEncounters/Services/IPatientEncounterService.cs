using MicroEMR.Application.PatientEncounters.Contracts;

namespace MicroEMR.Application.PatientEncounters.Services;

public interface IPatientEncounterService
{
    Task<IReadOnlyList<PatientEncounterListItemResponse>>
        GetByPatientUidAsync(
            Guid patientUid,
            CancellationToken cancellationToken = default);

    Task<PatientEncounterDetailsResponse?> GetByUidAsync(
        Guid encounterUid,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PatientEncounterHistoryResponse>> GetHistoryAsync(
        Guid patientUid,
        Guid encounterUid,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PatientEncounterAddendumResponse>> GetAddendumsAsync(
        Guid patientUid,
        Guid encounterUid,
        CancellationToken cancellationToken = default);

    Task<PatientEncounterAddendumResponse?> CreateAddendumAsync(
        Guid patientUid,
        Guid encounterUid,
        CreateEncounterAddendumRequest request,
        long? createdBy,
        CancellationToken cancellationToken = default);

    Task<PatientEncounterDetailsResponse> CreateAsync(
        Guid patientUid,
        CreatePatientEncounterRequest request,
        long? createdBy,
        string? createdByDisplayName,
        CancellationToken cancellationToken = default);

    Task<PatientEncounterDetailsResponse?> UpdateNoteAsync(
        Guid patientUid,
        Guid encounterUid,
        UpdateEncounterNoteRequest request,
        long? updatedBy,
        CancellationToken cancellationToken = default);

    Task<PatientEncounterDetailsResponse?> SignAsync(
        Guid patientUid,
        Guid encounterUid,
        long? signedBy,
        CancellationToken cancellationToken = default);

    Task<StartEncounterFromAppointmentResponse?> StartFromAppointmentAsync(
        Guid appointmentUid,
        long? createdBy,
        CancellationToken cancellationToken = default);
}
