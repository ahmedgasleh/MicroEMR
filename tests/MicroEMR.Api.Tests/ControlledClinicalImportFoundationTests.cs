using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalDataMigration;
using MicroEMR.Infrastructure.ClinicalDataMigration;
using MicroEMR.Infrastructure.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ControlledClinicalImportFoundationTests
{
    [Fact]
    public void Migration0049IsNarrowNextAndLeavesPriorMigrationsImmutable()
    {
        var root=Root();var migration=Sql();var manifest=File.ReadAllText(Path.Combine(root,"db","tenant-clinical","manifest.json"));
        Assert.Equal(1,Count(manifest,"\"migrationId\": \"0049-clinical-data-migration-import-foundation\""));Assert.False(File.Exists(Path.Combine(root,"db","tenant-clinical","migrations","0050-clinical-data-migration-import-foundation.sql")));
        Assert.Contains("ClinicalDataMigrationSourceMapping",migration);Assert.Contains("Importing",migration);Assert.Contains("Imported",migration);Assert.Contains("ImportFailed",migration);
        Assert.DoesNotContain("ALTER TABLE dbo.Patient ",migration,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("ALTER TABLE dbo.PatientProblem ",migration,StringComparison.OrdinalIgnoreCase);
        Assert.Single(Directory.GetFiles(Path.Combine(root,"db","platform"),"021_prescriptions_prescribe_permission_governance.sql"));
    }

    [Fact]
    public void ImportIsExplicitValidatedBatchOnlyTenantLocalAndBatchSerialized()
    {
        var sql=Sql();Assert.Contains("ClinicalDataMigration_ImportValidatedBatch",sql);Assert.Contains("@Status NOT IN(N'Validated',N'ImportFailed',N'Importing')",sql);Assert.Contains("sp_getapplock",sql);Assert.Contains("ClinicalDataMigrationImport:",sql);Assert.Contains("@LockTimeout=0",sql);
        Assert.Contains(typeof(ITenantSqlConnectionFactory),Assert.Single(typeof(ClinicalDataMigrationRepository).GetConstructors()).GetParameters().Select(x=>x.ParameterType));
        Assert.DoesNotContain("TenantUid",typeof(ClinicalMigrationImportResult).GetProperties().Select(x=>x.Name));
    }

    [Fact]
    public void PatientAggregateTransactionsCreateOrReuseWithoutDemographicOverwrite()
    {
        var sql=Sql();Assert.Contains("BEGIN TRANSACTION",sql);Assert.Contains("COMMIT",sql);Assert.Contains("PatientAggregateImportFailed",sql);Assert.Contains("MappingStatus=N'ReadyToCreate'",sql);Assert.Contains("MappingStatus=N'MappedExisting'",sql);
        Assert.Contains("INSERT dbo.Patient(",sql);Assert.DoesNotContain("UPDATE dbo.Patient SET",sql,StringComparison.OrdinalIgnoreCase);Assert.Contains("@TargetPatientUid=NEWID()",sql);Assert.DoesNotContain("@TargetPatientUid=@Source",sql,StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHERE PatientUid=@TargetPatientUid AND IsDeleted=0",sql);
    }

    [Fact]
    public void ProblemsUseSupportedStatusesPatientRelationshipAndNoTextMerge()
    {
        var sql=Sql();Assert.Contains("RecordType IN(N'Patient',N'Problem')",sql);Assert.Contains("ProblemStatus",sql);Assert.Contains("TargetPatientProblemUid",sql);Assert.Contains("PatientProblemUid,PatientUid",sql);Assert.Contains("PatientProblemUid=@TargetProblemUid AND PatientUid=@TargetPatientUid",sql);
        Assert.DoesNotContain("WHERE ProblemName=",sql,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("LIKE @Problem",sql,StringComparison.OrdinalIgnoreCase);Assert.Contains("UQ_ClinicalDataMigrationSourceMapping_Source",sql);
    }

    [Fact]
    public void ProvenanceActorsAndAuditsRemainDistinctAndRedacted()
    {
        var sql=Sql();foreach(var value in new[]{"MigrationBatchUid","SourceSystem","SourceObjectId","SourcePatientId","SourceCreatedAt","SourceUpdatedAt","SourceAuthor","ImportedBy","RequestedBy","ImportRequestedBy","MigrationActorUserId"})Assert.Contains(value,sql);
        Assert.Contains("system-data-migration",sql);Assert.Contains("AuthSubjectId IS NULL",sql);Assert.Contains("DataMigrationStarted",sql);Assert.Contains("DataMigrationCompleted",sql);Assert.Contains("DataMigrationFailed",sql);Assert.Contains("MigrationCreate",sql);
        Assert.Contains("SourceAuthor,ImportedBy",sql);Assert.DoesNotContain("ProblemDescription FOR JSON",sql,StringComparison.OrdinalIgnoreCase);Assert.Equal(2,Count(sql,"INSERT dbo.AuditLog(UserId,PatientId,ActionName,EntityName,EntityId,NewValue,CreatedAt) VALUES(@MigrationActor"));
    }

    [Fact]
    public void ReplayResumeAndFailureSemanticsAreDurable()
    {
        var sql=Sql();Assert.Contains("IF @Status=N'Imported'",sql);Assert.Contains("@Replay BIT=0",sql);Assert.Contains("ImportStatus<>N'Imported'",sql);Assert.Contains("TargetObjectUid FROM dbo.ClinicalDataMigrationSourceMapping",sql);Assert.Contains("Status=@FinalStatus",sql);Assert.Contains("CASE WHEN @FailedPatients>0 THEN N'ImportFailed' ELSE N'Imported' END",sql);Assert.Contains("N'UnexpectedImportFailure' ErrorCode",sql);
        Assert.Contains("IF XACT_STATE()<>0 ROLLBACK",sql);Assert.Contains("ImportErrorCode=N'PatientAggregateImportFailed'",sql);
    }

    [Fact]
    public async Task ImportServiceIsReplaySafeThroughRepositoryContract()
    {
        var batch=Guid.NewGuid();var repository=new Repository(batch);var service=new ClinicalDataMigrationImportService(repository);var first=await service.ImportAsync(batch,42);var replay=await service.ImportAsync(batch,42);
        Assert.NotNull(first);Assert.Equal("Imported",first.Status);Assert.True(replay!.Replayed);Assert.Equal(2,repository.Calls);Assert.Equal(batch,replay.MigrationBatchUid);
    }

    [Fact]
    public void ApiKeepsAdministrativePermissionAndSeparateExplicitPost()
    {
        var auth=typeof(ClinicalDataMigrationController).GetCustomAttributes(true).OfType<AuthorizeAttribute>();Assert.Contains(auth,x=>x.Policy?.EndsWith(PermissionKeys.UsersManageAccess,StringComparison.Ordinal)==true);Assert.NotEqual(PermissionKeys.ClinicalDataManage,PermissionKeys.UsersManageAccess);
        var import=typeof(ClinicalDataMigrationController).GetMethod(nameof(ClinicalDataMigrationController.Import))!;var post=Assert.Single(import.GetCustomAttributes(true).OfType<Microsoft.AspNetCore.Mvc.HttpPostAttribute>());Assert.Equal("batches/{batchUid:guid}/import",post.Template);
        Assert.DoesNotContain(import.GetParameters(),x=>x.Name?.Contains("tenant",StringComparison.OrdinalIgnoreCase)==true||x.ParameterType==typeof(ClinicalMigrationPackageV1));
    }

    [Fact]
    public void ValidateOnlyFoundationRemainsSeparateAndClinicalDomainsStayExcluded()
    {
        var controller=File.ReadAllText(Path.Combine(Root(),"src","MicroEMR.Api","Controllers","ClinicalDataMigrationController.cs"));Assert.Contains("Validate",controller);Assert.Contains("Import",controller);Assert.DoesNotContain("ValidateAsync(package",Sql());
        foreach(var domain in new[]{"PatientAllergy","PatientMedication","PatientImmunization","PatientEncounter","PatientResult","PatientDocument","PatientFile"})Assert.DoesNotContain($"INSERT dbo.{domain}",Sql(),StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM dbo.",Sql(),StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("OntarioMD",Sql(),StringComparison.OrdinalIgnoreCase);
    }

    private static string Sql()=>File.ReadAllText(Path.Combine(Root(),"db","tenant-clinical","migrations","0049-clinical-data-migration-import-foundation.sql"));
    private static int Count(string value,string token)=>(value.Length-value.Replace(token,"",StringComparison.OrdinalIgnoreCase).Length)/token.Length;
    private static string Root([System.Runtime.CompilerServices.CallerFilePath]string source="")=>Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!,"..",".."));
    private sealed class Repository(Guid batch):IClinicalDataMigrationImportRepository
    {public int Calls{get;private set;}public Task<ClinicalMigrationImportResult?>ImportAsync(Guid uid,long actor,CancellationToken token=default){Calls++;return Task.FromResult<ClinicalMigrationImportResult?>(new(batch,"Imported",2,1,1,2,0,0,Calls>1));}}
}
