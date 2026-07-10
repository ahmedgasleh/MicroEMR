namespace MicroEMR.Application.Scheduling;

public sealed class SchedulingConflictException : Exception
{
    public SchedulingConflictException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
