using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.PatientReferrals;
using MicroEMR.Web.Services.PatientReferrals;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ReferralClinicalSelectionTransportTests
{
    [Fact]
    public async Task WebClientForwardsPatientScopedRoutesBearerTokenReferencesAndNewVersion()
    {
        var patient=Guid.NewGuid();var referral=Guid.NewGuid();var calls=new List<string>();
        var response=new PatientReferralClinicalSelectionsResponse(patient,referral,"new-version",
            [new(Guid.NewGuid(),"CPP","PROBLEMS",null,null,DateTime.UtcNow,7)]);
        var client=Client(async message=>
        {
            calls.Add(message.Method+" "+message.RequestUri!.AbsolutePath);
            if(message.Method==HttpMethod.Put)
            {
                using var json=JsonDocument.Parse(await message.Content!.ReadAsStringAsync());
                Assert.Equal("expected-version",json.RootElement.GetProperty("rowVersion").GetString());
                Assert.Equal("PROBLEMS",json.RootElement.GetProperty("selections")[0].GetProperty("cppCategoryCode").GetString());
                Assert.False(json.RootElement.TryGetProperty("patientUid",out _));
                Assert.False(json.RootElement.GetProperty("selections")[0].TryGetProperty("label",out _));
            }
            return new HttpResponseMessage(HttpStatusCode.OK) {Content=message.RequestUri!.AbsolutePath.EndsWith("clinical-options")
                ? JsonContent.Create(new ReferralClinicalOptionsResponse([],[],[])) : JsonContent.Create(response)};
        });
        Assert.Empty((await client.GetClinicalOptionsAsync(patient)).Encounters);
        Assert.Equal("new-version",(await client.GetClinicalSelectionsAsync(patient,referral))!.RowVersion);
        Assert.Equal("new-version",(await client.ReplaceClinicalSelectionsAsync(patient,referral,
            new() {RowVersion="expected-version",Selections=[new("CPP","PROBLEMS")]}))!.RowVersion);
        Assert.Equal(new[] {$"GET /api/patients/{patient}/referrals/clinical-options",$"GET /api/patients/{patient}/referrals/{referral}/clinical-selections",$"PUT /api/patients/{patient}/referrals/{referral}/clinical-selections"},calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Conflict)]
    public async Task WebControllerPreservesRestrictedAndConcurrencyFailures(HttpStatusCode code)
    {
        var client=Client(_=>Task.FromResult(new HttpResponseMessage(code)));
        var controller=new MicroEMR.Web.Controllers.PatientReferralsController(client,NullLogger<MicroEMR.Web.Controllers.PatientReferralsController>.Instance);
        var action=await controller.ReplaceClinicalSelections(new() {PatientUid=Guid.NewGuid(),ReferralUid=Guid.NewGuid(),RowVersion="version"},default);
        Assert.Equal((int)code,Assert.IsAssignableFrom<ObjectResult>(action).StatusCode);
        Assert.NotNull(typeof(MicroEMR.Web.Controllers.PatientReferralsController).GetMethod("ReplaceClinicalSelections")!
            .GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public async Task ApiReturnsForbiddenForRestrictedPreviewAndSelectionRead()
    {
        var service=Stub<IPatientReferralService>((_,_)=>throw new UnauthorizedAccessException());
        var controller=new MicroEMR.Api.Controllers.PatientReferralsController(service,NullLogger<MicroEMR.Api.Controllers.PatientReferralsController>.Instance);
        Assert.Equal(403,Assert.IsType<StatusCodeResult>(await controller.PreviewLetter(Guid.NewGuid(),Guid.NewGuid(),default)).StatusCode);
        Assert.Equal(403,Assert.IsType<StatusCodeResult>(await controller.ClinicalSelections(Guid.NewGuid(),Guid.NewGuid(),default)).StatusCode);
    }

    private static PatientReferralApiClient Client(Func<HttpRequestMessage,Task<HttpResponseMessage>> send)
    {
        var properties=new AuthenticationProperties(); properties.StoreTokens([new AuthenticationToken {Name="access_token",Value="test-token"}]);
        var ticket=new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([],"test")),properties,"test");
        var context=new DefaultHttpContext {RequestServices=new ServiceCollection().AddSingleton<IAuthenticationService>(
            Stub<IAuthenticationService>((_,_)=>Task.FromResult(AuthenticateResult.Success(ticket)))).BuildServiceProvider()};
        return new(new HttpClient(new Handler(send)) {BaseAddress=new("https://api.test/")},new HttpContextAccessor {HttpContext=context});
    }
    private sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> send):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            Assert.Equal("Bearer",request.Headers.Authorization!.Scheme);Assert.Equal("test-token",request.Headers.Authorization.Parameter);
            return send(request);
        }
    }
    private static T Stub<T>(Func<string,object?[],object> call) where T:class
    {
        var proxy=DispatchProxy.Create<T,ReferralLetterCompositionTests.StrictProxy>();
        ((ReferralLetterCompositionTests.StrictProxy)(object)proxy).Call=call;return proxy;
    }
}
