using MicroEMR.Application.PatientEncounters.Contracts;
using MicroEMR.Application.PatientEncounters.Repositories;

namespace MicroEMR.Application.PatientEncounters.Services;

public sealed class PatientEncounterService : IPatientEncounterService
{
    private readonly IPatientEncounterRepository _repository;

    public PatientEncounterService(
        IPatientEncounterRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<PatientEncounterListItemResponse>>
        GetByPatientUidAsync(
            Guid patientUid,
            CancellationToken cancellationToken = default)
    {
        return _repository.GetByPatientUidAsync(
            patientUid,
            cancellationToken);
    }

    public Task<PatientEncounterDetailsResponse?> GetByUidAsync(
        Guid encounterUid,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetByUidAsync(
            encounterUid,
            cancellationToken);
    }

    public Task<PatientEncounterDetailsResponse> CreateAsync(
        Guid patientUid,
        CreatePatientEncounterRequest request,
        long? createdBy,
        string? createdByDisplayName,
        CancellationToken cancellationToken = default)
    {
        return _repository.CreateAsync(
            patientUid,
            request,
            createdBy,
            createdByDisplayName,
            cancellationToken);
    }

    public Task<PatientEncounterDetailsResponse?> UpdateNoteAsync(
        Guid patientUid,
        Guid encounterUid,
        UpdateEncounterNoteRequest request,
        long? updatedBy,
        CancellationToken cancellationToken = default)
    {
        if (patientUid == Guid.Empty)
            throw new ArgumentException("Patient identifier is required.", nameof(patientUid));

        if (encounterUid == Guid.Empty)
            throw new ArgumentException("Encounter identifier is required.", nameof(encounterUid));

        return _repository.UpdateNoteAsync(
            patientUid,
            encounterUid,
            request,
            updatedBy,
            cancellationToken);
    }

    public Task<PatientEncounterDetailsResponse?> SignAsync(
        Guid patientUid,
        Guid encounterUid,
        long? signedBy,
        CancellationToken cancellationToken = default)
    {
        if (patientUid == Guid.Empty)
            throw new ArgumentException("Patient identifier is required.", nameof(patientUid));

        if (encounterUid == Guid.Empty)
            throw new ArgumentException("Encounter identifier is required.", nameof(encounterUid));

        return _repository.SignAsync(
            patientUid,
            encounterUid,
            signedBy,
            cancellationToken);
    }

    public Task<StartEncounterFromAppointmentResponse?> StartFromAppointmentAsync(
        Guid appointmentUid,
        long? createdBy,
        CancellationToken cancellationToken = default)
    {
        if (appointmentUid == Guid.Empty)
            throw new ArgumentException("Appointment identifier is required.", nameof(appointmentUid));

        return _repository.StartFromAppointmentAsync(
            appointmentUid, createdBy, cancellationToken);
    }
}
