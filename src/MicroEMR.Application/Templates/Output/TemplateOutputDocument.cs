namespace MicroEMR.Application.Templates.Output;

public sealed record TemplateOutputDocument(IReadOnlyList<TemplateOutputSection> Sections);

public sealed record TemplateOutputSection(
    string? Key,
    string Title,
    int Order,
    IReadOnlyList<TemplateOutputItem> Items);

public sealed record TemplateOutputItem(
    string? Key,
    string? Label,
    string DisplayValue,
    string FieldType,
    bool IsStaticContent,
    int Order);
