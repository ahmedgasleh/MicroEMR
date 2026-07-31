using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Web.Models.Dashboard;

public sealed class UpdateDashboardAppointmentStatusViewModel
{
    public Guid AppointmentUid { get; set; }

    [Required]
    [RegularExpression("^(Confirmed|Arrived|CheckedIn|Roomed|Seen|Completed|NoShow)$")]
    public string Status { get; set; } = string.Empty;
    [Required]
    public string ExpectedStatus { get; set; } = string.Empty;
    [Required]
    public string RowVersion { get; set; } = string.Empty;
}
