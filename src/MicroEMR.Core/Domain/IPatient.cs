namespace MicroEMR.Core.Domain;

public interface IPatient : IEntity, ISoftDelete, IAuditableEntity
{
    string MedicalRecordNumber { get; }
    string FirstName { get; }
    string LastName { get; }
    DateTime DateOfBirth { get; }
    string Gender { get; }
    string? Email { get; }
    string? Phone { get; }
    string? Address { get; }
    bool IsActive { get; }
}
