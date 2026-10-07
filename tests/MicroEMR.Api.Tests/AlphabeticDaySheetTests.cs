using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
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

public sealed class AlphabeticDaySheetTests
{
    private static readonly Guid ClinicianA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ClinicianB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Room = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Inactive = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task AllCliniciansUsesSelectedDayAndNamesRatherThanTimeOrder()
    {
        var calls = new List<string>();
        var request = Request();
        var service = Service([
            Appointment("Zulu, Amy", ClinicianA, 8), Appointment("Alpha, Zoe", ClinicianB, 14),
            Appointment("Room patient", Room, 9), Appointment("Inactive patient", Inactive, 9),
            Appointment("Cancelled patient", ClinicianA, 9, "Cancelled"),
            Appointment("Previous day", ClinicianA, -2), Appointment("Next day", ClinicianA, 24)
        ], calls, (start, end, resource) =>
        {
            Assert.Equal(request.Start.UtcDateTime, start);
            Assert.Equal(request.End.UtcDateTime, end);
            Assert.Null(resource);
        });

        var result = await service.GetDaySheetAsync(request);

        Assert.Equal(request.Date, result.Date);
        Assert.Equal("All Clinicians", result.ClinicianScope);
        Assert.Equal(["Alpha, Zoe", "Zulu, Amy"], result.Appointments.Select(row => row.PatientName));
        Assert.Equal(["Dr Baker", "Dr Adams"], result.Appointments.Select(row => row.ClinicianName));
        Assert.Equal(["GetActiveResourcesAsync", "GetAppointmentsAsync"], calls);
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("secret HCN", json);
        Assert.DoesNotContain("private reason", json);
    }

    [Fact]
    public async Task SelectedClinicianExcludesUnselectedCliniciansOnServer()
    {
        var request = Request();
        request.ClinicianUids = [ClinicianB];
        var result = await Service([
            Appointment("Alpha", ClinicianA, 8), Appointment("Zulu", ClinicianB, 8),
            Appointment("Beta", ClinicianB, 12)
        ]).GetDaySheetAsync(request);
        Assert.Equal("Dr Baker", result.ClinicianScope);
        Assert.Equal(["Beta", "Zulu"], result.Appointments.Select(row => row.PatientName));
        Assert.All(result.Appointments, row => Assert.Equal("Dr Baker", row.ClinicianName));
    }

    [Fact]
    public async Task MultipleSelectedCliniciansHaveClearScopeAndOneAppointmentRead()
    {
        var request = Request();
        request.ClinicianUids = [ClinicianB, ClinicianA, ClinicianB];
        var calls = new List<string>();
        var result = await Service([Appointment("Beta", ClinicianB, 9), Appointment("Alpha", ClinicianA, 10)], calls)
            .GetDaySheetAsync(request);
        Assert.Equal("Dr Adams, Dr Baker", result.ClinicianScope);
        Assert.Equal(2, result.Appointments.Count);
        Assert.Equal(1, calls.Count(call => call == "GetAppointmentsAsync"));
    }

    [Fact]
    public async Task DuplicateNamesUseTimeThenAppointmentUidAndPreserveRecordedStatus()
    {
        var later = Appointment("Same, Patient", ClinicianA, 13, "Completed");
        var earlier = Appointment("Same, Patient", ClinicianB, 8, "Arrived");
        var tie = Appointment("Same, Patient", ClinicianA, 8, "Scheduled");
        earlier.AppointmentUid = Guid.Parse("00000000-0000-0000-0000-000000000001");
        tie.AppointmentUid = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var result = await Service([later, tie, earlier]).GetDaySheetAsync(Request());
        Assert.Equal(["Arrived", "Scheduled", "Completed"], result.Appointments.Select(row => row.Status));
    }

    [Fact]
    public async Task EmptyDayIsValidForAllAndSelectedScope()
    {
        var request = Request();
        Assert.Empty((await Service([]).GetDaySheetAsync(request)).Appointments);
        request.ClinicianUids = [ClinicianA];
        var selected = await Service([]).GetDaySheetAsync(request);
        Assert.Empty(selected.Appointments);
        Assert.Equal("Dr Adams", selected.ClinicianScope);
    }

    [Fact]
    public async Task RoomInactiveForeignAndEmptyClinicianSelectionsFailClosedBeforeAppointmentRead()
    {
        foreach (var selection in new Guid[][] { [Room], [Inactive], [Guid.NewGuid()], [Guid.Empty], [] })
        {
            var request = Request();
            request.ClinicianUids = selection;
            var calls = new List<string>();
            await Assert.ThrowsAsync<ArgumentException>(() => Service([], calls).GetDaySheetAsync(request));
            Assert.DoesNotContain("GetAppointmentsAsync", calls);
        }
    }

    [Fact]
    public async Task InvalidDateRangeOrNonMidnightIsRejectedBeforeReads()
    {
        var wrongDate = Request(); wrongDate.Date = wrongDate.Date.AddDays(1);
        var range = Request(); range.End = range.End.AddDays(1);
        var middle = Request(); middle.Start = middle.Start.AddHours(1);
        var missing = new SchedulingDaySheetRequest();
        foreach (var request in new[] { wrongDate, range, middle, missing })
        {
            var calls = new List<string>();
            await Assert.ThrowsAsync<ArgumentException>(() => Service([], calls).GetDaySheetAsync(request));
            Assert.Empty(calls);
        }
    }

    [Theory]
    [InlineData("2026-03-08T00:00:00-05:00", "2026-03-09T00:00:00-04:00", 23)]
    [InlineData("2026-11-01T00:00:00-04:00", "2026-11-02T00:00:00-05:00", 25)]
    public async Task DaylightSavingDaysKeepExactUtcBoundaries(string start, string end, int hours)
    {
        var request = new SchedulingDaySheetRequest
        {
            Start = DateTimeOffset.Parse(start), End = DateTimeOffset.Parse(end),
            Date = DateOnly.FromDateTime(DateTimeOffset.Parse(start).Date)
        };
        await Service([], inspectRange: (actualStart, actualEnd, _) =>
        {
            Assert.Equal(hours, (actualEnd - actualStart).TotalHours);
            Assert.Equal(request.Start.UtcDateTime, actualStart);
            Assert.Equal(request.End.UtcDateTime, actualEnd);
        }).GetDaySheetAsync(request);
    }

    [Fact]
    public async Task ApiIsReadOnlyNoStoreAndRejectsInvalidSelection()
    {
        var calls = new List<string>();
        var controller = new ApiController(Service([], calls), null!, null!)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var response = Assert.IsType<OkObjectResult>(await controller.GetDaySheet(Request()));
        Assert.IsType<SchedulingDaySheetResponse>(response.Value);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
        Assert.Equal(["GetActiveResourcesAsync", "GetAppointmentsAsync"], calls);
        var invalid = Request(); invalid.ClinicianUids = [Room];
        Assert.IsType<BadRequestObjectResult>(await controller.GetDaySheet(invalid));
    }

    [Fact]
    public async Task WebPrintUsesTypedViewAndPreservesScopeAndNoStore()
    {
        var expected = new WebModels.SchedulingDaySheetResponse { Date = Request().Date, ClinicianScope = "Dr Adams" };
        var client = Proxy<MicroEMR.Web.Services.Scheduling.ISchedulingApiClient>((method, arguments) =>
        {
            Assert.Equal("GetDaySheetAsync", method.Name);
            Assert.Equal([ClinicianA], Assert.IsType<WebModels.SchedulingDaySheetRequest>(arguments![0]).ClinicianUids!);
            return Task.FromResult(expected);
        });
        var controller = Web(client);
        var request = new WebModels.SchedulingDaySheetRequest { Date = expected.Date, ClinicianUids = [ClinicianA] };
        var view = Assert.IsType<ViewResult>(await controller.PrintDaySheet(request, default));
        Assert.Equal("~/Views/Scheduling/PrintDaySheet.cshtml", view.ViewName);
        Assert.Same(expected, view.Model);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
        controller.ModelState.AddModelError("date", "invalid");
        Assert.IsType<BadRequestObjectResult>(await controller.PrintDaySheet(request, default));
    }

    [Fact]
    public void BothPrintEndpointsRetainAuthenticationAndSchedulingPermissionAndTenantFactory()
    {
        foreach (var type in new[] { typeof(ApiController), typeof(WebController) })
        {
            var attributes = type.GetCustomAttributes<AuthorizeAttribute>().ToArray();
            Assert.Contains(attributes, attribute => attribute.Policy?.EndsWith(PermissionKeys.SchedulingView, StringComparison.Ordinal) == true);
            Assert.Contains(attributes, attribute => attribute.Policy is null);
            var action = type.GetMethod(type == typeof(ApiController) ? "GetDaySheet" : "PrintDaySheet")!;
            Assert.NotNull(action.GetCustomAttribute<HttpGetAttribute>());
            Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
        }
        Assert.Contains(typeof(SchedulingReadRepository).GetConstructors().Single().GetParameters(),
            parameter => parameter.ParameterType == typeof(ITenantSqlConnectionFactory));
    }

    [Fact]
    public async Task WebApiClientUsesBearerGetAndRoundTripsDayOffsetsAndMultipleClinicians()
    {
        var request = new WebModels.SchedulingDaySheetRequest
        {
            Date = Request().Date, Start = Request().Start, End = Request().End, ClinicianUids = [ClinicianA, ClinicianB]
        };
        var properties = new AuthenticationProperties();
        properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = "test-token" }]);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity("test")), properties, "test");
        var authentication = Proxy<IAuthenticationService>((method, _) =>
            method.Name == "AuthenticateAsync" ? Task.FromResult(AuthenticateResult.Success(ticket)) : throw new NotSupportedException());
        using var services = new ServiceCollection().AddSingleton(authentication).BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        using var http = new HttpClient(new Handler(message =>
        {
            Assert.Equal(HttpMethod.Get, message.Method);
            Assert.Equal("Bearer test-token", message.Headers.Authorization!.ToString());
            Assert.Equal("/api/scheduling/day-sheet", message.RequestUri!.AbsolutePath);
            var query = QueryHelpers.ParseQuery(message.RequestUri.Query);
            Assert.Equal("2026-10-07", query["date"].ToString());
            Assert.Equal(request.Start, DateTimeOffset.Parse(query["start"].ToString()));
            Assert.Equal(request.End, DateTimeOffset.Parse(query["end"].ToString()));
            Assert.Collection(query["clinicianUids"],
                value => Assert.Equal(ClinicianA.ToString(), value),
                value => Assert.Equal(ClinicianB.ToString(), value));
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = JsonContent.Create(new WebModels.SchedulingDaySheetResponse { Date = request.Date, ClinicianScope = "Dr Adams, Dr Baker" }) };
        })) { BaseAddress = new Uri("https://microemr.test/") };
        var client = new MicroEMR.Web.Services.Scheduling.SchedulingApiClient(http,
            new HttpContextAccessor { HttpContext = context }, NullLogger<MicroEMR.Web.Services.Scheduling.SchedulingApiClient>.Instance);
        Assert.Equal(request.Date, (await client.GetDaySheetAsync(request)).Date);
    }

    [Fact]
    public void PrintTemplateContainsRequiredBindingsEmptyStateAndPageSafePrintStyles()
    {
        var root = Root();
        var view = File.ReadAllText(Path.Combine(root, "src/MicroEMR.Web/Views/Scheduling/PrintDaySheet.cshtml"));
        Assert.Contains("@appointment.PatientName", view);
        Assert.Contains("@Model.Date.ToString", view);
        Assert.Contains("@Model.ClinicianScope", view);
        Assert.Contains("No appointments scheduled.", view);
        Assert.Contains("@@media print", view);
        Assert.Contains("table-header-group", view);
        Assert.Contains("break-inside: avoid", view);
        Assert.DoesNotContain("Html.Raw", view);
        Assert.DoesNotContain("HealthCard", view);
        Assert.DoesNotContain("Reason", view);
    }

    private static SchedulingDaySheetRequest Request() => new()
    {
        Date = new DateOnly(2026, 10, 7),
        Start = new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.FromHours(-4)),
        End = new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.FromHours(-4))
    };

    private static ScheduleAppointmentListItemResponse Appointment(string name, Guid clinician, int hour, string status = "Scheduled") => new()
    {
        AppointmentUid = Guid.NewGuid(), PatientUid = Guid.NewGuid(), PatientDisplayName = name,
        PrimaryResourceUid = clinician, StartDateTimeUtc = Request().Start.UtcDateTime.AddHours(hour),
        EndDateTimeUtc = Request().Start.UtcDateTime.AddHours(hour).AddMinutes(30), Status = status,
        PatientHealthCardNumber = "secret HCN", Reason = "private reason"
    };

    private static SchedulingReadService Service(IReadOnlyList<ScheduleAppointmentListItemResponse> rows,
        List<string>? calls = null, Action<DateTime, DateTime, Guid?>? inspectRange = null) =>
        new(Proxy<ISchedulingReadRepository>((method, args) =>
        {
            calls?.Add(method.Name);
            if (method.Name == "GetActiveResourcesAsync")
                return Task.FromResult<IReadOnlyList<ScheduleResourceResponse>>([
                    new() { ResourceUid = ClinicianA, ResourceType = "Provider", IsActive = true, DisplayName = "Dr Adams" },
                    new() { ResourceUid = ClinicianB, ResourceType = "Provider", IsActive = true, DisplayName = "Dr Baker" },
                    new() { ResourceUid = Room, ResourceType = "Room", IsActive = true, DisplayName = "Room 1" },
                    new() { ResourceUid = Inactive, ResourceType = "Provider", IsActive = false, DisplayName = "Inactive" }
                ]);
            if (method.Name == "GetAppointmentsAsync")
            {
                inspectRange?.Invoke((DateTime)args![0]!, (DateTime)args[1]!, (Guid?)args[2]);
                return Task.FromResult(rows);
            }
            throw new InvalidOperationException("Unexpected read/mutation: " + method.Name);
        }));

    private static WebController Web(MicroEMR.Web.Services.Scheduling.ISchedulingApiClient client) =>
        new(client, null!, NullLogger<WebController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

    private static T Proxy<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, Stub>();
        ((Stub)(object)proxy).Handler = handler;
        return proxy;
    }

    public class Stub : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
