using Microsoft.Extensions.Configuration;
using MicroEMR.Infrastructure.Provisioning;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PatientDocumentPreserveInstanceContentMigrationTests
{
    [Fact]
    public async Task CreateProcedurePreservesSubmittedContentAndTemplateProvenance()
    {
        var sql = await LoadMigrationAsync();

        Assert.Contains("DECLARE @ResolvedContent NVARCHAR(MAX) = @DocumentContent", sql);
        Assert.Contains("@TemplateContent = version.TemplateContent", sql);
        Assert.Contains("IF @ResolvedContent IS NULL", sql);
        Assert.Contains("SET @ResolvedContent = @TemplateContent", sql);
        Assert.DoesNotContain("@ResolvedContent = version.TemplateContent", sql);
        Assert.Contains("@DocumentUid, @PatientId, @PatientUid, @TemplateUid, @TemplateVersionUid", sql);
        Assert.Contains("(@DocumentUid, @ResolvedContent, SYSUTCDATETIME(), @CreatedBy)", sql);
    }

    [Fact]
    public async Task CreateProcedureDoesNotMutatePublishedTemplateContent()
    {
        var sql = await LoadMigrationAsync();

        Assert.DoesNotContain("UPDATE dbo.DocumentTemplate", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE dbo.DocumentTemplateVersion", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT dbo.DocumentTemplate", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT dbo.DocumentTemplateVersion", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> LoadMigrationAsync()
    {
        var source = new FileTenantDatabaseMigrationSource(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenantProvisioning:SqlAssetsPath"] = Path.Combine(AppContext.BaseDirectory, "database")
            }).Build());

        return Assert.Single(await source.GetAvailableMigrationsAsync(),
            item => item.MigrationId == "0031-patient-document-create-preserve-instance-content").Script;
    }
}
