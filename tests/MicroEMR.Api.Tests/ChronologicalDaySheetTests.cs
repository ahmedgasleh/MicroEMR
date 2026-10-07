using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.Scheduling.Contracts;
using MicroEMR.Application.Scheduling.Repositories;
using MicroEMR.Application.Scheduling.Services;
using MicroEMR.Infrastructure.Scheduling;
using MicroEMR.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ApiController = MicroEMR.Api.Controllers.SchedulingController;
using WebController = MicroEMR.Web.Controllers.Scheduling.SchedulingController;
using WebModels = MicroEMR.Web.Models.Scheduling;

namespace MicroEMR.Api.Tests;

public sealed class ChronologicalDaySheetTests
{
    private static readonly Guid ProviderA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProviderB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task AllCliniciansUsesSelectedDayAndAscendingStartRatherThanPatientName()
    {
        var request = Request();
        var result = await Service([
            Row("Adam", ProviderA, 10), Row("Yusuf", ProviderB, 8), Row("Brown", ProviderA, 9),
            Row("Cancelled", ProviderA, 7, "Cancelled"), Row("Previous day", ProviderA, -2),
            Row("Next day", ProviderA, 24), Row("Other resource", Guid.NewGuid(), 6)
        ], (method, args) =>
        {
            if (method.Name != "GetAppointmentsAsync") return;
            Assert.Equal(request.Start.UtcDateTime, args![0]);
            Assert.Equal(request.End.UtcDateTime, args[1]);
            Assert.Null(args[2]);
        }).GetDaySheetAsync(request);
        Assert.Equal("Chronological", result.Order);
        Assert.Equal(request.Date, result.Date);
        Assert.Equal("All Clinicians", result.ClinicianScope);
        Assert.Equal(["Yusuf", "Brown", "Adam"], result.Appointments.Select(row => row.PatientName));
        Assert.Equal([8, 9, 10], result.Appointments.Select(row => (int)(row.StartDateTimeUtc - request.Start.UtcDateTime).TotalHours));
        Assert.Equal(["Dr Baker", "Dr Adams", "Dr Adams"], result.Appointments.Select(row => row.ClinicianName));
    }

    [Fact]
    public async Task SelectedClinicianKeepsAscendingTimesAndExcludesOthers()
    {
        var request = Request(); request.ClinicianUids = [ProviderA];
        var result = await Service([Row("Adam", ProviderA, 10), Row("Yusuf", ProviderA, 8), Row("Brown", ProviderB, 9)])
            .GetDaySheetAsync(request);
        Assert.Equal("Dr Adams", result.ClinicianScope);
        Assert.Equal(["Yusuf", "Adam"], result.Appointments.Select(row => row.PatientName));
        Assert.All(result.Appointments, row => Assert.Equal("Dr Adams", row.ClinicianName));
        request.ClinicianUids = [ProviderB, ProviderA];
        Assert.Equal("Dr Adams, Dr Baker", (await Service([]).GetDaySheetAsync(request)).ClinicianScope);
    }

    [Fact]
    public async Task EqualStartTimesUseClinicianThenPatientThenUidWithoutStatusSorting()
    {
        var first = Row("Alpha", ProviderA, 8, "Scheduled"); first.AppointmentUid = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var second = Row("Alpha", ProviderA, 8, "Arrived"); second.AppointmentUid = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var third = Row("Beta", ProviderA, 8, "Completed");
        var fourth = Row("Alpha", ProviderB, 8, "Seen");
        var result = await Service([fourth, third, second, first]).GetDaySheetAsync(Request());
        Assert.Equal(["Scheduled", "Arrived", "Completed", "Seen"], result.Appointments.Select(row => row.Status));
    }

    [Fact]
    public async Task EmptyChronologicalOutputIsValidForAllAndSelectedClinicians()
    {
        var request = Request();
        var all = await Service([]).GetDaySheetAsync(request);
        Assert.Empty(all.Appointments); Assert.Equal("Chronological", all.Order);
        request.ClinicianUids = [ProviderA];
        var selected = await Service([]).GetDaySheetAsync(request);
        Assert.Empty(selected.Appointments); Assert.Equal("Dr Adams", selected.ClinicianScope);
    }

    [Fact]
    public async Task InvalidOrdersFailBeforeReadsAndCanonicalOrderIsReturned()
    {
        foreach (var order in new[] { "", "Descending", "Unknown", null })
        {
            var request = Request(); request.Order = order!;
            await Assert.ThrowsAsync<ArgumentException>(() => Service([], (_, _) => throw new InvalidOperationException("Unexpected read"))
                .GetDaySheetAsync(request));
        }
        var lowercase = Request(); lowercase.Order = "chronological";
        Assert.Equal("Chronological", (await Service([]).GetDaySheetAsync(lowercase)).Order);
    }

    [Fact]
    public async Task SharedEndpointsCarryModeWithNoStoreAndExistingProtection()
    {
        var api = new ApiController(Service([Row("Yusuf", ProviderA, 8)]), null!, null!)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var payload = Assert.IsType<SchedulingDaySheetResponse>(Assert.IsType<OkObjectResult>(await api.GetDaySheet(Request())).Value);
        Assert.Equal("Chronological", payload.Order);
        Assert.Equal("no-store", api.Response.Headers.CacheControl.ToString());
        var invalid = Request(); invalid.Order = "Unknown";
        Assert.IsType<BadRequestObjectResult>(await api.GetDaySheet(invalid));

        var webRequest = new WebModels.SchedulingDaySheetRequest { Order = "Chronological" };
        var webPayload = new WebModels.SchedulingDaySheetResponse { Order = "Chronological", Date = Request().Date, ClinicianScope = "All Clinicians" };
        var client = Proxy<MicroEMR.Web.Services.Scheduling.ISchedulingApiClient>((method, args) =>
        {
            Assert.Equal("GetDaySheetAsync", method.Name); Assert.Same(webRequest, args![0]);
            return Task.FromResult(webPayload);
        });
        var web = new WebController(client, null!, NullLogger<WebController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var view = Assert.IsType<ViewResult>(await web.PrintDaySheet(webRequest, default));
        Assert.Equal("~/Views/Scheduling/PrintDaySheet.cshtml", view.ViewName); Assert.Same(webPayload, view.Model);
        Assert.Equal("no-store", web.Response.Headers.CacheControl.ToString());
        foreach (var type in new[] { typeof(ApiController), typeof(WebController) })
        {
            Assert.Contains(type.GetCustomAttributes<AuthorizeAttribute>(), attribute =>
                attribute.Policy?.EndsWith(PermissionKeys.SchedulingView, StringComparison.Ordinal) == true);
            Assert.Contains(type.GetCustomAttributes<AuthorizeAttribute>(), attribute => attribute.Policy is null);
            var action = type.GetMethod(type == typeof(ApiController) ? "GetDaySheet" : "PrintDaySheet")!;
            Assert.NotNull(action.GetCustomAttribute<HttpGetAttribute>());
            Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
        }
    }

    [Fact]
    public async Task ChronologicalModeRetainsTenantResourceValidationAndReadOnlyRepositoryBoundary()
    {
        var request = Request(); request.ClinicianUids = [Guid.NewGuid()];
        await Assert.ThrowsAsync<ArgumentException>(() => Service([], (method, _) =>
            Assert.Equal("GetActiveResourcesAsync", method.Name)).GetDaySheetAsync(request));
        Assert.Contains(typeof(SchedulingReadRepository).GetConstructors().Single().GetParameters(),
            parameter => parameter.ParameterType == typeof(ITenantSqlConnectionFactory));
    }

    [Fact]
    public async Task WebClientForwardsChronologicalOrderWithBearerAndExistingDateScope()
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = "test-token" }]);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity("test")), properties, "test");
        using var services = new ServiceCollection().AddSingleton(Proxy<IAuthenticationService>((method, _) =>
            method.Name == "AuthenticateAsync" ? Task.FromResult(AuthenticateResult.Success(ticket)) : throw new NotSupportedException()))
            .BuildServiceProvider();
        var request = new WebModels.SchedulingDaySheetRequest
        { Date = Request().Date, Start = Request().Start, End = Request().End, ClinicianUids = [ProviderA], Order = "Chronological" };
        using var http = new HttpClient(new Handler(message =>
        {
            Assert.Equal(HttpMethod.Get, message.Method);
            Assert.Equal("Bearer test-token", message.Headers.Authorization!.ToString());
            Assert.Equal("/api/scheduling/day-sheet", message.RequestUri!.AbsolutePath);
            var query = QueryHelpers.ParseQuery(message.RequestUri.Query);
            Assert.Equal("Chronological", query["order"].ToString());
            Assert.Equal("2026-10-07", query["date"].ToString());
            Assert.Equal(ProviderA.ToString(), query["clinicianUids"].ToString());
            Assert.Equal(request.Start, DateTimeOffset.Parse(query["start"].ToString()));
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = JsonContent.Create(new WebModels.SchedulingDaySheetResponse { Order = "Chronological", Date = request.Date }) };
        })) { BaseAddress = new Uri("https://microemr.test/") };
        var client = new MicroEMR.Web.Services.Scheduling.SchedulingApiClient(http,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext { RequestServices = services } },
            NullLogger<MicroEMR.Web.Services.Scheduling.SchedulingApiClient>.Instance);
        Assert.Equal("Chronological", (await client.GetDaySheetAsync(request)).Order);
    }

    private static SchedulingDaySheetRequest Request() => new()
    {
        Order = "Chronological", Date = new DateOnly(2026, 10, 7),
        Start = new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.FromHours(-4)),
        End = new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.FromHours(-4))
    };
    private static ScheduleAppointmentListItemResponse Row(string name, Guid provider, int hour, string status = "Scheduled") => new()
    {
        AppointmentUid = Guid.NewGuid(), PatientDisplayName = name, PrimaryResourceUid = provider,
        StartDateTimeUtc = Request().Start.UtcDateTime.AddHours(hour), EndDateTimeUtc = Request().Start.UtcDateTime.AddHours(hour).AddMinutes(30), Status = status
    };
    private static SchedulingReadService Service(IReadOnlyList<ScheduleAppointmentListItemResponse> rows,
        Action<MethodInfo, object?[]?>? inspect = null) => new(Proxy<ISchedulingReadRepository>((method, args) =>
    {
        inspect?.Invoke(method, args);
        if (method.Name == "GetActiveResourcesAsync") return Task.FromResult<IReadOnlyList<ScheduleResourceResponse>>([
            new() { ResourceUid = ProviderA, ResourceType = "Provider", IsActive = true, DisplayName = "Dr Adams" },
            new() { ResourceUid = ProviderB, ResourceType = "Provider", IsActive = true, DisplayName = "Dr Baker" }
        ]);
        if (method.Name == "GetAppointmentsAsync") return Task.FromResult(rows);
        throw new InvalidOperationException("Unexpected read or mutation: " + method.Name);
    }));
    private static T Proxy<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, Stub>(); ((Stub)(object)proxy).Handler = handler; return proxy;
    }
    public class Stub : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args);
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
}
