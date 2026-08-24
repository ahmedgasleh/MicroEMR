using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalDataMigration;
using MicroEMR.Infrastructure.ClinicalDataMigration;
using MicroEMR.Infrastructure.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ClinicalDataMigrationValidationFoundationTests
{
    [Fact]
    public void Migration0048IsNextTenantLocalValidateOnlyAndDoesNotWriteClinicalTables()
    {
        var root=Root();var path=Path.Combine(root,"db","tenant-clinical","migrations","0048-clinical-data-migration-validation-foundation.sql");var sql=File.ReadAllText(path);var manifest=File.ReadAllText(Path.Combine(root,"db","tenant-clinical","manifest.json"));
        Assert.True(File.Exists(path));Assert.Equal(1,Count(manifest,"\"migrationId\": \"0048-clinical-data-migration-validation-foundation\""));Assert.False(File.Exists(Path.Combine(root,"db","tenant-clinical","migrations","0049-clinical-data-migration-validation-foundation.sql")));
        Assert.Contains("ClinicalDataMigrationBatch",sql);Assert.Contains("ValidationMode=N'ValidateOnly'",sql);Assert.Contains("Status IN(N'Created',N'Validating',N'ValidationFailed',N'Validated')",sql);
        Assert.Contains("UQ_ClinicalDataMigrationBatch_SourceFingerprint",sql);Assert.Contains("UQ_ClinicalDataMigrationBatch_SourcePackage",sql);Assert.Contains("IX_ClinicalDataMigrationStagedPatient_BatchSourceObject",sql);Assert.Contains("IX_ClinicalDataMigrationStagedProblem_BatchSourceObject",sql);
        foreach(var table in new[]{"Patient","PatientProblem","PatientAllergy","PatientMedication","PatientImmunization","PatientEncounter","PatientResult","PatientDocument","PatientFile"})
        {Assert.DoesNotContain($"INSERT dbo.{table}(",sql,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain($"UPDATE dbo.{table} SET",sql,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain($"DELETE FROM dbo.{table}",sql,StringComparison.OrdinalIgnoreCase);}
        Assert.Single(Directory.GetFiles(Path.Combine(root,"db","platform"),"021_prescriptions_prescribe_permission_governance.sql"));
    }

    [Fact]
    public void MigrationPreservesProvenanceUsesRedactedAdministrativeAuditAndNoPerRecordAudit()
    {
        var sql=Sql();foreach(var field in new[]{"SourceSystem","SourceObjectId","SourcePatientId","SourceCreatedAt","SourceUpdatedAt","SourceAuthor","PackageFingerprint","RequestedBy"})Assert.Contains(field,sql);
        Assert.Equal(1,Count(sql,"INSERT dbo.AuditLog"));Assert.Contains("DataMigrationValidated",sql);Assert.Contains("DataMigrationValidationFailed",sql);Assert.DoesNotContain("HealthCardNumber Package",sql);Assert.DoesNotContain("ProblemName Problem",sql);
        Assert.Contains(typeof(ITenantSqlConnectionFactory),Assert.Single(typeof(ClinicalDataMigrationRepository).GetConstructors()).GetParameters().Select(x=>x.ParameterType));
    }

    [Fact]
    public void FingerprintIsDeterministicOrderIndependentAndSensitiveToMaterialChange()
    {
        var first=Package();var reordered=Package();reordered.Patients=reordered.Patients.Reverse().ToArray();reordered.Problems=reordered.Problems.Reverse().ToArray();
        var a=ClinicalMigrationFingerprint.Calculate(first);var b=ClinicalMigrationFingerprint.Calculate(reordered);Assert.Equal(64,a.Length);Assert.Equal(a,b);
        reordered.Problems[0].ProblemName="Changed";Assert.NotEqual(a,ClinicalMigrationFingerprint.Calculate(reordered));
    }

    [Fact]
    public async Task ValidateStagesPatientsProblemsIssuesProvenanceAndStructuredCountsWithoutClinicalDependencies()
    {
        var repo=new Repository();var service=Service(repo);var package=Package();
        package.Patients=[package.Patients[0],new(){SourceObjectId="patient-2",SourcePatientId="p2",FirstName="Same",LastName="Person",DateOfBirth=new(1980,1,1),SourceAuthor="External Dr",SourceCreatedAt=new DateTimeOffset(2010,1,1,0,0,0,TimeSpan.Zero)}];
        repo.Matches["p2"]=new(null,0,1);
        package.Problems=[package.Problems[0],new(){SourceObjectId="problem-bad",SourcePatientId="missing",ProblemName=" ",Status="Unknown"}];
        var report=await service.ValidateAsync(package,12);
        Assert.Equal("ValidationFailed",report.Status);Assert.Equal(4,report.TotalRecords);Assert.Equal(2,report.ValidRecords);Assert.Equal(1,report.WarningRecords);Assert.Equal(1,report.FailedRecords);
        Assert.Contains(repo.Patients,x=>x.MappingStatus=="ReadyToCreate");Assert.Contains(repo.Patients,x=>x.MappingStatus=="RequiresReview"&&x.SourceAuthor=="External Dr"&&x.SourceCreatedAt is not null);
        Assert.Contains(repo.Issues,x=>x.Code==ClinicalMigrationIssueCodes.UnknownSourcePatient);Assert.Contains(repo.Issues,x=>x.Code==ClinicalMigrationIssueCodes.MissingProblemDescription);Assert.All(repo.Issues,x=>Assert.DoesNotContain("Hypertension",x.Message));
        var ctor=Assert.Single(typeof(ClinicalDataMigrationValidationService).GetConstructors());Assert.DoesNotContain(ctor.GetParameters(),x=>x.ParameterType.FullName?.Contains("Patient",StringComparison.Ordinal)==true||x.ParameterType.FullName?.Contains("Problem",StringComparison.Ordinal)==true);
    }

    [Fact]
    public async Task MatchingUsesStrongIdentifierButNeverAcceptsNameAndDobAlone()
    {
        var repo=new Repository();var package=Package();repo.Matches["p1"]=new(Guid.NewGuid(),1,1);var mapped=await Service(repo).ValidateAsync(package,12);Assert.Equal("MappedExisting",repo.Patients.Single().MappingStatus);Assert.Equal("Validated",mapped.Status);
        repo=new();package=Package();repo.Matches["p1"]=new(null,0,2);await Service(repo).ValidateAsync(package,12);Assert.Equal("RequiresReview",repo.Patients.Single().MappingStatus);Assert.Null(repo.Patients.Single().TargetPatientUid);Assert.Contains(repo.Issues,x=>x.Code==ClinicalMigrationIssueCodes.PossibleDemographicMatch);
    }

    [Fact]
    public async Task ExactReplayReusesBatchWithoutDuplicatingStagingAndNamespacesSourceSystems()
    {
        var repo=new Repository();var service=Service(repo);var package=Package();var first=await service.ValidateAsync(package,12);var second=await service.ValidateAsync(package,12);
        Assert.Equal(first.MigrationBatchUid,second.MigrationBatchUid);Assert.True(second.ReusedExistingBatch);Assert.Single(repo.Patients);Assert.Single(repo.Problems);Assert.Equal(1,repo.Completed);
        var other=Package();other.SourceSystem="another-emr";var third=await service.ValidateAsync(other,12);Assert.NotEqual(first.MigrationBatchUid,third.MigrationBatchUid);Assert.Equal(2,repo.Patients.Count);
    }

    [Fact]
    public async Task EnvelopeLimitsAndRelationshipRulesUseGovernedCodes()
    {
        var repo=new Repository();var service=Service(repo,maxPatients:1,maxProblems:1);var package=Package();package.SchemaVersion=2;var unsupported=await Assert.ThrowsAsync<ClinicalMigrationPackageException>(()=>service.ValidateAsync(package,12));Assert.Equal(ClinicalMigrationIssueCodes.UnsupportedSchemaVersion,unsupported.Code);
        package=Package();package.SourceSystem=" ";var missing=await Assert.ThrowsAsync<ClinicalMigrationPackageException>(()=>service.ValidateAsync(package,12));Assert.Equal(ClinicalMigrationIssueCodes.MissingSourceSystem,missing.Code);
        package=Package();package.Patients=[package.Patients[0],new(){SourceObjectId="2",SourcePatientId="2",FirstName="A",LastName="B",DateOfBirth=new(2000,1,1)}];var limit=await Assert.ThrowsAsync<ClinicalMigrationPackageException>(()=>service.ValidateAsync(package,12));Assert.Equal(ClinicalMigrationIssueCodes.PatientLimitExceeded,limit.Code);
    }

    [Fact]
    public void ApiRequiresAdministrativePermissionAndCanonicalInputOnly()
    {
        var auth=typeof(ClinicalDataMigrationController).GetCustomAttributes(true).OfType<AuthorizeAttribute>();Assert.Contains(auth,x=>x.Policy?.EndsWith(PermissionKeys.UsersManageAccess,StringComparison.Ordinal)==true);
        Assert.NotEqual(PermissionKeys.ClinicalDataManage,PermissionKeys.UsersManageAccess);Assert.NotNull(typeof(ClinicalDataMigrationController).GetMethod(nameof(ClinicalDataMigrationController.Validate)));
        Assert.DoesNotContain(Assembly.GetAssembly(typeof(ClinicalDataMigrationController))!.GetTypes(),x=>x.Name.Contains("Ontario",StringComparison.OrdinalIgnoreCase)||x.Name.Contains("Zip",StringComparison.OrdinalIgnoreCase));
    }

    private static ClinicalMigrationPackageV1 Package()=>new(){PackageUid=Guid.Parse("10000000-0000-0000-0000-000000000001"),SourceSystem="source-emr",SourceSystemVersion="1",PackageSchemaVersion="internal-v1",Patients=[new(){SourceObjectId="patient-1",SourcePatientId="p1",FirstName="Jane",LastName="Doe",DateOfBirth=new(1980,1,1),HealthCardNumber="123",SourceAuthor="Dr Source",SourceCreatedAt=new DateTimeOffset(2010,1,1,0,0,0,TimeSpan.Zero)}],Problems=[new(){SourceObjectId="problem-1",SourcePatientId="p1",ProblemName="Hypertension",Status="Active",OnsetDate=new(2012,1,1),SourceAuthor="Dr Source"}]};
    private static ClinicalDataMigrationValidationService Service(Repository repository,int maxPatients=10,int maxProblems=10)=>new(repository,Options.Create(new ClinicalDataMigrationOptions{MaxPatients=maxPatients,MaxProblems=maxProblems}));
    private static string Sql()=>File.ReadAllText(Path.Combine(Root(),"db","tenant-clinical","migrations","0048-clinical-data-migration-validation-foundation.sql"));
    private static int Count(string value,string token)=>(value.Length-value.Replace(token,"",StringComparison.OrdinalIgnoreCase).Length)/token.Length;
    private static string Root([System.Runtime.CompilerServices.CallerFilePath]string source="")=>Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!,"..",".."));

    private sealed class Repository:IClinicalDataMigrationRepository
    {
        private readonly Dictionary<(string Source,Guid Package),Guid>batches=[];private readonly Dictionary<Guid,ClinicalMigrationValidationReport>reports=[];
        public Dictionary<string,PatientMatchCandidate>Matches{get;}=[];public List<StagedMigrationPatient>Patients{get;}=[];public List<StagedMigrationProblem>Problems{get;}=[];public List<ClinicalMigrationIssue>Issues{get;}=[];public int Completed{get;private set;}
        public Task<ClinicalMigrationBatchStart>BeginValidationAsync(ClinicalMigrationPackageV1 p,string f,long actor,CancellationToken t=default){var key=(p.SourceSystem.Trim(),p.PackageUid);if(batches.TryGetValue(key,out var existing))return Task.FromResult(new ClinicalMigrationBatchStart(existing,true,reports[existing].Status));var uid=Guid.NewGuid();batches[key]=uid;reports[uid]=new(uid,key.Item1,p.PackageUid,f,"Validating",0,0,0,0,[],new Dictionary<string,int>(),false);return Task.FromResult(new ClinicalMigrationBatchStart(uid,false,"Validating"));}
        public Task<PatientMatchCandidate>FindPatientMatchAsync(string s,string id,string?h,string f,string l,DateOnly?d,CancellationToken t=default)=>Task.FromResult(Matches.GetValueOrDefault(id,new(null,0,0)));
        public Task StagePatientAsync(Guid b,string s,StagedMigrationPatient p,CancellationToken t=default){Patients.Add(p);return Task.CompletedTask;}public Task StageProblemAsync(Guid b,string s,StagedMigrationProblem p,CancellationToken t=default){Problems.Add(p);return Task.CompletedTask;}public Task AddIssueAsync(Guid b,ClinicalMigrationIssue i,CancellationToken t=default){Issues.Add(i);return Task.CompletedTask;}
        public Task CompleteValidationAsync(Guid b,long a,CancellationToken t=default){Completed++;var patient=Patients.Select(x=>x.ValidationState).ToArray();var problem=Problems.Select(x=>x.ValidationState).ToArray();var states=patient.Concat(problem).ToArray();var old=reports[b];var failed=states.Count(x=>x=="Invalid");var summary=Issues.GroupBy(x=>x.Code).ToDictionary(x=>x.Key,x=>x.Count());reports[b]=old with{Status=failed>0?"ValidationFailed":"Validated",TotalRecords=states.Length,ValidRecords=states.Count(x=>x=="Valid"),WarningRecords=states.Count(x=>x=="Warning"),FailedRecords=failed,CountsByRecordType=[CountType("Patient",patient),CountType("Problem",problem)],IssueSummary=summary};return Task.CompletedTask;}
        public Task<ClinicalMigrationValidationReport?>GetReportAsync(Guid b,bool reused=false,CancellationToken t=default)=>Task.FromResult<ClinicalMigrationValidationReport?>(reports.TryGetValue(b,out var r)?r with{ReusedExistingBatch=reused}:null);public Task<IReadOnlyList<ClinicalMigrationIssue>>ListIssuesAsync(Guid b,int skip,int take,CancellationToken t=default)=>Task.FromResult<IReadOnlyList<ClinicalMigrationIssue>>(Issues.Skip(skip).Take(take).ToArray());
        private static ClinicalMigrationRecordTypeCount CountType(string type,string[]states)=>new(type,states.Length,states.Count(x=>x=="Valid"),states.Count(x=>x=="Warning"),states.Count(x=>x=="Invalid"));
    }
}
