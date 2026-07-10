using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Web.Models.Scheduling;

public sealed class CreateScheduleAppointmentViewModel : IValidatableObject
{
    public Guid PatientUid { get; set; }
    public Guid PrimaryResourceUid { get; set; }
    public Guid? RoomResourceUid { get; set; }

    [Required]
    public DateTime StartDateTimeLocal { get; set; }

    [Required]
    public DateTime EndDateTimeLocal { get; set; }

    [StringLength(100)]
    public string? AppointmentType { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PatientUid == Guid.Empty)
            yield return new ValidationResult("Patient is required.", [nameof(PatientUid)]);
        if (PrimaryResourceUid == Guid.Empty)
            yield return new ValidationResult("Primary resource is required.", [nameof(PrimaryResourceUid)]);
        if (EndDateTimeLocal <= StartDateTimeLocal)
            yield return new ValidationResult("End time must be after start time.", [nameof(EndDateTimeLocal)]);
    }
}
