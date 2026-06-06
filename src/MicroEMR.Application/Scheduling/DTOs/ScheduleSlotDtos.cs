namespace MicroEMR.Application.Scheduling.DTOs;

public class ScheduleSlotDto
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public Guid? ClinicResourceId { get; set; }
    public string? ResourceName { get; set; }
    public DateTime SlotStartTime { get; set; }
    public DateTime SlotEndTime { get; set; }
    public string Status { get; set; } = string.Empty; // Available, Blocked, Booked
    public string? BlockReason { get; set; }
}

public class GenerateScheduleSlotsRequest
{
    public Guid ProviderId { get; set; }
    public Guid? ClinicResourceId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int SlotDurationMinutes { get; set; } = 15;
    public List<TimeSlot> AvailableTimeSlots { get; set; } = new();
}

public class TimeSlot
{
    public int DayOfWeek { get; set; } // 0 = Sunday, 6 = Saturday
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}
