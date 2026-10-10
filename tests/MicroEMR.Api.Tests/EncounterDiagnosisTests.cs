using System.Reflection;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PatientEncounters;
using MicroEMR.Application.PatientEncounters.Contracts;
using MicroEMR.Application.PatientEncounters.Repositories;
using MicroEMR.Application.ReadAudit;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class EncounterDiagnosisTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task OneRepositoryOperationPreservesTwoDiscreteDiagnosesAndExplicitDestination(bool cpp)
    {
        var f=new Fixture();
        var result=await f.Service.SaveAsync(f.Patient,f.Encounter,new() {RowVersion=Version,SaveToCpp=cpp,Diagnoses=[new() {Name=" A ",Description=" details "},new() {Name="B"}]});
        Assert.Equal(2,result!.Diagnoses.Count);Assert.Equal(1,f.Saves);Assert.Equal(cpp,f.Input!.SaveToCpp);
        Assert.Equal("A",f.Input.Diagnoses[0].Name);Assert.Equal("details",f.Input.Diagnoses[0].Description);Assert.Equal(7,f.Actor);
    }
    [Fact]
    public async Task ReadAuditsAndDerivesDestinationPermissions()
    {
        var f=new Fixture();f.Access.Remove(PermissionKeys.ClinicalDataManage);
        var result=await f.Service.GetAsync(f.Patient,f.Encounter,"trace");
        Assert.True(result!.CanEdit);Assert.False(result.CanSaveToCpp);Assert.Equal(1,f.Audits);
    }
    [Theory]
    [InlineData("permission")] [InlineData("cpp-permission")] [InlineData("patient-permission")]
    [InlineData("actor")] [InlineData("signed")] [InlineData("ownership")]
    public async Task UnauthorizedOrSignedSaveCannotReachPersistence(string failure)
    {
        var f=new Fixture();
        if(failure=="permission")f.Access.Remove(PermissionKeys.EncountersEdit);
        if(failure=="cpp-permission")f.Access.Remove(PermissionKeys.ClinicalDataManage);
        if(failure=="patient-permission")f.Access.Remove(PermissionKeys.PatientsView);
        f.Failure=failure;
        await Assert.ThrowsAnyAsync<Exception>(()=>f.Service.SaveAsync(f.Patient,f.Encounter,new() {RowVersion=Version,SaveToCpp=true,Diagnoses=[new() {Name="A"}]}));
        Assert.Equal(0,f.Saves);
    }
    [Fact]
    public async Task EncounterOnlyIsAllowedWithoutProblemsMutationPermission()
    {
        var f=new Fixture();f.Access.Remove(PermissionKeys.ClinicalDataManage);f.Access.Remove(PermissionKeys.PatientsView);
        await f.Service.SaveAsync(f.Patient,f.Encounter,new() {RowVersion=Version,Diagnoses=[new() {Name="A"}]});Assert.Equal(1,f.Saves);
    }
    [Theory]
    [InlineData("version")] [InlineData("blank")] [InlineData("length")] [InlineData("duplicate")] [InlineData("uid")]
    public void InvalidInputsFailBeforePersistence(string failure)
    {
        var request=new SaveEncounterDiagnosesRequest {RowVersion=Version,Diagnoses=[new() {Name="A"}]};
        if(failure=="version")request.RowVersion="invalid";
        if(failure=="blank")request.Diagnoses[0].Name=" ";
        if(failure=="length")request.Diagnoses[0].Description=new string('x',1001);
        if(failure=="duplicate")request.Diagnoses=[new() {Name="A"},new() {Name=" a "}];
        if(failure=="uid")request.Diagnoses[0].DiagnosisUid=Guid.Empty;
        Assert.Throws<ArgumentException>(()=>EncounterDiagnosisService.Validate(request));
    }
    [Fact]
    public async Task ReadAuditFailureAndWrongRepositoryResponseAreNotDisclosed()
    {
        var f=new Fixture {Failure="audit"};await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.GetAsync(f.Patient,f.Encounter,"trace"));Assert.Equal(0,f.Reads);
        f.Failure="response";await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service.GetAsync(f.Patient,f.Encounter,"trace"));
    }
    [Fact]
    public void WebMutationRequiresAntiforgeryAndApiRemainsAuthenticated()
    {
        Assert.NotNull(typeof(MicroEMR.Web.Controllers.EncounterDiagnosesController).GetMethod("Save")!.GetCustomAttribute<Microsoft.AspNetCore.Mvc.ValidateAntiForgeryTokenAttribute>());
        Assert.NotEmpty(typeof(MicroEMR.Api.Controllers.EncounterDiagnosesController).GetCustomAttributes<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>());
    }
    private const string Version="AQIDBAUGBwg=";
    private sealed class Fixture
    {
        public Guid Patient=Guid.NewGuid(),Encounter=Guid.NewGuid();public int Saves,Reads,Audits;public long Actor;public string Failure="";
        public SaveEncounterDiagnosesRequest? Input;
        public HashSet<string> Access=[PermissionKeys.EncountersView,PermissionKeys.EncountersEdit,PermissionKeys.PatientsView,PermissionKeys.ClinicalDataManage];
        public EncounterDiagnosisService Service;
        public Fixture()
        {
            Service=new(Stub<IEncounterDiagnosisRepository>((method,args)=>
            {
                IReadOnlyList<EncounterDiagnosis> rows=[];
                if(method=="SaveAsync") {Saves++;Input=(SaveEncounterDiagnosesRequest)args[2]!;Actor=(long)args[3]!;rows=Input.Diagnoses.Select(x=>new EncounterDiagnosis(Guid.NewGuid(),x.Name,x.Description,x.OnsetDate,null)).ToArray();}else Reads++;
                return Task.FromResult<EncounterDiagnosesResponse?>(new(Failure=="response"?Guid.NewGuid():Patient,Encounter,Version,"Open",rows));
            }),Stub<IPatientEncounterRepository>((_,_)=>Task.FromResult<PatientEncounterDetailsResponse?>(new() {PatientUid=Failure=="ownership"?Guid.NewGuid():Patient,EncounterUid=Encounter,Status=Failure=="signed"?"Signed":"Open"})),
            Stub<ICurrentUserPermissionService>((_,_)=>Task.FromResult<IReadOnlySet<string>>(Access)),
            Stub<IAuthenticatedClinicalUserAccessor>((_,_)=>Failure=="actor"?throw new ClinicalUserResolutionException("Unavailable"):Task.FromResult(7L)),
            Stub<IPatientChartReadAuditService>((_,args)=> {if(Failure=="audit")throw new InvalidOperationException();Assert.Equal("trace",args[1]);Audits++;return Task.FromResult(Guid.NewGuid());}));
        }
    }
    public class Proxy:DispatchProxy {public Func<string,object?[],object> Call=null!;protected override object Invoke(MethodInfo? method,object?[]? args)=>Call(method!.Name,args!);}
    private static T Stub<T>(Func<string,object?[],object> call) where T:class {var value=DispatchProxy.Create<T,Proxy>();((Proxy)(object)value).Call=call;return value;}
}
