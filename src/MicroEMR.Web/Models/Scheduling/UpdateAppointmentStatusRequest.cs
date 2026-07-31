namespace MicroEMR.Web.Models.Scheduling;

public sealed class UpdateAppointmentStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string ExpectedStatus { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
