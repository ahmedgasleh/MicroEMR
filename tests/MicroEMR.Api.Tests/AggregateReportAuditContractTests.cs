using System.Security.Cryptography;
using System.Text.Json;
using MicroEMR.Infrastructure.Provisioning;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class AggregateReportAuditContractTests
{
    private static readonly string Sql = File.ReadAllText(Path.Combine(Root(), "db", "tenant-clinical",
        "migrations", "0046-aggregate-report-audit-events.sql"));

    [Fact]
    public void PreservesEveryPatientScopedEventPair()
    {
        foreach (var pair in new[]
                 {
                     "@EventType = N'EncounterViewed' AND @ResourceType = N'Encounter'",
                     "@EventType = N'PatientDocumentViewed' AND @ResourceType = N'PatientDocument'",
                     "@EventType = N'PatientDocumentDownloaded' AND @ResourceType = N'PatientDocument'",
                     "@EventType = N'PatientFileDownloaded' AND @ResourceType = N'PatientFile'"
                 })
            Assert.Contains(pair, Sql);
    }

    [Fact]
    public void AllowsOnlyApprovedAggregateEventsAndGovernedReportIdentity()
    {
        Assert.Contains("@EventType IN (N'ReportExecuted', N'CsvExported')", Sql);
        Assert.Contains("@ResourceType = N'Report'", Sql);
        Assert.Contains("@ReportKey <> N'AppointmentStatusDateReport'", Sql);
        Assert.Contains("THROW 52218, 'Unsupported aggregate report identity.'", Sql);
        Assert.Contains("THROW 52210, 'Unsupported structured read audit event/resource combination.'", Sql);
    }

    [Fact]
    public void AggregateReportsRequireNullPatientAndResourceUid()
    {
        Assert.Contains("IF @PatientUid IS NOT NULL", Sql);
        Assert.Contains("Aggregate report audit events cannot identify one patient", Sql);
        Assert.Contains("IF @ResourceUid IS NOT NULL", Sql);
        Assert.Contains("use a governed report key, not a resource UID", Sql);
        Assert.Contains("CASE WHEN @IsAggregateReport = 1 THEN @ReportKey", Sql);
    }

    [Fact]
    public void PatientScopedEventsStillRequireValidatedPatientAndResourceUid()
    {
        Assert.Contains("IF @ResourceUid IS NULL OR @ResourceUid =", Sql);
        Assert.Contains("IF @PatientUid IS NULL OR @PatientUid =", Sql);
        Assert.Contains("WHERE PatientUid = @PatientUid AND IsDeleted = 0", Sql);
        Assert.Contains("IF @PatientId IS NULL THROW 52214", Sql);
        Assert.Contains("Patient-scoped audit events cannot include a report identity", Sql);
    }

    [Fact]
    public void ProcedureIsOneInsertWithoutSchemaOrFilterPayloadChanges()
    {
        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.AuditLog_RecordStructuredRead", Sql);
        Assert.Equal(1, Sql.Split("INSERT dbo.AuditLog", StringSplitOptions.None).Length - 1);
        foreach (var forbidden in new[] { "UPDATE dbo.AuditLog", "DELETE dbo.AuditLog", "CREATE TABLE",
                     "ALTER TABLE", "FilterJson", "StartDate", "EndDate", "ReportRows", "CsvContent" })
            Assert.DoesNotContain(forbidden, Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ManifestSupportsUpgradeAndFreshProvisioningThroughUnique0046()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "db", "tenant-clinical", "manifest.json")));
        var ids = manifest.RootElement.EnumerateArray().Select(x => x.GetProperty("migrationId").GetString()).ToArray();
        Assert.Equal(48, ids.Length);
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("0045-structured-disclosure-audit-events", ids[^3]);
        Assert.Equal("0046-aggregate-report-audit-events", ids[^2]);
        Assert.Equal("0047-patient-immunization-history", ids[^1]);
        Assert.Single(ids, x => x == "0046-aggregate-report-audit-events");
        Assert.Single(SqlBatchParser.Parse(Sql));
    }

    [Fact]
    public void AppliedAuditMigrationsRemainByteForByteUnchanged()
    {
        AssertHash("0043-patient-chart-read-audit.sql", "4181A3487AA1C5837460AFC389F7C25443216F0C379EB6A781E3264A34461406");
        AssertHash("0044-structured-read-audit-procedure.sql", "11A26DCA8CE3A4CB57FF68DB30D7D555ED2235D0E94A3E3D7AA6777D62D60EED");
        AssertHash("0045-structured-disclosure-audit-events.sql", "925183F64F679B223E36EE2572EB1575E3683BC0400281557FA62590BFB85D07");
    }

    private static void AssertHash(string file, string expected)
    {
        var bytes = File.ReadAllBytes(Path.Combine(Root(), "db", "tenant-clinical", "migrations", file));
        Assert.Equal(expected, Convert.ToHexString(SHA256.HashData(bytes)));
    }

    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", ".."));
}
