using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MicroEMR.Web.Services.PatientReferrals;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ReferralSupportingDocumentsWebTests
{
    [Fact]
    public async Task ClientUsesPatientReferralDocumentRoutesAndSendsOnlyRowVersionForMutations()
    {
        var patientUid = Guid.NewGuid(); var referralUid = Guid.NewGuid(); var documentUid = Guid.NewGuid();
        var handler = new Handler();
        var client = new PatientReferralApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") },
            new HttpContextAccessor { HttpContext = Context() });

        await client.GetLinkedDocumentsAsync(patientUid, referralUid);
        await client.LinkDocumentAsync(patientUid, referralUid, documentUid, "version");
        await client.UnlinkDocumentAsync(patientUid, referralUid, documentUid, "version");

        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
        Assert.Equal(HttpMethod.Delete, handler.Requests[2].Method);
        Assert.Equal($"api/patients/{patientUid}/referrals/{referralUid}/documents", handler.Requests[0].Path);
        Assert.EndsWith($"/documents/{documentUid}", handler.Requests[1].Path);
        Assert.Equal("{\"rowVersion\":\"version\"}", handler.Requests[1].Body);
        Assert.Equal(handler.Requests[1].Body, handler.Requests[2].Body);
        Assert.All(handler.Requests, x => Assert.Equal("Bearer test-token", x.Authorization));
    }

    private static DefaultHttpContext Context()
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = "test-token" }]);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user")], "test")), properties, "test");
        var services = new ServiceCollection().AddSingleton<IAuthenticationService>(new Auth(ticket)).BuildServiceProvider();
        return new DefaultHttpContext { RequestServices = services };
    }

    private sealed class Handler : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path, string? Body, string? Authorization)> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method, request.RequestUri!.PathAndQuery.TrimStart('/'),
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken),
                request.Headers.Authorization?.ToString()));
            return new HttpResponseMessage(request.Method == HttpMethod.Get ? HttpStatusCode.OK : HttpStatusCode.NoContent)
            { Content = request.Method == HttpMethod.Get ? new StringContent("[]", System.Text.Encoding.UTF8, "application/json") : null };
        }
    }

    private sealed class Auth(AuthenticationTicket ticket) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => Task.FromResult(AuthenticateResult.Success(ticket));
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }
}
