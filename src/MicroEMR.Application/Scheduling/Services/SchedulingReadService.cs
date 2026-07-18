using MicroEMR.Application.Scheduling.Contracts;
using MicroEMR.Application.Scheduling.Repositories;

namespace MicroEMR.Application.Scheduling.Services;

public sealed class SchedulingReadService : ISchedulingReadService
{
    private readonly ISchedulingReadRepository _repository;

    public SchedulingReadService(ISchedulingReadRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<ScheduleResourceResponse>> GetActiveResourcesAsync(
        CancellationToken cancellationToken = default)
    {
        return _repository.GetActiveResourcesAsync(cancellationToken);
    }

    public Task<IReadOnlyList<ScheduleAppointmentListItemResponse>> GetAppointmentsAsync(
        DateTime startUtc,
        DateTime endUtc,
        Guid? resourceUid,
        CancellationToken cancellationToken = default)
    {
        if (endUtc <= startUtc)
        {
            throw new ArgumentException(
                "The appointment end range must be after the start range.");
        }

        return _repository.GetAppointmentsAsync(
            NormalizeUtc(startUtc),
            NormalizeUtc(endUtc),
            resourceUid,
            cancellationToken);
    }

    public Task<ScheduleAppointmentDetailsResponse?> GetAppointmentByUidAsync(
        Guid appointmentUid,
        CancellationToken cancellationToken = default)
    {
        if (appointmentUid == Guid.Empty)
            throw new ArgumentException("Appointment UID is required.", nameof(appointmentUid));

        return _repository.GetAppointmentByUidAsync(appointmentUid, cancellationToken);
    }

    public Task<IReadOnlyList<AppointmentHistoryResponse>> GetHistoryAsync(
        Guid appointmentUid,
        CancellationToken cancellationToken = default)
    {
        if (appointmentUid == Guid.Empty)
            throw new ArgumentException("Appointment UID is required.", nameof(appointmentUid));

        return _repository.GetHistoryAsync(appointmentUid, cancellationToken);
    }

    public Task<IReadOnlyList<ScheduleMonthSummaryItemResponse>> GetMonthSummaryAsync(
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken = default)
    {
        if (endUtc <= startUtc)
            throw new ArgumentException("The month summary end range must be after the start range.");
        if (endUtc - startUtc > TimeSpan.FromDays(45))
            throw new ArgumentException("The month summary range cannot exceed 45 days.");

        return _repository.GetMonthSummaryAsync(
            NormalizeUtc(startUtc), NormalizeUtc(endUtc), cancellationToken);
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        if (value.Kind == DateTimeKind.Unspecified)
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Local)
                .ToUniversalTime();
        }

        return value.ToUniversalTime();
    }
}
