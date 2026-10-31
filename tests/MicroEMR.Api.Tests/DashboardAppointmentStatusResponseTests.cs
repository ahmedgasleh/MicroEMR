using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Web.Controllers;
using MicroEMR.Web.Models.Dashboard;
using MicroEMR.Web.Services.Scheduling;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class DashboardAppointmentStatusResponseTests
{
    [Theory]
    [InlineData(HttpStatusCode.Forbidden, 403, "You do not have permission to perform this action.")]
    [InlineData(HttpStatusCode.Conflict, 409, "The appointment status cannot be changed from its current state. Refresh and try again.")]
    [InlineData(HttpStatusCode.InternalServerError, 502, "Appointment status could not be updated.")]
    [InlineData(HttpStatusCode.ServiceUnavailable, 502, "Appointment status could not be updated.")]
    [InlineData(HttpStatusCode.Unauthorized, 502, "Appointment status could not be updated.")]
    public async Task UpstreamResponse_ReturnsSafeDashboardJson(
        HttpStatusCode upstreamStatus, int expectedStatus, string expectedMessage)
    {
        using var fixture = new Fixture(upstreamStatus);

        var result = await fixture.Controller.UpdateAppointmentStatus(fixture.Model, default);

        var response = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(JsonSerializer.Serialize(new { success = false, message = expectedMessage }),
            JsonSerializer.Serialize(response.Value));
        Assert.Equal(1, fixture.Handler.RequestCount);
        Assert.Equal($"/api/scheduling/appointments/{fixture.Model.AppointmentUid}/status", fixture.Handler.Path);
        Assert.Equal("Seen", fixture.Model.Status);
        Assert.Equal("Arrived", fixture.Handler.AppointmentStatus);
        Assert.Equal(0, fixture.Handler.SuccessfulMutations);
    }

    [Fact]
    public async Task Forbidden_StopsAtDeniedRequestWithoutRetryOrSuccessDeserialization()
    {
        using var fixture = new Fixture(HttpStatusCode.Forbidden);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => fixture.Client.UpdateAppointmentStatusAsync(
            fixture.Model.AppointmentUid, new() { Status = "Seen" }));

        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.Equal(1, fixture.Handler.RequestCount);
        Assert.Equal("Arrived", fixture.Handler.AppointmentStatus);
        Assert.Equal(0, fixture.Handler.SuccessfulMutations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuthenticationOrConnectionFailure_PreservesGeneric502(bool connectionFailure)
    {
        using var fixture = new Fixture(HttpStatusCode.OK, hasToken: connectionFailure);
        fixture.Handler.ConnectionFailure = connectionFailure;

        var result = await fixture.Controller.UpdateAppointmentStatus(fixture.Model, default);

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(502, response.StatusCode);
        Assert.Equal(JsonSerializer.Serialize(new { success = false, message = "Appointment status could not be updated." }),
            JsonSerializer.Serialize(response.Value));
        Assert.Equal(connectionFailure ? 1 : 0, fixture.Handler.RequestCount);
        Assert.Equal(0, fixture.Handler.SuccessfulMutations);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ServiceProvider _services;
        private readonly HttpClient _http;
        public StatusHandler Handler { get; }
        public SchedulingApiClient Client { get; }
        public HomeController Controller { get; }
        public UpdateDashboardAppointmentStatusViewModel Model { get; } = new()
        {
            AppointmentUid = Guid.NewGuid(), Status = "Seen"
        };

        public Fixture(HttpStatusCode status, bool hasToken = true)
        {
            var properties = new AuthenticationProperties();
            if (hasToken)
                properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = "test-token" }]);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([], "test")), properties, "test");
            _services = new ServiceCollection().AddSingleton(
                CppSelectivePrintTests.Stub<IAuthenticationService>((_, _) =>
                    Task.FromResult(AuthenticateResult.Success(ticket)))).BuildServiceProvider();
            Handler = new StatusHandler(status);
            _http = new HttpClient(Handler) { BaseAddress = new Uri("https://test.invalid/") };
            Client = new SchedulingApiClient(_http,
                new HttpContextAccessor { HttpContext = new DefaultHttpContext { RequestServices = _services } },
                NullLogger<SchedulingApiClient>.Instance);
            Controller = new HomeController(Client, null!, null!, NullLogger<HomeController>.Instance);
        }

        public void Dispose()
        {
            _http.Dispose();
            _services.Dispose();
        }
    }

    // In-memory upstream only: no database or live appointment is accessed.
    private sealed class StatusHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public string? Path { get; private set; }
        public string AppointmentStatus { get; private set; } = "Arrived";
        public int SuccessfulMutations { get; private set; }
        public bool ConnectionFailure { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            Path = request.RequestUri!.AbsolutePath;
            Assert.Equal(HttpMethod.Post, request.Method);
            if (ConnectionFailure)
                throw new HttpRequestException("internal connection details test-token");
            if (status == HttpStatusCode.OK)
            {
                AppointmentStatus = "Seen";
                SuccessfulMutations++;
            }
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("internal server details test-token; not valid JSON")
            });
        }
    }
}
