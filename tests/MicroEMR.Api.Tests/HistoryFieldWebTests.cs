using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MicroEMR.Application.PatientClinicalHistory;
using MicroEMR.Web.Models.PatientClinicalHistory;
using MicroEMR.Web.Services.PatientClinicalHistory;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class HistoryFieldWebTests
{
    [Theory]
    [InlineData("Medical")]
    [InlineData("Surgical")]
    public async Task WebClientCarriesClinicalFieldsLegacyDateAndVersionAndExplicitClear(string kind)
    {
        var properties=new AuthenticationProperties();properties.StoreTokens([new(){Name="access_token",Value="history-fixture"}]);
        var ticket=new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity("fixture")),properties,"fixture");
        var context=new DefaultHttpContext{RequestServices=new ServiceCollection().AddSingleton<IAuthenticationService>(CppSelectivePrintTests.Stub<IAuthenticationService>((_,_)=>Task.FromResult(AuthenticateResult.Success(ticket)))).BuildServiceProvider()};
        var bodies=new List<JsonElement>();var verbs=new List<HttpMethod>();
        using var http=new HttpClient(new Capture(async request=>
        {
            Assert.Equal("Bearer",request.Headers.Authorization!.Scheme);Assert.Equal("history-fixture",request.Headers.Authorization.Parameter);
            verbs.Add(request.Method);using var json=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());bodies.Add(json.RootElement.Clone());
            return new(HttpStatusCode.OK){Content=new StringContent("{}",System.Text.Encoding.UTF8,"application/json")};
        })){BaseAddress=new Uri("https://history.fixture/")};
        var client=new PatientClinicalHistoryApiClient(http,new HttpContextAccessor{HttpContext=context});
        var fields=kind=="Medical"?new HistoryClinicalFields{OnsetDate=new(){Year=2000,Month=2},LifeStageCode="C",ClinicalStatus="Active",Notes="TEST ONLY notes"}:new HistoryClinicalFields{ProcedureDate=new(){Year=2004},Notes="TEST ONLY notes"};
        var model=new SavePatientClinicalHistoryViewModel{HistoryType=kind,Description="TEST ONLY",RelevantDate=new(1990,1,2),RowVersion="AAAAAAAAAAE=",ClinicalFieldsIncluded=true,ClinicalFields=fields};
        await client.Create(Guid.NewGuid(),model);await client.Update(Guid.NewGuid(),Guid.NewGuid(),model);
        Assert.Equal(HttpMethod.Post,verbs[0]);Assert.Equal(HttpMethod.Put,verbs[1]);
        Assert.Equal("1990-01-02",bodies[1].GetProperty("relevantDate").GetString());Assert.Equal(model.RowVersion,bodies[1].GetProperty("rowVersion").GetString());
        var clinical=bodies[1].GetProperty("clinicalFields");Assert.Equal("TEST ONLY notes",clinical.GetProperty("notes").GetString());
        Assert.Equal(kind=="Medical"?2000:2004,clinical.GetProperty(kind=="Medical"?"onsetDate":"procedureDate").GetProperty("year").GetInt32());
        model.ClinicalFields=null;await client.Update(Guid.NewGuid(),Guid.NewGuid(),model);Assert.Equal(JsonValueKind.Object,bodies[2].GetProperty("clinicalFields").ValueKind);
        model.ClinicalFieldsIncluded=false;await client.Update(Guid.NewGuid(),Guid.NewGuid(),model);Assert.Equal(JsonValueKind.Null,bodies[3].GetProperty("clinicalFields").ValueKind);
    }
    private sealed class Capture(Func<HttpRequestMessage,Task<HttpResponseMessage>> send):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>send(request);}
}
