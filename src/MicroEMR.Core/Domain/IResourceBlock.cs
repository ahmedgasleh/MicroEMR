namespace MicroEMR.Core.Domain;

public interface IResourceBlock : IEntity, ISoftDelete, IAuditableEntity
{
    Guid ResourceId { get; }
    Guid ProviderId { get; }
    DateTime BlockStartTime { get; }
    DateTime BlockEndTime { get; }
    string Reason { get; }
    string? BlockType { get; } // Break, Lunch, Training, Maintenance
}
