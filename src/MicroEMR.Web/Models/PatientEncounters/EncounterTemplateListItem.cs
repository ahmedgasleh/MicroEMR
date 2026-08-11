namespace MicroEMR.Web.Models.PatientEncounters;

public sealed class EncounterTemplateListItem
{
    public Guid TemplateUid { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string TemplateScope { get; set; } = string.Empty;
}
