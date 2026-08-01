namespace MicroEMR.Application.Scheduling.Services;

public interface IAppointmentStatusTransitionService
{
    bool CanTransition(AppointmentStatus currentStatus, AppointmentStatus targetStatus);

    void EnsureCanTransition(AppointmentStatus currentStatus, AppointmentStatus targetStatus);
}
