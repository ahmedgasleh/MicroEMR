namespace MicroEMR.Application.PatientCareTeam;

public sealed record PatientCareTeamHeaderMember(string Label, string ProviderDisplayName);

public static class PatientCareTeamHeaderSummary
{
    public static IReadOnlyList<PatientCareTeamHeaderMember> Select(
        IReadOnlyList<PatientCareTeamRelationship> relationships)
    {
        ArgumentNullException.ThrowIfNull(relationships);

        var result = new List<PatientCareTeamHeaderMember>(3);
        AddRole("REFERRING", "Referring");
        AddRole("ATTENDING", "Attending");
        AddRole("MRP", "MRP");
        return result;

        void AddRole(string code, string label)
        {
            var active = relationships.Where(item => item.IsActive && item.RelationshipTypeCode == code).ToArray();
            // ListAsync preserves the stored procedure's deterministic order for the fallback.
            var selected = active.FirstOrDefault(item => item.IsPrimary) ?? active.FirstOrDefault();
            if (selected is not null)
                result.Add(new PatientCareTeamHeaderMember(label, selected.ProviderDisplayName));
        }
    }
}
