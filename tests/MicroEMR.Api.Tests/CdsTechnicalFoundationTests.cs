using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.Cds;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class CdsTechnicalFoundationTests
{
    private static readonly string Migration = File.ReadAllText(Path.Combine(Root(), "db", "tenant-clinical", "migrations", "0052-cds-foundation.sql"));

    [Fact]
    public void Migration0052IsUniqueCanonicalAnd0053DoesNotExist()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "db", "tenant-clinical", "manifest.json")));
        var entries = manifest.RootElement.EnumerateArray().ToArray();
        Assert.Equal("0052-cds-foundation", entries[^7].GetProperty("migrationId").GetString());
        Assert.Equal("tenant-clinical/migrations/0052-cds-foundation.sql", entries[^7].GetProperty("script").GetString());
        Assert.Single(entries, x => x.GetProperty("migrationId").GetString() == "0052-cds-foundation");
        Assert.False(File.Exists(Path.Combine(Root(), "db", "tenant-clinical", "migrations", "0053-cds-foundation.sql")));
    }

    [Fact]
    public void SchemaConstrainsSeverityStatusFingerprintAndCompoundIdentity()
    {
        Assert.Contains("CREATE TABLE dbo.CdsAlert", Migration);
        Assert.Contains("CREATE TABLE dbo.CdsAlertHistory", Migration);
        Assert.Contains("Severity IN (N'Info', N'Warning')", Migration);
        Assert.Contains("Status IN (N'Active', N'Acknowledged', N'Dismissed', N'Resolved')", Migration);
        Assert.Contains("UQ_CdsAlert_Finding UNIQUE (PatientUid, RuleKey, RuleVersion, FindingFingerprint)", Migration);
        Assert.Contains("RowVersion ROWVERSION", Migration);
        Assert.DoesNotContain("N'Critical'", Migration);
        Assert.DoesNotContain("N'Expired'", Migration);
    }

    [Fact]
    public void HistoryIsAppendOnlyAndHumanResponsesAreAtomicMinimalAudit()
    {
        Assert.Contains("INSTEAD OF UPDATE, DELETE", Migration);
        Assert.Contains("CdsAlertHistory_AppendOnly", Migration);
        Assert.Contains("DECLARE @ResolvedFindings TABLE", Migration);
        Assert.Contains("INTO @ResolvedFindings", Migration);
        Assert.DoesNotContain("INTO dbo.CdsAlertHistory", Migration);
        Assert.Contains("N'CdsAlertAcknowledged'", Migration);
        Assert.Contains("N'CdsAlertDismissed'", Migration);
        Assert.Contains("BEGIN TRANSACTION", Migration);
        Assert.Contains("Status=Acknowledged", Migration);
        Assert.Contains("Status=Dismissed;ReasonCode=", Migration);
        Assert.Equal(2, Migration.Split("INSERT dbo.AuditLog", StringSplitOptions.None).Length - 1);
        Assert.Contains("N'Status=Acknowledged',@Now", Migration);
        Assert.Contains("N'Status=Dismissed;ReasonCode='+@ReasonCode,@Now", Migration);
        Assert.DoesNotContain("NewValue,Explanation", Migration);
        Assert.DoesNotContain("NewValue,DismissComment", Migration);
    }

    [Fact]
    public void CompoundPatientLookupAndNoDirectResolveProtectIsolation()
    {
        Assert.Contains("a.PatientUid = @PatientUid AND a.CdsAlertUid = @CdsAlertUid", Migration);
        Assert.Contains("h.PatientUid = @PatientUid AND h.CdsAlertUid = @CdsAlertUid", Migration);
        Assert.DoesNotContain("CdsAlert_ResolveByUser", Migration);
        Assert.Null(typeof(PatientCdsController).GetMethod("Resolve"));
    }

    [Fact]
    public void ApiEnforcesReadAndResponsePermissionsAndAcceptsNoActor()
    {
        var type = typeof(PatientCdsController);
        Assert.Contains(type.GetCustomAttributes<RequirePermissionAttribute>(), x => x.Policy?.Contains(PermissionKeys.PatientsView) == true);
        foreach (var name in new[] { nameof(PatientCdsController.Acknowledge), nameof(PatientCdsController.Dismiss) })
            Assert.Contains(type.GetMethod(name)!.GetCustomAttributes<RequirePermissionAttribute>(),
                x => x.Policy?.Contains(PermissionKeys.ClinicalDataManage) == true);
        Assert.Null(typeof(AcknowledgeCdsAlertRequest).GetProperty("ActorUserId"));
        Assert.Null(typeof(DismissCdsAlertRequest).GetProperty("ActorUserId"));
    }

    [Fact]
    public void DefaultRegistryIsEmptyAndSyntheticRuleExistsOnlyInTests()
    {
        Assert.Empty(new CdsRuleRegistry([]).ActiveRules);
        var applicationDi = File.ReadAllText(Path.Combine(Root(), "src", "MicroEMR.Application", "DependencyInjection.cs"));
        var apiProgram = File.ReadAllText(Path.Combine(Root(), "src", "MicroEMR.Api", "Program.cs"));
        Assert.DoesNotContain("AddSingleton<ICdsRule,", applicationDi);
        Assert.DoesNotContain("AddScoped<ICdsRule,", applicationDi);
        Assert.DoesNotContain("TEST_ONLY", applicationDi);
        Assert.DoesNotContain("TEST_ONLY", apiProgram);
        Assert.Equal("TEST_ONLY_SYNTHETIC", new SyntheticRule(true).Metadata.RuleKey);
    }

    [Fact]
    public void RegistryRejectsDuplicatesAndInvalidMetadata()
    {
        Assert.Throws<InvalidOperationException>(() => new CdsRuleRegistry([new SyntheticRule(true), new SyntheticRule(false)]));
        Assert.Throws<InvalidOperationException>(() => new CdsRuleRegistry([new InvalidRule()]));
    }

    [Fact]
    public void FingerprintIsStableAndChangesForMaterialFactsVersionOrPatient()
    {
        var patient = Guid.NewGuid();
        var first = CdsFingerprint.Compute("TEST_ONLY", 1, patient, "condition=true");
        Assert.Equal(first, CdsFingerprint.Compute("TEST_ONLY", 1, patient, "condition=true"));
        Assert.NotEqual(first, CdsFingerprint.Compute("TEST_ONLY", 1, patient, "condition=false"));
        Assert.NotEqual(first, CdsFingerprint.Compute("TEST_ONLY", 2, patient, "condition=true"));
        Assert.NotEqual(first, CdsFingerprint.Compute("TEST_ONLY", 1, Guid.NewGuid(), "condition=true"));
        Assert.Matches("^[0-9a-f]{64}$", first);
    }

    [Fact]
    public async Task SyntheticTriggerCreatesOneFindingAndRepeatDoesNotDuplicate()
    {
        var repository = new FakeRepository();
        var service = Service(repository, [new SyntheticRule(true)], [new SyntheticFacts(true)]);
        var patient = Guid.NewGuid();
        var first = await service.EvaluatePatientAsync(patient, default);
        var second = await service.EvaluatePatientAsync(patient, default);
        Assert.Single(first.Alerts);
        Assert.Single(second.Alerts);
        Assert.Single(repository.Alerts);
        Assert.Equal(2, repository.RecordCalls);
        Assert.True(repository.Alerts[0].LastEvaluatedAtUtc >= repository.Alerts[0].FirstDetectedAtUtc);
    }

    [Fact]
    public async Task SyntheticNonTriggerCreatesNoFindingAndResolvesOnlySuccessfulEvaluation()
    {
        var repository = new FakeRepository();
        var service = Service(repository, [new SyntheticRule(false)], [new SyntheticFacts(false)]);
        var result = await service.EvaluatePatientAsync(Guid.NewGuid(), default);
        Assert.Empty(result.Alerts);
        Assert.Equal(1, repository.ResolveCalls);
        Assert.Equal(0, result.RulesFailed);
    }

    [Fact]
    public async Task RuleFailureIsIsolatedAndDoesNotResolveOrFabricate()
    {
        var repository = new FakeRepository();
        var service = Service(repository, [new ThrowingRule(), new SyntheticRule(true)], [new SyntheticFacts(true)]);
        var result = await service.EvaluatePatientAsync(Guid.NewGuid(), default);
        Assert.Equal(1, result.RulesFailed);
        Assert.Equal(1, result.RulesEvaluated);
        Assert.Single(result.Alerts);
        Assert.Equal(0, repository.ResolveCalls); // failed rule never resolves; triggered reconciliation is atomic in RecordFinding
    }

    [Fact]
    public void SqlLifecycleUsesLocksRowVersionReasonAndSystemResolution()
    {
        Assert.Contains("WITH (UPDLOCK, HOLDLOCK)", Migration);
        Assert.Contains("@ExpectedRowVersion BINARY(8)", Migration);
        Assert.Contains("THROW 51402", Migration);
        Assert.Contains("N'NotApplicable',N'AlreadyAddressed',N'DuplicateFinding',N'Other'", Migration);
        Assert.Contains("@ReasonCode=N'Other' AND @Comment IS NULL", Migration);
        Assert.Contains("CdsAlert_ResolveRuleFindings", Migration);
        Assert.Contains("Status IN (N'Active', N'Acknowledged')", Migration);
    }

    [Fact]
    public void PatientChartIsAsyncNonModalAndHonorsResponsePermission()
    {
        var view = File.ReadAllText(Path.Combine(Root(), "src", "MicroEMR.Web", "Views", "Patients", "Details.cshtml"));
        var ui = File.ReadAllText(Path.Combine(Root(), "src", "MicroEMR.Web", "ClientApp", "patients", "patient-cds.ts"));
        Assert.Contains("patientCdsRoot", view);
        Assert.Contains("data-can-respond", view);
        Assert.Contains("No active clinical decision support findings.", ui);
        Assert.Contains("void load();", ui);
        Assert.DoesNotContain("modal", ui, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PatientChartOpened", ui);
    }

    [Fact]
    public void TelemetryAndRuleContractContainNoClinicalDomainRuleOrPatientLogging()
    {
        var telemetry = File.ReadAllText(Path.Combine(Root(), "src", "MicroEMR.Application", "OperationalTelemetry", "OperationalTelemetry.cs"));
        var cds = File.ReadAllText(Path.Combine(Root(), "src", "MicroEMR.Application", "Cds", "CdsServices.cs"));
        Assert.Contains("CDS_RULE_EVALUATION_FAILED", telemetry);
        Assert.Contains("RuleKey", telemetry);
        Assert.Contains("RuleVersion", telemetry);
        Assert.DoesNotContain("PatientUid:", telemetry);
        Assert.DoesNotContain("Medication", cds);
        Assert.DoesNotContain("Allerg", cds);
        Assert.DoesNotContain("Immun", cds);
        Assert.DoesNotContain("Result", cds);
    }

    private static CdsEvaluationService Service(FakeRepository repository, IEnumerable<ICdsRule> rules,
        IEnumerable<ICdsFactProvider> providers) => new(new CdsRuleRegistry(rules), providers, repository,
            NullLogger<CdsEvaluationService>.Instance);

    private sealed class SyntheticRule(bool trigger) : ICdsRule
    {
        public CdsRuleMetadata Metadata => new("TEST_ONLY_SYNTHETIC", 1, "Synthetic test condition", CdsSeverities.Info,
            "Exercises technical behavior only.", "TEST_ONLY", "TEST_ONLY_FACTS");
        public ValueTask<CdsRuleEvaluation> EvaluateAsync(CdsFactSet facts, CancellationToken cancellationToken) =>
            ValueTask.FromResult(trigger
                ? CdsRuleEvaluation.Triggered(new("Synthetic finding", "Synthetic condition is true.", "No clinical action.", "condition=true"))
                : CdsRuleEvaluation.NotTriggered());
    }

    private sealed class ThrowingRule : ICdsRule
    {
        public CdsRuleMetadata Metadata => new("TEST_ONLY_THROW", 1, "Synthetic failure", CdsSeverities.Info,
            "Exercises failure isolation.", "TEST_ONLY", "TEST_ONLY_FACTS");
        public ValueTask<CdsRuleEvaluation> EvaluateAsync(CdsFactSet facts, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("TEST_PATIENT_FACT_DO_NOT_LOG");
    }

    private sealed class InvalidRule : ICdsRule
    {
        public CdsRuleMetadata Metadata => new("bad key!", 0, "", "Critical", "", null, "");
        public ValueTask<CdsRuleEvaluation> EvaluateAsync(CdsFactSet facts, CancellationToken cancellationToken) =>
            ValueTask.FromResult(CdsRuleEvaluation.NotTriggered());
    }

    private sealed class SyntheticFacts(bool value) : ICdsFactProvider
    {
        public string ProviderKey => "TEST_ONLY_FACTS";
        public Task<CdsFactSet> GetFactsAsync(Guid patientUid, CancellationToken cancellationToken) =>
            Task.FromResult(new CdsFactSet(ProviderKey, new Dictionary<string, string> { ["condition"] = value.ToString() }));
    }

    private sealed class FakeRepository : ICdsRepository
    {
        public List<CdsAlertResponse> Alerts { get; } = [];
        public int RecordCalls { get; private set; }
        public int ResolveCalls { get; private set; }
        public Task<bool> PatientExistsAsync(Guid patientUid, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<IReadOnlyList<CdsAlertResponse>> ListAsync(Guid patientUid, bool includeHistory, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CdsAlertResponse>>(Alerts.Where(x => x.PatientUid == patientUid && (includeHistory || x.Status is "Active" or "Acknowledged")).ToArray());
        public Task<IReadOnlyList<CdsAlertHistoryResponse>> GetHistoryAsync(Guid patientUid, Guid alertUid, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CdsAlertHistoryResponse>>([]);
        public Task<CdsAlertResponse> RecordFindingAsync(PersistedCdsFinding finding, CancellationToken cancellationToken)
        {
            RecordCalls++;
            var existing = Alerts.SingleOrDefault(x => x.PatientUid == finding.PatientUid && x.FindingFingerprint == finding.Fingerprint);
            if (existing is not null) { Alerts.Remove(existing); Alerts.Add(Copy(existing, DateTime.UtcNow)); return Task.FromResult(Alerts[^1]); }
            var alert = new CdsAlertResponse { CdsAlertUid=Guid.NewGuid(),PatientUid=finding.PatientUid,RuleKey=finding.RuleKey,RuleVersion=finding.RuleVersion,FindingFingerprint=finding.Fingerprint,Severity=finding.Severity,Status="Active",Title=finding.Title,Explanation=finding.Explanation,SuggestedAction=finding.SuggestedAction,FirstDetectedAtUtc=DateTime.UtcNow,LastEvaluatedAtUtc=DateTime.UtcNow,RowVersion="AAAAAAAAAAA=" };
            Alerts.Add(alert); return Task.FromResult(alert);
        }
        public Task ResolveRuleFindingsAsync(Guid patientUid, string ruleKey, int ruleVersion, string? exceptFingerprint, CancellationToken cancellationToken) { ResolveCalls++; return Task.CompletedTask; }
        public Task<CdsAlertResponse?> AcknowledgeAsync(Guid patientUid, Guid alertUid, byte[] rowVersion, long actorUserId, CancellationToken cancellationToken) => Task.FromResult<CdsAlertResponse?>(null);
        public Task<CdsAlertResponse?> DismissAsync(Guid patientUid, Guid alertUid, string reasonCode, string? comment, byte[] rowVersion, long actorUserId, CancellationToken cancellationToken) => Task.FromResult<CdsAlertResponse?>(null);
        private static CdsAlertResponse Copy(CdsAlertResponse x, DateTime evaluated) => new() { CdsAlertUid=x.CdsAlertUid,PatientUid=x.PatientUid,RuleKey=x.RuleKey,RuleVersion=x.RuleVersion,FindingFingerprint=x.FindingFingerprint,Severity=x.Severity,Status=x.Status,Title=x.Title,Explanation=x.Explanation,SuggestedAction=x.SuggestedAction,FirstDetectedAtUtc=x.FirstDetectedAtUtc,LastEvaluatedAtUtc=evaluated,RowVersion=x.RowVersion };
    }

    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", ".."));
}
