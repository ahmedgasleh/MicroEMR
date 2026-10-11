using MicroEMR.Application.Templates.Output;

namespace MicroEMR.Application.PatientEncounters;

public static class EncounterContributionOutput
{
    public const string Unattributed = "Earlier contribution identity unavailable";

    public static TemplateOutputDocument Include(TemplateOutputDocument document,
        IReadOnlyList<EncounterContributionResponse> contributions)
    {
        var sections = document.Sections.Select(section => section with
        {
            Items = section.Items.Select(item => item.IsStaticContent ? item : item with
            { Label = $"{item.Label} ({(contributions.SingleOrDefault(c => c.ValueKind == "Value" && c.FieldKey == item.Key) is { } value ? Identity(value) : Unattributed)})" }).ToArray()
        }).ToList();
        foreach (var value in contributions.Where(c => c.ValueKind == "Value"))
        {
            var index = sections.FindIndex(section => section.Key == value.SectionKey);
            if (index >= 0 && !sections[index].Items.Any(item => item.Key == value.FieldKey))
                sections[index] = sections[index] with { Items = sections[index].Items.Append(
                    new TemplateOutputItem(value.FieldKey, $"{value.FieldLabel}: {Identity(value)}", "Value cleared", "Text", false, 0)).ToArray() };
        }
        foreach (var group in contributions.Where(c => c.ValueKind == "Narrative").GroupBy(c => c.SectionKey))
        {
            var items = group.SelectMany(c => new[]
            {
                new TemplateOutputItem(null, $"{c.FieldLabel ?? c.SectionTitle}: {Identity(c)}", c.Content, "TextArea", false, 0)
            }).ToArray();
            var index = sections.FindIndex(s => s.Key == group.Key);
            if (index >= 0) sections[index] = sections[index] with { Items = sections[index].Items.Concat(items).ToArray() };
            else sections.Add(new(group.Key, group.First().SectionTitle, sections.Count, items));
        }
        return new(sections);
    }

    public static string Identity(EncounterContributionResponse contribution)
    {
        var original = $"{(contribution.ValueKind == "Value" ? "Original contributor of tracked value" : "Original contributor")}: {contribution.OriginalContributorDisplayName} (clinical user {contribution.OriginalContributorId}), {contribution.CreatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC";
        var revisions = contribution.Revisions.Where(r => r.RevisionNumber > 1).Select(r =>
            $"Revision {r.RevisionNumber}: {r.EditorDisplayName} (clinical user {r.EditorId}), {r.RevisedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        return string.Join("; ", new[] { original }.Concat(revisions));
    }
}
