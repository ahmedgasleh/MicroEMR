namespace MicroEMR.Core.Domain;

public interface IClinicResource : IEntity, ISoftDelete, IAuditableEntity
{
    string Name { get; }
    string ResourceType { get; }
    string? Location { get; }
    string? Description { get; }
    bool IsActive { get; }
}
