using MicroEMR.Application.PatientEncounters.Contracts;
using MicroEMR.Application.PatientEncounters.Repositories;
using MicroEMR.Application.Scheduling;
using MicroEMR.Application.Scheduling.Repositories;
using MicroEMR.Application.Scheduling.Services;

namespace MicroEMR.Application.PatientEncounters.Services;

public sealed class PatientEncounterService : IPatientEncounterService
{
    private readonly IPatientEncounterRepository _repository;
    private readonly ISchedulingAppointmentRepository _schedulingAppointmentRepository;
    private readonly IAppointmentStatusTransitionService _appointmentStatusTransitionService;

    public PatientEncounterService(
        IPatientEncounterRepository repository,
        ISchedulingAppointmentRepository schedulingAppointmentRepository,
        IAppointmentStatusTransitionService appointmentStatusTransitionService)
    {
        _repository = repository;
        _schedulingAppointmentRepository = schedulingAppointmentRepository;
        _appointmentStatusTransitionService = appointmentStatusTransitionService;
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

        request.AddendumText = request.AddendumText.Trim();
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

    public async Task<StartEncounterFromAppointmentResponse?> StartFromAppointmentAsync(
        Guid appointmentUid,
        long? createdBy,
        CancellationToken cancellationToken = default)
    {
        if (appointmentUid == Guid.Empty)
            throw new ArgumentException("Appointment identifier is required.", nameof(appointmentUid));

        var appointmentStatus = await _schedulingAppointmentRepository.GetStatusAsync(
            appointmentUid,
            cancellationToken);
        if (appointmentStatus is null)
            return null;

        switch (appointmentStatus.Value)
        {
            case AppointmentStatus.Cancelled:
                throw new AppointmentCancelledException(
                    "Cancelled appointments cannot start encounters.");
            case AppointmentStatus.NoShow:
                throw new AppointmentNoShowException(
                    "No-show appointments cannot start encounters.");
            case AppointmentStatus.Completed:
                throw new AppointmentCompletedException(
                    "Completed appointments cannot start new encounters.");
            case AppointmentStatus.Seen:
                break;
            default:
                _appointmentStatusTransitionService.EnsureCanTransition(
                    appointmentStatus.Value,
                    AppointmentStatus.Seen);
                break;
        }

        return await _repository.StartFromAppointmentAsync(
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
