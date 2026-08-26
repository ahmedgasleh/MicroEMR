using System.Security.Cryptography;
using System.Text.Json;
using MicroEMR.Infrastructure.Provisioning;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class StructuredDisclosureAuditProcedureTests
{
    private static readonly string Sql = File.ReadAllText(Path.Combine(Root(), "db", "tenant-clinical",
        "migrations", "0045-structured-disclosure-audit-events.sql"));

    [Fact]
    public void PreservesViewsAndAllowsOnlyApprovedDownloads()
    {
        foreach (var pair in new[]
                 {
                     "@EventType = N'EncounterViewed' AND @ResourceType = N'Encounter'",
                     "@EventType = N'PatientDocumentViewed' AND @ResourceType = N'PatientDocument'",
                     "@EventType = N'PatientDocumentDownloaded' AND @ResourceType = N'PatientDocument'",
                     "@EventType = N'PatientFileDownloaded' AND @ResourceType = N'PatientFile'"
                 })
            Assert.Contains(pair, Sql);

        Assert.Contains("THROW 52210, 'Unsupported structured read audit event/resource combination.'", Sql);
    }

    [Fact]
    public void PrintAndUnknownCombinationsRemainRejected()
    {
        Assert.DoesNotContain("EncounterPrinted", Sql);
        Assert.DoesNotContain("PatientDocumentPrinted", Sql);
        Assert.DoesNotContain("PatientFileDownloaded' AND @ResourceType = N'Encounter", Sql);
        Assert.DoesNotContain("Unknown", Sql);
    }

    [Fact]
    public void RemainsInsertOnlyContentFreeAndContractCompatible()
    {
        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.AuditLog_RecordStructuredRead", Sql);
        Assert.Equal(1, Sql.Split("INSERT dbo.AuditLog", StringSplitOptions.None).Length - 1);
        foreach (var forbidden in new[] { "UPDATE dbo.AuditLog", "DELETE dbo.AuditLog", "CREATE TABLE",
                     "ALTER TABLE", "DocumentContent", "FileBytes", "FileName", "EncounterNote", "DetailsJson" })
            Assert.DoesNotContain(forbidden, Sql, StringComparison.OrdinalIgnoreCase);

        var signature = Sql[..Sql.IndexOf("AS\nBEGIN", StringComparison.Ordinal)];
        foreach (var parameter in new[] { "@EventType", "@ResourceType", "@ResourceUid", "@PatientUid",
                     "@ClinicalUserId", "@RequestCorrelationId", "@SourceApplication" })
            Assert.Contains(parameter, signature);
    }

    [Fact]
    public void ManifestSupportsUpgradeAndFreshProvisioningThroughUnique0045()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "db", "tenant-clinical", "manifest.json")));
        var ids = manifest.RootElement.EnumerateArray().Select(x => x.GetProperty("migrationId").GetString()).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("0044-structured-read-audit-procedure", ids[^11]);
        Assert.Equal("0045-structured-disclosure-audit-events", ids[^10]);
        Assert.Equal("0046-aggregate-report-audit-events", ids[^9]);
        Assert.Equal("0047-patient-immunization-history", ids[^8]);
        Assert.Equal("0048-clinical-data-migration-validation-foundation", ids[^7]);
        Assert.Equal("0049-clinical-data-migration-import-foundation", ids[^6]);
        Assert.Equal("0050-patient-prescription-foundation", ids[^5]);
        Assert.Equal("0051-result-review-acknowledgement-hardening", ids[^4]);
        Assert.Equal("0052-cds-foundation", ids[^3]);
        Assert.Equal("0053-cdm-enrollment-foundation", ids[^2]);
        Assert.Equal("0054-results-provenance-correction-foundation", ids[^1]);
        Assert.Single(ids, x => x == "0045-structured-disclosure-audit-events");
        Assert.Single(SqlBatchParser.Parse(Sql));
    }

    [Fact]
    public void AppliedAuditMigrationsRemainByteForByteUnchanged()
    {
        AssertHash("0043-patient-chart-read-audit.sql",
            "4181A3487AA1C5837460AFC389F7C25443216F0C379EB6A781E3264A34461406");
        AssertHash("0044-structured-read-audit-procedure.sql",
            "11A26DCA8CE3A4CB57FF68DB30D7D555ED2235D0E94A3E3D7AA6777D62D60EED");
    }

    private static void AssertHash(string file, string expected)
    {
        var bytes = File.ReadAllBytes(Path.Combine(Root(), "db", "tenant-clinical", "migrations", file));
        Assert.Equal(expected, Convert.ToHexString(SHA256.HashData(bytes)));
    }

    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", ".."));
}
