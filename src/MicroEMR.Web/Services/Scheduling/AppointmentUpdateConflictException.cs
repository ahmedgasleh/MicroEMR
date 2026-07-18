namespace MicroEMR.Web.Services.Scheduling;

public sealed class AppointmentUpdateConflictException : Exception
{
    public AppointmentUpdateConflictException(bool isCancelled, bool isBlockedTime = false)
        : base(isBlockedTime
            ? "This resource is blocked during the selected time."
            : isCancelled
            ? "Cancelled appointments cannot be edited."
            : "The selected time conflicts with another appointment for this resource.")
    {
        IsCancelled = isCancelled;
        IsBlockedTime = isBlockedTime;
    }

    public bool IsCancelled { get; }
    public bool IsBlockedTime { get; }
}
