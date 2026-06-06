namespace MicroEMR.Application.Scheduling.DTOs;

public class AppointmentHistoryDto
{
    public Guid Id { get; set; }
    public Guid AppointmentId { get; set; }
    public DateTime ChangedAt { get; set; }
    public Guid ChangedBy { get; set; }
    public string ChangedByUserName { get; set; } = string.Empty;
    public string? ChangeType { get; set; }
    public string? OldStartTime { get; set; }
    public string? NewStartTime { get; set; }
    public string? Reason { get; set; }
}

public class CalendarViewDto
{
    public Guid ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public Guid? ClinicResourceId { get; set; }
    public string? ResourceName { get; set; }
    public DateTime ViewDate { get; set; }
    public List<ScheduleSlotDto> AvailableSlots { get; set; } = new();
    public List<AppointmentDto> Appointments { get; set; } = new();
    public List<ResourceBlockDto> ResourceBlocks { get; set; } = new();
}
