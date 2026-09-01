using System.Reflection;
using System.Text.Json;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.Cdm;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class CdmEnrollmentFoundationTests
{
    private static readonly string Migration=File.ReadAllText(Path.Combine(Root(),"db","tenant-clinical","migrations","0053-cdm-enrollment-foundation.sql"));

    [Fact] public void Migration0053IsCanonicalAndExactlyOnce()
    {
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(),"db","tenant-clinical","manifest.json")));
        var entries=json.RootElement.EnumerateArray().ToArray();
        Assert.Equal("0053-cdm-enrollment-foundation",entries[^4].GetProperty("migrationId").GetString());
        Assert.Equal("0054-results-provenance-correction-foundation",entries[^3].GetProperty("migrationId").GetString());
        Assert.Single(entries,x=>x.GetProperty("migrationId").GetString()=="0053-cdm-enrollment-foundation");
        Assert.False(File.Exists(Path.Combine(Root(),"db","tenant-clinical","migrations","0054-cdm-enrollment-foundation.sql")));
    }

    [Fact] public void SchemaIsPatientProblemScopedVersionedAndNeverDeletes()
    {
        Assert.Contains("CREATE TABLE dbo.ChronicDiseaseEnrollment",Migration);
        Assert.Contains("FOREIGN KEY (PatientUid) REFERENCES dbo.Patient(PatientUid)",Migration);
        Assert.Contains("FOREIGN KEY (PatientProblemUid) REFERENCES dbo.PatientProblem(PatientProblemUid)",Migration);
        Assert.Contains("ProgramVersion INT NOT NULL",Migration);
        Assert.Contains("Status IN (N'Active', N'Inactive')",Migration);
        Assert.Contains("RowVersion ROWVERSION",Migration);
        Assert.Contains("WHERE Status = N'Active'",Migration);
        Assert.Contains("PatientUid=@PatientUid AND e.ChronicDiseaseEnrollmentUid=@ChronicDiseaseEnrollmentUid",Migration);
        Assert.DoesNotContain("DELETE FROM dbo.ChronicDiseaseEnrollment",Migration,StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public void ProblemOwnershipDuplicateConcurrencyAndAtomicMinimalAuditAreEnforced()
    {
        Assert.Contains("PatientUid=@PatientUid AND PatientProblemUid=@PatientProblemUid AND ProblemStatus=N'Active'",Migration);
        Assert.Contains("PatientUid=@PatientUid AND ProgramKey=@ProgramKey AND Status=N'Active'",Migration);
        Assert.Contains("RowVersion=@RowVersion",Migration);
        Assert.Contains("N'CdmEnrollmentCreated'",Migration);
        Assert.Contains("N'CdmEnrollmentInactivated'",Migration);
        Assert.Equal(2,Migration.Split("INSERT dbo.AuditLog",StringSplitOptions.None).Length-1);
        Assert.DoesNotContain("ProblemName",Migration[Migration.IndexOf("INSERT dbo.AuditLog",StringComparison.Ordinal)..]);
    }

    [Fact] public void ProductionRegistryIsEmptySyntheticDefinitionIsTestOnlyAndDuplicatesFail()
    {
        Assert.Empty(new CdmProgramRegistry([]).Programs);
        var synthetic=new SyntheticProgram(); var registry=new CdmProgramRegistry([synthetic]);
        Assert.Equal("TEST_CDM_PROGRAM",Assert.Single(registry.Programs).ProgramKey);
        Assert.Throws<InvalidOperationException>(()=>new CdmProgramRegistry([synthetic,new SyntheticProgram()]));
        var application=File.ReadAllText(Path.Combine(Root(),"src","MicroEMR.Application","DependencyInjection.cs"));
        var api=File.ReadAllText(Path.Combine(Root(),"src","MicroEMR.Api","Program.cs"));
        Assert.DoesNotContain("AddSingleton<ICdmProgramDefinition",application);
        Assert.DoesNotContain("TEST_CDM_PROGRAM",application);
        Assert.DoesNotContain("TEST_CDM_PROGRAM",api);
        Assert.DoesNotContain("hypertension",application,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("diabetes",application,StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public async Task ExplicitEnrollmentRetainsActorProblemAndExactVersion()
    {
        var repository=new FakeRepository(); var service=new CdmEnrollmentService(new CdmProgramRegistry([new SyntheticProgram()]),repository);
        var patient=Guid.NewGuid();var problem=Guid.NewGuid();
        var result=await service.CreateAsync(patient,new(){PatientProblemUid=problem,ProgramKey="TEST_CDM_PROGRAM",ProgramVersion=1},42,default);
        Assert.Equal(patient,result.PatientUid);Assert.Equal(problem,result.PatientProblemUid);Assert.Equal(42,result.EnrolledBy);Assert.Equal(1,result.ProgramVersion);
        await Assert.ThrowsAsync<CdmEnrollmentValidationException>(()=>service.CreateAsync(patient,new(){PatientProblemUid=problem,ProgramKey="ARBITRARY",ProgramVersion=1},42,default));
    }

    [Fact] public void ApiUsesReadMutationPermissionsAndAcceptsNoActor()
    {
        var type=typeof(PatientCdmController);
        Assert.Contains(type.GetCustomAttributes<RequirePermissionAttribute>(),x=>x.Policy?.Contains(PermissionKeys.PatientsView)==true);
        foreach(var name in new[]{nameof(PatientCdmController.Enroll),nameof(PatientCdmController.Inactivate)})
            Assert.Contains(type.GetMethod(name)!.GetCustomAttributes<RequirePermissionAttribute>(),x=>x.Policy?.Contains(PermissionKeys.ClinicalDataManage)==true);
        Assert.Null(typeof(CreateCdmEnrollmentRequest).GetProperty("EnrolledBy"));
        Assert.Null(typeof(InactivateCdmEnrollmentRequest).GetProperty("InactivatedBy"));
    }

    [Fact] public void FoundationDoesNotModifyCdsCreateTasksOrAddMeasurements()
    {
        Assert.Null(typeof(CdmEnrollmentResponse).GetProperty("Target"));
        Assert.Null(typeof(CdmEnrollmentResponse).GetProperty("Measurement"));
        Assert.DoesNotContain("CdsAlert",Migration);
        Assert.DoesNotContain("PatientTask",Migration);
        var cdmFiles=Directory.GetFiles(Path.Combine(Root(),"src"),"*Cdm*.cs",SearchOption.AllDirectories).Select(File.ReadAllText);
        Assert.DoesNotContain(cdmFiles,x=>x.Contains("ICdsEvaluationService",StringComparison.Ordinal)||x.Contains("IPatientTaskRepository",StringComparison.Ordinal));
    }

    private sealed class SyntheticProgram:ICdmProgramDefinition { public CdmProgramMetadata Metadata=>new("TEST_CDM_PROGRAM",1,"Test CDM Program","Technical test program only."); }
    private sealed class FakeRepository:ICdmEnrollmentRepository
    {
        public Task<IReadOnlyList<CdmEnrollmentResponse>> ListAsync(Guid p,CancellationToken t)=>Task.FromResult<IReadOnlyList<CdmEnrollmentResponse>>([]);
        public Task<CdmEnrollmentResponse?> GetAsync(Guid p,Guid e,CancellationToken t)=>Task.FromResult<CdmEnrollmentResponse?>(null);
        public Task<CdmEnrollmentResponse> CreateAsync(Guid p,Guid problem,CdmProgramMetadata program,long actor,CancellationToken t)=>Task.FromResult(new CdmEnrollmentResponse{ChronicDiseaseEnrollmentUid=Guid.NewGuid(),PatientUid=p,PatientProblemUid=problem,ProblemName="Synthetic Problem",ProgramKey=program.ProgramKey,ProgramVersion=program.ProgramVersion,ProgramName=program.Name,Status="Active",EnrolledBy=actor,EnrolledAtUtc=DateTime.UtcNow,RowVersion="AAAAAAAAAAA="});
        public Task<CdmEnrollmentResponse?> InactivateAsync(Guid p,Guid e,byte[] row,string? reason,long actor,CancellationToken t)=>Task.FromResult<CdmEnrollmentResponse?>(null);
    }
    private static string Root([System.Runtime.CompilerServices.CallerFilePath]string source="")=>Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!,"..",".."));
}
