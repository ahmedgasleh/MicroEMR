using MicroEMR.Application.Scheduling.Contracts;

namespace MicroEMR.Application.Scheduling.Services;

public interface ISchedulingReadService
{
    Task<IReadOnlyList<ScheduleResourceResponse>> GetActiveResourcesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScheduleAppointmentListItemResponse>> GetAppointmentsAsync(
        DateTime startUtc,
        DateTime endUtc,
        Guid? resourceUid,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScheduleMonthSummaryItemResponse>> GetMonthSummaryAsync(
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken = default);

    Task<ScheduleAppointmentDetailsResponse?> GetAppointmentByUidAsync(
        Guid appointmentUid,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppointmentHistoryResponse>> GetHistoryAsync(
        Guid appointmentUid,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchedulingBlockedTimeResponse>> GetBlockedTimesAsync(
        DateTime startDateTimeUtc,
        DateTime endDateTimeUtc,
        CancellationToken cancellationToken = default);
}
