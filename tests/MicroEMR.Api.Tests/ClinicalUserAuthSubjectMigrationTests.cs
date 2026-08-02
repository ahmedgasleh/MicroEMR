using Microsoft.Extensions.Configuration;
using MicroEMR.Infrastructure.Provisioning;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ClinicalUserAuthSubjectMigrationTests
{
    private static async Task<string> MigrationScriptAsync()
    {
        var source = new FileTenantDatabaseMigrationSource(
            new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["TenantProvisioning:SqlAssetsPath"] =
                        Path.Combine(AppContext.BaseDirectory, "database")
                }).Build());

        var migration = Assert.Single(
            await source.GetAvailableMigrationsAsync(),
            x => x.MigrationId == "0018-clinical-user-auth-subject");
        return migration.Script;
    }

    [Fact]
    public async Task AddsNullableSubjectAndFilteredUniqueIndex()
    {
        var sql = await MigrationScriptAsync();

        Assert.Contains("AuthSubjectId NVARCHAR(450)", sql, StringComparison.Ordinal);
        Assert.Contains("AuthSubjectId IS NOT NULL", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE UNIQUE INDEX UX_ApplicationUser_AuthSubjectId", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("AuthSubjectId NVARCHAR(450) NOT NULL", sql, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LookupIsExactActiveAndHasNoWeakFallback()
    {
        var sql = await MigrationScriptAsync();

        Assert.Contains("ApplicationUser_GetByAuthSubjectId", sql, StringComparison.Ordinal);
        Assert.Contains("Latin1_General_100_BIN2", sql, StringComparison.Ordinal);
        Assert.Contains("IsActive = 1", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("WHERE Username", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WHERE Email", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WHERE UserUid", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MappingProcedureRejectsMissingUsersDuplicatesAndRemaps()
    {
        var sql = await MigrationScriptAsync();

        Assert.Contains("THROW 51091", sql, StringComparison.Ordinal);
        Assert.Contains("THROW 51092", sql, StringComparison.Ordinal);
        Assert.Contains("THROW 51093", sql, StringComparison.Ordinal);
        Assert.Contains("WITH (UPDLOCK, HOLDLOCK)", sql, StringComparison.Ordinal);
    }
}
