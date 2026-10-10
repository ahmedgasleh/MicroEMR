using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.Scheduling;
using MicroEMR.Application.Scheduling.Contracts;
using MicroEMR.Application.Scheduling.Repositories;
using MicroEMR.Application.Scheduling.Services;
using MicroEMR.Infrastructure.Scheduling;
using MicroEMR.Infrastructure.Tenancy;
using MicroEMR.Web.Services.Scheduling;
using Xunit;
using ApiController = MicroEMR.Api.Controllers.SchedulingController;
using WebController = MicroEMR.Web.Controllers.Scheduling.SchedulingController;

namespace MicroEMR.Api.Tests;

public sealed class NextAvailableAppointmentTests
{
    [Fact]
    public async Task EarliestOrderedProviderSlotsExcludeBusyTimesAndFitWholeDuration()
    {
        var f = new Fixture();
        f.Busy = [new(Utc(8), Utc(8, 30)), new(Utc(9), Utc(10))];
        var request = f.Request(); request.DurationMinutes = 30; request.PreferredEnd = new(11, 0);
        request.AppointmentType = "Consultation";
        var slots = (await f.Service.GetNextAvailableAsync(request)).Slots;
        Assert.Equal(Utc(8, 30), slots[0].StartDateTimeUtc);
        Assert.All(slots, s => { Assert.Equal(f.Provider, s.ClinicianUid); Assert.Equal("Consultation", s.AppointmentType);
            Assert.Equal(TimeSpan.FromMinutes(30), s.EndDateTimeUtc - s.StartDateTimeUtc);
            Assert.True(s.EndDateTimeUtc <= Utc(11));
            Assert.DoesNotContain(f.Busy, p => s.StartDateTimeUtc < p.EndDateTimeUtc && s.EndDateTimeUtc > p.StartDateTimeUtc); });
        Assert.Equal(slots.OrderBy(s => s.StartDateTimeUtc), slots);
        Assert.Equal(1, f.Reads);
    }

    [Fact]
    public async Task WeekdaysAndTimeWindowApplyTogetherWithEarliestAlignedStart()
    {
        var f = new Fixture(); var request = f.Request(); request.HorizonDays = 4;
        request.Weekdays = [1]; request.PreferredStart = new(10, 7); request.PreferredEnd = new(11, 0);
        var slots = (await f.Service.GetNextAvailableAsync(request)).Slots;
        Assert.Equal(new DateTime(2030, 1, 7, 10, 15, 0), slots[0].StartDateTimeLocal);
        Assert.All(slots, s => Assert.Equal(DayOfWeek.Monday, s.StartDateTimeLocal.DayOfWeek));
    }

    [Fact]
    public async Task RoomConstraintIsPassedAlongsideProviderAndNoPatientDataIsReturned()
    {
        var f = new Fixture(); var request = f.Request(); request.RoomUid = f.Room;
        var result = await f.Service.GetNextAvailableAsync(request);
        Assert.All(result.Slots, s => Assert.Equal(f.Room, s.RoomUid));
        Assert.Equal(f.Room, f.ReadRoom);
        Assert.DoesNotContain(typeof(AvailableAppointmentSlot).GetProperties(), p => p.Name.Contains("Patient"));
        Assert.Contains(typeof(ITenantSqlConnectionFactory), typeof(SchedulingReadRepository).GetConstructors().Single().GetParameters().Select(p => p.ParameterType));
    }

    [Fact]
    public async Task EntirelyBusyOrOutsideCalendarWindowReturnsNoResults()
    {
        var f = new Fixture { Busy = [new(Utc(0), Utc(23))] };
        Assert.Empty((await f.Service.GetNextAvailableAsync(f.Request())).Slots);
        f.Busy = []; var request = f.Request(); request.PreferredStart = new(19, 0); request.PreferredEnd = new(20, 0);
        Assert.Empty((await f.Service.GetNextAvailableAsync(request)).Slots);
    }

    [Fact]
    public async Task HorizonIsExclusiveAndOnlyFirstTwentySuggestionsAreReturned()
    {
        var f = new Fixture(); var result = await f.Service.GetNextAvailableAsync(f.Request());
        Assert.Equal(20, result.Slots.Count); Assert.Equal(Utc(0).AddDays(1), f.ReadEnd);
        var request = f.Request(); request.PreferredStart = new(17, 30); request.DurationMinutes = 30;
        var slot = Assert.Single((await f.Service.GetNextAvailableAsync(request)).Slots);
        Assert.Equal(Utc(18), slot.EndDateTimeUtc);
    }

    [Fact]
    public async Task CurrentTimeExcludesPastSlotsAndRetainsAdjacentFreeSlot()
    {
        var f = new Fixture(Utc(8, 7)) { Busy = [new(Utc(8), Utc(8, 30))] };
        Assert.Equal(Utc(8, 30), (await f.Service.GetNextAvailableAsync(f.Request())).Slots[0].StartDateTimeUtc);
    }

    [Theory]
    [InlineData("provider")] [InlineData("room")] [InlineData("inactive")]
    public async Task ForeignTenantOrInactiveResourcesAreRejectedBeforeOccupancyRead(string kind)
    {
        var f = new Fixture(); var request = f.Request();
        if (kind == "provider") request.ClinicianUid = Guid.NewGuid();
        if (kind == "room") request.RoomUid = Guid.NewGuid();
        if (kind == "inactive") f.Resources[0].IsActive = false;
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GetNextAvailableAsync(request));
        Assert.Equal(0, f.Reads);
    }

    [Theory]
    [InlineData("horizon")] [InlineData("duration")] [InlineData("type")] [InlineData("weekday")]
    [InlineData("window")] [InlineData("past")] [InlineData("future")] [InlineData("zone")]
    public async Task InvalidOrUnboundedCriteriaAreRejected(string kind)
    {
        var f = new Fixture(); var r = f.Request();
        switch (kind) {
            case "horizon": r.HorizonDays = 91; break;
            case "duration": r.DurationMinutes = 16; break;
            case "type": r.AppointmentType = "Unknown"; break;
            case "weekday": r.Weekdays = [7]; break;
            case "window": r.PreferredStart = new(12, 0); r.PreferredEnd = new(11, 0); break;
            case "past": r.StartDate = r.StartDate.AddDays(-1); break;
            case "future": r.StartDate = r.StartDate.AddDays(91); break;
            case "zone": r.TimeZoneId = "Unavailable/Zone"; break;
        }
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GetNextAvailableAsync(r));
        Assert.Equal(0, f.Reads);
    }

    [Fact]
    public async Task ConfiguredLowerHorizonIsEnforcedAndOccupancyFailuresFailClosed()
    {
        var f = new Fixture(); var limited = new SchedulingReadService(f.Repository, new Clock(Utc(0)), Options.Create(new NextAvailableSearchOptions { MaxHorizonDays = 2 }));
        var request = f.Request(); request.HorizonDays = 3;
        await Assert.ThrowsAsync<ArgumentException>(() => limited.GetNextAvailableAsync(request));
        f.FailRead = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.GetNextAvailableAsync(f.Request()));
    }

    [Theory]
    [InlineData(2030, 3, 10, 12)] [InlineData(2030, 11, 3, 13)]
    public async Task DstDateUsesCorrectLocalHoursAndUtcOffset(int year, int month, int day, int utcHour)
    {
        var f = new Fixture(new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc));
        var request = f.Request(); request.StartDate = new(year, month, day); request.TimeZoneId = "America/Toronto";
        var slot = (await f.Service.GetNextAvailableAsync(request)).Slots[0];
        Assert.Equal(8, slot.StartDateTimeLocal.Hour); Assert.Equal(utcHour, slot.StartDateTimeUtc.Hour);
    }

    [Fact]
    public async Task ApiSearchReturnsNoStoreAndValidationErrors()
    {
        var f = new Fixture(); var controller = new ApiController(f.Service, null!, null!) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        Assert.IsType<OkObjectResult>(await controller.GetNextAvailable(f.Request()));
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
        Assert.IsType<BadRequestObjectResult>(await controller.GetNextAvailable(new()));
    }

    [Theory]
    [InlineData("Scheduling.View")] [InlineData("Scheduling.Manage")]
    public async Task SearchAndBookingAuthorizationRejectDeniedRoles(string permission)
    {
        foreach (var type in new[] { typeof(ApiController), typeof(WebController) }) {
            Assert.Contains(type.GetCustomAttributes<AuthorizeAttribute>(), a => a.Policy == "Permission:" + PermissionKeys.SchedulingView);
            Assert.Contains(type.GetMethod("CreateAppointment")!.GetCustomAttributes<AuthorizeAttribute>(), a => a.Policy == "Permission:" + PermissionKeys.SchedulingManage);
        }
        var denied = Proxy<ICurrentUserPermissionService>((_, _) => Task.FromResult(false));
        var requirement = new MicroEMR.Api.Authorization.PermissionRequirement(permission);
        var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(new ClaimsIdentity([], "test")), null);
        await new MicroEMR.Api.Authorization.PermissionAuthorizationHandler(denied, NullLogger<MicroEMR.Api.Authorization.PermissionAuthorizationHandler>.Instance).HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task ExistingBookingStillDelegatesToAuthoritativeCreateAndMapsConflict()
    {
        var calls = 0; var conflict = false;
        var repository = Proxy<ISchedulingAppointmentRepository>((name, args) => {
            Assert.Equal("CreateAsync", name); Assert.Equal(7L, args[1]); calls++;
            if (conflict) throw new SchedulingConflictException("Occupied");
            return Task.FromResult(new ScheduleAppointmentListItemResponse { AppointmentUid = Guid.NewGuid() });
        });
        var service = new SchedulingAppointmentService(repository, null!);
        var request = new CreateScheduleAppointmentRequest { PatientUid = Guid.NewGuid(), PrimaryResourceUid = Guid.NewGuid(), StartDateTimeUtc = Utc(8), EndDateTimeUtc = Utc(8, 15) };
        Assert.NotEqual(Guid.Empty, (await service.CreateAsync(request, 7)).AppointmentUid);
        conflict = true;
        await Assert.ThrowsAsync<SchedulingConflictException>(() => service.CreateAsync(request, 7));
        Assert.Equal(2, calls);
    }

    private static DateTime Utc(int hour, int minute = 0) => new(2030, 1, 4, hour, minute, 0, DateTimeKind.Utc);
    private sealed class Clock(DateTime now) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(now); }
    private sealed class Fixture
    {
        public Guid Provider = Guid.NewGuid(), Room = Guid.NewGuid();
        public ScheduleResourceResponse[] Resources;
        public IReadOnlyList<SchedulingBusyPeriod> Busy = [];
        public int Reads; public Guid? ReadRoom; public DateTime ReadEnd; public bool FailRead;
        public ISchedulingReadRepository Repository; public SchedulingReadService Service;
        public Fixture(DateTime? now = null) {
            Resources = [new() { ResourceUid = Provider, ResourceType = "Provider", IsActive = true }, new() { ResourceUid = Room, ResourceType = "Room", IsActive = true }];
            Repository = Proxy<ISchedulingReadRepository>((name, args) => {
                if (name == "GetActiveResourcesAsync") return Task.FromResult<IReadOnlyList<ScheduleResourceResponse>>(Resources);
                Assert.Equal("GetAvailabilityBusyPeriodsAsync", name); Assert.Equal(Provider, args[0]);
                ReadRoom = (Guid?)args[1]; ReadEnd = (DateTime)args[3]!; Reads++;
                if (FailRead) throw new InvalidOperationException("Unavailable schema");
                return Task.FromResult(Busy);
            });
            Service = new(Repository, new Clock(now ?? Utc(0)));
        }
        public NextAvailableAppointmentsRequest Request() => new() { ClinicianUid = Provider, StartDate = new(2030, 1, 4), HorizonDays = 1, TimeZoneId = "UTC" };
    }
    public class Stub : DispatchProxy {
        public Func<string, object?[], object?> Handler = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!.Name, args ?? []);
    }
    private static T Proxy<T>(Func<string, object?[], object?> handler) where T : class {
        var value = DispatchProxy.Create<T, Stub>(); ((Stub)(object)value).Handler = handler; return value;
    }
}
