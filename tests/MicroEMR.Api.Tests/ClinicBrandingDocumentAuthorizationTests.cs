using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicConfiguration;
using Xunit;
using static MicroEMR.Api.Tests.ClinicBrandingTests;
using static MicroEMR.Api.Tests.ConsultationSigningTests;
using Operation=MicroEMR.Application.ClinicConfiguration.ClinicBrandingDocumentOperation;
using BrandingController=MicroEMR.Api.Controllers.ClinicBrandingController;

namespace MicroEMR.Api.Tests;

public sealed class ClinicBrandingDocumentAuthorizationTests
{
    private static HashSet<string> Access(string values)=>values.Split(',',StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
    private static Fixture Create(string values,DefaultHttpContext? context=null)=>new(effectivePermissions:Access(values),
        subject:new AuthenticatedSubjectAccessor(new HttpContextAccessor{HttpContext=context??new DefaultHttpContext{User=new(new ClaimsIdentity([new Claim("sub","document-reader")],"test"))}}));

    [Theory]
    [InlineData(Operation.ConsultationPreview,"Documents.View,Documents.Manage")]
    [InlineData(Operation.ConsultationSigning,"Documents.View,Documents.Sign")]
    public async Task Positive_ConsultationOperationsReuseDiagnosticCasesWithoutPatientOrAdministrationAccess(Operation operation,string keys)
    {
        var f=Create(keys);
        await f.Repository.SaveAsync(new([1,2,3],1,1),null,7);
        var header=await f.Service.GetPrintHeaderForDocumentAsync(operation,"Consultation Report");
        Assert.Equal("Tenant A Clinic",header.Clinic.Name);Assert.Equal("Consultation Report",header.DocumentTitle);
        Assert.Equal(new byte[]{1,2,3},header.Logo!.PngBytes);
        Assert.Equal(1,f.ConfigurationReads);Assert.Equal(1,f.Repository.Reads);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service.GetPrintHeaderAsync("General preview"));
    }

    [Fact]
    public async Task Positive_ReferralGenerationKeepsExistingPatientViewPrerequisiteAndNoLogoFallback()
    {
        var f=Create("Patients.View,Referrals.View,Referrals.Manage");
        var header=await f.Service.GetPrintHeaderForDocumentAsync(Operation.ReferralGeneration,"Referral Letter");
        Assert.Equal("Referral Letter",header.DocumentTitle);Assert.Null(header.Logo);
        Assert.Equal("Tenant A Clinic",header.Clinic.Name);
    }

    [Fact]
    public async Task Positive_TrustedTenantContextSelectsOnlyItsOwnLogoAndClinic()
    {
        var f=Create("Documents.View,Documents.Manage");var tenantA=f.Tenant.TenantUid;
        await f.Repository.SaveAsync(new([1],1,1),null,7);
        f.Tenant.TenantUid=Guid.NewGuid();f.Tenant.DisplayName="Tenant B Clinic";
        var emptyB=await f.Service.GetPrintHeaderForDocumentAsync(Operation.ConsultationPreview,"Report");Assert.Null(emptyB.Logo);
        await f.Repository.SaveAsync(new([2],1,1),null,7);
        var b=await f.Service.GetPrintHeaderForDocumentAsync(Operation.ConsultationPreview,"Report");
        Assert.Equal("Tenant B Clinic",b.Clinic.Name);Assert.Equal(new byte[]{2},b.Logo!.PngBytes);
        f.Tenant.TenantUid=tenantA;f.Tenant.DisplayName="Tenant A Clinic";
        Assert.Equal(new byte[]{1},(await f.Service.GetPrintHeaderForDocumentAsync(Operation.ConsultationPreview,"Report")).Logo!.PngBytes);
        var parameters=typeof(ClinicBrandingService).GetMethod(nameof(ClinicBrandingService.GetPrintHeaderForDocumentAsync))!.GetParameters();
        Assert.DoesNotContain(parameters,p=>p.ParameterType==typeof(Guid)||p.Name!.Contains("tenant",StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(Operation.ConsultationPreview,"")]
    [InlineData(Operation.ConsultationPreview,"Documents.View")]
    [InlineData(Operation.ConsultationPreview,"Documents.Manage")]
    [InlineData(Operation.ConsultationPreview,"Documents.View,Documents.Sign")]
    [InlineData(Operation.ConsultationSigning,"Documents.View")]
    [InlineData(Operation.ConsultationSigning,"Documents.Sign")]
    [InlineData(Operation.ConsultationSigning,"Documents.View,Documents.Manage")]
    [InlineData(Operation.ConsultationSigning,"ClinicSettings.Manage")]
    [InlineData(Operation.ReferralGeneration,"Referrals.View,Referrals.Manage")]
    [InlineData(Operation.ReferralGeneration,"Patients.View,Referrals.Manage")]
    [InlineData(Operation.ReferralGeneration,"Patients.View,Referrals.View")]
    [InlineData(Operation.ReferralGeneration,"Documents.View,Documents.Manage,Documents.Sign")]
    [InlineData((Operation)999,"Patients.View,Documents.View,Documents.Manage,Documents.Sign,Referrals.View,Referrals.Manage,ClinicSettings.Manage")]
    public async Task Negative_OperationRequiresEveryExistingPermissionBeforeReadingData(Operation operation,string keys)
    {
        var f=Create(keys);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service.GetPrintHeaderForDocumentAsync(operation,"Report"));
        Assert.Equal(0,f.ConfigurationReads);Assert.Equal(0,f.Repository.Reads);Assert.Equal(0,f.Repository.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Negative_MissingAuthenticatedRequestOrSubjectDeniesBeforeStorage(bool hasContext)
    {
        var f=new Fixture(effectivePermissions:Access("Documents.View,Documents.Manage"),
            subject:new AuthenticatedSubjectAccessor(new HttpContextAccessor{HttpContext=hasContext?new DefaultHttpContext():null}));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service.GetPrintHeaderForDocumentAsync(Operation.ConsultationPreview,"Report"));
        Assert.Equal(0,f.ConfigurationReads);Assert.Equal(0,f.Repository.Reads);
    }

    [Fact]
    public async Task Negative_MissingTrustedTenantDeniesEvenWithDocumentPermissions()
    {
        var f=Create("Documents.View,Documents.Manage");f.Tenant.TenantUid=Guid.Empty;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service.GetPrintHeaderForDocumentAsync(Operation.ConsultationPreview,"Report"));
        Assert.Equal(0,f.ConfigurationReads);Assert.Equal(0,f.Repository.Reads);
    }

    [Theory]
    [InlineData("Documents.View,Documents.Manage")]
    [InlineData("Documents.View,Documents.Sign")]
    public async Task Negative_DocumentReadersCannotUploadReplaceRemoveOrReadAdministrationState(string keys)
    {
        var f=Create(keys);var saved=await f.Repository.SaveAsync(new([1],1,1),null,7);var writes=f.Repository.Writes;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service.SaveLogoAsync([2],false,null));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service.SaveLogoAsync([2],false,saved.RowVersion));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service.SaveLogoAsync(null,true,saved.RowVersion));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service.GetStateAsync());
        Assert.Equal(writes,f.Repository.Writes);Assert.Equal(0,f.DecodeCalls);
    }

    [Theory]
    [InlineData("Documents.View,Documents.Manage")]
    [InlineData("Documents.View,Documents.Sign")]
    public async Task Negative_ServerSideMethodIsNotExposedByGeneralBrandingApi(string keys)
    {
        var f=Create(keys);var controller=new BrandingController(f.Service,NullLogger<BrandingController>.Instance)
            {ControllerContext=new(){HttpContext=new DefaultHttpContext()}};
        Assert.Equal(403,Assert.IsType<ObjectResult>(await controller.Header(default)).StatusCode);
        Assert.Equal(403,Assert.IsType<ObjectResult>(await controller.Logo(default)).StatusCode);
        Assert.Equal(403,Assert.IsType<ObjectResult>(await controller.State(default)).StatusCode);
        Assert.DoesNotContain(typeof(BrandingController).GetMethods(),m=>m.GetParameters().Any(p=>p.ParameterType==typeof(Operation)));
        Assert.Equal(0,f.ConfigurationReads);Assert.Equal(0,f.Repository.Reads);
    }

    [Fact]
    public async Task Negative_BrandingFailurePropagatesAndNeverSkipsOperationAuthorization()
    {
        var access=Access("Documents.View,Documents.Manage");var f=new Fixture(effectivePermissions:access);
        f.ConfigurationFailure=new IOException("Private diagnostic test detail");
        await Assert.ThrowsAsync<IOException>(()=>f.Service.GetPrintHeaderForDocumentAsync(Operation.ConsultationPreview,"Report"));
        Assert.Equal(1,f.ConfigurationReads);Assert.Equal(0,f.Repository.Reads);
        access.Remove(PermissionKeys.DocumentsManage);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service.GetPrintHeaderForDocumentAsync(Operation.ConsultationPreview,"Report"));
        Assert.Equal(1,f.ConfigurationReads);
    }

    [Theory]
    [InlineData("PreviewPdf","Documents.View,Documents.Manage",true)]
    [InlineData("SignConsultation","Documents.View,Documents.Sign",true)]
    [InlineData("SignConsultation","Documents.View",false)]
    [InlineData("SignConsultation","Documents.View,Documents.Manage",false)]
    public async Task Existing_DocumentEndpointPoliciesRemainAuthoritative(string action,string keys,bool allowed)
    {
        var type=typeof(MicroEMR.Api.Controllers.PatientDocumentsController);
        var metadata=type.GetCustomAttributes<AuthorizeAttribute>().Concat(type.GetMethod(action)!.GetCustomAttributes<AuthorizeAttribute>()).ToArray();
        var policy=await AuthorizationPolicy.CombineAsync(new PermissionPolicyProvider(Options.Create(new AuthorizationOptions())),metadata);
        var permissions=Proxy<ICurrentUserPermissionService>((method,args)=>method.Name==nameof(ICurrentUserPermissionService.HasPermissionAsync)
            ? Task.FromResult(Access(keys).Contains((string)args![0]!)) : Task.FromResult<IReadOnlySet<string>>(Access(keys)));
        var context=new AuthorizationHandlerContext(policy!.Requirements,new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub","test")],"test")),null);
        await new PermissionAuthorizationHandler(permissions,NullLogger<PermissionAuthorizationHandler>.Instance).HandleAsync(context);
        await new Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement().HandleAsync(context);
        Assert.Equal(allowed,context.HasSucceeded);
    }
}
