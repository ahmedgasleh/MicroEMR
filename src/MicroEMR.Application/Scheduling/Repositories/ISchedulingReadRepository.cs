using MicroEMR.Application.Scheduling.Contracts;

namespace MicroEMR.Application.Scheduling.Repositories;

public interface ISchedulingReadRepository
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
}
