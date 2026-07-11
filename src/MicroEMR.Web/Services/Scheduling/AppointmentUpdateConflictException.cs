namespace MicroEMR.Web.Services.Scheduling;

public sealed class AppointmentUpdateConflictException : Exception
{
    public AppointmentUpdateConflictException(bool isCancelled)
        : base(isCancelled
            ? "Cancelled appointments cannot be edited."
            : "The selected time conflicts with another appointment for this resource.")
    {
        IsCancelled = isCancelled;
    }

    public bool IsCancelled { get; }
}
