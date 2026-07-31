using MicroEMR.Application.Scheduling.Contracts;
using MicroEMR.Application.Scheduling.Repositories;

namespace MicroEMR.Application.Scheduling.Services;

public sealed class SchedulingAppointmentService : ISchedulingAppointmentService
{
    private readonly ISchedulingAppointmentRepository _repository;
    private readonly IAppointmentStatusTransitionService _transitionService;

    public SchedulingAppointmentService(
        ISchedulingAppointmentRepository repository,
        IAppointmentStatusTransitionService transitionService)
    {
        _repository = repository;
        _transitionService = transitionService;
    }

    public Task<ScheduleAppointmentListItemResponse> CreateAsync(
        CreateScheduleAppointmentRequest request,
        long? createdBy,
        CancellationToken cancellationToken = default)
    {
        if (request.EndDateTimeUtc <= request.StartDateTimeUtc)
        {
            throw new ArgumentException("The end time must be after the start time.");
        }

        return _repository.CreateAsync(request, createdBy, cancellationToken);
    }

    public Task<CancelScheduleAppointmentResponse?> CancelAsync(
        Guid appointmentUid,
        CancelScheduleAppointmentRequest request,
        long? cancelledBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (appointmentUid == Guid.Empty)
            throw new ArgumentException("Appointment identifier is required.", nameof(appointmentUid));
        if (request.CancelReason?.Length > 500)
            throw new ArgumentException("Cancel reason cannot exceed 500 characters.", nameof(request));

        return _repository.CancelAsync(appointmentUid, request, cancelledBy, cancellationToken);
    }

    public Task<ScheduleAppointmentDetailsResponse?> UpdateAsync(
        Guid appointmentUid,
        UpdateScheduleAppointmentRequest request,
        long? modifiedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (appointmentUid == Guid.Empty)
            throw new ArgumentException("Appointment identifier is required.", nameof(appointmentUid));
        if (request.PrimaryResourceUid == Guid.Empty)
            throw new ArgumentException("Primary resource is required.", nameof(request));
        if (request.EndDateTimeUtc <= request.StartDateTimeUtc)
            throw new ArgumentException("End time must be after start time.", nameof(request));

        return _repository.UpdateAsync(appointmentUid, request, modifiedBy, cancellationToken);
    }

    public Task<ScheduleAppointmentDetailsResponse?> RescheduleAsync(
        Guid appointmentUid,
        RescheduleAppointmentRequest request,
        long? modifiedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (appointmentUid == Guid.Empty)
            throw new ArgumentException("Appointment identifier is required.", nameof(appointmentUid));
        if (request.PrimaryResourceUid == Guid.Empty)
            throw new ArgumentException("Primary resource is required.", nameof(request));
        if (request.EndDateTimeUtc <= request.StartDateTimeUtc)
            throw new ArgumentException("End time must be after start time.", nameof(request));

        return _repository.RescheduleAsync(appointmentUid, request, modifiedBy, cancellationToken);
    }

    public Task<UpdateAppointmentStatusResponse?> UpdateStatusAsync(
        Guid appointmentUid,
        UpdateAppointmentStatusRequest request,
        long? updatedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (appointmentUid == Guid.Empty)
            throw new ArgumentException("Appointment identifier is required.", nameof(appointmentUid));
        if (string.IsNullOrWhiteSpace(request.ExpectedStatus))
            throw new ArgumentException("Expected status is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.RowVersion))
            throw new ArgumentException("Row version is required.", nameof(request));
        var current = AppointmentStatusCatalog.Parse(request.ExpectedStatus);
        var target = AppointmentStatusCatalog.Parse(request.Status);
        _transitionService.EnsureCanTransition(current, target);
        request.ExpectedStatus = AppointmentStatusCatalog.ToStoredValue(current);
        request.Status = AppointmentStatusCatalog.ToStoredValue(target);
        request.Reason = request.Reason?.Trim();
        if (request.Reason?.Length > 500)
            throw new ArgumentException("Reason cannot exceed 500 characters.", nameof(request));
        return _repository.UpdateStatusAsync(appointmentUid, request, updatedBy, cancellationToken);
    }

    public Task<SchedulingBlockedTimeResponse?> CreateBlockedTimeAsync(
        CreateSchedulingBlockedTimeRequest request,
        long? createdBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ResourceUid == Guid.Empty)
            throw new ArgumentException("Resource is required.", nameof(request));
        if (request.EndDateTimeUtc <= request.StartDateTimeUtc)
            throw new ArgumentException("End time must be after start time.", nameof(request));
        if (request.Reason?.Length > 500)
            throw new ArgumentException("Reason cannot exceed 500 characters.", nameof(request));
        return _repository.CreateBlockedTimeAsync(request, createdBy, cancellationToken);
    }

    public Task<SchedulingBlockedTimeResponse?> CancelBlockedTimeAsync(
        Guid blockedTimeUid,
        long? cancelledBy,
        CancellationToken cancellationToken = default)
    {
        if (blockedTimeUid == Guid.Empty)
            throw new ArgumentException("Blocked-time identifier is required.", nameof(blockedTimeUid));
        return _repository.CancelBlockedTimeAsync(blockedTimeUid, cancelledBy, cancellationToken);
    }
}
