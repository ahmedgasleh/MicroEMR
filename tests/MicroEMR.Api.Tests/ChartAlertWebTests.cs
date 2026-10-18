using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientChartAlerts;
using MicroEMR.Application.ReadAudit;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Services.PatientChartAlerts;
using Xunit;

namespace MicroEMR.Api.Tests;
public sealed class ChartAlertWebTests
{
    [Fact]
    public async Task ClientUsesPatientScopedRoutesCarriesReviewedTokensAndWebEnforcesPermissions()
    {
        var properties=new AuthenticationProperties();properties.StoreTokens([new(){Name="access_token",Value="alert-fixture"}]);var ticket=new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity("fixture")),properties,"fixture");
        var context=new DefaultHttpContext{RequestServices=new ServiceCollection().AddSingleton<IAuthenticationService>(CppSelectivePrintTests.Stub<IAuthenticationService>((_,_)=>Task.FromResult(AuthenticateResult.Success(ticket)))).BuildServiceProvider()};
        var paths=new List<string>();var bodies=new List<JsonElement>();
        using var http=new HttpClient(new Capture(async request=>{Assert.Equal("alert-fixture",request.Headers.Authorization!.Parameter);paths.Add(request.RequestUri!.AbsolutePath);if(request.Content is not null){using var json=JsonDocument.Parse(await request.Content.ReadAsStringAsync());bodies.Add(json.RootElement.Clone());}return new(HttpStatusCode.OK){Content=new StringContent(request.Method==HttpMethod.Get?"[]":"{}",System.Text.Encoding.UTF8,"application/json")};})){BaseAddress=new Uri("https://alert.fixture/")};
        var client=new PatientChartAlertApiClient(http,new HttpContextAccessor{HttpContext=context});var p=Guid.NewGuid();var uid=Guid.NewGuid();
        await client.List(p,"All");await client.Update(p,uid,new MicroEMR.Web.Models.PatientChartAlerts.UpdatePatientChartAlertViewModel{AlertTitle="TEST ONLY",RowVersion="reviewed-edit"},default);await client.Resolve(p,uid,new MicroEMR.Web.Models.PatientChartAlerts.ResolvePatientChartAlertViewModel{RowVersion="reviewed-resolve",ResolutionReason="TEST ONLY reason"},default);
        Assert.Equal($"/api/patients/{p}/chart-alerts",paths[0]);Assert.Equal($"/api/patients/{p}/chart-alerts/{uid}",paths[1]);Assert.Equal(paths[1]+"/resolve",paths[2]);Assert.Equal("reviewed-edit",bodies[0].GetProperty("rowVersion").GetString());Assert.Equal("reviewed-resolve",bodies[1].GetProperty("rowVersion").GetString());
        var type=typeof(MicroEMR.Web.Controllers.PatientChartAlertsController);Assert.Contains(type.GetCustomAttributes<RequireWebPermissionAttribute>(),x=>x.Policy!.Contains(PermissionKeys.PatientsView));
        foreach(var name in new[]{"Create","Update","Resolve"}){var method=type.GetMethod(name)!;Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());Assert.Contains(method.GetCustomAttributes<RequireWebPermissionAttribute>(),x=>x.Policy!.Contains(PermissionKeys.ClinicalDataManage));}
    }
    [Theory]
    [InlineData(400)] [InlineData(401)] [InlineData(403)] [InlineData(404)] [InlineData(409)] [InlineData(503)]
    public async Task WebPreservesFailureStatusAndReviewedTokenWithoutRetry(int status)
    {
        var calls=0;var client=CppSelectivePrintTests.Stub<IPatientChartAlertApiClient>((method,args)=>{calls++;Assert.Equal("Resolve",method);Assert.Equal("reviewed",((MicroEMR.Web.Models.PatientChartAlerts.ResolvePatientChartAlertViewModel)args[2]!).RowVersion);throw new HttpRequestException("TEST ONLY",null,(HttpStatusCode)status);});
        var controller=new MicroEMR.Web.Controllers.PatientChartAlertsController(client,NullLogger<MicroEMR.Web.Controllers.PatientChartAlertsController>.Instance);
        var result=Assert.IsAssignableFrom<ObjectResult>(await controller.Resolve(new(){PatientUid=Guid.NewGuid(),PatientChartAlertUid=Guid.NewGuid(),RowVersion="reviewed",ResolutionReason="unsaved reason"},default));Assert.Equal(status,result.StatusCode);Assert.Equal(1,calls);if(status==409)Assert.Contains("Reload",JsonSerializer.Serialize(result.Value));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ApiOwnershipValidationPrecedesSuccessReadAuditEvenWithFaultySource(bool detail)
    {
        var p=Guid.NewGuid();var foreign=new PatientChartAlertResponse{PatientUid=Guid.NewGuid(),AlertTitle="foreign"};
        var service=CppSelectivePrintTests.Stub<IPatientChartAlertService>((_,_)=>detail?Task.FromResult<PatientChartAlertResponse?>(foreign):Task.FromResult<IReadOnlyList<PatientChartAlertResponse>>([foreign]));
        var audits=0;var audit=CppSelectivePrintTests.Stub<IPatientChartReadAuditService>((_,_)=>{audits++;return Task.FromResult(Guid.NewGuid());});
        var controller=new MicroEMR.Api.Controllers.PatientChartAlertsController(service,audit,NullLogger<MicroEMR.Api.Controllers.PatientChartAlertsController>.Instance){ControllerContext=new(){HttpContext=new DefaultHttpContext()}};
        Assert.IsType<ForbidResult>(detail?await controller.Get(p,Guid.NewGuid(),default):await controller.List(p,"All"));Assert.Equal(0,audits);
    }
    [Fact]
    public async Task WebDeniedReadCannotBecomeEmptyClinicalData()
    {
        var client=CppSelectivePrintTests.Stub<IPatientChartAlertApiClient>((_,_)=>throw new HttpRequestException("TEST ONLY",null,HttpStatusCode.Forbidden));
        var controller=new MicroEMR.Web.Controllers.PatientChartAlertsController(client,NullLogger<MicroEMR.Web.Controllers.PatientChartAlertsController>.Instance);
        Assert.Equal(403,Assert.IsAssignableFrom<ObjectResult>(await controller.List(Guid.NewGuid())).StatusCode);
    }
    private sealed class Capture(Func<HttpRequestMessage,Task<HttpResponseMessage>> send):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>send(request);}
}
