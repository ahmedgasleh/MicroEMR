using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.ClinicConfiguration;
using MicroEMR.Application.Tenancy;
using Xunit;
using static MicroEMR.Api.Tests.ConsultationSigningTests;
using ApiController = MicroEMR.Api.Controllers.ClinicBrandingController;

namespace MicroEMR.Api.Tests;

public sealed class ClinicBrandingTests
{
    [Fact]
    public async Task AdministratorCanUploadReplaceAndRemoveWithCurrentVersionAndAuthenticatedActor()
    {
        var f = new Fixture();
        Assert.False((await f.Service.GetStateAsync()).HasLogo);
        var first = await f.Service.SaveLogoAsync([1], false, null);
        Assert.True(first.HasLogo); Assert.Equal(7, f.Repository.LastActor);
        var second = await f.Service.SaveLogoAsync([2], false, first.RowVersion);
        Assert.NotEqual(first.RowVersion, second.RowVersion);
        Assert.Equal(new byte[]{2}, (await f.Service.GetLogoAsync())!.PngBytes);
        await Assert.ThrowsAsync<ClinicConfigurationConcurrencyException>(() => f.Service.SaveLogoAsync([3], false, first.RowVersion));
        var removed = await f.Service.SaveLogoAsync(null, true, second.RowVersion);
        Assert.False(removed.HasLogo); Assert.NotNull(removed.RowVersion); Assert.Null(await f.Service.GetLogoAsync());
    }

    [Fact]
    public async Task ClinicalReadersCanRenderTheirHeaderButCannotChangeBranding()
    {
        var f = new Fixture(PermissionKeys.PatientsView);
        var header = await f.Service.GetPrintHeaderAsync("Clinical preview");
        Assert.Equal("Tenant A Clinic", header.Clinic.Name);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service.SaveLogoAsync([1], false, null));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service.GetStateAsync());
        Assert.Equal(0, f.Repository.Writes); Assert.Equal(0, f.DecodeCalls);
    }

    [Fact]
    public async Task NoPermissionAndMissingTenantDenyReadsAndWritesBeforeStorage()
    {
        var f = new Fixture("");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service.GetLogoAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service.GetPrintHeaderAsync("Preview"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service.SaveLogoAsync([1], false, null));
        var missing = new Fixture(); missing.Tenant.TenantUid = Guid.Empty;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>missing.Service.SaveLogoAsync([1], false, null));
        Assert.Equal(0, f.Repository.Reads); Assert.Equal(0, f.Repository.Writes);
    }

    [Fact]
    public async Task AuthenticatedTenantContextSelectsBrandingAndNoClientTenantOrStorageKeyIsAccepted()
    {
        var f = new Fixture();
        var tenantA = f.Tenant.TenantUid;
        var a = await f.Service.SaveLogoAsync([1], false, null);
        f.Tenant.TenantUid = Guid.NewGuid(); f.Tenant.DisplayName = "Tenant B Clinic";
        Assert.Null(await f.Service.GetLogoAsync());
        Assert.Equal("Tenant B Clinic", (await f.Service.GetPrintHeaderAsync("B")).Clinic.Name);
        var b = await f.Service.SaveLogoAsync([2], false, null);
        f.Tenant.TenantUid = tenantA; f.Tenant.DisplayName = "Tenant A Clinic";
        Assert.Equal(new byte[]{1}, (await f.Service.GetLogoAsync())!.PngBytes);
        await Assert.ThrowsAsync<ClinicConfigurationConcurrencyException>(()=>f.Service.SaveLogoAsync(null, true, b.RowVersion));
        Assert.Equal(a.RowVersion, (await f.Service.GetStateAsync()).RowVersion);
        var parameters = typeof(ApiController).GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly)
            .SelectMany(m=>m.GetParameters()).Select(p=>p.Name).ToArray();
        Assert.DoesNotContain("tenantUid", parameters); Assert.DoesNotContain("storageKey", parameters);
    }

    [Fact]
    public async Task OversizedEmptyConflictingAndInvalidVersionInputsNeverWrite()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.SaveLogoAsync(new byte[ClinicBrandingService.MaximumLogoBytes+1], false, null));
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.SaveLogoAsync([], false, null));
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.SaveLogoAsync([1], true, null));
        await Assert.ThrowsAsync<FormatException>(()=>f.Service.SaveLogoAsync([1], false, "bad-version"));
        Assert.Equal(0, f.DecodeCalls); Assert.Equal(0, f.Repository.Writes);
    }

    [Fact]
    public async Task HeaderUsesExistingLegalNameContactFieldsAndEncodesAllText()
    {
        var f = new Fixture();
        f.Configuration = f.Configuration with {LegalName="Legal <Clinic>", Phone="555-0100", Fax="555-0101", Email="clinic@example.invalid",
            AddressLine1="1 <Main>", City="Test City", Country="Canada"};
        var html = ClinicPrintHeaderRenderer.RenderDocument(await f.Service.GetPrintHeaderAsync("Letter <title>"));
        Assert.Contains("Legal &lt;Clinic&gt;", html); Assert.Contains("1 &lt;Main&gt;", html);
        Assert.Contains("Tel: 555-0100", html); Assert.Contains("Fax: 555-0101", html); Assert.Contains("clinic@example.invalid", html);
        Assert.Contains("Canada", html); Assert.Contains("Letter &lt;title&gt;", html);
        Assert.DoesNotContain("<Clinic>", html); Assert.DoesNotContain("<img", html);
    }

    [Fact]
    public void MissingLogoAndOptionalFieldsProduceCleanTextFallback()
    {
        var header = new ClinicPrintHeader(new("Clinic",null,null,null,null,null,null,null,null),"Preview");
        var html = ClinicPrintHeaderRenderer.RenderDocument(header);
        Assert.Contains("Clinic",html); Assert.Contains("Preview",html);
        Assert.DoesNotContain("<img",html); Assert.DoesNotContain("Tel:",html); Assert.DoesNotContain("Fax:",html);
        Assert.Contains("@page { size: Letter; margin: 0.65in; }",html);
        Assert.Contains("object-fit: contain",html);
    }

    [Fact]
    public async Task ApiDenialsAndFailuresAreSafeAndLogoIsPrivateNonSniffablePng()
    {
        var denied = new ApiController(new Fixture("").Service, NullLogger<ApiController>.Instance)
            {ControllerContext=new(){HttpContext=new DefaultHttpContext()}};
        Assert.Equal(403, Assert.IsType<ObjectResult>(await denied.Logo(default)).StatusCode);
        var f = new Fixture(); await f.Service.SaveLogoAsync([1],false,null);
        var controller = new ApiController(f.Service,NullLogger<ApiController>.Instance)
            {ControllerContext=new(){HttpContext=new DefaultHttpContext()}};
        var image = Assert.IsType<FileContentResult>(await controller.Logo(default));
        Assert.Equal("image/png",image.ContentType); Assert.Empty(image.FileDownloadName);
        Assert.Equal("private, no-store",controller.Response.Headers.CacheControl.ToString());
        Assert.Equal("nosniff",controller.Response.Headers["X-Content-Type-Options"].ToString());
        var invalid = Assert.IsType<BadRequestObjectResult>(await controller.Save(null,false,null,default));
        Assert.DoesNotContain("Exception",System.Text.Json.JsonSerializer.Serialize(invalid.Value));
    }

    [Fact]
    public async Task PermissionPoliciesProtectApiAndWebWritesAndWebRequiresAntiforgery()
    {
        foreach(var method in new[]{typeof(ApiController).GetMethod("Save")!,typeof(MicroEMR.Web.Controllers.ClinicBrandingController).GetMethod("Save")!})
            Assert.Contains(method.GetCustomAttributes<AuthorizeAttribute>(),a=>a.Policy!.EndsWith(PermissionKeys.ClinicSettingsManage));
        Assert.NotNull(typeof(MicroEMR.Web.Controllers.ClinicBrandingController).GetMethod("Save")!.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        var permissions = Proxy<ICurrentUserPermissionService>((_,_)=>Task.FromResult(false));
        var handler = new PermissionAuthorizationHandler(permissions,NullLogger<PermissionAuthorizationHandler>.Instance);
        var requirement = new PermissionRequirement(PermissionKeys.ClinicSettingsManage);
        var context = new AuthorizationHandlerContext([requirement],new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity([],"test")),null);
        await handler.HandleAsync(context); Assert.False(context.HasSucceeded);
    }

    internal sealed class Fixture
    {
        public TestTenant Tenant {get;} = new();
        public TestRepository Repository {get;}
        public ClinicBrandingService Service {get;}
        public int DecodeCalls;
        public ClinicConfigurationResponse Configuration = new("Tenant A Clinic","UTC",null,null,null,null,null,null,null,null,null,null,null,null,null,null);
        public Fixture(string permission=PermissionKeys.ClinicSettingsManage)
        {
            Repository = new(Tenant);
            Service = new(Repository,Proxy<IClinicLogoProcessor>((_,args)=>{DecodeCalls++;return Task.FromResult(new ClinicLogo((byte[])args![0]!,1,1));}),
                Proxy<IClinicConfigurationService>((_,_)=>Task.FromResult(Configuration with {ClinicName=Tenant.DisplayName})),
                Proxy<ICurrentUserPermissionService>((_,_)=>Task.FromResult<IReadOnlySet<string>>(new HashSet<string>{permission})),
                Proxy<IAuthenticatedClinicalUserAccessor>((_,_)=>Task.FromResult(7L)),Tenant);
        }
    }
    internal sealed class TestTenant : ITenantContext
    {
        public Guid TenantUid {get;set;}=Guid.NewGuid(); public string TenantKey=>"test"; public string DisplayName {get;set;}="Tenant A Clinic";
    }
    internal sealed class TestRepository(TestTenant tenant) : IClinicLogoRepository
    {
        private readonly Dictionary<Guid,ClinicLogoRecord> rows=[];
        private long version;
        public int Writes,Reads; public long LastActor;
        public Task<ClinicLogoRecord?> GetAsync(CancellationToken token=default) {Reads++;return Task.FromResult(rows.GetValueOrDefault(tenant.TenantUid));}
        public Task<ClinicLogoRecord> SaveAsync(ClinicLogo? logo,string? expected,long actor,CancellationToken token=default)
        {
            if (rows.GetValueOrDefault(tenant.TenantUid)?.RowVersion != expected) throw new ClinicConfigurationConcurrencyException("Stale");
            Writes++;LastActor=actor;
            var result=new ClinicLogoRecord(logo,Convert.ToBase64String(BitConverter.GetBytes(++version)));
            rows[tenant.TenantUid]=result;return Task.FromResult(result);
        }
    }
}
