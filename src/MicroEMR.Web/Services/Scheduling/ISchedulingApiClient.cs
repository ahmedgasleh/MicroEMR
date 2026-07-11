using MicroEMR.Web.Models.Scheduling;

namespace MicroEMR.Web.Services.Scheduling;

public interface ISchedulingApiClient
{
    Task<IReadOnlyList<ScheduleResourceResponse>>
        GetActiveResourcesAsync(
            CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScheduleAppointmentListItemResponse>>
        GetAppointmentsAsync(
            DateTime startUtc,
            DateTime endUtc,
            Guid? resourceUid,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScheduleMonthSummaryItemResponse>> GetMonthSummaryAsync(
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken = default);

    Task<ScheduleAppointmentListItemResponse> CreateAppointmentAsync(
        CreateScheduleAppointmentRequest request,
        CancellationToken cancellationToken = default);

    Task<ScheduleAppointmentDetailsResponse?> GetAppointmentByUidAsync(
        Guid appointmentUid,
        CancellationToken cancellationToken = default);

    Task<CancelScheduleAppointmentResponse?> CancelAppointmentAsync(
        Guid appointmentUid,
        CancelScheduleAppointmentRequest request,
        CancellationToken cancellationToken = default);

    Task<ScheduleAppointmentDetailsResponse?> UpdateAppointmentAsync(
        Guid appointmentUid,
        UpdateScheduleAppointmentRequest request,
        CancellationToken cancellationToken = default);

    Task<ScheduleAppointmentDetailsResponse?> RescheduleAppointmentAsync(
        Guid appointmentUid,
        RescheduleAppointmentRequest request,
        CancellationToken cancellationToken = default);
}
