using MicroEMR.Application.Scheduling.DTOs;

namespace MicroEMR.Application.Scheduling.Services;

public interface IScheduleSlotService
{
    Task<List<ScheduleSlotDto>> GenerateSlotsAsync(GenerateScheduleSlotsRequest request, CancellationToken cancellationToken = default);
    Task<List<ScheduleSlotDto>> GetAvailableSlotsAsync(Guid providerId, Guid? clinicResourceId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<ScheduleSlotDto?> GetSlotAsync(Guid slotId, CancellationToken cancellationToken = default);
    Task<bool> BlockSlotAsync(Guid slotId, string reason, CancellationToken cancellationToken = default);
    Task<bool> UnblockSlotAsync(Guid slotId, CancellationToken cancellationToken = default);
}
