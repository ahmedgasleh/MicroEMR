using Microsoft.Extensions.Configuration;
using MicroEMR.Infrastructure.Provisioning;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PatientDocumentDraftEditingMigrationTests
{
    [Fact]
    public async Task UpdateDraftEnforcesStatusAndBothConcurrencyTokens()
    {
        var sql = await LoadMigrationAsync();

        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.PatientDocument_UpdateDraft", sql);
        Assert.Contains("IF @DocumentStatus <> N'Draft'", sql);
        Assert.Contains("THROW 51081", sql);
        Assert.Contains("@DocumentRowVersion <> @ExpectedDocumentRowVersion", sql);
        Assert.Contains("@ContentRowVersion <> @ExpectedContentRowVersion", sql);
        Assert.Contains("THROW 51082", sql);
        Assert.Contains("BEGIN TRANSACTION", sql);
        Assert.Contains("COMMIT TRANSACTION", sql);
    }

    [Fact]
    public async Task UpdateDraftChangesOnlyAllowedDocumentFieldsAndAuditsWithoutContent()
    {
        var sql = await LoadMigrationAsync();

        Assert.Contains("SET DocumentTitle =", sql);
        Assert.Contains("DocumentType =", sql);
        Assert.Contains("SET DocumentContent = @DocumentContent", sql);
        Assert.Contains("N'UpdateDraft', N'PatientDocument'", sql);
        Assert.Contains("N'Draft document updated'", sql);
        Assert.DoesNotContain("SET TemplateUid", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SET TemplateVersionUid", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE dbo.DocumentTemplate", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE dbo.DocumentTemplateVersion", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OldValue", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DetailsReturnsDocumentAndContentRowVersions()
    {
        var sql = await LoadMigrationAsync();

        Assert.Contains("pd.RowVersion", sql);
        Assert.Contains("content.RowVersion AS ContentRowVersion", sql);
    }

    private static async Task<string> LoadMigrationAsync()
    {
        var source = new FileTenantDatabaseMigrationSource(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenantProvisioning:SqlAssetsPath"] = Path.Combine(AppContext.BaseDirectory, "database")
            }).Build());

        return Assert.Single(await source.GetAvailableMigrationsAsync(),
            item => item.MigrationId == "0032-patient-document-draft-editing").Script;
    }
}
