namespace MicroEMR.Application.Scheduling.DTOs;

public class ResourceBlockDto
{
    public Guid Id { get; set; }
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public Guid ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public DateTime BlockStartTime { get; set; }
    public DateTime BlockEndTime { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? BlockType { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateResourceBlockRequest
{
    public Guid ResourceId { get; set; }
    public Guid ProviderId { get; set; }
    public DateTime BlockStartTime { get; set; }
    public DateTime BlockEndTime { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? BlockType { get; set; }
}
