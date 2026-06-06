using MicroEMR.Application.Scheduling.DTOs;

namespace MicroEMR.Application.Scheduling.Services;

public interface ICalendarService
{
    Task<CalendarViewDto> GetCalendarViewAsync(Guid providerId, Guid? clinicResourceId, DateTime viewDate, CancellationToken cancellationToken = default);
    Task<List<CalendarViewDto>> GetMultiProviderCalendarAsync(List<Guid> providerIds, DateTime viewDate, CancellationToken cancellationToken = default);
    Task<List<CalendarViewDto>> GetMultiResourceCalendarAsync(Guid providerId, DateTime viewDate, CancellationToken cancellationToken = default);
    Task<List<ScheduleSlotDto>> FindAvailableSlotsAsync(Guid patientId, List<Guid> providerIds, DateTime preferredDate, int durationMinutes, CancellationToken cancellationToken = default);
}
