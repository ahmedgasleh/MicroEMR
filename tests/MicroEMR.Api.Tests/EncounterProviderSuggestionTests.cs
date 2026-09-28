using MicroEMR.Application.PatientCareTeam;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class EncounterProviderSuggestionTests
{
    [Fact]
    public void PrimaryAttendingWinsOverPrimaryMrp()
    {
        var result = EncounterProviderSuggestion.Select([
            Relationship("MRP", "Dr. MRP"),
            Relationship("ATTENDING", "Dr. Attending")]);

        Assert.Equal("Dr. Attending", result);
    }

    [Fact]
    public void PrimaryMrpIsUsedWhenNoActivePrimaryAttendingExists()
    {
        var result = EncounterProviderSuggestion.Select([
            Relationship("ATTENDING", "Dr. Former", active: false),
            Relationship("ATTENDING", "Dr. Other", primary: false),
            Relationship("MRP", "Dr. MRP")]);

        Assert.Equal("Dr. MRP", result);
    }

    [Fact]
    public void PrimaryAttendingIsUsedWithoutMrp()
    {
        Assert.Equal("Dr. Attending", EncounterProviderSuggestion.Select([
            Relationship("ATTENDING", "Dr. Attending")]));
    }

    [Fact]
    public void OtherRolesAndNonPrimaryOrInactiveRelationshipsAreNotSuggested()
    {
        Assert.Null(EncounterProviderSuggestion.Select([
            Relationship("ATTENDING", "Dr. Nonprimary", primary: false),
            Relationship("MRP", "Dr. Inactive", active: false),
            Relationship("REFERRING", "Dr. Referring"),
            Relationship("PCP", "Dr. PCP"),
            Relationship("CONSULTING", "Dr. Consulting")]));
        Assert.Null(EncounterProviderSuggestion.Select([]));
    }

    private static PatientCareTeamRelationship Relationship(string code, string name,
        bool primary = true, bool active = true) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), name, code, code, primary,
            new DateOnly(2026, 9, 1), active ? null : new DateOnly(2026, 9, 2), active,
            DateTime.UtcNow, 1, null, null, "AAAAAAAAAAA=");
}
