namespace MicroEMR.Core.Domain;

public interface IAppointment : IEntity, ISoftDelete, IAuditableEntity
{
    Guid PatientId { get; }
    Guid ProviderId { get; }
    Guid? ClinicResourceId { get; }
    DateTime StartAt { get; }
    DateTime EndAt { get; }
    string Status { get; }
    string? AppointmentType { get; }
    string? Notes { get; }
    bool IsConfirmed { get; }
}
