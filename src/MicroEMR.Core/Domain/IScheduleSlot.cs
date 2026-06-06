namespace MicroEMR.Core.Domain;

public interface IScheduleSlot : IEntity, IAuditableEntity
{
    Guid ProviderId { get; }
    Guid? ClinicResourceId { get; }
    DateTime SlotStartTime { get; }
    DateTime SlotEndTime { get; }
    string Status { get; } // Available, Blocked, Booked
    string? BlockReason { get; }
}
