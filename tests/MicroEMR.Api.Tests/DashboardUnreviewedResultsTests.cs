using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MicroEMR.Web.Services.PatientResults;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class DashboardUnreviewedResultsTests
{
    [Fact]
    public void MigrationCountsOnlyCanonicalNewUnreviewedResultsForActivePatients()
    {
        var sql=File.ReadAllText(Path.Combine(Root(),"db","tenant-clinical","migrations","0027-dashboard-unreviewed-results.sql"));
        Assert.Contains("COUNT_BIG(*)",sql);Assert.Contains("r.ResultStatus = N'New'",sql);
        Assert.Contains("r.ReviewedAt IS NULL",sql);Assert.Contains("p.IsDeleted = 0",sql);
        Assert.DoesNotContain("TenantUid",sql); // tenant isolation is the selected tenant database
    }

    [Fact]
    public async Task WebClientUsesAuthenticatedAggregateEndpointAndDeserializesCount()
    {
        var handler=new Handler();var client=new PatientResultApiClient(new HttpClient(handler){BaseAddress=new Uri("https://api.test/")},new HttpContextAccessor{HttpContext=Context()});
        Assert.Equal(3,await client.GetUnreviewedCount());
        Assert.Equal("api/results/unreviewed-count",handler.Path);Assert.Equal("Bearer test-token",handler.Authorization);
    }

    [Fact]
    public void DashboardRendersRealMetricAndKeepsLiveSectionsWithoutPlaceholders()
    {
        var view=File.ReadAllText(Path.Combine(Root(),"src","MicroEMR.Web","Views","Home","Index.cshtml"));
        Assert.Contains("Unreviewed Results",view);Assert.Contains("Model.UnreviewedResultCount",view);
        Assert.DoesNotContain("Patients Checked In",view);Assert.DoesNotContain("Documents to Review",view);Assert.DoesNotContain(">0</div>",view);
        Assert.Contains("Today's Schedule",view);Assert.Contains("My Open Tasks",view);Assert.Contains("Recent Patients",view);Assert.Contains("Quick Actions",view);
    }

    private sealed class Handler:HttpMessageHandler
    {public string?Path{get;private set;}public string?Authorization{get;private set;}protected override Task<HttpResponseMessage>SendAsync(HttpRequestMessage request,CancellationToken token){Path=request.RequestUri!.PathAndQuery.TrimStart('/');Authorization=request.Headers.Authorization?.ToString();return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"count\":3}",System.Text.Encoding.UTF8,"application/json")});}}
    private static DefaultHttpContext Context(){var properties=new AuthenticationProperties();properties.StoreTokens([new AuthenticationToken{Name="access_token",Value="test-token"}]);var ticket=new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub","user")],"test")),properties,"test");return new DefaultHttpContext{RequestServices=new ServiceCollection().AddSingleton<IAuthenticationService>(new Auth(ticket)).BuildServiceProvider()};}
    private sealed class Auth(AuthenticationTicket ticket):IAuthenticationService{public Task<AuthenticateResult>AuthenticateAsync(HttpContext c,string?s)=>Task.FromResult(AuthenticateResult.Success(ticket));public Task ChallengeAsync(HttpContext c,string?s,AuthenticationProperties?p)=>Task.CompletedTask;public Task ForbidAsync(HttpContext c,string?s,AuthenticationProperties?p)=>Task.CompletedTask;public Task SignInAsync(HttpContext c,string?s,ClaimsPrincipal p,AuthenticationProperties?x)=>Task.CompletedTask;public Task SignOutAsync(HttpContext c,string?s,AuthenticationProperties?p)=>Task.CompletedTask;}
    private static string Root([System.Runtime.CompilerServices.CallerFilePath]string file="")=>Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!,"..",".."));
}
