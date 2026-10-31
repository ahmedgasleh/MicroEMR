using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.Scheduling.Preferences;
using MicroEMR.Web.Services.Scheduling;
using Xunit;
using WebController=MicroEMR.Web.Controllers.SchedulingResourcePreferencesController;

namespace MicroEMR.Api.Tests;

public sealed class SchedulingResourcePreferencesWebTests
{
    [Theory]
    [InlineData(HttpStatusCode.BadRequest,400)]
    [InlineData(HttpStatusCode.Unauthorized,401)]
    [InlineData(HttpStatusCode.Forbidden,403)]
    [InlineData(HttpStatusCode.Conflict,409)]
    [InlineData(HttpStatusCode.InternalServerError,503)]
    public async Task WebSavePreservesSafeValidationDenialConflictAndFailureResponses(HttpStatusCode upstream,int expected)
    {
        using var fixture=new Fixture(upstream);
        var response=await fixture.Controller.Save(new(){ResourceUids=[Guid.NewGuid()]},default);
        var status=response is ObjectResult body?body.StatusCode:Assert.IsType<StatusCodeResult>(response).StatusCode;
        Assert.Equal(expected,status);
        var json=JsonSerializer.Serialize((response as ObjectResult)?.Value);
        Assert.DoesNotContain("internal",json,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("test-access-token",json,StringComparison.Ordinal);
        Assert.Equal(HttpMethod.Put,fixture.Handler.Method);
        Assert.Equal("/api/scheduling/resource-preferences",fixture.Handler.Path);
        Assert.DoesNotContain("userId",fixture.Handler.Body!,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tenant",fixture.Handler.Body!,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAndSaveRoundTripUseNormalBearerFlowAndReturnSafePreferences()
    {
        using var fixture=new Fixture(HttpStatusCode.OK);
        var get=Assert.IsType<JsonResult>(await fixture.Controller.Get(default));
        Assert.IsType<SchedulingResourcePreferencesResponse>(get.Value);Assert.Equal(HttpMethod.Get,fixture.Handler.Method);
        var saved=Assert.IsType<JsonResult>(await fixture.Controller.Save(new(){ResourceUids=[Guid.NewGuid()]},default));
        Assert.IsType<SchedulingResourcePreferencesResponse>(saved.Value);Assert.Equal(HttpMethod.Put,fixture.Handler.Method);
    }

    private sealed class Fixture:IDisposable
    {
        private readonly ServiceProvider services;
        private readonly HttpClient http;
        public Handler Handler {get;}
        public WebController Controller {get;}
        public Fixture(HttpStatusCode status)
        {
            var properties=new AuthenticationProperties();properties.StoreTokens([new AuthenticationToken{Name="access_token",Value="test-access-token"}]);
            var ticket=new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([],"test")),properties,"test");
            services=new ServiceCollection().AddSingleton(CppSelectivePrintTests.Stub<IAuthenticationService>((_,_)=>Task.FromResult(AuthenticateResult.Success(ticket)))).BuildServiceProvider();
            Handler=new Handler(status);http=new HttpClient(Handler){BaseAddress=new Uri("https://test.invalid/")};
            var client=new SchedulingResourcePreferencesApiClient(http,new HttpContextAccessor{HttpContext=new DefaultHttpContext{RequestServices=services}});
            Controller=new WebController(client,NullLogger<WebController>.Instance);
        }
        public void Dispose(){http.Dispose();services.Dispose();}
    }
    private sealed class Handler(HttpStatusCode status):HttpMessageHandler
    {
        public HttpMethod? Method;public string? Path,Body;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Method=request.Method;Path=request.RequestUri!.AbsolutePath;
            Body=request.Content is null?null:await request.Content.ReadAsStringAsync(token);
            Assert.Equal("Bearer test-access-token",request.Headers.Authorization!.ToString());
            return new(status){Content=status==HttpStatusCode.OK?JsonContent.Create(new SchedulingResourcePreferencesResponse([],false,null)):new StringContent("internal API details test-access-token")};
        }
    }
}
