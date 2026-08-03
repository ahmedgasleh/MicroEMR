using Microsoft.Extensions.Configuration;
using MicroEMR.Application.PatientReferrals;
using MicroEMR.Infrastructure.Provisioning;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PatientReferralDocumentLinkageTests
{
    private static async Task<string> SqlAsync()
    {
        var source = new FileTenantDatabaseMigrationSource(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenantProvisioning:SqlAssetsPath"] = Path.Combine(AppContext.BaseDirectory, "database")
            }).Build());
        return Assert.Single(await source.GetAvailableMigrationsAsync(),
            x => x.MigrationId == "0023-patient-referral-document-linkage").Script;
    }

    [Fact]
    public async Task LinkTableUsesExistingDocumentIdentityRestrictingCompositeRelationship()
    {
        var sql = await SqlAsync();
        Assert.Contains("CREATE TABLE dbo.PatientReferralDocument", sql);
        Assert.Contains("PRIMARY KEY (ReferralUid, DocumentUid)", sql);
        Assert.Contains("REFERENCES dbo.PatientReferral(ReferralUid)", sql);
        Assert.Contains("REFERENCES dbo.PatientDocument(PatientDocumentUid)", sql);
        Assert.DoesNotContain("ON DELETE CASCADE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProceduresEnforceDraftPatientDocumentStateConcurrencyDuplicatesAndAudit()
    {
        var sql = await SqlAsync();
        Assert.Contains("PatientReferralDocument_GetByReferralUid", sql);
        Assert.Contains("PatientReferralDocument_Link", sql);
        Assert.Contains("PatientReferralDocument_Unlink", sql);
        Assert.Contains("d.PatientUid=@PatientUid AND d.IsDeleted=0", sql);
        Assert.Contains("PatientDocumentUid=@DocumentUid AND PatientUid=@PatientUid AND IsDeleted=0", sql);
        Assert.Equal(2, Count(sql, "IF @Status<>N'Draft'"));
        Assert.Equal(2, Count(sql, "@Version<>@ExpectedRowVersion"));
        Assert.Contains("THROW 51605,'Document already linked.'", sql);
        Assert.Contains("DELETE FROM dbo.PatientReferralDocument", sql);
        Assert.DoesNotContain("DELETE FROM dbo.PatientDocument", sql);
        Assert.Equal(2, Count(sql, "INSERT dbo.AuditLog"));
    }

    [Fact]
    public void ListContractContainsMetadataButNoDocumentContent()
    {
        var names = typeof(ReferralDocumentLinkResponse).GetProperties().Select(x => x.Name).ToHashSet();
        Assert.Contains("DocumentUid", names);
        Assert.Contains("Title", names);
        Assert.Contains("DocumentStatus", names);
        Assert.Contains("LinkedAtUtc", names);
        Assert.DoesNotContain("Content", names);
        Assert.DoesNotContain("TenantUid", names);
    }

    private static int Count(string value, string part) =>
        value.Split(part, StringSplitOptions.None).Length - 1;
}
