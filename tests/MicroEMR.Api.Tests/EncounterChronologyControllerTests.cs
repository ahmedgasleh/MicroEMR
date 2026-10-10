using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientEncounters.Chronology;
using MicroEMR.Web.Services.PatientEncounters;
using Xunit;
using ChronologyApi = MicroEMR.Api.Controllers.EncounterChronologyController;
using ChronologyWeb = MicroEMR.Web.Controllers.PatientEncountersController;

namespace MicroEMR.Api.Tests;

public sealed class EncounterChronologyControllerTests
{
    [Fact]
    public async Task ApiReturnsOnePatientScopedModelWithNoStoreAndRequestCorrelation()
    {
        var patient = Guid.NewGuid(); var criteria = new EncounterChronologyRequest();
        var service = Proxy<IEncounterChronologyService>((name, args) => {
            Assert.Equal("GetAsync", name); Assert.Equal(patient, args[0]); Assert.Same(criteria, args[1]); Assert.Equal("correlation", args[2]);
            return Task.FromResult<EncounterChronologyResponse?>(Response(patient, criteria));
        });
        var api = Controller(service);
        var result = Assert.IsType<OkObjectResult>(await api.Get(patient, criteria, default));
        Assert.Equal(patient, Assert.IsType<EncounterChronologyResponse>(result.Value).PatientUid);
        Assert.Equal("no-store", api.Response.Headers.CacheControl.ToString());
    }

    [Theory]
    [InlineData("validation")][InlineData("denied")][InlineData("missing")][InlineData("unavailable")]
    public async Task ApiFailsSafelyWithoutReturningClinicalContent(string failure)
    {
        var service = Proxy<IEncounterChronologyService>((_, _) => failure switch {
            "validation" => throw new ArgumentException("private validation"), "denied" => throw new UnauthorizedAccessException("foreign patient"),
            "unavailable" => throw new IOException("private storage key"), _ => Task.FromResult<EncounterChronologyResponse?>(null) });
        var result = await Controller(service).Get(Guid.NewGuid(), new(), default);
        switch (failure) {
            case "validation": Assert.IsType<BadRequestObjectResult>(result); break;
            case "denied": Assert.IsType<ForbidResult>(result); break;
            case "missing": Assert.IsType<NotFoundResult>(result); break;
            default: var problem = Assert.IsType<ObjectResult>(result); Assert.Equal(503, problem.StatusCode); Assert.DoesNotContain("private", Assert.IsType<ProblemDetails>(problem.Value).Detail!); break;
        }
    }

    [Fact]
    public void ViewAndAttachmentRoutesRequireBothPatientAndEncounterPermissions()
    {
        var api = typeof(ChronologyApi).GetCustomAttributes<AuthorizeAttribute>().Select(x => x.Policy);
        Assert.Contains("Permission:" + PermissionKeys.PatientsView, api); Assert.Contains("Permission:" + PermissionKeys.EncountersView, api);
        foreach (var action in new[] { "Chronology", "ChronologyAttachment" })
            Assert.Contains(typeof(ChronologyWeb).GetMethod(action)!.GetCustomAttributes<AuthorizeAttribute>(), x => x.Policy == "Permission:" + PermissionKeys.PatientsView);
        Assert.Contains(typeof(ChronologyWeb).GetCustomAttributes<AuthorizeAttribute>(), x => x.Policy == "Permission:" + PermissionKeys.EncountersView);
    }

    [Fact]
    public async Task WebUsesTrustedDisplayTimeZoneAndDisplaysSameOrderedModelThatWillPrint()
    {
        var patient = Guid.NewGuid(); var criteria = new EncounterChronologyRequest { TimeZoneId = "Untrusted/Zone", Direction = "Descending" };
        var expected = Response(patient, criteria);
        var client = Proxy<IPatientEncounterApiClient>((name, args) => {
            Assert.Equal("GetChronologyAsync", name); Assert.Equal(TimeZoneInfo.Local.Id, ((EncounterChronologyRequest)args[1]!).TimeZoneId);
            return Task.FromResult<EncounterChronologyResponse?>(expected);
        });
        var web = new ChronologyWeb(client, null!, null!, null!, NullLogger<ChronologyWeb>.Instance) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        var result = Assert.IsType<ViewResult>(await web.Chronology(patient, criteria, default));
        Assert.Same(expected, result.Model); Assert.Equal("no-store", web.Response.Headers.CacheControl.ToString());
    }

    private static ChronologyApi Controller(IEncounterChronologyService service) => new(service, NullLogger<ChronologyApi>.Instance) {
        ControllerContext = new() { HttpContext = new DefaultHttpContext { TraceIdentifier = "correlation" } } };
    private static EncounterChronologyResponse Response(Guid patient, EncounterChronologyRequest criteria) =>
        new(patient, "Test patient", "CHART", "HCN", new(2000,1,1), criteria, [], [], DateTime.UtcNow);
    private static T Proxy<T>(Func<string, object?[], object?> handler) where T : class {
        var value = DispatchProxy.Create<T, EncounterChronologyTests.Stub>(); ((EncounterChronologyTests.Stub)(object)value).Handler = handler; return value;
    }
}
