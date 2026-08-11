using MicroEMR.Application.Templates.Variables;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class TemplateVariableResolverTests
{
    private readonly TemplateVariableResolver _resolver = new();
    private readonly TemplateVariableContext _context = new(
        "Ada Lovelace", new DateOnly(1815, 12, 10), "Dr. Test",
        new DateTime(2026, 8, 11, 14, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 8, 11));

    [Fact]
    public void Resolve_ReplacesControlledClinicalContextVariables()
    {
        var result = _resolver.Resolve(
            "{{Patient.FullName}}|{{Patient.DateOfBirth}}|{{Provider.DisplayName}}|{{Encounter.Date}}|{{CurrentDate}}", _context);

        Assert.Equal("Ada Lovelace|1815-12-10|Dr. Test|2026-08-11|2026-08-11", result);
    }

    [Theory]
    [InlineData("{{Patient[123].FullName}}")]
    [InlineData("{{Patient.FullName.ToString()}}")]
    [InlineData("{{System.Environment}}")]
    public void Resolve_RejectsUnknownOrExecutablePaths(string input) =>
        Assert.Throws<TemplateVariableResolutionException>(() => _resolver.Resolve(input, _context));

    [Fact]
    public void Registry_ContainsOnlyTheInitialControlledSet() =>
        Assert.Equal(new[] { "Patient.FullName", "Patient.DateOfBirth", "Provider.DisplayName", "Encounter.Date", "CurrentDate" },
            _resolver.Registry.Select(x => x.Key));
}
