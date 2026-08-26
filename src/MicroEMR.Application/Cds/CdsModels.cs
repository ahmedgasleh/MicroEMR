using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Application.Cds;

public static class CdsSeverities
{
    public const string Info = "Info";
    public const string Warning = "Warning";
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Info, Warning };
}

public static class CdsAlertStatuses
{
    public const string Active = "Active";
    public const string Acknowledged = "Acknowledged";
    public const string Dismissed = "Dismissed";
    public const string Resolved = "Resolved";
}

public static class CdsDismissReasons
{
    public const string NotApplicable = "NotApplicable";
    public const string AlreadyAddressed = "AlreadyAddressed";
    public const string DuplicateFinding = "DuplicateFinding";
    public const string Other = "Other";
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
        { NotApplicable, AlreadyAddressed, DuplicateFinding, Other };
}

public sealed record CdsRuleMetadata(
    string RuleKey,
    int Version,
    string Name,
    string Severity,
    string ClinicalRationale,
    string? SourceReference,
    string FactProviderKey);

public sealed record CdsFactSet(string ProviderKey, IReadOnlyDictionary<string, string> Values);

public sealed record CdsFinding(
    string Title,
    string Explanation,
    string SuggestedAction,
    string RelevantFactIdentity);

public sealed record CdsRuleEvaluation(bool ConditionDetermined, CdsFinding? Finding)
{
    public static CdsRuleEvaluation Triggered(CdsFinding finding) => new(true, finding);
    public static CdsRuleEvaluation NotTriggered() => new(true, null);
    public static CdsRuleEvaluation Indeterminate() => new(false, null);
}

public sealed class CdsAlertResponse
{
    public Guid CdsAlertUid { get; init; }
    public Guid PatientUid { get; init; }
    public string RuleKey { get; init; } = string.Empty;
    public int RuleVersion { get; init; }
    public string FindingFingerprint { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Explanation { get; init; } = string.Empty;
    public string SuggestedAction { get; init; } = string.Empty;
    public string? RuleSourceReference { get; init; }
    public DateTime FirstDetectedAtUtc { get; init; }
    public DateTime LastEvaluatedAtUtc { get; init; }
    public long? AcknowledgedBy { get; init; }
    public DateTime? AcknowledgedAtUtc { get; init; }
    public long? DismissedBy { get; init; }
    public DateTime? DismissedAtUtc { get; init; }
    public string? DismissReasonCode { get; init; }
    public string? DismissComment { get; init; }
    public DateTime? ResolvedAtUtc { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class CdsAlertHistoryResponse
{
    public Guid CdsAlertHistoryUid { get; init; }
    public Guid CdsAlertUid { get; init; }
    public string EventType { get; init; } = string.Empty;
    public long? ActorUserId { get; init; }
    public string? ActorDisplayName { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public string? ReasonCode { get; init; }
    public string? Comment { get; init; }
    public string RuleKey { get; init; } = string.Empty;
    public int RuleVersion { get; init; }
}

public sealed class AcknowledgeCdsAlertRequest
{
    [Required, RegularExpression("^[A-Za-z0-9+/]{11}=$")]
    public string ExpectedRowVersion { get; init; } = string.Empty;
}

public sealed class DismissCdsAlertRequest
{
    [Required, StringLength(50)] public string ReasonCode { get; init; } = string.Empty;
    [StringLength(500)] public string? Comment { get; init; }
    [Required, RegularExpression("^[A-Za-z0-9+/]{11}=$")]
    public string ExpectedRowVersion { get; init; } = string.Empty;
}

public sealed record CdsEvaluationResponse(IReadOnlyList<CdsAlertResponse> Alerts, int RulesEvaluated, int RulesFailed);

public sealed class CdsConcurrencyException : Exception;
public sealed class CdsInvalidTransitionException : Exception;
public sealed class CdsInvalidDismissReasonException : Exception;

