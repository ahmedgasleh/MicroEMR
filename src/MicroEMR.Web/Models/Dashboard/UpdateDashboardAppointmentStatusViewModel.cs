using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Web.Models.Dashboard;

public sealed class UpdateDashboardAppointmentStatusViewModel
{
    public Guid AppointmentUid { get; set; }

    [Required]
    [RegularExpression("^(Scheduled|Arrived|Roomed|Seen|Completed)$")]
    public string Status { get; set; } = string.Empty;
}
