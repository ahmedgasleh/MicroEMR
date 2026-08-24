using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientImmunizations;
using MicroEMR.Infrastructure.PatientImmunizations;
using MicroEMR.Infrastructure.Tenancy;
using Xunit;
using ApiController=MicroEMR.Api.Controllers.PatientImmunizationsController;
using WebController=MicroEMR.Web.Controllers.PatientImmunizationsController;

namespace MicroEMR.Api.Tests;

public sealed class BasicImmunizationHistoryTests
{
    [Fact]
    public void RequestsEnforceCoreClinicalValidation()
    {
        var valid=new CreatePatientImmunizationRequest{VaccineName="Influenza",AdministrationDate=DateOnly.FromDateTime(DateTime.UtcNow),SourceType="ClinicAdministered",AdministeredByName="Dr Example",DoseNumber=1};
        Assert.Empty(Validate(valid));
        var historical=new CreatePatientImmunizationRequest{VaccineName="MMR",AdministrationDate=new(2000,1,1),SourceType="HistoricalExternal"};
        Assert.Empty(Validate(historical));
        var required=Validate(new CreatePatientImmunizationRequest{VaccineName=" ",AdministrationDate=new(2020,1,1),SourceType="HistoricalExternal"});
        Assert.Contains(required,x=>x.MemberNames.Contains(nameof(valid.VaccineName)));
        var clinical=Validate(new CreatePatientImmunizationRequest{VaccineName="Influenza",AdministrationDate=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1),SourceType="ClinicAdministered"});
        Assert.Contains(clinical,x=>x.MemberNames.Contains(nameof(valid.AdministrationDate)));
        Assert.Contains(clinical,x=>x.MemberNames.Contains(nameof(valid.AdministeredByName)));
        var dose=Validate(new CreatePatientImmunizationRequest{VaccineName="Influenza",AdministrationDate=new(2020,1,1),SourceType="HistoricalExternal",DoseNumber=0});
        Assert.Contains(dose,x=>x.MemberNames.Contains(nameof(valid.DoseNumber)));
    }

    [Fact]
    public async Task ServicePreservesPatientActorLifecycleAndTerminalState()
    {
        var repository=new Repository();var service=new PatientImmunizationService(repository);var patient=Guid.NewGuid();
        var created=await service.CreateAsync(patient,new(){VaccineName="Influenza",AdministrationDate=new(2020,1,2),SourceType="HistoricalExternal"},41);
        var updated=await service.UpdateAsync(patient,created.ImmunizationUid,new(){VaccineName="Influenza vaccine",AdministrationDate=new(2020,1,2),SourceType="HistoricalExternal",RowVersion=created.RowVersion},42);
        var terminal=await service.MarkEnteredInErrorAsync(patient,created.ImmunizationUid,new(){Reason="Duplicate source record",RowVersion=updated!.RowVersion},43);
        Assert.Equal([41L,42L,43L],repository.Actors);Assert.Equal("EnteredInError",terminal!.Status);Assert.Equal(patient,terminal.PatientUid);
        await Assert.ThrowsAsync<PatientImmunizationTerminalException>(()=>repository.UpdateAsync(patient,created.ImmunizationUid,new(){VaccineName="No",AdministrationDate=new(2020,1,2),SourceType="HistoricalExternal",RowVersion=terminal.RowVersion},44));
    }

    [Fact]
    public void ApiWebRepositoryAndUiUseEstablishedSecurityPatterns()
    {
        AssertPermission(typeof(ApiController),PermissionKeys.PatientsView);AssertPermission(typeof(WebController),PermissionKeys.PatientsView);
        foreach(var method in new[]{nameof(ApiController.Create),nameof(ApiController.Update),nameof(ApiController.MarkEnteredInError)})AssertPermission(typeof(ApiController).GetMethod(method)!,PermissionKeys.ClinicalDataManage);
        foreach(var method in new[]{nameof(WebController.Create),nameof(WebController.Update),nameof(WebController.MarkEnteredInError)})AssertPermission(typeof(WebController).GetMethod(method)!,PermissionKeys.ClinicalDataManage);
        Assert.Contains(typeof(ITenantSqlConnectionFactory),Assert.Single(typeof(PatientImmunizationRepository).GetConstructors()).GetParameters().Select(x=>x.ParameterType));
        var view=File.ReadAllText(Path.Combine(Root(),"src","MicroEMR.Web","Views","Patients","Details.cshtml"));
        Assert.Contains("Patient Chart",view);Assert.Contains("Immunizations",view);Assert.Contains("data-can-manage",view);Assert.Contains("Mark entered in error",view);Assert.DoesNotContain("Delete Immunization",view);
    }

    [Fact]
    public void MigrationIsGovernedPatientScopedConcurrentAuditedAndRetained()
    {
        var sql=Sql();
        Assert.Contains("SourceType IN (N'ClinicAdministered', N'HistoricalExternal')",sql);
        Assert.Contains("Status IN (N'Completed', N'EnteredInError')",sql);
        Assert.Contains("DoseNumber IS NULL OR DoseNumber > 0",sql);
        Assert.Contains("WHERE i.PatientUid = @PatientUid AND i.ImmunizationUid = @ImmunizationUid",sql);
        Assert.Equal(2,Count(sql,"WITH (UPDLOCK,HOLDLOCK)"));Assert.Equal(2,Count(sql,"@CurrentVersion<>@ExpectedRowVersion"));
        Assert.Equal(3,Count(sql,"INSERT dbo.AuditLog"));
        Assert.Contains("ImmunizationCreated",sql);Assert.Contains("ImmunizationUpdated",sql);Assert.Contains("ImmunizationEnteredInError",sql);
        Assert.DoesNotContain("DELETE FROM dbo.PatientImmunization",sql,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Notes AS Notes FOR JSON",sql,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MigrationIsExactlyNextAndManifestedOnce()
    {
        var root=Root();var manifest=File.ReadAllText(Path.Combine(root,"db","tenant-clinical","manifest.json"));
        Assert.Equal(1,Count(manifest,"\"migrationId\": \"0047-patient-immunization-history\""));
        Assert.True(File.Exists(Path.Combine(root,"db","tenant-clinical","migrations","0047-patient-immunization-history.sql")));
        Assert.False(File.Exists(Path.Combine(root,"db","tenant-clinical","migrations","0048-patient-immunization-history.sql")));
        Assert.Contains("IX_PatientImmunization_Patient_Status_Date",Sql());
    }

    [Fact]
    public void ScopeExcludesDeferredClinicalAndIntegrationFeatures()
    {
        var sql=Sql();var code=File.ReadAllText(Path.Combine(Root(),"src","MicroEMR.Application","PatientImmunizations","PatientImmunizationModels.cs"));
        foreach(var term in new[]{"Manufacturer","ExpiryDate","VaccineCode","NextDose","Refused","NotGiven","DHIR"}){Assert.DoesNotContain(term,sql,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain(term,code,StringComparison.OrdinalIgnoreCase);}
    }

    private static IReadOnlyList<ValidationResult> Validate(object value){var results=new List<ValidationResult>();Validator.TryValidateObject(value,new ValidationContext(value),results,true);return results;}
    private static void AssertPermission(MemberInfo member,string permission)=>Assert.Contains(member.GetCustomAttributes(true).OfType<AuthorizeAttribute>(),x=>x.Policy?.EndsWith(permission,StringComparison.Ordinal)==true);
    private static string Sql()=>File.ReadAllText(Path.Combine(Root(),"db","tenant-clinical","migrations","0047-patient-immunization-history.sql"));
    private static int Count(string value,string token)=>(value.Length-value.Replace(token,"",StringComparison.OrdinalIgnoreCase).Length)/token.Length;
    private static string Root([System.Runtime.CompilerServices.CallerFilePath]string source="")=>Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!,"..",".."));

    private sealed class Repository:IPatientImmunizationRepository
    {
        public List<long>Actors{get;}=[];private PatientImmunizationResponse? item;private byte version;
        public Task<IReadOnlyList<PatientImmunizationResponse>>ListAsync(Guid p,string s,CancellationToken t=default)=>Task.FromResult<IReadOnlyList<PatientImmunizationResponse>>(item is null?[]:[item]);
        public Task<PatientImmunizationResponse?>GetAsync(Guid p,Guid u,CancellationToken t=default)=>Task.FromResult(item?.PatientUid==p&&item.ImmunizationUid==u?item:null);
        public Task<PatientImmunizationResponse>CreateAsync(Guid p,CreatePatientImmunizationRequest r,long a,CancellationToken t=default){Actors.Add(a);item=New(p,r.VaccineName,"Completed",a);return Task.FromResult(item);}
        public Task<PatientImmunizationResponse?>UpdateAsync(Guid p,Guid u,UpdatePatientImmunizationRequest r,long a,CancellationToken t=default){Check(r.RowVersion);if(item!.Status!="Completed")throw new PatientImmunizationTerminalException();Actors.Add(a);item=New(p,r.VaccineName,"Completed",a);return Task.FromResult<PatientImmunizationResponse?>(item);}
        public Task<PatientImmunizationResponse?>MarkEnteredInErrorAsync(Guid p,Guid u,MarkImmunizationEnteredInErrorRequest r,long a,CancellationToken t=default){Check(r.RowVersion);if(item!.Status!="Completed")throw new PatientImmunizationTerminalException();Actors.Add(a);item=New(p,item.VaccineName,"EnteredInError",a);return Task.FromResult<PatientImmunizationResponse?>(item);}
        private void Check(string v){if(item is null||v!=item.RowVersion)throw new PatientImmunizationConcurrencyException();}
        private PatientImmunizationResponse New(Guid p,string name,string status,long actor){version++;return new(){ImmunizationUid=item?.ImmunizationUid??Guid.NewGuid(),PatientUid=p,VaccineName=name,AdministrationDate=new(2020,1,2),SourceType="HistoricalExternal",Status=status,CreatedAtUtc=DateTime.UtcNow,CreatedBy=actor,RowVersion=Convert.ToBase64String([0,0,0,0,0,0,0,version])};}
    }
}
