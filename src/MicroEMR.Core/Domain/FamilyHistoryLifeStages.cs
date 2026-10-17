namespace MicroEMR.Core.Domain;

public static class FamilyHistoryLifeStages
{
    public static IReadOnlyDictionary<string, string> Labels { get; } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            ["N"] = "New born", ["I"] = "Infant", ["C"] = "Child", ["T"] = "Adolescent", ["A"] = "Adult"
        });
    public static bool IsValid(string? code) => code is null || Labels.ContainsKey(code);
    public static string Display(string? code) => code is not null && Labels.TryGetValue(code, out var label) ? label : "Not recorded";
}
