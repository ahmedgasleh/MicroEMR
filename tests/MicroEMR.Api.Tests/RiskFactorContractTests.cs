using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientRiskFactors;
using MicroEMR.Core.Domain;
using Xunit;
using RiskApi = MicroEMR.Api.Controllers.PatientRiskFactorsController;
using RiskWeb = MicroEMR.Web.Controllers.PatientRiskFactorsController;

namespace MicroEMR.Api.Tests;

public sealed class RiskFactorContractTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("N", true)]
    [InlineData("I", true)]
    [InlineData("C", true)]
    [InlineData("T", true)]
    [InlineData("A", true)]
    [InlineData("UK", false)]
    [InlineData("a", false)]
    [InlineData("X", false)]
    public void RecordedLifeStageUsesExactCt016AndUnrecordedHasNoDefault(string? code, bool valid)
    {
        var request = RiskFactorSqlTests.Draft(); request.LifeStageCode = code;
        var errors = new List<ValidationResult>();
        Assert.Equal(valid, Validator.TryValidateObject(request, new(request), errors, true));
        Assert.Equal("Not recorded", FamilyHistoryLifeStages.Display(null));
    }

    [Theory]
    [InlineData("RiskFactor")]
    [InlineData("ExposureDetails")]
    [InlineData("Notes")]
    public void FieldLengthsAreValidated(string field)
    {
        var request = RiskFactorSqlTests.Draft();
        typeof(SavePatientRiskFactorRequest).GetProperty(field)!.SetValue(request, new string('x', field == "RiskFactor" ? 201 : 1001));
        var errors = new List<ValidationResult>(); Assert.False(Validator.TryValidateObject(request, new(request), errors, true));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void RiskFactorIsRequiredButOtherUnknownFieldsAreOptional(string blank)
    {
        var errors = new List<ValidationResult>();
        var invalid = new CreatePatientRiskFactorRequest { RiskFactor=blank };
        Assert.False(Validator.TryValidateObject(invalid, new(invalid),errors,true));
        var request = new CreatePatientRiskFactorRequest { RiskFactor="TEST ONLY - exposure" };
        Assert.True(Validator.TryValidateObject(request,new(request),[],true));
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad")]
    [InlineData("AQ==")]
    public void MalformedRowVersionCannotReachSql(string token) => Assert.Throws<ArgumentException>(() => PatientRiskFactorService.ValidateVersion(token));

    [Fact]
    public async Task ReadOnlyCallerCannotMutateThroughServiceOrController()
    {
        var calls=0;
        var repo=Stub<IPatientRiskFactorRepository>((_,_)=>{calls++;throw new Exception("Must not access SQL");});
        var service=new PatientRiskFactorService(repo,Permissions(PermissionKeys.PatientsView));
        var patient=Guid.NewGuid();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.CreateAsync(patient,RiskFactorSqlTests.Draft(),7));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.UpdateAsync(patient,Guid.NewGuid(),new(),7));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.ArchiveAsync(patient,Guid.NewGuid(),"bad",7));
        var controller=new RiskApi(service) { ControllerContext=new() { HttpContext=new DefaultHttpContext() } };
        ClinicalUserActorContext.Set(controller.HttpContext,7);
        Assert.IsType<ForbidResult>(await controller.Create(patient,RiskFactorSqlTests.Draft(),default));
        Assert.Equal(0,calls);
    }

    [Fact]
    public async Task ReadPreservesPatientsViewAndRejectsForeignRepositoryResults()
    {
        var patient=Guid.NewGuid();var uid=Guid.NewGuid();var foreign=false;
        var service=new PatientRiskFactorService(Stub<IPatientRiskFactorRepository>((_,_)=>Task.FromResult<PatientRiskFactorResponse?>(new() {PatientUid=foreign?Guid.NewGuid():patient,RiskFactorUid=uid})),Permissions(PermissionKeys.PatientsView));
        Assert.NotNull(await service.GetAsync(patient,uid)); foreign=true;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.GetAsync(patient,uid));
        var denied=new PatientRiskFactorService(null!,Permissions());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>denied.GetAsync(patient,uid));
    }

    [Fact]
    public async Task MissingPatientMapsTo404AndOnlyTrustedActorReachesWriteContract()
    {
        var service=Stub<IPatientRiskFactorService>((_,_)=>throw new PatientRiskFactorNotFoundException());
        var controller=new RiskApi(service); Assert.IsType<NotFoundResult>(await controller.List(Guid.NewGuid()));
        foreach(var type in new[]{typeof(CreatePatientRiskFactorRequest),typeof(UpdatePatientRiskFactorRequest),typeof(ArchivePatientRiskFactorRequest)})
            Assert.DoesNotContain(type.GetProperties(),x=>x.Name.Contains("Actor")||x.Name.Contains("Tenant")||x.Name.Contains("CreatedBy"));
    }

    [Fact]
    public void ApiAndWebHaveIndependentReadWriteAndAntiforgeryBoundaries()
    {
        foreach(var type in new[]{typeof(RiskApi),typeof(RiskWeb)})
        {
            Assert.Contains(type.GetCustomAttributes<AuthorizeAttribute>(),x=>x.Policy?.EndsWith(PermissionKeys.PatientsView,StringComparison.Ordinal)==true);
            foreach(var name in new[]{"Create","Update","Archive"})
            {
                var method=type.GetMethod(name)!;
                Assert.Contains(method.GetCustomAttributes<AuthorizeAttribute>(),x=>x.Policy?.EndsWith(PermissionKeys.ClinicalDataManage,StringComparison.Ordinal)==true);
                if(type==typeof(RiskWeb))Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
            }
        }
    }

    private static ICurrentUserPermissionService Permissions(params string[] keys) => Stub<ICurrentUserPermissionService>((_,_)=>Task.FromResult<IReadOnlySet<string>>(keys.ToHashSet()));
    private static T Stub<T>(Func<string,object?[],object> call) where T:class
    {var proxy=DispatchProxy.Create<T,CppSelectivePrintTests.Proxy>();((CppSelectivePrintTests.Proxy)(object)proxy).Call=call;return proxy;}
}
