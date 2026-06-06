namespace MicroEMR.Core.Domain;

public interface IUserAccess : IEntity, IAuditableEntity
{
    Guid UserId { get; }
    string Role { get; }
    string ResourceType { get; }
    Guid? ResourceId { get; }
    string Permission { get; }
    DateTime GrantedAt { get; }
    DateTime? ExpiresAt { get; }
    bool IsActive { get; }
}
