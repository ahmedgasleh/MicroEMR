using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.Scheduling.Contracts;
using MicroEMR.Web.Services.Scheduling;
using Xunit;
using WebController = MicroEMR.Web.Controllers.Scheduling.SchedulingController;

namespace MicroEMR.Api.Tests;

public sealed class NextAvailableAppointmentTransportTests
{
    [Fact]
    public async Task WebUsesBookingTimeZoneReturnsSlotsAndNoStore()
    {
        var request = new NextAvailableAppointmentsRequest { TimeZoneId = "Untrusted/Zone" };
        var client = Proxy<ISchedulingApiClient>((_, args) => {
            Assert.Same(request, args[0]); Assert.Equal(TimeZoneInfo.Local.Id, request.TimeZoneId);
            return Task.FromResult(new NextAvailableAppointmentsResponse { TimeZoneId = request.TimeZoneId });
        });
        var controller = Controller(client);
        Assert.IsType<JsonResult>(await controller.NextAvailable(request, default));
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
    }

    [Theory]
    [InlineData(400,400)] [InlineData(403,403)] [InlineData(401,401)] [InlineData(500,502)]
    public async Task WebPreservesDenialsAndDoesNotReturnAvailabilityOnFailure(int apiStatus, int expected)
    {
        var client = Proxy<ISchedulingApiClient>((_, _) => throw new HttpRequestException("Internal detail", null, (HttpStatusCode)apiStatus));
        var result = await Controller(client).NextAvailable(new(), default);
        var status = result is ObjectResult body ? body.StatusCode : ((StatusCodeResult)result).StatusCode;
        Assert.Equal(expected, status);
    }

    [Fact]
    public async Task ClientForwardsAllCriteriaWeekdaysAndBearerToken()
    {
        var clinician = Guid.NewGuid(); var room = Guid.NewGuid();
        var handler = new Handler();
        var properties = new AuthenticationProperties();
        properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = "test-token" }]);
        var auth = Proxy<IAuthenticationService>((_, _) => Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([], "test")), properties, "test"))));
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().AddSingleton(auth).BuildServiceProvider() };
        var client = new SchedulingApiClient(new HttpClient(handler) { BaseAddress = new("https://test.invalid/") }, new HttpContextAccessor { HttpContext = context }, NullLogger<SchedulingApiClient>.Instance);
        await client.GetNextAvailableAsync(new() { ClinicianUid = clinician, RoomUid = room, StartDate = new(2030,1,4), HorizonDays = 7, Weekdays = [1,3], PreferredStart = new(9,0), PreferredEnd = new(12,0), AppointmentType = "Office Visit", DurationMinutes = 30, TimeZoneId = "UTC" });
        Assert.Equal("Bearer test-token", handler.Authorization);
        Assert.Equal("/api/scheduling/next-available", handler.Uri!.AbsolutePath);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(handler.Uri.Query);
        Assert.Equal(clinician.ToString(), query["clinicianUid"].ToString()); Assert.Equal(room.ToString(), query["roomUid"].ToString());
        Assert.Equal(new[] { "1", "3" }, query["weekdays"].ToArray()); Assert.Equal("2030-01-04", query["startDate"].ToString());
        Assert.Equal("30", query["durationMinutes"].ToString()); Assert.Equal("Office Visit", query["appointmentType"].ToString());
        Assert.Equal("09:00", query["preferredStart"].ToString()); Assert.Equal("12:00", query["preferredEnd"].ToString()); Assert.Equal("UTC", query["timeZoneId"].ToString());
    }
    private static WebController Controller(ISchedulingApiClient client) => new(client, null!, NullLogger<WebController>.Instance) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
    private sealed class Handler : HttpMessageHandler {
        public Uri? Uri; public string? Authorization;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) {
            Uri = request.RequestUri; Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new NextAvailableAppointmentsResponse()) });
        }
    }
    public class Stub : DispatchProxy { public Func<string, object?[], object?> Handler = null!; protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!.Name, args ?? []); }
    private static T Proxy<T>(Func<string, object?[], object?> handler) where T : class { var value = DispatchProxy.Create<T, Stub>(); ((Stub)(object)value).Handler = handler; return value; }
}
