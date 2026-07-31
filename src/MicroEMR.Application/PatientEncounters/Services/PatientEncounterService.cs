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

    public Task<IReadOnlyList<PatientEncounterHistoryResponse>> GetHistoryAsync(
        Guid patientUid,
        Guid encounterUid,
        CancellationToken cancellationToken = default)
    {
        if (patientUid == Guid.Empty)
            throw new ArgumentException("Patient identifier is required.", nameof(patientUid));
        if (encounterUid == Guid.Empty)
            throw new ArgumentException("Encounter identifier is required.", nameof(encounterUid));

        return _repository.GetHistoryAsync(patientUid, encounterUid, cancellationToken);
    }

    public Task<IReadOnlyList<PatientEncounterAddendumResponse>> GetAddendumsAsync(
        Guid patientUid,
        Guid encounterUid,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifiers(patientUid, encounterUid);
        return _repository.GetAddendumsAsync(patientUid, encounterUid, cancellationToken);
    }

    public Task<PatientEncounterAddendumResponse?> CreateAddendumAsync(
        Guid patientUid,
        Guid encounterUid,
        CreateEncounterAddendumRequest request,
        long? createdBy,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifiers(patientUid, encounterUid);
        if (string.IsNullOrWhiteSpace(request.AddendumText))
            throw new ArgumentException("Addendum text is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ReasonForAmendment))
            throw new ArgumentException("A reason for amendment is required.", nameof(request));

        request.AddendumText = request.AddendumText.Trim();
        request.ReasonForAmendment = request.ReasonForAmendment.Trim();
        return _repository.CreateAddendumAsync(
            patientUid, encounterUid, request, createdBy, cancellationToken);
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

    public Task<PatientEncounterDetailsResponse?> UpdateSoapNoteAsync(
        Guid patientUid,
        Guid encounterUid,
        UpdateEncounterSoapNoteRequest request,
        long? updatedBy,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifiers(patientUid, encounterUid);
        return _repository.UpdateSoapNoteAsync(
            patientUid, encounterUid, request, updatedBy, cancellationToken);
    }

    public async Task<PatientEncounterDetailsResponse?> SignAsync(
        Guid patientUid,
        Guid encounterUid,
        SignPatientEncounterRequest request,
        long? signedBy,
        CancellationToken cancellationToken = default)
    {
        if (patientUid == Guid.Empty)
            throw new ArgumentException("Patient identifier is required.", nameof(patientUid));

        if (encounterUid == Guid.Empty)
            throw new ArgumentException("Encounter identifier is required.", nameof(encounterUid));

        if (string.IsNullOrWhiteSpace(request.RowVersion))
            throw new ArgumentException("Row version is required.", nameof(request));

        var encounter = await _repository.GetByUidAsync(encounterUid, cancellationToken);
        if (encounter is null || encounter.PatientUid != patientUid)
            return null;

        var errors = EncounterSigningValidator.Validate(encounter);
        if (errors.Count > 0)
            throw new EncounterSigningValidationException(errors);

        return await _repository.SignAsync(
            patientUid,
            encounterUid,
            request,
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

    private static void ValidateIdentifiers(Guid patientUid, Guid encounterUid)
    {
        if (patientUid == Guid.Empty)
            throw new ArgumentException("Patient identifier is required.", nameof(patientUid));
        if (encounterUid == Guid.Empty)
            throw new ArgumentException("Encounter identifier is required.", nameof(encounterUid));
    }
}
