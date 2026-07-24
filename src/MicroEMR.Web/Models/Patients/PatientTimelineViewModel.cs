namespace MicroEMR.Web.Models.Patients;

public sealed class PatientTimelineItemViewModel
{
    public DateTime EventDateTime { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Status { get; set; }
    public string? SourceUid { get; set; }
    public string? SourceType { get; set; }
    public string? DisplayDate { get; set; }
    public string? IconCssClass { get; set; }
    public string? BadgeCssClass { get; set; }
}

public sealed class PatientTimelineViewModel
{
    public Guid PatientUid { get; set; }
    public IReadOnlyList<PatientTimelineItemViewModel> Items { get; set; } = [];
    public string ActiveFilter { get; set; } = "All";
    public bool IsLimited { get; set; }
    public bool LoadFailed { get; set; }
}
