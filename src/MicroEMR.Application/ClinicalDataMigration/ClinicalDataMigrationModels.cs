using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MicroEMR.Application.ClinicalDataMigration;

public static class ClinicalMigrationConstants
{
    public const int CanonicalSchemaVersion = 1;
    public const string ValidationMode = "ValidateOnly";
    public const int DefaultMaxPatients = 1_000;
    public const int DefaultMaxProblems = 5_000;
}

public sealed class ClinicalDataMigrationOptions
{
    public int MaxPatients { get; set; } = ClinicalMigrationConstants.DefaultMaxPatients;
    public int MaxProblems { get; set; } = ClinicalMigrationConstants.DefaultMaxProblems;
}

public sealed class ClinicalMigrationPackageV1
{
    public int SchemaVersion { get; set; } = ClinicalMigrationConstants.CanonicalSchemaVersion;
    public Guid PackageUid { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public string? SourceSystemVersion { get; set; }
    public string? PackageSchemaVersion { get; set; }
    public IReadOnlyList<ClinicalMigrationPatientV1> Patients { get; set; } = [];
    public IReadOnlyList<ClinicalMigrationProblemV1> Problems { get; set; } = [];
}

public abstract class ClinicalMigrationRecordV1
{
    public string SourceObjectId { get; set; } = string.Empty;
    public string SourcePatientId { get; set; } = string.Empty;
    public DateTimeOffset? SourceCreatedAt { get; set; }
    public DateTimeOffset? SourceUpdatedAt { get; set; }
    public string? SourceAuthor { get; set; }
}

public sealed class ClinicalMigrationPatientV1 : ClinicalMigrationRecordV1
{
    public string? ChartNumber { get; set; }
    public string? HealthCardNumber { get; set; }
    public string? HealthCardVersion { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string? SexAtBirth { get; set; }
    public string? GenderIdentity { get; set; }
    public string? PreferredName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AlternatePhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public string? PostalCode { get; set; }
    public string CountryCode { get; set; } = "CA";
}

public sealed class ClinicalMigrationProblemV1 : ClinicalMigrationRecordV1
{
    public string ProblemName { get; set; } = string.Empty;
    public string? ProblemDescription { get; set; }
    public DateOnly? OnsetDate { get; set; }
    public string Status { get; set; } = "Active";
    public DateOnly? ResolvedDate { get; set; }
}

public interface IClinicalDataMigrationPackageAdapter<in TPackage>
{
    Task<ClinicalMigrationPackageV1> ToCanonicalAsync(TPackage package, CancellationToken token = default);
}

public static class ClinicalMigrationIssueCodes
{
    public const string UnsupportedSchemaVersion = nameof(UnsupportedSchemaVersion);
    public const string MissingSourceSystem = nameof(MissingSourceSystem);
    public const string MissingPackageUid = nameof(MissingPackageUid);
    public const string PatientLimitExceeded = nameof(PatientLimitExceeded);
    public const string ProblemLimitExceeded = nameof(ProblemLimitExceeded);
    public const string MissingSourcePatientId = nameof(MissingSourcePatientId);
    public const string DuplicateSourcePatientId = nameof(DuplicateSourcePatientId);
    public const string MissingRequiredPatientField = nameof(MissingRequiredPatientField);
    public const string InvalidDateOfBirth = nameof(InvalidDateOfBirth);
    public const string AmbiguousPatientMatch = nameof(AmbiguousPatientMatch);
    public const string PossibleDemographicMatch = nameof(PossibleDemographicMatch);
    public const string MissingSourceProblemId = nameof(MissingSourceProblemId);
    public const string DuplicateSourceProblemId = nameof(DuplicateSourceProblemId);
    public const string UnknownSourcePatient = nameof(UnknownSourcePatient);
    public const string MissingProblemDescription = nameof(MissingProblemDescription);
    public const string InvalidProblemStatus = nameof(InvalidProblemStatus);
    public const string InvalidProblemDate = nameof(InvalidProblemDate);
}

public sealed record ClinicalMigrationIssue(
    string Code, string Severity, string RecordType, string? SourceObjectId, string Message,
    DateTimeOffset CreatedAtUtc);

public sealed record ClinicalMigrationRecordTypeCount(
    string RecordType, int Total, int Valid, int Warnings, int Failed);

public sealed record ClinicalMigrationValidationReport(
    Guid MigrationBatchUid, string SourceSystem, Guid PackageUid, string PackageFingerprint,
    string Status, int TotalRecords, int ValidRecords, int WarningRecords, int FailedRecords,
    IReadOnlyList<ClinicalMigrationRecordTypeCount> CountsByRecordType,
    IReadOnlyDictionary<string, int> IssueSummary, bool ReusedExistingBatch);

public sealed record ClinicalMigrationBatchStart(
    Guid MigrationBatchUid, bool ReusedExistingBatch, string Status);

public sealed record PatientMatchCandidate(Guid? PatientUid, int StrongMatchCount, int DemographicMatchCount);

public sealed record StagedMigrationPatient(
    string SourceObjectId, string SourcePatientId, string? ChartNumber, string? HealthCardNumber,
    string? HealthCardVersion, string FirstName, string? MiddleName, string LastName, DateOnly? DateOfBirth,
    string? SexAtBirth, string? GenderIdentity, string? PreferredName, string? PhoneNumber,
    string? AlternatePhoneNumber, string? Email, string? AddressLine1, string? AddressLine2,
    string? City, string? Province, string? PostalCode, string CountryCode,
    DateTimeOffset? SourceCreatedAt, DateTimeOffset? SourceUpdatedAt, string? SourceAuthor,
    string MappingStatus, Guid? TargetPatientUid, string ValidationState, int ErrorCount, int WarningCount);

public sealed record StagedMigrationProblem(
    string SourceObjectId, string SourcePatientId, string ProblemName, string? ProblemDescription,
    DateOnly? OnsetDate, string Status, DateOnly? ResolvedDate,
    DateTimeOffset? SourceCreatedAt, DateTimeOffset? SourceUpdatedAt, string? SourceAuthor,
    string ValidationState, int ErrorCount, int WarningCount);

public interface IClinicalDataMigrationRepository
{
    Task<ClinicalMigrationBatchStart> BeginValidationAsync(ClinicalMigrationPackageV1 package, string fingerprint, long actor, CancellationToken token = default);
    Task<PatientMatchCandidate> FindPatientMatchAsync(string sourceSystem, string sourcePatientId, string? healthCardNumber, string firstName, string lastName, DateOnly? dateOfBirth, CancellationToken token = default);
    Task StagePatientAsync(Guid batchUid, string sourceSystem, StagedMigrationPatient patient, CancellationToken token = default);
    Task StageProblemAsync(Guid batchUid, string sourceSystem, StagedMigrationProblem problem, CancellationToken token = default);
    Task AddIssueAsync(Guid batchUid, ClinicalMigrationIssue issue, CancellationToken token = default);
    Task CompleteValidationAsync(Guid batchUid, long actor, CancellationToken token = default);
    Task<ClinicalMigrationValidationReport?> GetReportAsync(Guid batchUid, bool reused = false, CancellationToken token = default);
    Task<IReadOnlyList<ClinicalMigrationIssue>> ListIssuesAsync(Guid batchUid, int skip, int take, CancellationToken token = default);
}

public interface IClinicalDataMigrationValidationService
{
    Task<ClinicalMigrationValidationReport> ValidateAsync(ClinicalMigrationPackageV1 package, long actor, CancellationToken token = default);
    Task<ClinicalMigrationValidationReport?> GetReportAsync(Guid batchUid, CancellationToken token = default);
    Task<IReadOnlyList<ClinicalMigrationIssue>> ListIssuesAsync(Guid batchUid, int page, int pageSize, CancellationToken token = default);
}

public sealed record ClinicalMigrationImportResult(
    Guid MigrationBatchUid, string Status, int AttemptedPatients, int CreatedPatients,
    int ReusedPatients, int ImportedProblems, int SkippedRecords, int FailedPatients, bool Replayed);

public interface IClinicalDataMigrationImportRepository
{
    Task<ClinicalMigrationImportResult?> ImportAsync(Guid batchUid, long initiatingOperator, CancellationToken token = default);
}

public interface IClinicalDataMigrationImportService
{
    Task<ClinicalMigrationImportResult?> ImportAsync(Guid batchUid, long initiatingOperator, CancellationToken token = default);
}

public sealed class ClinicalDataMigrationImportService(IClinicalDataMigrationImportRepository repository) : IClinicalDataMigrationImportService
{
    public Task<ClinicalMigrationImportResult?> ImportAsync(Guid batchUid,long initiatingOperator,CancellationToken token=default)
    {
        if(batchUid==Guid.Empty)throw new ArgumentException("A migration batch identifier is required.",nameof(batchUid));
        if(initiatingOperator<=0)throw new ArgumentOutOfRangeException(nameof(initiatingOperator));
        return repository.ImportAsync(batchUid,initiatingOperator,token);
    }
}

public static class ClinicalMigrationFingerprint
{
    public static string Calculate(ClinicalMigrationPackageV1 package)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", package.SchemaVersion);
            writer.WriteString("packageUid", package.PackageUid);
            Write(writer, "sourceSystem", package.SourceSystem);
            Write(writer, "sourceSystemVersion", package.SourceSystemVersion);
            Write(writer, "packageSchemaVersion", package.PackageSchemaVersion);
            writer.WriteStartArray("patients");
            foreach (var patient in package.Patients.OrderBy(x => Normalize(x.SourcePatientId), StringComparer.Ordinal).ThenBy(x => Normalize(x.SourceObjectId), StringComparer.Ordinal))
            {
                writer.WriteStartObject(); Common(writer, patient);
                Write(writer,"chartNumber",patient.ChartNumber); Write(writer,"healthCardNumber",patient.HealthCardNumber); Write(writer,"healthCardVersion",patient.HealthCardVersion);
                Write(writer,"firstName",patient.FirstName); Write(writer,"middleName",patient.MiddleName); Write(writer,"lastName",patient.LastName);
                writer.WriteString("dateOfBirth",patient.DateOfBirth?.ToString("yyyy-MM-dd")); Write(writer,"sexAtBirth",patient.SexAtBirth); Write(writer,"genderIdentity",patient.GenderIdentity);
                Write(writer,"preferredName",patient.PreferredName); Write(writer,"phoneNumber",patient.PhoneNumber); Write(writer,"alternatePhoneNumber",patient.AlternatePhoneNumber);
                Write(writer,"email",patient.Email); Write(writer,"addressLine1",patient.AddressLine1); Write(writer,"addressLine2",patient.AddressLine2); Write(writer,"city",patient.City);
                Write(writer,"province",patient.Province); Write(writer,"postalCode",patient.PostalCode); Write(writer,"countryCode",patient.CountryCode); writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.WriteStartArray("problems");
            foreach (var problem in package.Problems.OrderBy(x => Normalize(x.SourcePatientId), StringComparer.Ordinal).ThenBy(x => Normalize(x.SourceObjectId), StringComparer.Ordinal))
            {
                writer.WriteStartObject(); Common(writer, problem); Write(writer,"problemName",problem.ProblemName); Write(writer,"problemDescription",problem.ProblemDescription);
                writer.WriteString("onsetDate",problem.OnsetDate?.ToString("yyyy-MM-dd")); Write(writer,"status",problem.Status); writer.WriteString("resolvedDate",problem.ResolvedDate?.ToString("yyyy-MM-dd")); writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.WriteEndObject();
        }
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private static void Common(Utf8JsonWriter writer, ClinicalMigrationRecordV1 record)
    {
        Write(writer,"sourceObjectId",record.SourceObjectId); Write(writer,"sourcePatientId",record.SourcePatientId);
        writer.WriteString("sourceCreatedAt",Utc(record.SourceCreatedAt)); writer.WriteString("sourceUpdatedAt",Utc(record.SourceUpdatedAt)); Write(writer,"sourceAuthor",record.SourceAuthor);
    }
    private static string? Utc(DateTimeOffset? value) => value?.ToUniversalTime().ToString("O");
    private static void Write(Utf8JsonWriter writer, string name, string? value) => writer.WriteString(name, Normalize(value));
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
