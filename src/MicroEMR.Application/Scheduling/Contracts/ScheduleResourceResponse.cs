namespace MicroEMR.Application.Scheduling.Contracts;

public sealed class ScheduleResourceResponse
{
    public Guid ResourceUid { get; set; }

    public string ResourceType { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? ColorCode { get; set; }

    public bool IsActive { get; set; }

    public int SortOrder { get; set; }
}
