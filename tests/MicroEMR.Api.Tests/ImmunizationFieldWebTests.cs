using System.Net;
using System.Text.Json;
using System.Security.Claims;
using System.Reflection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Web.Models.PatientImmunizations;
using MicroEMR.Web.Services.PatientImmunizations;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ImmunizationFieldWebTests
{
    [Theory]
    [InlineData(400)] [InlineData(403)] [InlineData(404)] [InlineData(409)] [InlineData(503)]
    public async Task WebPreservesValidationDeniedMissingConflictAndUnavailableResponses(int status)
    {
        var client=CppSelectivePrintTests.Stub<IPatientImmunizationApiClient>((_,_)=>throw new HttpRequestException("TEST ONLY failure",null,(HttpStatusCode)status));
        var web=new MicroEMR.Web.Controllers.PatientImmunizationsController(client,NullLogger<MicroEMR.Web.Controllers.PatientImmunizationsController>.Instance);
        var response=Assert.IsAssignableFrom<ObjectResult>(await web.Update(Guid.NewGuid(),Guid.NewGuid(),new(){VaccineName="TEST ONLY",RowVersion="read-token"},default));
        Assert.Equal(status,response.StatusCode);Assert.Contains("false",JsonSerializer.Serialize(response.Value));
        if(status==409)Assert.Contains("Reload",JsonSerializer.Serialize(response.Value));
        Assert.Equal(status,Assert.IsAssignableFrom<ObjectResult>(await web.MarkEnteredInError(Guid.NewGuid(),Guid.NewGuid(),new(){Reason="TEST ONLY",RowVersion="read-token"},default)).StatusCode);
    }

    [Fact]
    public async Task ProxySerializesPrecisionNewFieldsExplicitClearAndCapturedTokensWithoutLegacyNullInjection()
    {
        var properties=new AuthenticationProperties();properties.StoreTokens([new(){Name="access_token",Value="immunization-fixture"}]);
        var ticket=new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity("fixture")),properties,"fixture");
        var context=new DefaultHttpContext{RequestServices=new ServiceCollection().AddSingleton<IAuthenticationService>(CppSelectivePrintTests.Stub<IAuthenticationService>((_,_)=>Task.FromResult(AuthenticateResult.Success(ticket)))).BuildServiceProvider()};
        var bodies=new List<JsonElement>();
        using var http=new HttpClient(new Capture(async request=>
        {
            Assert.Equal("immunization-fixture",request.Headers.Authorization!.Parameter);using var json=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());bodies.Add(json.RootElement.Clone());
            return new(HttpStatusCode.OK){Content=new StringContent("{}",System.Text.Encoding.UTF8,"application/json")};
        })){BaseAddress=new Uri("https://immunization.fixture/")};
        var client=new PatientImmunizationApiClient(http,new HttpContextAccessor{HttpContext=context});var patient=Guid.NewGuid();var uid=Guid.NewGuid();
        await client.CreateAsync(patient,new(){VaccineName="TEST ONLY",DoseIncluded=true,Dose=new(){Amount=0.001m,UnitCode="mL"},SourceType="HistoricalExternal",ClinicalFieldsIncluded=true,ClinicalFields=new(){DIN="00123456",Manufacturer="TEST ONLY",ImmunizationTypeCode="Inf",Refused=false,AdministrationDate=new(){Year=2000,Month=2}}});
        await client.UpdateAsync(patient,uid,new(){VaccineName="TEST ONLY",RowVersion="read-token",ClinicalFieldsIncluded=true});
        await client.UpdateAsync(patient,uid,new(){VaccineName="TEST ONLY",RowVersion="legacy-token",AdministrationDate=null});
        await client.MarkEnteredInErrorAsync(patient,uid,new(){RowVersion="error-token",Reason="TEST ONLY"});
        Assert.Equal(0.001m,bodies[0].GetProperty("Dose").GetProperty("amount").GetDecimal());Assert.Equal("mL",bodies[0].GetProperty("Dose").GetProperty("unitCode").GetString());Assert.False(bodies[0].TryGetProperty("AdministrationDate",out _));var fields=bodies[0].GetProperty("ClinicalFields");
        Assert.Equal("00123456",fields.GetProperty("din").GetString());Assert.Equal("Inf",fields.GetProperty("immunizationTypeCode").GetString());Assert.Equal(2,fields.GetProperty("administrationDate").GetProperty("month").GetInt32());
        Assert.Equal("read-token",bodies[1].GetProperty("RowVersion").GetString());Assert.Equal(JsonValueKind.Object,bodies[1].GetProperty("ClinicalFields").ValueKind);
        Assert.Equal(JsonValueKind.Null,bodies[2].GetProperty("AdministrationDate").ValueKind);Assert.Equal("error-token",bodies[3].GetProperty("rowVersion").GetString());
        foreach(var method in new[]{"Create","Update","MarkEnteredInError"})Assert.NotNull(typeof(MicroEMR.Web.Controllers.PatientImmunizationsController).GetMethod(method)!.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }
    private sealed class Capture(Func<HttpRequestMessage,Task<HttpResponseMessage>> send):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>send(request);}
}
