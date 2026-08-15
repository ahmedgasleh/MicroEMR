using System.ComponentModel.DataAnnotations;
using System.Reflection;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientClinicalHistory;
using MicroEMR.Infrastructure.PatientClinicalHistory;
using MicroEMR.Infrastructure.Tenancy;
using Xunit;
using ApiController = MicroEMR.Api.Controllers.PatientClinicalHistoryController;
using WebController = MicroEMR.Web.Controllers.PatientClinicalHistoryController;

namespace MicroEMR.Api.Tests;

public sealed class CumulativePatientProfileCertificationTests
{
    [Fact]
    public void MedicalAndSurgicalHistoryValidationIsStructuredAndRejectsInvalidValues()
    {
        Assert.Empty(Validate(new CreatePatientClinicalHistoryRequest { HistoryType="Medical",Description="Childhood asthma",RelevantDate=new(1990,1,1) }));
        Assert.Empty(Validate(new CreatePatientClinicalHistoryRequest { HistoryType="Surgical",Description="Appendectomy",RelevantDate=new(2001,2,3) }));
        var invalidDescription=new CreatePatientClinicalHistoryRequest { HistoryType="Medical",Description=" " };
        var invalidValues=new CreatePatientClinicalHistoryRequest { HistoryType="Other",Description="Recorded value",RelevantDate=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1) };
        Assert.Contains(Validate(invalidDescription),x=>x.MemberNames.Contains(nameof(invalidDescription.Description)));
        var errors=Validate(invalidValues);
        Assert.Contains(errors,x=>x.MemberNames.Contains(nameof(invalidValues.HistoryType)));
        Assert.Contains(errors,x=>x.MemberNames.Contains(nameof(invalidValues.RelevantDate)));
    }

    [Fact]
    public async Task ServicePreservesPatientActorStatusAndConcurrencyContract()
    {
        var repository=new Repository();var service=new PatientClinicalHistoryService(repository);var patient=Guid.NewGuid();
        await service.CreateAsync(patient,new(){HistoryType="Medical",Description="Asthma"},73);
        await service.UpdateAsync(patient,repository.Item.HistoryUid,new(){HistoryType="Surgical",Description="Appendectomy",RowVersion=repository.Item.RowVersion},74);
        await service.ArchiveAsync(patient,repository.Item.HistoryUid,repository.Item.RowVersion,75);
        Assert.Equal(patient,repository.Item.PatientUid);Assert.Equal([73L,74L,75L],repository.Actors);Assert.Equal("Archived",repository.Item.Status);
        await Assert.ThrowsAsync<PatientClinicalHistoryConcurrencyException>(()=>repository.ArchiveAsync(patient,repository.Item.HistoryUid,"stale",76));
    }

    [Fact]
    public void ApiAndWebUsePatientViewAndClinicalManagePermissions()
    {
        AssertPermission(typeof(ApiController),PermissionKeys.PatientsView);AssertPermission(typeof(WebController),PermissionKeys.PatientsView);
        foreach(var name in new[]{nameof(ApiController.Create),nameof(ApiController.Update),nameof(ApiController.Archive)})AssertPermission(typeof(ApiController).GetMethod(name)!,PermissionKeys.ClinicalDataManage);
        foreach(var name in new[]{nameof(WebController.Create),nameof(WebController.Update),nameof(WebController.Archive)})AssertPermission(typeof(WebController).GetMethod(name)!,PermissionKeys.ClinicalDataManage);
        Assert.Contains(typeof(ITenantSqlConnectionFactory),Assert.Single(typeof(PatientClinicalHistoryRepository).GetConstructors()).GetParameters().Select(x=>x.ParameterType));
    }

    [Fact]
    public void MigrationIsPatientScopedStructuredConcurrentRetainedAndAudited()
    {
        var sql=File.ReadAllText(Path.Combine(Root(),"db","tenant-clinical","migrations","0041-patient-clinical-history.sql"));
        Assert.Contains("HistoryType IN (N'Medical', N'Surgical')",sql);Assert.Contains("Status IN (N'Active', N'Archived')",sql);
        Assert.Contains("FOREIGN KEY (PatientUid)",sql);Assert.Contains("WHERE h.PatientUid = @PatientUid AND h.HistoryUid = @HistoryUid",sql);
        Assert.Equal(2,Count(sql,"WITH (UPDLOCK, HOLDLOCK)"));Assert.Equal(2,Count(sql,"@CurrentVersion <> @ExpectedRowVersion"));
        Assert.Equal(3,Count(sql,"INSERT dbo.AuditLog"));Assert.Contains("OldValue, NewValue",sql);Assert.Contains("@Actor",sql);
        Assert.DoesNotContain("DELETE FROM dbo.PatientClinicalHistory",sql,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExistingCppSourcesRemainAuthoritativeAndHistoryHasSummaryAndEmptyState()
    {
        var view=File.ReadAllText(Path.Combine(Root(),"src","MicroEMR.Web","Views","Patients","Details.cshtml"));
        var script=File.ReadAllText(Path.Combine(Root(),"src","MicroEMR.Web","ClientApp","patients","patient-clinical-history.ts"));
        Assert.Contains("Active Problems",view);Assert.Contains("Active Allergies",view);Assert.Contains("Active Medications",view);
        Assert.Contains("Past Medical and Surgical History",view);Assert.Contains("clinicalHistorySummary",view);
        Assert.Contains("No past medical or surgical history recorded.",script);Assert.Contains("data-can-manage",view);
    }

    private static IReadOnlyList<ValidationResult>Validate(object value){var x=new List<ValidationResult>();Validator.TryValidateObject(value,new ValidationContext(value),x,true);return x;}
    private static void AssertPermission(MemberInfo member,string permission)=>Assert.Contains(member.GetCustomAttributes(true).OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(),x=>x.Policy?.EndsWith(permission,StringComparison.Ordinal)==true);
    private static int Count(string value,string token)=>(value.Length-value.Replace(token,"",StringComparison.OrdinalIgnoreCase).Length)/token.Length;
    private static string Root([System.Runtime.CompilerServices.CallerFilePath]string source="")=>Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!,"..",".."));

    private sealed class Repository:IPatientClinicalHistoryRepository
    {
        public List<long>Actors{get;}=[];public PatientClinicalHistoryResponse Item{get;private set;}=new(){HistoryUid=Guid.NewGuid(),PatientUid=Guid.Empty,HistoryType="Medical",Description="",Status="Active",CreatedBy=0,RowVersion=Convert.ToBase64String(new byte[8])};
        public Task<IReadOnlyList<PatientClinicalHistoryResponse>>ListAsync(Guid p,string s,CancellationToken c=default)=>Task.FromResult<IReadOnlyList<PatientClinicalHistoryResponse>>([Item]);
        public Task<PatientClinicalHistoryResponse>CreateAsync(Guid p,CreatePatientClinicalHistoryRequest x,long a,CancellationToken c=default){Actors.Add(a);Item=New(p,x.HistoryType,x.Description,"Active",a,1);return Task.FromResult(Item);}
        public Task<PatientClinicalHistoryResponse?>UpdateAsync(Guid p,Guid h,UpdatePatientClinicalHistoryRequest x,long a,CancellationToken c=default){Check(x.RowVersion);Actors.Add(a);Item=New(p,x.HistoryType,x.Description,"Active",a,2);return Task.FromResult<PatientClinicalHistoryResponse?>(Item);}
        public Task<PatientClinicalHistoryResponse?>ArchiveAsync(Guid p,Guid h,string v,long a,CancellationToken c=default){Check(v);Actors.Add(a);Item=New(p,Item.HistoryType,Item.Description,"Archived",a,3);return Task.FromResult<PatientClinicalHistoryResponse?>(Item);}
        private void Check(string v){if(v!=Item.RowVersion)throw new PatientClinicalHistoryConcurrencyException();}
        private PatientClinicalHistoryResponse New(Guid p,string t,string d,string s,long a,byte v)=>new(){HistoryUid=Item.HistoryUid,PatientUid=p,HistoryType=t,Description=d,Status=s,CreatedAt=DateTime.UtcNow,CreatedBy=a,RowVersion=Convert.ToBase64String([0,0,0,0,0,0,0,v])};
    }
}
