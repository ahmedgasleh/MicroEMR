using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Web.Models.PatientProblems;
using MicroEMR.Web.Services;
using MicroEMR.Web.Services.PatientProblems;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ProblemFieldWebTests
{
    [Fact]
    public async Task WebFormsAndApiClientCarryClinicalFieldsAndExactReadTokens()
    {
        var patient=Guid.NewGuid();var uid=Guid.NewGuid();var token="AAAAAAAAAAE=";
        CreatePatientProblemRequest? created=null;UpdatePatientProblemRequest? edited=null;ResolvePatientProblemRequest? resolved=null;
        var proxy=CppSelectivePrintTests.Stub<IPatientProblemApiClient>((name,args)=>
        {
            if(name=="CreateAsync")created=(CreatePatientProblemRequest)args[1]!;
            if(name=="UpdateAsync")edited=(UpdatePatientProblemRequest)args[2]!;
            if(name=="ResolveAsync")resolved=(ResolvePatientProblemRequest)args[2]!;
            return Task.FromResult<PatientProblemViewModel?>(new(){PatientUid=patient,PatientProblemUid=uid,RowVersion=token});
        });
        var web=new MicroEMR.Web.Controllers.PatientProblemsController(proxy,NullLogger<MicroEMR.Web.Controllers.PatientProblemsController>.Instance);
        Assert.IsType<JsonResult>(await web.CreateProblem(new(){PatientUid=patient,ProblemName="TEST ONLY web",ClinicalFieldsIncluded=true,ClinicalFields=new(){OnsetDate=new(){Year=2000,Month=2},LifeStageCode="C",Notes="TEST ONLY notes"}},default));
        Assert.IsType<JsonResult>(await web.UpdateProblem(new(){PatientUid=patient,PatientProblemUid=uid,ProblemName="TEST ONLY web",RowVersion=token,ClinicalFieldsIncluded=true,ClinicalFields=new(){Notes="TEST ONLY edited"}},default));
        Assert.IsType<JsonResult>(await web.ResolveProblem(new(){PatientUid=patient,PatientProblemUid=uid,RowVersion=token,ClinicalResolutionDate=new(){Year=2010},ResolutionReason="TEST ONLY reason"},default));
        Assert.NotNull(created);Assert.False(created.OnsetDateSpecified);Assert.Equal(2,created.ClinicalFields!.OnsetDate!.Month);Assert.NotNull(edited);Assert.Equal(token,edited.RowVersion);Assert.Equal("TEST ONLY edited",edited.ClinicalFields!.Notes);Assert.NotNull(resolved);Assert.Equal(token,resolved.RowVersion);Assert.Equal(2010,resolved.ClinicalResolutionDate!.Year);
        var properties=new AuthenticationProperties();properties.StoreTokens([new(){Name="access_token",Value="problem-fixture"}]);
        var ticket=new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity("fixture")),properties,"fixture");
        var context=new DefaultHttpContext{RequestServices=new ServiceCollection().AddSingleton<IAuthenticationService>(CppSelectivePrintTests.Stub<IAuthenticationService>((_,_)=>Task.FromResult(AuthenticateResult.Success(ticket)))).BuildServiceProvider()};
        var bodies=new List<JsonElement>();
        using var http=new HttpClient(new Capture(async request=>
        {
            Assert.Equal("problem-fixture",request.Headers.Authorization!.Parameter);using var json=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());bodies.Add(json.RootElement.Clone());
            return new(HttpStatusCode.OK){Content=new StringContent("{}",System.Text.Encoding.UTF8,"application/json")};
        })){BaseAddress=new Uri("https://problem.fixture/")};
        var client=new PatientProblemApiClient(http,new HttpContextAccessor{HttpContext=context},NullLogger<PatientProblemApiClient>.Instance);
        await client.CreateAsync(patient,created);await client.UpdateAsync(patient,uid,edited);await client.ResolveAsync(patient,uid,resolved);
        Assert.False(bodies[0].TryGetProperty("OnsetDate",out _));Assert.Equal("TEST ONLY notes",bodies[0].GetProperty("ClinicalFields").GetProperty("notes").GetString());
        Assert.Equal(token,bodies[1].GetProperty("RowVersion").GetString());Assert.Equal(token,bodies[2].GetProperty("rowVersion").GetString());
        var legacy=new UpdatePatientProblemRequest{ProblemName="TEST ONLY legacy",RowVersion=token,OnsetDate=null};await client.UpdateAsync(patient,uid,legacy);Assert.Equal(JsonValueKind.Null,bodies[3].GetProperty("OnsetDate").ValueKind);
        foreach(var method in new[]{"CreateProblem","UpdateProblem","ResolveProblem"})Assert.NotNull(typeof(MicroEMR.Web.Controllers.PatientProblemsController).GetMethod(method)!.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Theory]
    [InlineData(400)] [InlineData(403)] [InlineData(404)] [InlineData(409)] [InlineData(503)]
    public async Task WebDoesNotTurnDenialsOrConflictsIntoSuccessOrGeneric502(int status)
    {
        var client=CppSelectivePrintTests.Stub<IPatientProblemApiClient>((_,_)=>throw new SafeApiResponseException((HttpStatusCode)status,"{\"message\":\"Invalid clinical date.\"}"));
        var web=new MicroEMR.Web.Controllers.PatientProblemsController(client,NullLogger<MicroEMR.Web.Controllers.PatientProblemsController>.Instance);
        var result=Assert.IsAssignableFrom<ObjectResult>(await web.UpdateProblem(new(){PatientUid=Guid.NewGuid(),PatientProblemUid=Guid.NewGuid(),ProblemName="TEST ONLY",RowVersion="AAAAAAAAAAE="},default));
        Assert.Equal(status,result.StatusCode);var json=JsonSerializer.Serialize(result.Value);Assert.Contains("false",json);
        if(status==409){Assert.Contains("Reload",json);Assert.Contains("review",json);}
        var resolve=Assert.IsAssignableFrom<ObjectResult>(await web.ResolveProblem(new(){PatientUid=Guid.NewGuid(),PatientProblemUid=Guid.NewGuid(),RowVersion="AAAAAAAAAAE="},default));Assert.Equal(status,resolve.StatusCode);
    }
    private sealed class Capture(Func<HttpRequestMessage,Task<HttpResponseMessage>> send):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>send(request);}
}
