namespace MicroEMR.Core.Domain;

public interface IAuditLog : IEntity
{
    string EntityName { get; }
    Guid EntityId { get; }
    string Action { get; }
    Guid PerformedBy { get; }
    string? PerformedByUserName { get; }
    DateTime PerformedAt { get; }
    string? Changes { get; }
    string? Notes { get; }
}
