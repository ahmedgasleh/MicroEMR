namespace MicroEMR.Web.Services.Scheduling;

public sealed class StartEncounterConflictException : Exception
{
    public StartEncounterConflictException(string code)
        : base(code switch
        {
            "appointment_completed" => "Completed appointments cannot start a new encounter.",
            "appointment_no_show" => "An encounter cannot be started for a no-show appointment.",
            "appointment_cancelled" => "Cancelled appointments cannot start encounters.",
            _ => "An encounter cannot be started from the appointment's current status."
        })
    {
        Code = code;
    }

    public string Code { get; }
}
