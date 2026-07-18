namespace MicroEMR.Application.Scheduling;

public sealed class SchedulingBlockedTimeConflictException : Exception
{
    public SchedulingBlockedTimeConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
