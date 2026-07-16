namespace MicroEMR.Web.Services.Scheduling;

public sealed class StartEncounterConflictException : Exception
{
    public StartEncounterConflictException(bool isCompleted)
        : base(isCompleted
            ? "Completed appointments cannot start a new encounter."
            : "Cancelled appointments cannot start encounters.")
    {
        IsCompleted = isCompleted;
    }

    public bool IsCompleted { get; }
}
