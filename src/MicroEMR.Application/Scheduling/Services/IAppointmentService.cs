using MicroEMR.Application.Scheduling.DTOs;

namespace MicroEMR.Application.Scheduling.Services;

public interface IAppointmentService
{
    Task<AppointmentDto> CreateAppointmentAsync(CreateAppointmentRequest request, Guid currentUserId, CancellationToken cancellationToken = default);
    Task<AppointmentDto> RescheduleAppointmentAsync(RescheduleAppointmentRequest request, Guid currentUserId, CancellationToken cancellationToken = default);
    Task<AppointmentDto> CancelAppointmentAsync(CancelAppointmentRequest request, Guid currentUserId, CancellationToken cancellationToken = default);
    Task<AppointmentDto?> GetAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken = default);
    Task<List<AppointmentDto>> GetPatientAppointmentsAsync(Guid patientId, CancellationToken cancellationToken = default);
    Task<List<AppointmentDto>> GetProviderAppointmentsAsync(Guid providerId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<List<AppointmentHistoryDto>> GetAppointmentHistoryAsync(Guid appointmentId, CancellationToken cancellationToken = default);
    Task<bool> ConfirmAppointmentAsync(Guid appointmentId, Guid currentUserId, CancellationToken cancellationToken = default);
}
