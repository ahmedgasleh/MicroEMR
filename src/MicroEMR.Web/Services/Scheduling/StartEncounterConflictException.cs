namespace MicroEMR.Web.Services.Scheduling;

public sealed class StartEncounterConflictException : Exception
{
    public StartEncounterConflictException()
        : base("Cancelled appointments cannot start encounters.")
    {
    }
}
