using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using MicroEMR.Application.Templates.Runtime;
using MicroEMR.Application.Templates.Serialization;
using MicroEMR.Application.Templates.Validation;
using MicroEMR.Infrastructure.Provisioning;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ConsultationReportTemplateTests
{
    [Fact]
    public async Task PublishedConsultationTemplateCanCreateAnEmptyStructuredDraft()
    {
        var source = new FileTenantDatabaseMigrationSource(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenantProvisioning:SqlAssetsPath"] = Path.Combine(AppContext.BaseDirectory, "database")
            }).Build());
        var migration = Assert.Single(await source.GetAvailableMigrationsAsync(),
            item => item.MigrationId == "0063-consultation-report-template");
        var sql = migration.Script;
        Assert.Contains("N'CONSULTATION_REPORT'", sql);
        Assert.Contains("N'Consultation Report'", sql);
        Assert.Contains("N'Document', N'System'", sql);
        Assert.Contains("N'Published', 1", sql);
        Assert.DoesNotContain("PatientDocumentRecipient", sql);

        var match = Regex.Match(sql, @"DECLARE @DefinitionJson NVARCHAR\(MAX\) = N'(?<json>.*?)';",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert.True(match.Success, "Consultation definition was not found in the migration.");
        var serializer = new TemplateDefinitionSerializer(new TemplateDefinitionValidator());
        var definition = serializer.Process(match.Groups["json"].Value);
        Assert.True(definition.IsValid, string.Join("; ", definition.Errors.Select(x => x.Message)));
        Assert.Equal(new[]
        {
            "Reason for Consultation", "Relevant History", "Examination / Findings",
            "Investigations", "Impression / Assessment", "Recommendations / Plan",
            "Medication Changes", "Follow-up"
        }, definition.Definition!.Sections!.Select(x => x.Title));

        var draft = new TemplateInstanceRuntime(serializer).CreateInitial(definition.Definition);
        Assert.True(draft.IsValid, string.Join("; ", draft.Errors.Select(x => x.Message)));
        Assert.NotNull(draft.Json);
    }
}
