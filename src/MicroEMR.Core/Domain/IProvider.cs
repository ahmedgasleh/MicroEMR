namespace MicroEMR.Core.Domain;

public interface IProvider : IEntity, ISoftDelete, IAuditableEntity
{
    string NationalProviderIdentifier { get; }
    string FirstName { get; }
    string LastName { get; }
    string Specialty { get; }
    string? Email { get; }
    string? Phone { get; }
    bool IsActive { get; }
}
