namespace MicroEMR.Application.PatientCareTeam;

public static class EncounterProviderSuggestion
{
    public static string? Select(IReadOnlyList<PatientCareTeamRelationship> relationships)
    {
        ArgumentNullException.ThrowIfNull(relationships);

        return relationships.FirstOrDefault(item => item.IsActive && item.IsPrimary &&
            item.RelationshipTypeCode == "ATTENDING")?.ProviderDisplayName
            ?? relationships.FirstOrDefault(item => item.IsActive && item.IsPrimary &&
                item.RelationshipTypeCode == "MRP")?.ProviderDisplayName;
    }
}
