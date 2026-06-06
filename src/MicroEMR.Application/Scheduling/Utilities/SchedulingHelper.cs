namespace MicroEMR.Application.Scheduling.Utilities;

/// <summary>
/// Utility class for scheduling operations
/// </summary>
public static class SchedulingHelper
{
    /// <summary>
    /// Validates appointment time slots (15-minute alignment)
    /// </summary>
    public static bool IsValidSlotTime(DateTime time)
    {
        return time.Minute % 15 == 0;
    }

    /// <summary>
    /// Generates 15-minute slots between start and end times
    /// </summary>
    public static List<DateTime> GenerateSlots(DateTime startTime, DateTime endTime)
    {
        var slots = new List<DateTime>();
        
        // Align to 15-minute boundary
        var current = startTime.AddMinutes(-(startTime.Minute % 15));
        
        while (current <= endTime)
        {
            if (current >= startTime)
            {
                slots.Add(current);
            }
            current = current.AddMinutes(15);
        }
        
        return slots;
    }

    /// <summary>
    /// Calculates available slots considering existing appointments and blocks
    /// </summary>
    public static List<(DateTime Start, DateTime End)> CalculateAvailableSlots(
        List<(DateTime Start, DateTime End)> workingHours,
        List<(DateTime Start, DateTime End)> appointments,
        List<(DateTime Start, DateTime End)> blocks)
    {
        var availableSlots = new List<(DateTime, DateTime)>();

        foreach (var (workStart, workEnd) in workingHours)
        {
            var current = workStart;
            
            while (current < workEnd)
            {
                var slotEnd = current.AddMinutes(15);
                
                // Check if slot overlaps with any appointment or block
                bool isAvailable = !appointments.Any(a => IsOverlapping(current, slotEnd, a.Start, a.End))
                                && !blocks.Any(b => IsOverlapping(current, slotEnd, b.Start, b.End));
                
                if (isAvailable)
                {
                    availableSlots.Add((current, slotEnd));
                }
                
                current = slotEnd;
            }
        }
        
        return availableSlots;
    }

    /// <summary>
    /// Checks if two time ranges overlap
    /// </summary>
    public static bool IsOverlapping(DateTime start1, DateTime end1, DateTime start2, DateTime end2)
    {
        return start1 < end2 && end1 > start2;
    }

    /// <summary>
    /// Checks for conflicts between appointment and resource blocks
    /// </summary>
    public static bool HasConflict(
        DateTime appointmentStart,
        DateTime appointmentEnd,
        List<(DateTime Start, DateTime End)> existingAppointments,
        List<(DateTime Start, DateTime End)> resourceBlocks)
    {
        var appointments = existingAppointments
            .Where(a => IsOverlapping(appointmentStart, appointmentEnd, a.Start, a.End))
            .ToList();
        
        var blocks = resourceBlocks
            .Where(b => IsOverlapping(appointmentStart, appointmentEnd, b.Start, b.End))
            .ToList();
        
        return appointments.Count > 0 || blocks.Count > 0;
    }

    /// <summary>
    /// Generates recurring schedule slots based on weekly pattern
    /// </summary>
    public static List<DateTime> GenerateRecurringSlots(
        DateTime startDate,
        DateTime endDate,
        List<(DayOfWeek Day, TimeOnly StartTime, TimeOnly EndTime)> weeklyPattern,
        int slotDurationMinutes = 15)
    {
        var slots = new List<DateTime>();
        var current = startDate.Date;
        
        while (current <= endDate.Date)
        {
            var pattern = weeklyPattern.FirstOrDefault(p => p.Day == current.DayOfWeek);
            
            if (pattern != default)
            {
                var slotStart = current.Date.Add(pattern.StartTime.ToTimeSpan());
                var slotEnd = current.Date.Add(pattern.EndTime.ToTimeSpan());
                
                var slotTimes = GenerateSlots(slotStart, slotEnd);
                slots.AddRange(slotTimes);
            }
            
            current = current.AddDays(1);
        }
        
        return slots;
    }

    /// <summary>
    /// Formats time span for display (e.g., "15 minutes", "1 hour 30 minutes")
    /// </summary>
    public static string FormatDuration(DateTime start, DateTime end)
    {
        var duration = end - start;
        
        if (duration.TotalMinutes < 60)
        {
            return $"{duration.Minutes} minutes";
        }
        
        var hours = (int)duration.TotalHours;
        var minutes = duration.Minutes;
        
        if (minutes == 0)
        {
            return $"{hours} hour{(hours > 1 ? "s" : "")}";
        }
        
        return $"{hours} hour{(hours > 1 ? "s" : "")} {minutes} minutes";
    }

    /// <summary>
    /// Gets next available appointment slot
    /// </summary>
    public static DateTime? GetNextAvailableSlot(
        List<DateTime> availableSlots,
        DateTime minDate = default)
    {
        if (minDate == default)
        {
            minDate = DateTime.Now;
        }
        
        return availableSlots
            .Where(s => s >= minDate)
            .OrderBy(s => s)
            .FirstOrDefault();
    }

    /// <summary>
    /// Validates appointment can be cancelled (not in past and within cancellation window)
    /// </summary>
    public static bool CanCancelAppointment(DateTime appointmentStart, int cancellationWindowHours = 24)
    {
        var cancellationDeadline = DateTime.Now.AddHours(cancellationWindowHours);
        return appointmentStart > cancellationDeadline;
    }

    /// <summary>
    /// Validates appointment can be rescheduled
    /// </summary>
    public static bool CanRescheduleAppointment(DateTime appointmentStart, int rescheduleWindowHours = 24)
    {
        var rescheduleDeadline = DateTime.Now.AddHours(rescheduleWindowHours);
        return appointmentStart > rescheduleDeadline;
    }
}
