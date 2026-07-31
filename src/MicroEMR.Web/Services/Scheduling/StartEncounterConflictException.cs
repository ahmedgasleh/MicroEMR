namespace MicroEMR.Web.Services.Scheduling;

public enum StartEncounterConflictReason { Cancelled, Completed, NoShow }

public sealed class StartEncounterConflictException : Exception
{
    public StartEncounterConflictException(StartEncounterConflictReason reason)
        : base(reason switch
        {
            StartEncounterConflictReason.Completed => "Completed appointments cannot start a new encounter.",
            StartEncounterConflictReason.NoShow => "No-show appointments cannot start encounters.",
            _ => "Cancelled appointments cannot start encounters."
        })
    {
        Reason = reason;
    }

    public StartEncounterConflictReason Reason { get; }
    public bool IsCompleted => Reason == StartEncounterConflictReason.Completed;
    public bool IsNoShow => Reason == StartEncounterConflictReason.NoShow;
}
