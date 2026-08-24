using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace MicroEMR.Application.ClinicalDataMigration;

public sealed class ClinicalDataMigrationValidationService(
    IClinicalDataMigrationRepository repository,
    IOptions<ClinicalDataMigrationOptions> options) : IClinicalDataMigrationValidationService
{
    public async Task<ClinicalMigrationValidationReport> ValidateAsync(ClinicalMigrationPackageV1 package, long actor, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (actor <= 0) throw new ArgumentOutOfRangeException(nameof(actor));
        ValidateEnvelope(package);
        var fingerprint = ClinicalMigrationFingerprint.Calculate(package);
        var start = await repository.BeginValidationAsync(package, fingerprint, actor, token);
        if (start.ReusedExistingBatch)
            return await repository.GetReportAsync(start.MigrationBatchUid, true, token)
                ?? throw new InvalidOperationException("The existing migration validation batch could not be loaded.");

        var importReadyPatientIds = new HashSet<string>(StringComparer.Ordinal);
        var duplicatePatientIds = Duplicates(package.Patients.Select(x => Normalize(x.SourcePatientId)));
        var duplicateProblemIds = Duplicates(package.Problems.Select(x => Normalize(x.SourceObjectId)));

        foreach (var source in package.Patients)
            if (await ValidatePatientAsync(start.MigrationBatchUid, package.SourceSystem.Trim(), source, duplicatePatientIds, token) is { } readyId)
                importReadyPatientIds.Add(readyId);
        foreach (var source in package.Problems)
            await ValidateProblemAsync(start.MigrationBatchUid, package.SourceSystem.Trim(), source, importReadyPatientIds, duplicateProblemIds, token);

        await repository.CompleteValidationAsync(start.MigrationBatchUid, actor, token);
        return await repository.GetReportAsync(start.MigrationBatchUid, false, token)
            ?? throw new InvalidOperationException("The migration validation report could not be loaded.");
    }

    public Task<ClinicalMigrationValidationReport?> GetReportAsync(Guid batchUid, CancellationToken token = default) =>
        repository.GetReportAsync(batchUid, false, token);

    public Task<IReadOnlyList<ClinicalMigrationIssue>> ListIssuesAsync(Guid batchUid, int page, int pageSize, CancellationToken token = default) =>
        repository.ListIssuesAsync(batchUid, Math.Max(0, page - 1) * Math.Clamp(pageSize, 1, 100), Math.Clamp(pageSize, 1, 100), token);

    private void ValidateEnvelope(ClinicalMigrationPackageV1 package)
    {
        if (package.SchemaVersion != ClinicalMigrationConstants.CanonicalSchemaVersion) throw new ClinicalMigrationPackageException(ClinicalMigrationIssueCodes.UnsupportedSchemaVersion);
        if (string.IsNullOrWhiteSpace(package.SourceSystem) || package.SourceSystem.Trim().Length > 100) throw new ClinicalMigrationPackageException(ClinicalMigrationIssueCodes.MissingSourceSystem);
        if (package.PackageUid == Guid.Empty) throw new ClinicalMigrationPackageException(ClinicalMigrationIssueCodes.MissingPackageUid);
        if (package.Patients.Count > options.Value.MaxPatients) throw new ClinicalMigrationPackageException(ClinicalMigrationIssueCodes.PatientLimitExceeded);
        if (package.Problems.Count > options.Value.MaxProblems) throw new ClinicalMigrationPackageException(ClinicalMigrationIssueCodes.ProblemLimitExceeded);
    }

    private async Task<string?> ValidatePatientAsync(Guid batchUid, string sourceSystem, ClinicalMigrationPatientV1 source, HashSet<string> duplicates, CancellationToken token)
    {
        var errors = new List<(string Code,string Message)>(); var warnings = new List<(string Code,string Message)>();
        var sourcePatientId = Normalize(source.SourcePatientId);
        if (sourcePatientId is null) errors.Add((ClinicalMigrationIssueCodes.MissingSourcePatientId,"A bounded source patient identifier is required."));
        else if (sourcePatientId.Length > 200) errors.Add((ClinicalMigrationIssueCodes.MissingSourcePatientId,"The source patient identifier exceeds the supported length."));
        else if (duplicates.Contains(sourcePatientId)) errors.Add((ClinicalMigrationIssueCodes.DuplicateSourcePatientId,"The source patient identifier is duplicated in this package."));
        if (string.IsNullOrWhiteSpace(source.FirstName) || source.FirstName.Trim().Length > 100 || string.IsNullOrWhiteSpace(source.LastName) || source.LastName.Trim().Length > 100 || source.DateOfBirth is null)
            errors.Add((ClinicalMigrationIssueCodes.MissingRequiredPatientField,"Required patient demographics are missing or exceed supported lengths."));
        if (source.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow)) errors.Add((ClinicalMigrationIssueCodes.InvalidDateOfBirth,"The patient date of birth is invalid."));

        var mapping = "Invalid"; Guid? target = null;
        if (errors.Count == 0)
        {
            var match = await repository.FindPatientMatchAsync(sourceSystem, sourcePatientId!, Normalize(source.HealthCardNumber), source.FirstName.Trim(), source.LastName.Trim(), source.DateOfBirth, token);
            if (match.StrongMatchCount == 1) { mapping = "MappedExisting"; target = match.PatientUid; }
            else if (match.StrongMatchCount > 1) { mapping = "RequiresReview"; errors.Add((ClinicalMigrationIssueCodes.AmbiguousPatientMatch,"Multiple strong patient matches require review.")); }
            else if (match.DemographicMatchCount > 0) { mapping = "RequiresReview"; warnings.Add((ClinicalMigrationIssueCodes.PossibleDemographicMatch,"A demographic candidate requires manual review and was not automatically matched.")); }
            else mapping = "ReadyToCreate";
        }
        var state = errors.Count > 0 ? "Invalid" : warnings.Count > 0 ? "Warning" : "Valid";
        await repository.StagePatientAsync(batchUid,sourceSystem,new(Trim(source.SourceObjectId)??sourcePatientId??string.Empty,sourcePatientId??string.Empty,Trim(source.ChartNumber),Trim(source.HealthCardNumber),Trim(source.HealthCardVersion),Trim(source.FirstName)??string.Empty,Trim(source.MiddleName),Trim(source.LastName)??string.Empty,source.DateOfBirth,Trim(source.SexAtBirth),Trim(source.GenderIdentity),Trim(source.PreferredName),Trim(source.PhoneNumber),Trim(source.AlternatePhoneNumber),Trim(source.Email),Trim(source.AddressLine1),Trim(source.AddressLine2),Trim(source.City),Trim(source.Province),Trim(source.PostalCode),string.IsNullOrWhiteSpace(source.CountryCode)?"CA":source.CountryCode.Trim(),source.SourceCreatedAt,source.SourceUpdatedAt,Trim(source.SourceAuthor),mapping,target,state,errors.Count,warnings.Count),token);
        await SaveIssues(batchUid,"Patient",source.SourceObjectId,errors,warnings,token);
        return errors.Count == 0 && mapping is "ReadyToCreate" or "MappedExisting" ? sourcePatientId : null;
    }

    private async Task ValidateProblemAsync(Guid batchUid, string sourceSystem, ClinicalMigrationProblemV1 source, HashSet<string> patientIds, HashSet<string> duplicates, CancellationToken token)
    {
        var errors = new List<(string Code,string Message)>(); var sourceId=Normalize(source.SourceObjectId); var patientId=Normalize(source.SourcePatientId);
        if(sourceId is null||sourceId.Length>200)errors.Add((ClinicalMigrationIssueCodes.MissingSourceProblemId,"A bounded source problem identifier is required."));
        else if(duplicates.Contains(sourceId))errors.Add((ClinicalMigrationIssueCodes.DuplicateSourceProblemId,"The source problem identifier is duplicated in this package."));
        if(patientId is null||!patientIds.Contains(patientId))errors.Add((ClinicalMigrationIssueCodes.UnknownSourcePatient,"The source patient relationship is missing or unresolved."));
        if(string.IsNullOrWhiteSpace(source.ProblemName)||source.ProblemName.Trim().Length>200)errors.Add((ClinicalMigrationIssueCodes.MissingProblemDescription,"A supported problem name is required."));
        var status=Normalize(source.Status)??"Active"; if(status is not ("Active" or "Resolved"))errors.Add((ClinicalMigrationIssueCodes.InvalidProblemStatus,"Problem status must be Active or Resolved."));
        if(source.OnsetDate>DateOnly.FromDateTime(DateTime.UtcNow)||status=="Resolved"&&source.ResolvedDate is null||source.ResolvedDate<source.OnsetDate)errors.Add((ClinicalMigrationIssueCodes.InvalidProblemDate,"Problem dates are invalid for the supplied status."));
        var state=errors.Count>0?"Invalid":"Valid";
        await repository.StageProblemAsync(batchUid,sourceSystem,new(Trim(source.SourceObjectId)??string.Empty,patientId??string.Empty,Trim(source.ProblemName)??string.Empty,Trim(source.ProblemDescription),source.OnsetDate,status,source.ResolvedDate,source.SourceCreatedAt,source.SourceUpdatedAt,Trim(source.SourceAuthor),state,errors.Count,0),token);
        await SaveIssues(batchUid,"Problem",source.SourceObjectId,errors,[],token);
    }

    private async Task SaveIssues(Guid batchUid,string type,string? id,IEnumerable<(string Code,string Message)> errors,IEnumerable<(string Code,string Message)> warnings,CancellationToken token)
    { foreach(var x in errors)await repository.AddIssueAsync(batchUid,new(x.Code,"Error",type,Trim(id),x.Message,DateTimeOffset.UtcNow),token); foreach(var x in warnings)await repository.AddIssueAsync(batchUid,new(x.Code,"Warning",type,Trim(id),x.Message,DateTimeOffset.UtcNow),token); }
    private static HashSet<string> Duplicates(IEnumerable<string?> values)=>values.Where(x=>x is not null).GroupBy(x=>x!,StringComparer.Ordinal).Where(x=>x.Count()>1).Select(x=>x.Key).ToHashSet(StringComparer.Ordinal);
    private static string? Normalize(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
    private static string? Trim(string? value)=>Normalize(value);
}

public sealed class ClinicalMigrationPackageException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
