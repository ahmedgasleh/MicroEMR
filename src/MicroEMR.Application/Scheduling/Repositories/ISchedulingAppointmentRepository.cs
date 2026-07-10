using MicroEMR.Application.Scheduling.Contracts;

namespace MicroEMR.Application.Scheduling.Repositories;

public interface ISchedulingAppointmentRepository
{
    Task<ScheduleAppointmentListItemResponse> CreateAsync(
        CreateScheduleAppointmentRequest request,
        long? createdBy,
        CancellationToken cancellationToken = default);
}
