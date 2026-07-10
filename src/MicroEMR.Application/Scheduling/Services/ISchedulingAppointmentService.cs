using MicroEMR.Application.Scheduling.Contracts;

namespace MicroEMR.Application.Scheduling.Services;

public interface ISchedulingAppointmentService
{
    Task<ScheduleAppointmentListItemResponse> CreateAsync(
        CreateScheduleAppointmentRequest request,
        long? createdBy,
        CancellationToken cancellationToken = default);
}
