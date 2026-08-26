using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Application.Cdm;

public sealed record CdmProgramMetadata(string ProgramKey, int ProgramVersion, string Name, string Description);

public interface ICdmProgramDefinition { CdmProgramMetadata Metadata { get; } }

public interface ICdmProgramRegistry
{
    IReadOnlyList<CdmProgramMetadata> Programs { get; }
    CdmProgramMetadata? Find(string programKey, int programVersion);
}

public sealed class CdmProgramRegistry : ICdmProgramRegistry
{
    public CdmProgramRegistry(IEnumerable<ICdmProgramDefinition> definitions)
    {
        var programs = definitions.Select(x => x.Metadata).ToArray();
        foreach (var item in programs)
            if (string.IsNullOrWhiteSpace(item.ProgramKey) || item.ProgramKey.Length > 100 ||
                !item.ProgramKey.All(x => char.IsAsciiLetterOrDigit(x) || x is '_' or '-' or '.') ||
                item.ProgramVersion <= 0 || string.IsNullOrWhiteSpace(item.Name) || item.Name.Length > 200 ||
                string.IsNullOrWhiteSpace(item.Description) || item.Description.Length > 500)
                throw new InvalidOperationException("Invalid CDM program metadata.");
        if (programs.GroupBy(x => (x.ProgramKey, x.ProgramVersion), StringTupleComparer.Instance).Any(x => x.Count() > 1))
            throw new InvalidOperationException("Duplicate CDM ProgramKey and ProgramVersion registration.");
        Programs = programs;
    }

    public IReadOnlyList<CdmProgramMetadata> Programs { get; }
    public CdmProgramMetadata? Find(string programKey, int programVersion) => Programs.FirstOrDefault(x =>
        string.Equals(x.ProgramKey, programKey, StringComparison.Ordinal) && x.ProgramVersion == programVersion);

    private sealed class StringTupleComparer : IEqualityComparer<(string ProgramKey, int ProgramVersion)>
    {
        public static readonly StringTupleComparer Instance = new();
        public bool Equals((string ProgramKey, int ProgramVersion) x, (string ProgramKey, int ProgramVersion) y) =>
            x.ProgramVersion == y.ProgramVersion && string.Equals(x.ProgramKey, y.ProgramKey, StringComparison.Ordinal);
        public int GetHashCode((string ProgramKey, int ProgramVersion) value) => HashCode.Combine(StringComparer.Ordinal.GetHashCode(value.ProgramKey), value.ProgramVersion);
    }
}

public sealed class CdmEnrollmentResponse
{
    public Guid ChronicDiseaseEnrollmentUid { get; set; }
    public Guid PatientUid { get; set; }
    public Guid PatientProblemUid { get; set; }
    public string ProblemName { get; set; } = string.Empty;
    public string ProgramKey { get; set; } = string.Empty;
    public int ProgramVersion { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long EnrolledBy { get; set; }
    public string? EnrolledByDisplayName { get; set; }
    public DateTime EnrolledAtUtc { get; set; }
    public long? InactivatedBy { get; set; }
    public DateTime? InactivatedAtUtc { get; set; }
    public string? InactivationReason { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class CreateCdmEnrollmentRequest
{
    public Guid PatientProblemUid { get; set; }
    [Required, StringLength(100)] public string ProgramKey { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int ProgramVersion { get; set; }
}

public sealed class InactivateCdmEnrollmentRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [StringLength(500)] public string? Reason { get; set; }
}

public sealed record CdmSummaryResponse(IReadOnlyList<CdmProgramMetadata> AvailablePrograms, IReadOnlyList<CdmEnrollmentResponse> Enrollments);

public sealed class CdmEnrollmentConflictException(string message) : Exception(message);
public sealed class CdmEnrollmentValidationException(string message) : Exception(message);
public sealed class CdmEnrollmentConcurrencyException(string message) : Exception(message);
