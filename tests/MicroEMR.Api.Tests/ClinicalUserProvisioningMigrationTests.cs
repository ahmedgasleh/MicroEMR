using Microsoft.Extensions.Configuration;
using MicroEMR.Infrastructure.Provisioning;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ClinicalUserProvisioningMigrationTests
{
    private static async Task<string> ScriptAsync()
    {
        var source = new FileTenantDatabaseMigrationSource(
            new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["TenantProvisioning:SqlAssetsPath"] = Path.Combine(AppContext.BaseDirectory, "database")
                }).Build());
        return Assert.Single(
            await source.GetAvailableMigrationsAsync(),
            item => item.MigrationId == "0019-clinical-user-provisioning").Script;
    }

    [Fact]
    public async Task UsesExactSubjectAndDatabaseGeneratedIdentifiers()
    {
        var sql = await ScriptAsync();
        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.ApplicationUser_Provision", sql);
        Assert.Contains("AuthSubjectId = @AuthSubjectId COLLATE Latin1_General_100_BIN2", sql);
        Assert.Contains("INSERT dbo.ApplicationUser", sql);
        Assert.DoesNotContain("UserId, UserUid", sql);
        Assert.DoesNotContain("NEWID()", sql);
    }

    [Fact]
    public async Task IsIdempotentAndDoesNotInferExistingUsers()
    {
        var sql = await ScriptAsync();
        Assert.Contains("IF @UserId IS NOT NULL", sql);
        Assert.Contains("EXEC dbo.ApplicationUser_GetByAuthSubjectId", sql);
        Assert.Contains("AuthSubjectId IS NULL", sql);
        Assert.Contains("THROW 51096", sql);
        Assert.Contains("THROW 51097", sql);
        Assert.DoesNotContain("UPDATE dbo.ApplicationUser", sql);
    }

    [Fact]
    public async Task CreatesActiveUserAndValidatesRequiredValues()
    {
        var sql = await ScriptAsync();
        Assert.Contains("(@Username, @DisplayName, @Email, 1, @AuthSubjectId)", sql);
        Assert.Contains("THROW 51090", sql);
        Assert.Contains("THROW 51094", sql);
        Assert.Contains("THROW 51095", sql);
        Assert.Contains("WITH (UPDLOCK, HOLDLOCK)", sql);
    }
}
