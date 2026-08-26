using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.OperationalTelemetry;

namespace MicroEMR.Application.Cds;

public interface ICdsRule
{
    CdsRuleMetadata Metadata { get; }
    ValueTask<CdsRuleEvaluation> EvaluateAsync(CdsFactSet facts, CancellationToken cancellationToken);
}

public interface ICdsFactProvider
{
    string ProviderKey { get; }
    Task<CdsFactSet> GetFactsAsync(Guid patientUid, CancellationToken cancellationToken);
}

public interface ICdsRuleRegistry
{
    IReadOnlyList<ICdsRule> ActiveRules { get; }
}

public sealed class CdsRuleRegistry : ICdsRuleRegistry
{
    public CdsRuleRegistry(IEnumerable<ICdsRule> rules)
    {
        var active = rules.ToArray();
        foreach (var rule in active) Validate(rule.Metadata);
        if (active.GroupBy(x => (x.Metadata.RuleKey, x.Metadata.Version)).Any(x => x.Count() > 1))
            throw new InvalidOperationException("Duplicate CDS RuleKey and Version registration.");
        ActiveRules = active;
    }

    public IReadOnlyList<ICdsRule> ActiveRules { get; }

    private static void Validate(CdsRuleMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata.RuleKey) || metadata.RuleKey.Length > 100 ||
            !metadata.RuleKey.All(x => char.IsAsciiLetterOrDigit(x) || x is '_' or '-' or '.') ||
            metadata.Version <= 0 || string.IsNullOrWhiteSpace(metadata.Name) || metadata.Name.Length > 200 ||
            !CdsSeverities.All.Contains(metadata.Severity) || string.IsNullOrWhiteSpace(metadata.ClinicalRationale) ||
            string.IsNullOrWhiteSpace(metadata.FactProviderKey))
            throw new InvalidOperationException("Invalid CDS rule metadata.");
    }
}

public sealed record PersistedCdsFinding(
    Guid PatientUid, string RuleKey, int RuleVersion, string Fingerprint, string Severity,
    string Title, string Explanation, string SuggestedAction, string? SourceReference);

public interface ICdsRepository
{
    Task<bool> PatientExistsAsync(Guid patientUid, CancellationToken cancellationToken);
    Task<IReadOnlyList<CdsAlertResponse>> ListAsync(Guid patientUid, bool includeHistory, CancellationToken cancellationToken);
    Task<IReadOnlyList<CdsAlertHistoryResponse>> GetHistoryAsync(Guid patientUid, Guid alertUid, CancellationToken cancellationToken);
    Task<CdsAlertResponse> RecordFindingAsync(PersistedCdsFinding finding, CancellationToken cancellationToken);
    Task ResolveRuleFindingsAsync(Guid patientUid, string ruleKey, int ruleVersion, string? exceptFingerprint, CancellationToken cancellationToken);
    Task<CdsAlertResponse?> AcknowledgeAsync(Guid patientUid, Guid alertUid, byte[] rowVersion, long actorUserId, CancellationToken cancellationToken);
    Task<CdsAlertResponse?> DismissAsync(Guid patientUid, Guid alertUid, string reasonCode, string? comment, byte[] rowVersion, long actorUserId, CancellationToken cancellationToken);
}

public interface ICdsEvaluationService
{
    Task<CdsEvaluationResponse> EvaluatePatientAsync(Guid patientUid, CancellationToken cancellationToken);
}

public sealed class CdsEvaluationService(
    ICdsRuleRegistry registry,
    IEnumerable<ICdsFactProvider> factProviders,
    ICdsRepository repository,
    ILogger<CdsEvaluationService> logger) : ICdsEvaluationService
{
    private readonly IReadOnlyDictionary<string, ICdsFactProvider> _providers = factProviders
        .ToDictionary(x => x.ProviderKey, StringComparer.Ordinal);

    public async Task<CdsEvaluationResponse> EvaluatePatientAsync(Guid patientUid, CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty || !await repository.PatientExistsAsync(patientUid, cancellationToken))
            return new([], 0, 0);

        var evaluated = 0;
        var failed = 0;
        foreach (var rule in registry.ActiveRules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (!_providers.TryGetValue(rule.Metadata.FactProviderKey, out var provider))
                    throw new InvalidOperationException("Required CDS fact provider is not registered.");

                var facts = await provider.GetFactsAsync(patientUid, cancellationToken);
                var result = await rule.EvaluateAsync(facts, cancellationToken);
                evaluated++;
                if (!result.ConditionDetermined) continue;

                if (result.Finding is null)
                {
                    await repository.ResolveRuleFindingsAsync(patientUid, rule.Metadata.RuleKey,
                        rule.Metadata.Version, null, cancellationToken);
                    continue;
                }

                var fingerprint = CdsFingerprint.Compute(rule.Metadata.RuleKey, rule.Metadata.Version,
                    patientUid, result.Finding.RelevantFactIdentity);
                await repository.RecordFindingAsync(new(patientUid, rule.Metadata.RuleKey,
                    rule.Metadata.Version, fingerprint, rule.Metadata.Severity, result.Finding.Title,
                    result.Finding.Explanation, result.Finding.SuggestedAction,
                    rule.Metadata.SourceReference), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                failed++;
                logger.CdsRuleEvaluationFailed(rule.Metadata.RuleKey, rule.Metadata.Version);
            }
        }

        return new(await repository.ListAsync(patientUid, false, cancellationToken), evaluated, failed);
    }
}

public static class CdsFingerprint
{
    public static string Compute(string ruleKey, int version, Guid patientUid, string relevantFactIdentity)
    {
        var canonical = $"{ruleKey.Trim()}\n{version}\n{patientUid:D}\n{relevantFactIdentity.Trim()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}
