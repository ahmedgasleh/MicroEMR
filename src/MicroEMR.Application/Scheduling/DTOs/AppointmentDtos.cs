namespace MicroEMR.Application.Scheduling.DTOs;

public class AppointmentDto
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public Guid ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public Guid? ClinicResourceId { get; set; }
    public string? ResourceName { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string Status { get; set; } = string.Empty; // Scheduled, Confirmed, Completed, Cancelled
    public string? AppointmentType { get; set; }
    public string? Notes { get; set; }
    public bool IsConfirmed { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateAppointmentRequest
{
    public Guid PatientId { get; set; }
    public Guid ProviderId { get; set; }
    public Guid? ClinicResourceId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string? AppointmentType { get; set; }
    public string? Notes { get; set; }
}

public class RescheduleAppointmentRequest
{
    public Guid AppointmentId { get; set; }
    public DateTime NewStartAt { get; set; }
    public DateTime NewEndAt { get; set; }
    public string? Reason { get; set; }
}

public class CancelAppointmentRequest
{
    public Guid AppointmentId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
