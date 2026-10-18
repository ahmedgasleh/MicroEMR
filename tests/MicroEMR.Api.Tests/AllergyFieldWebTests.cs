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
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Models.PatientAllergies;
using MicroEMR.Web.Services.PatientAllergies;
using Xunit;

namespace MicroEMR.Api.Tests;
public sealed class AllergyFieldWebTests
{
    [Fact]
    public async Task StaleNkaCreateKeepsReviewedAssertionAndGivesReloadFeedback()
    {
        CreatePatientAllergyRequest? captured=null;
        var client=CppSelectivePrintTests.Stub<IPatientAllergyApiClient>((_,args)=>{captured=(CreatePatientAllergyRequest)args[1]!;throw new HttpRequestException("Changed",null,HttpStatusCode.Conflict);});
        var web=new MicroEMR.Web.Controllers.PatientAllergiesController(client,NullLogger<MicroEMR.Web.Controllers.PatientAllergiesController>.Instance);
        var model=new CreatePatientAllergyViewModel{PatientUid=Guid.NewGuid(),AllergenName="TEST ONLY",AllergenType="Medication",ClinicalFieldsIncluded=true,ClinicalFields=new(){DrugDIN="00123456",OnsetDate=new(){Year=2000,Month=2}},ConfirmReplaceNoKnownAllergies=true,ExpectedAssertionUid=Guid.NewGuid(),ExpectedAssertionRowVersion="reviewed-nka-token"};
        var result=Assert.IsType<ViewResult>(await web.Create(model,default));Assert.Same(model,result.Model);Assert.Equal(model.ExpectedAssertionUid,captured!.ExpectedAssertionUid);Assert.Equal("reviewed-nka-token",captured.ExpectedAssertionRowVersion);Assert.False(captured.OnsetDateSpecified);Assert.Equal("00123456",captured.ClinicalFields!.DrugDIN);
        Assert.Contains(web.ModelState.Values.SelectMany(x=>x.Errors),x=>x.ErrorMessage.Contains("Reload"));
    }

    [Fact]
    public async Task ScopedClientCarriesFieldsPresenceAndSeparateAllergyAssertionTokens()
    {
        var properties=new AuthenticationProperties();properties.StoreTokens([new(){Name="access_token",Value="allergy-fixture"}]);var ticket=new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity("fixture")),properties,"fixture");
        var context=new DefaultHttpContext{RequestServices=new ServiceCollection().AddSingleton<IAuthenticationService>(CppSelectivePrintTests.Stub<IAuthenticationService>((_,_)=>Task.FromResult(AuthenticateResult.Success(ticket)))).BuildServiceProvider()};var bodies=new List<JsonElement>();var paths=new List<string>();
        using var http=new HttpClient(new Capture(async request=>{Assert.Equal("allergy-fixture",request.Headers.Authorization!.Parameter);paths.Add(request.RequestUri!.AbsolutePath);if(request.Content is not null){using var json=JsonDocument.Parse(await request.Content.ReadAsStringAsync());bodies.Add(json.RootElement.Clone());}return new(HttpStatusCode.OK){Content=new StringContent("{}",System.Text.Encoding.UTF8,"application/json")};})){BaseAddress=new Uri("https://allergy.fixture/")};
        var client=new PatientAllergyApiClient(http,new HttpContextAccessor{HttpContext=context},NullLogger<PatientAllergyApiClient>.Instance);var patient=Guid.NewGuid();var uid=Guid.NewGuid();var assertion=Guid.NewGuid();
        await client.GetForPatientAsync(patient,uid);Assert.Equal($"/api/patients/{patient}/allergies/{uid}",paths[0]);
        await client.CreateAsync(patient,new(){AllergenName="TEST ONLY",AllergenType="Medication",ClinicalFields=new(){DrugDIN="00123456",LifeStageCode="C",OnsetDate=new(){Year=2000,Month=2}},ConfirmReplaceNoKnownAllergies=true,ExpectedAssertionUid=assertion,ExpectedAssertionRowVersion="assertion-token"});
        Assert.False(bodies[0].TryGetProperty("onsetDate",out _));Assert.Equal("00123456",bodies[0].GetProperty("clinicalFields").GetProperty("drugDIN").GetString());Assert.Equal(assertion,bodies[0].GetProperty("expectedAssertionUid").GetGuid());
        await client.UpdateAsync(patient,uid,new(){AllergenName="TEST ONLY",RowVersion="allergy-token",OnsetDate=null});Assert.Equal(JsonValueKind.Null,bodies[1].GetProperty("onsetDate").ValueKind);
        await client.ResolveAsync(patient,uid,new(){RowVersion="resolve-token"});Assert.Equal("resolve-token",bodies[2].GetProperty("rowVersion").GetString());
        await client.RevokeNoKnownAllergiesAsync(patient,new(){AssertionUid=assertion,RowVersion="nka-token"});Assert.Equal(assertion,bodies[3].GetProperty("assertionUid").GetGuid());Assert.Equal("nka-token",bodies[3].GetProperty("rowVersion").GetString());
        var web=typeof(MicroEMR.Web.Controllers.PatientAllergiesController);Assert.Contains(web.GetCustomAttributes<RequireWebPermissionAttribute>(),x=>x.Policy!.Contains(PermissionKeys.PatientsView));
        foreach(var method in web.GetMethods().Where(x=>x.Name is "Create" or "Edit" or "ResolveAllergy" or "AssertNoKnownAllergies" or "RevokeNoKnownAllergies"))
        {Assert.Contains(method.GetCustomAttributes<RequireWebPermissionAttribute>(),x=>x.Policy!.Contains(PermissionKeys.ClinicalDataManage));if(method.GetCustomAttribute<HttpPostAttribute>() is not null)Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());}
    }
    [Theory]
    [InlineData(400)] [InlineData(403)] [InlineData(404)] [InlineData(409)] [InlineData(503)]
    public async Task ResolvePreservesRejectedStatusAndReadToken(int status)
    {
        var client=CppSelectivePrintTests.Stub<IPatientAllergyApiClient>((name,args)=>{Assert.Equal("ResolveAsync",name);Assert.Equal("read-token",((ResolvePatientAllergyRequest)args[2]!).RowVersion);throw new HttpRequestException("TEST ONLY",null,(HttpStatusCode)status);});
        var web=new MicroEMR.Web.Controllers.PatientAllergiesController(client,NullLogger<MicroEMR.Web.Controllers.PatientAllergiesController>.Instance);
        var result=Assert.IsAssignableFrom<ObjectResult>(await web.ResolveAllergy(new(){PatientUid=Guid.NewGuid(),AllergyUid=Guid.NewGuid(),RowVersion="read-token"},default));Assert.Equal(status,result.StatusCode);if(status==409)Assert.Contains("Reload",JsonSerializer.Serialize(result.Value));
    }
    private sealed class Capture(Func<HttpRequestMessage,Task<HttpResponseMessage>> send):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>send(request);}
}
