namespace MicroEMR.Core.Domain;

public interface IAppointmentHistory : IEntity
{
    Guid AppointmentId { get; }
    DateTime ChangedAt { get; }
    Guid ChangedBy { get; }
    string? ChangeType { get; } // Created, Rescheduled, Cancelled, Confirmed
    string? OldStartTime { get; }
    string? NewStartTime { get; }
    string? Reason { get; }
}
