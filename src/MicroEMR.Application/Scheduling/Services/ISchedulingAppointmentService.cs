using MicroEMR.Application.Scheduling.Contracts;

namespace MicroEMR.Application.Scheduling.Services;

public interface ISchedulingAppointmentService
{
    Task<ScheduleAppointmentListItemResponse> CreateAsync(
        CreateScheduleAppointmentRequest request,
        long? createdBy,
        CancellationToken cancellationToken = default);

    Task<CancelScheduleAppointmentResponse?> CancelAsync(
        Guid appointmentUid,
        CancelScheduleAppointmentRequest request,
        long? cancelledBy,
        CancellationToken cancellationToken = default);

    Task<ScheduleAppointmentDetailsResponse?> UpdateAsync(
        Guid appointmentUid,
        UpdateScheduleAppointmentRequest request,
        long? modifiedBy,
        CancellationToken cancellationToken = default);

    Task<ScheduleAppointmentDetailsResponse?> RescheduleAsync(
        Guid appointmentUid,
        RescheduleAppointmentRequest request,
        long? modifiedBy,
        CancellationToken cancellationToken = default);
}
