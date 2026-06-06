namespace MicroEMR.Core.Domain;

public interface IEntity
{
    Guid Id { get; }
}

public interface ISoftDelete
{
    bool IsDeleted { get; }
}

public interface IAuditableEntity
{
    DateTime CreatedAt { get; }
    DateTime? UpdatedAt { get; }
}
