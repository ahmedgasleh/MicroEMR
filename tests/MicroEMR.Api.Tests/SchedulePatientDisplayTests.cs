using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.Scheduling.Repositories;
using MicroEMR.Application.Scheduling.Services;
using MicroEMR.Infrastructure.Scheduling;
using MicroEMR.Infrastructure.Tenancy;
using MicroEMR.Web.Services.Scheduling;
using Xunit;
using WebAppointment = MicroEMR.Web.Models.Scheduling.ScheduleAppointmentListItemResponse;
using ApiAppointment = MicroEMR.Application.Scheduling.Contracts.ScheduleAppointmentListItemResponse;
using WebController = MicroEMR.Web.Controllers.Scheduling.SchedulingController;
using ApiController = MicroEMR.Api.Controllers.SchedulingController;

namespace MicroEMR.Api.Tests;

public sealed class SchedulePatientDisplayTests
{
    [Fact]
    public async Task AppointmentReadContractCarriesOnlyRequiredAdditionalPatientFieldsThroughWebEvents()
    {
        var patientUid = Guid.NewGuid();
        var source = new ApiAppointment { AppointmentUid = Guid.NewGuid(), PatientUid = patientUid,
            PatientDisplayName = "Example, Jane", PatientHealthCardNumber = "1234567890",
            PatientDateOfBirth = new(1970, 1, 5), PatientGender = "Female", PrimaryResourceUid = Guid.NewGuid(),
            StartDateTimeUtc = DateTime.UtcNow, EndDateTimeUtc = DateTime.UtcNow.AddMinutes(30), Status = "Scheduled", Reason = "Follow-up" };
        var repository = Proxy<ISchedulingReadRepository>(_ => Task.FromResult<IReadOnlyList<ApiAppointment>>([source]));
        var result = Assert.Single(await new SchedulingReadService(repository).GetAppointmentsAsync(DateTime.UtcNow, DateTime.UtcNow.AddDays(1), null));
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var webAppointment = JsonSerializer.Deserialize<WebAppointment>(JsonSerializer.Serialize(result, options), options)!;
        var client = Proxy<ISchedulingApiClient>(_ => Task.FromResult<IReadOnlyList<WebAppointment>>([webAppointment]));
        var controller = new WebController(client, null!, NullLogger<WebController>.Instance)
            { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        var events = Assert.IsType<JsonResult>(await controller.Events(DateTime.UtcNow, DateTime.UtcNow.AddDays(1), null, default));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(events.Value, options));
        var item = json.RootElement[0];
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
        Assert.Equal("1234567890", item.GetProperty("patientHealthCardNumber").GetString());
        Assert.Equal("1970-01-05", item.GetProperty("patientDateOfBirth").GetString());
        Assert.Equal("Female", item.GetProperty("patientGender").GetString());
        Assert.Equal(source.AppointmentUid, item.GetProperty("id").GetGuid());
        Assert.Equal("Scheduled", item.GetProperty("status").GetString());
        Assert.Equal("Example, Jane - Follow-up", item.GetProperty("text").GetString());
        var patientFields = item.EnumerateObject().Where(p => p.Name.StartsWith("patient", StringComparison.Ordinal)).Select(p => p.Name).Order().ToArray();
        Assert.Equal(new[] { "patientDateOfBirth", "patientDisplayName", "patientGender", "patientHealthCardNumber" }, patientFields);
    }

    [Fact]
    public void NullableDemographicContractsAllowLegacyMissingValues()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var appointment = JsonSerializer.Deserialize<WebAppointment>("{\"patientDisplayName\":\"Legacy Patient\"}", options)!;
        Assert.Equal("Legacy Patient", appointment.PatientDisplayName);
        Assert.Null(appointment.PatientHealthCardNumber); Assert.Null(appointment.PatientDateOfBirth); Assert.Null(appointment.PatientGender);
    }

    [Fact]
    public async Task ExistingScheduleAuthorizationDeniesMissingPermissionAndTenantConnectionIsRetained()
    {
        foreach (var type in new[] { typeof(ApiController), typeof(WebController) })
        {
            Assert.Contains(type.GetCustomAttributes<AuthorizeAttribute>(), a => a.Policy == "Permission:" + PermissionKeys.SchedulingView);
            Assert.Empty(type.GetCustomAttributes<AllowAnonymousAttribute>());
        }
        Assert.Contains(typeof(ITenantSqlConnectionFactory), typeof(SchedulingReadRepository).GetConstructors().Single().GetParameters().Select(p => p.ParameterType));
        var permissions = Proxy<ICurrentUserPermissionService>(_ => Task.FromResult(false));
        var requirement = new MicroEMR.Api.Authorization.PermissionRequirement(PermissionKeys.SchedulingView);
        var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(new ClaimsIdentity([], "test")), null);
        await new MicroEMR.Api.Authorization.PermissionAuthorizationHandler(permissions,
            NullLogger<MicroEMR.Api.Authorization.PermissionAuthorizationHandler>.Instance).HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    public class Stub : DispatchProxy
    {
        public Func<object?> Handler = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler();
    }
    private static T Proxy<T>(Func<object?, object?> handler) where T : class
    { var proxy = DispatchProxy.Create<T, Stub>(); ((Stub)(object)proxy).Handler = () => handler(null); return proxy; }
}
