using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.Security;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Controllers;
using MicroEMR.Web.Models.ClinicConfiguration;
using MicroEMR.Web.Services.ClinicConfiguration;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ClinicConfigurationWebTests
{
    [Fact]
    public async Task ClientGetsCorrectEndpointAndSaveSendsOnlyApprovedFieldsAndRowVersion()
    {
        var handler = new RecordingHandler();
        var client = Client(handler);

        await client.GetAsync();
        await client.SaveAsync(new SaveClinicConfigurationRequest
        {
            Phone = "555-0199", RowVersion = "AAAAAAAAAAE="
        });

        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal("api/clinic-configuration", handler.Requests[0].Uri);
        Assert.Equal(HttpMethod.Put, handler.Requests[1].Method);
        using var json = JsonDocument.Parse(handler.Requests[1].Body!);
        Assert.Equal("AAAAAAAAAAE=", json.RootElement.GetProperty("rowVersion").GetString());
        Assert.False(json.RootElement.TryGetProperty("tenantUid", out _));
        Assert.False(json.RootElement.TryGetProperty("tenantKey", out _));
        Assert.False(json.RootElement.TryGetProperty("updatedBy", out _));
        Assert.False(json.RootElement.TryGetProperty("clinicName", out _));
        Assert.False(json.RootElement.TryGetProperty("timeZoneId", out _));
    }

    [Fact]
    public async Task ClientTurnsConflictIntoSafeTypedHttpFailure()
    {
        var handler = new RecordingHandler { PutStatus = HttpStatusCode.Conflict };
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            Client(handler).SaveAsync(new SaveClinicConfigurationRequest()));
        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
        Assert.Equal("Clinic settings were changed by another user.", exception.Message);
    }

    [Fact]
    public async Task ControllerReloadsLatestValuesAfterStaleUpdate()
    {
        var api = new FakeApiClient { ConflictOnSave = true };
        var controller = new ClinicConfigurationController(api,
            NullLogger<ClinicConfigurationController>.Instance);
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider());
        var result = await controller.Index(new ClinicConfigurationViewModel
        {
            Phone = "stale", RowVersion = "old"
        }, default);

        var view = Assert.IsType<ViewResult>(result);
        var latest = Assert.IsType<ClinicConfigurationViewModel>(view.Model);
        Assert.Equal("latest", latest.Phone);
        Assert.Equal("new", latest.RowVersion);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task ControllerSuccessfulSaveUsesApprovedFieldsAndReturnsToSettings()
    {
        var api = new FakeApiClient();
        var controller = new ClinicConfigurationController(api,
            NullLogger<ClinicConfigurationController>.Instance);
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider());
        var result = await controller.Index(new ClinicConfigurationViewModel
        {
            ClinicName = "Read only clinic",
            TimeZoneId = "Read only zone",
            LegalName = "Legal clinic",
            Phone = "555-0100",
            RowVersion = "current"
        }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Clinic settings saved.", controller.TempData["SuccessMessage"]);
        Assert.NotNull(api.SavedRequest);
        Assert.Equal("Legal clinic", api.SavedRequest!.LegalName);
        Assert.Equal("current", api.SavedRequest.RowVersion);
        Assert.DoesNotContain("ClinicName", api.SavedRequest.GetType().GetProperties().Select(x => x.Name));
        Assert.DoesNotContain("TimeZoneId", api.SavedRequest.GetType().GetProperties().Select(x => x.Name));
    }

    [Fact]
    public void ControllerAndNavigationUseSameNarrowTenantAdminClaimAsApi()
    {
        var authorize = Assert.Single(typeof(ClinicConfigurationController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal(TenantAuthorizationPolicies.ClinicAdministrator, authorize.Policy);
        Assert.Equal("TenantClinicAdministrator", ClinicConfigurationAuthorization.Policy);
        Assert.Equal("ClinicAdministrator", ClinicConfigurationAuthorization.Role);

        var admin = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(MicroEmrClaimTypes.TenantRole, "ClinicAdministrator")], "test"));
        var clinician = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(MicroEmrClaimTypes.TenantRole, "Nurse")], "test"));
        Assert.True(admin.HasClaim(MicroEmrClaimTypes.TenantRole, ClinicConfigurationAuthorization.Role));
        Assert.False(clinician.HasClaim(MicroEmrClaimTypes.TenantRole, ClinicConfigurationAuthorization.Role));
    }

    private static ClinicConfigurationApiClient Client(RecordingHandler handler)
    {
        var authentication = new TestAuthenticationService();
        var services = new ServiceCollection().AddSingleton<IAuthenticationService>(authentication).BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        return new ClinicConfigurationApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") },
            new HttpContextAccessor { HttpContext = context });
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpStatusCode PutStatus { get; init; } = HttpStatusCode.OK;
        public List<(HttpMethod Method, string Uri, string? Body)> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method, request.RequestUri!.PathAndQuery.TrimStart('/'), body));
            var status = request.Method == HttpMethod.Put ? PutStatus : HttpStatusCode.OK;
            return new HttpResponseMessage(status)
            {
                Content = new StringContent("{\"clinicName\":\"Clinic\",\"timeZoneId\":\"UTC\",\"rowVersion\":\"new\"}", Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class FakeApiClient : IClinicConfigurationApiClient
    {
        public bool ConflictOnSave { get; init; }
        public SaveClinicConfigurationRequest? SavedRequest { get; private set; }
        public Task<ClinicConfigurationViewModel> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ClinicConfigurationViewModel { ClinicName = "Clinic", TimeZoneId = "UTC", Phone = "latest", RowVersion = "new" });
        public Task<ClinicConfigurationViewModel> SaveAsync(SaveClinicConfigurationRequest request, CancellationToken cancellationToken = default)
        {
            SavedRequest = request;
            return ConflictOnSave
                ? Task.FromException<ClinicConfigurationViewModel>(new HttpRequestException("conflict", null, HttpStatusCode.Conflict))
                : GetAsync(cancellationToken);
        }
    }

    private sealed class TestAuthenticationService : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(CreateResult());
        private static AuthenticateResult CreateResult()
        {
            var properties = new AuthenticationProperties();
            properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = "token" }]);
            return AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity("test")), properties, "test"));
        }
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        private Dictionary<string, object> _values = [];
        public IDictionary<string, object> LoadTempData(HttpContext context) => _values;
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) =>
            _values = new Dictionary<string, object>(values);
    }
}
