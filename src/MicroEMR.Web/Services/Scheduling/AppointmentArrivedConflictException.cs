namespace MicroEMR.Web.Services.Scheduling;

public sealed class AppointmentArrivedConflictException(bool isConcurrencyConflict)
    : Exception("The appointment could not be marked Arrived.")
{
    public bool IsConcurrencyConflict { get; } = isConcurrencyConflict;
}
