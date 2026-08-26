using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using MicroEMR.Application.ReadAudit;
using MicroEMR.Infrastructure.ReadAudit;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class StructuredReadAuditProcedureTests
{
    private static readonly string Sql = File.ReadAllText(Path.Combine(Root(), "db", "tenant-clinical",
        "migrations", "0044-structured-read-audit-procedure.sql"));

    [Fact]
    public void ProcedureAllowsOnlyApprovedEventResourcePairs()
    {
        Assert.Contains("@EventType = N'EncounterViewed' AND @ResourceType = N'Encounter'", Sql);
        Assert.Contains("@EventType = N'PatientDocumentViewed' AND @ResourceType = N'PatientDocument'", Sql);
        Assert.Contains("THROW 52210, 'Unsupported structured read audit event/resource combination.'", Sql);
        Assert.DoesNotContain("PatientChartOpened", Sql);
    }

    [Fact]
    public void ProcedureStoresTrustedStructuredIdentityAndNoClinicalContent()
    {
        foreach (var value in new[] { "@ResourceUid", "@PatientUid", "@ClinicalUserId",
                     "@RequestCorrelationId", "@SourceApplication", "AuditEventUid", "ClinicalRead", "Succeeded" })
            Assert.Contains(value, Sql);

        foreach (var forbidden in new[] { "PatientName", "HealthCard", "EncounterNote", "DocumentContent",
                     "Subjective", "Objective", "Assessment", "Diagnosis", "Medication", "DetailsJson" })
            Assert.DoesNotContain(forbidden, Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcedureIsInsertOnlyAndMakesNoSchemaChange()
    {
        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.AuditLog_RecordStructuredRead", Sql);
        Assert.Equal(1, Sql.Split("INSERT dbo.AuditLog", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("UPDATE dbo.AuditLog", Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE dbo.AuditLog", Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE TABLE", Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER TABLE", Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcedureAcceptsNoTenantOrClinicalContentParameters()
    {
        var signature = Sql[..Sql.IndexOf("AS\nBEGIN", StringComparison.Ordinal)];
        Assert.DoesNotContain("TenantUid", signature, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Database", signature, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Connection", signature, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Json", signature, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Content", signature, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RepositoryExposesOneReusableUnwiredContract()
    {
        var method = typeof(IReadAuditRepository).GetMethod(nameof(IReadAuditRepository.RecordStructuredReadAsync));
        Assert.NotNull(method);
        Assert.NotNull(typeof(ReadAuditRepository).GetMethod(method!.Name));
        Assert.Equal([typeof(string), typeof(string), typeof(Guid), typeof(Guid), typeof(long), typeof(string),
            typeof(string), typeof(CancellationToken)], method.GetParameters().Select(x => x.ParameterType));
        Assert.Equal(ReadAuditActions.EncounterViewed, "EncounterViewed");
        Assert.Equal(ReadAuditActions.PatientDocumentViewed, "PatientDocumentViewed");
        Assert.Equal(ReadAuditResourceTypes.Encounter, "Encounter");
        Assert.Equal(ReadAuditResourceTypes.PatientDocument, "PatientDocument");
    }

    [Fact]
    public void ManifestAppendsUnique0044AndSupportsFreshProvisioning()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "db", "tenant-clinical", "manifest.json")));
        var ids = manifest.RootElement.EnumerateArray().Select(x => x.GetProperty("migrationId").GetString()).ToArray();
        Assert.Equal(55, ids.Length);
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("0043-patient-chart-read-audit", ids[^12]);
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
        Assert.NotEmpty(MicroEMR.Infrastructure.Provisioning.SqlBatchParser.Parse(Sql));
    }

    [Fact]
    public void Migration0043RemainsByteForByteUnchanged()
    {
        var bytes = File.ReadAllBytes(Path.Combine(Root(), "db", "tenant-clinical", "migrations",
            "0043-patient-chart-read-audit.sql"));
        Assert.Equal("4181A3487AA1C5837460AFC389F7C25443216F0C379EB6A781E3264A34461406",
            Convert.ToHexString(SHA256.HashData(bytes)));
        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.AuditLog_RecordPatientChartOpened",
            System.Text.Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void ExistingMutationAuditRemainsUntouched()
    {
        Assert.Contains("INSERT dbo.AuditLog", File.ReadAllText(Path.Combine(Root(), "db", "tenant-clinical",
            "migrations", "0039-patient-demographic-audit.sql")));
    }

    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", ".."));
}
