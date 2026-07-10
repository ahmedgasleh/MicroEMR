using MicroEMR.Application.Scheduling.Contracts;
using MicroEMR.Application.Scheduling.Repositories;

namespace MicroEMR.Application.Scheduling.Services;

public sealed class SchedulingAppointmentService : ISchedulingAppointmentService
{
    private readonly ISchedulingAppointmentRepository _repository;

    public SchedulingAppointmentService(ISchedulingAppointmentRepository repository)
    {
        _repository = repository;
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
}
