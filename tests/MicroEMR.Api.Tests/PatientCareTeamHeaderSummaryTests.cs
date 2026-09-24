using MicroEMR.Application.PatientCareTeam;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PatientCareTeamHeaderSummaryTests
{
    [Fact]
    public void EmptyCareTeamProducesNoHeaderItems()
    {
        Assert.Empty(PatientCareTeamHeaderSummary.Select([]));
    }

    [Theory]
    [InlineData("REFERRING", "Referring")]
    [InlineData("ATTENDING", "Attending")]
    [InlineData("MRP", "MRP")]
    public void ASingleActiveRoleProducesItsHeaderItem(string code, string label)
    {
        var result = PatientCareTeamHeaderSummary.Select([Relationship(code, "Dr. One")]);

        var item = Assert.Single(result);
        Assert.Equal(label, item.Label);
        Assert.Equal("Dr. One", item.ProviderDisplayName);
    }

    [Fact]
    public void ThreeRolesAppearInHeaderOrder()
    {
        var result = PatientCareTeamHeaderSummary.Select([
            Relationship("MRP", "Dr. M"),
            Relationship("ATTENDING", "Dr. A"),
            Relationship("REFERRING", "Dr. R")]);

        Assert.Equal(["Referring", "Attending", "MRP"], result.Select(item => item.Label));
    }

    [Fact]
    public void PrimaryWinsAndFallbackPreservesServiceOrder()
    {
        var result = PatientCareTeamHeaderSummary.Select([
            Relationship("REFERRING", "Dr. First"),
            Relationship("REFERRING", "Dr. Second"),
            Relationship("ATTENDING", "Dr. Nonprimary"),
            Relationship("ATTENDING", "Dr. Primary", primary: true)]);

        Assert.Equal("Dr. First", result[0].ProviderDisplayName);
        Assert.Equal("Dr. Primary", result[1].ProviderDisplayName);
    }

    [Fact]
    public void InactiveAndOtherRolesAreOmitted()
    {
        var result = PatientCareTeamHeaderSummary.Select([
            Relationship("REFERRING", "Dr. Former", primary: true, active: false),
            Relationship("ATTENDING", "Dr. Active"),
            Relationship("MRP", "Dr. Ended", active: false),
            Relationship("PCP", "Dr. PCP")]);

        var item = Assert.Single(result);
        Assert.Equal("Attending", item.Label);
        Assert.Equal("Dr. Active", item.ProviderDisplayName);
    }

    private static PatientCareTeamRelationship Relationship(string code, string name, bool primary = false, bool active = true) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), name, code, code, primary,
            new DateOnly(2026, 9, 24), active ? null : new DateOnly(2026, 9, 25), active,
            DateTime.UtcNow, 1, null, null, "AAAAAAAAAAA=");
}
