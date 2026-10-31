using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.ClinicConfiguration;
using MicroEMR.Web.Controllers;
using MicroEMR.Web.Services.ClinicConfiguration;
using Xunit;
using static MicroEMR.Api.Tests.ConsultationSigningTests;

namespace MicroEMR.Api.Tests;

public sealed class ClinicBrandingWebTests
{
    [Fact]
    public async Task ClientForwardsOnlyCurrentTenantRoutesAndAuthenticatedMultipartContent()
    {
        var handler=new Handler();
        var properties=new AuthenticationProperties();properties.StoreTokens([new(){Name="access_token",Value="test-token"}]);
        var authentication=Proxy<IAuthenticationService>((_,_)=>Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(new ClaimsIdentity("test")),properties,"test"))));
        var context=new DefaultHttpContext{RequestServices=new ServiceCollection().AddSingleton(authentication).BuildServiceProvider()};
        var client=new ClinicBrandingApiClient(new HttpClient(handler){BaseAddress=new("https://api.test/")},new HttpContextAccessor{HttpContext=context});
        Assert.False((await client.GetStateAsync()).HasLogo);
        var file=new FormFile(new MemoryStream([1,2,3]),0,3,"logo","untrusted-name.svg");
        await client.SaveAsync(file,false,"AAAAAAAAAAE=");
        Assert.NotNull(await client.GetLogoAsync()); await client.GetHeaderAsync();
        Assert.Equal(new[]{"/api/clinic-branding","/api/clinic-branding/logo","/api/clinic-branding/logo","/api/clinic-branding/header"},handler.Paths);
        Assert.Contains("name=rowVersion",handler.Multipart);Assert.Contains("AAAAAAAAAAE=",handler.Multipart);
        Assert.DoesNotContain("untrusted-name.svg",handler.Multipart);Assert.DoesNotContain("tenantUid",handler.Multipart);
        Assert.DoesNotContain("storageKey",handler.Multipart);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden,403)]
    [InlineData(HttpStatusCode.InternalServerError,502)]
    public async Task WebReadFailureReturnsSafeStatusAndNeverApiDetails(HttpStatusCode status,int expected)
    {
        var client=Proxy<IClinicBrandingApiClient>((_,_)=>Task.FromException<byte[]?>(new HttpRequestException("secret storage/path/token",null,status)));
        var controller=new ClinicBrandingController(client,NullLogger<ClinicBrandingController>.Instance){ControllerContext=new(){HttpContext=new DefaultHttpContext()}};
        var result=Assert.IsType<ObjectResult>(await controller.Logo(default));
        Assert.Equal(expected,result.StatusCode);Assert.DoesNotContain("secret",JsonSerializer.Serialize(result.Value));
    }

    [Fact]
    public async Task UploadAndRemovalUseExplicitSaveAndRedirectToExistingClinicSettings()
    {
        var writes=new List<(bool Remove,string? Version)>();
        var client=Proxy<IClinicBrandingApiClient>((_,args)=>{writes.Add(((bool)args![1]!, (string?)args[2]));return Task.CompletedTask;});
        var controller=new ClinicBrandingController(client,NullLogger<ClinicBrandingController>.Instance)
            {TempData=new TempDataDictionary(new DefaultHttpContext(),new TempProvider())};
        var file=new FormFile(new MemoryStream([1]),0,1,"logo","logo.png");
        var result=Assert.IsType<RedirectToActionResult>(await controller.Save(file,false,null,default));
        Assert.Equal("ClinicConfiguration",result.ControllerName);Assert.Equal("Index",result.ActionName);
        await controller.Save(null,true,"AAAAAAAAAAE=",default);
        Assert.Equal(new[]{(false,(string?)null),(true,"AAAAAAAAAAE=")},writes);
        await controller.Save(null,false,null,default);Assert.Equal(2,writes.Count);
        Assert.NotNull(controller.TempData["BrandingError"]);
    }

    private sealed class Handler : HttpMessageHandler
    {
        public List<string> Paths=[];public string? Multipart;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Assert.Equal("Bearer",request.Headers.Authorization!.Scheme);Assert.Equal("test-token",request.Headers.Authorization.Parameter);
            Paths.Add(request.RequestUri!.AbsolutePath);
            if(request.Method==HttpMethod.Post)Multipart=await request.Content!.ReadAsStringAsync(token);
            return new(HttpStatusCode.OK){Content=new StringContent("{\"hasLogo\":false,\"rowVersion\":null}")};
        }
    }
    private sealed class TempProvider : ITempDataProvider
    {public IDictionary<string,object> LoadTempData(HttpContext context)=>new Dictionary<string,object>();public void SaveTempData(HttpContext context,IDictionary<string,object> values){} }
}
