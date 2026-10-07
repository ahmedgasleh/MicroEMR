using System.Reflection;
using System.Security.Claims;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.Scheduling.Contracts;
using MicroEMR.Application.Scheduling.Repositories;
using MicroEMR.Application.Scheduling.Services;
using MicroEMR.Infrastructure.Scheduling;
using MicroEMR.Infrastructure.Tenancy;
using MicroEMR.Web.Services.Scheduling;
using Xunit;
using ApiController = MicroEMR.Api.Controllers.SchedulingController;
using WebController = MicroEMR.Web.Controllers.Scheduling.SchedulingController;
using WebItem = MicroEMR.Web.Models.Scheduling.PatientAppointmentResponse;

namespace MicroEMR.Api.Tests;

public sealed class PatientAppointmentHistoryTests
{
    [Fact]
    public async Task PatientReadRetainsPastFutureCompletedCancelledAndOtherStatusesInOneCall()
    {
        var patientUid = Guid.NewGuid();
        var items = new[] { Item(patientUid, -10, "Completed"), Item(patientUid, -5, "Cancelled"),
            Item(patientUid, -2, "NoShow"), Item(patientUid, 0, "Arrived"), Item(patientUid, 5, "Scheduled") };
        var calls = 0;
        var repository = Proxy<ISchedulingReadRepository>(call => {
            Assert.Equal("GetPatientAppointmentsAsync", call.Name); Assert.Equal(patientUid, call.Args[0]); calls++;
            return Task.FromResult<IReadOnlyList<PatientAppointmentResponse>?>(items);
        });
        var result = await new SchedulingReadService(repository).GetPatientAppointmentsAsync(patientUid);
        Assert.Equal(items, result); Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(0)]
    public async Task PastOnlyFutureOnlyAndEmptyHistoryAreValid(int offset)
    {
        var patientUid = Guid.NewGuid();
        IReadOnlyList<PatientAppointmentResponse> items = offset == 0 ? [] : [Item(patientUid, offset, "Scheduled")];
        var repository = Proxy<ISchedulingReadRepository>(_ => Task.FromResult<IReadOnlyList<PatientAppointmentResponse>?>(items));
        Assert.Equal(items, await new SchedulingReadService(repository).GetPatientAppointmentsAsync(patientUid));
    }

    [Fact]
    public async Task EmptyPatientUidAndCrossPatientRowsAreRejected()
    {
        var repository = Proxy<ISchedulingReadRepository>(_ => Task.FromResult<IReadOnlyList<PatientAppointmentResponse>?>([Item(Guid.NewGuid(), 1, "Scheduled")]));
        var service = new SchedulingReadService(repository);
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetPatientAppointmentsAsync(Guid.Empty));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetPatientAppointmentsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task UnavailablePatientInCurrentTenantReturnsNotFoundAndTenantFactoryRemainsRequired()
    {
        var repository = Proxy<ISchedulingReadRepository>(_ => Task.FromResult<IReadOnlyList<PatientAppointmentResponse>?>(null));
        var service = new SchedulingReadService(repository);
        var api = new ApiController(service, null!, null!) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        Assert.IsType<NotFoundResult>(await api.GetPatientAppointments(Guid.NewGuid()));
        Assert.Contains(typeof(ITenantSqlConnectionFactory), typeof(SchedulingReadRepository).GetConstructors().Single().GetParameters().Select(p => p.ParameterType));
    }

    [Fact]
    public async Task ApiAndWebReturnPatientHistoryAndNoStore()
    {
        var patientUid = Guid.NewGuid();
        var items = new[] { Item(patientUid, -1, "Completed"), Item(patientUid, 1, "Scheduled") };
        var read = Proxy<ISchedulingReadService>(call => {
            Assert.Equal(patientUid, call.Args[0]); return Task.FromResult<IReadOnlyList<PatientAppointmentResponse>?>(items);
        });
        var api = new ApiController(read, null!, null!) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        Assert.Equal(items, Assert.IsType<OkObjectResult>(await api.GetPatientAppointments(patientUid)).Value);
        Assert.Equal("no-store", api.Response.Headers.CacheControl.ToString());
        var client = Proxy<ISchedulingApiClient>(call => {
            Assert.Equal(patientUid, call.Args[0]); return Task.FromResult<IReadOnlyList<WebItem>?>([]);
        });
        var web = new WebController(client, null!, NullLogger<WebController>.Instance)
            { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        Assert.IsType<JsonResult>(await web.PatientAppointments(patientUid, default));
        Assert.Equal("no-store", web.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task PatientAndSchedulePermissionsAreBothRequired()
    {
        foreach (var type in new[] { typeof(ApiController), typeof(WebController) })
        {
            Assert.Contains(type.GetCustomAttributes<AuthorizeAttribute>(), a => a.Policy == "Permission:" + PermissionKeys.SchedulingView);
            var method = type.GetMethod(type == typeof(ApiController) ? "GetPatientAppointments" : "PatientAppointments")!;
            Assert.Contains(method.GetCustomAttributes<AuthorizeAttribute>(), a => a.Policy == "Permission:" + PermissionKeys.PatientsView);
            Assert.Empty(method.GetCustomAttributes<AllowAnonymousAttribute>());
        }
        var denied = Proxy<ICurrentUserPermissionService>(_ => Task.FromResult(false));
        foreach (var key in new[] { PermissionKeys.PatientsView, PermissionKeys.SchedulingView })
        {
            var requirement = new MicroEMR.Api.Authorization.PermissionRequirement(key);
            var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(new ClaimsIdentity([], "test")), null);
            await new MicroEMR.Api.Authorization.PermissionAuthorizationHandler(denied,
                NullLogger<MicroEMR.Api.Authorization.PermissionAuthorizationHandler>.Instance).HandleAsync(context);
            Assert.False(context.HasSucceeded);
        }
    }

    [Fact]
    public async Task ExistingCalendarReadStillUsesOriginalRangeResourceAndMethod()
    {
        var start = DateTime.UtcNow; var end = start.AddDays(1); var resource = Guid.NewGuid();
        var repository = Proxy<ISchedulingReadRepository>(call => {
            Assert.Equal("GetAppointmentsAsync", call.Name); Assert.Equal(start, call.Args[0]);
            Assert.Equal(end, call.Args[1]); Assert.Equal(resource, call.Args[2]);
            return Task.FromResult<IReadOnlyList<ScheduleAppointmentListItemResponse>>([]);
        });
        Assert.Empty(await new SchedulingReadService(repository).GetAppointmentsAsync(start, end, resource));
    }

    [Fact]
    public async Task WebClientUsesPatientScopedRouteBearerAndMissingPatientResponse()
    {
        var patientUid = Guid.NewGuid();
        var handler = new HistoryHandler(Item(patientUid, 1, "Scheduled"));
        var properties = new AuthenticationProperties();
        properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = "test-access" }]);
        var auth = Proxy<IAuthenticationService>(_ => Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity([], "test")), properties, "test"))));
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().AddSingleton(auth).BuildServiceProvider() };
        var client = new SchedulingApiClient(new HttpClient(handler) { BaseAddress = new("https://test.invalid/") },
            new HttpContextAccessor { HttpContext = context }, NullLogger<SchedulingApiClient>.Instance);
        var item = Assert.Single((await client.GetPatientAppointmentsAsync(patientUid))!);
        Assert.Equal(patientUid, item.PatientUid); Assert.Equal("Scheduled", item.Status);
        Assert.Equal($"/api/scheduling/patients/{patientUid}/appointments", handler.Path);
        Assert.Equal("Bearer test-access", handler.Authorization);
        handler.Status = HttpStatusCode.NotFound;
        Assert.Null(await client.GetPatientAppointmentsAsync(Guid.NewGuid()));
    }

    private sealed class HistoryHandler(PatientAppointmentResponse item) : HttpMessageHandler
    {
        public HttpStatusCode Status = HttpStatusCode.OK;
        public string? Path, Authorization;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri!.AbsolutePath; Authorization = request.Headers.Authorization!.ToString();
            return Task.FromResult(new HttpResponseMessage(Status) { Content = JsonContent.Create(new[] { item }) });
        }
    }

    private static PatientAppointmentResponse Item(Guid patientUid, int days, string status) => new()
    { PatientUid = patientUid, AppointmentUid = Guid.NewGuid(), StartDateTimeUtc = DateTime.UtcNow.AddDays(days),
        EndDateTimeUtc = DateTime.UtcNow.AddDays(days).AddMinutes(30), Status = status, PrimaryResourceName = "Dr Example", Reason = "Follow-up" };

    public class Stub : DispatchProxy
    {
        public Func<(string Name, object?[] Args), object?> Handler = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler((method!.Name, args ?? []));
    }
    private static T Proxy<T>(Func<(string Name, object?[] Args), object?> handler) where T : class
    { var proxy = DispatchProxy.Create<T, Stub>(); ((Stub)(object)proxy).Handler = handler; return proxy; }
}
