using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicConfiguration;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.PatientImmunizations;
using MicroEMR.Application.Patients.Contracts;
using MicroEMR.Application.Patients.Services;
using MicroEMR.Application.ReadAudit;
using MicroEMR.Web.Services.PatientImmunizations;
using Xunit;
using ApiController = MicroEMR.Api.Controllers.PatientImmunizationsController;
using WebController = MicroEMR.Web.Controllers.PatientImmunizationsController;

namespace MicroEMR.Api.Tests;

public sealed class ImmunizationSummaryTests
{
    [Fact]
    public async Task CompleteHistoryIncludesDemographicsDatesHistoricalCliniciansAndCorrections()
    {
        var f = new Fixture();
        f.History = [f.Item("Old historical vaccine", new(1990, 2, 3), "Historical Dr"),
            f.Item("Recent <vaccine>", new(2025, 4, 5), "Dr <Admin>"),
            f.Item("Corrected entry", new(2020, 1, 2), null, "EnteredInError")];
        var bytes = await f.Service.GenerateAsync(f.PatientUid, "trace");
        Assert.Equal(f.Pdf.Bytes, bytes);
        var html = f.Pdf.Html!;
        Assert.Contains("Patient:</dt><dd>Jane Example", html);
        Assert.Contains("DOB:</dt><dd>January 5, 1970", html);
        Assert.Contains("Health Card:</dt><dd>1234567890 AB", html);
        Assert.Contains("February 3, 1990", html);
        Assert.Contains("April 5, 2025", html);
        Assert.Contains("Historical Dr", html);
        Assert.Contains("Dr &lt;Admin&gt;", html);
        Assert.Contains("Recent &lt;vaccine&gt;", html);
        Assert.Contains("Corrected entry", html);
        Assert.Contains("Entered in error", html);
        Assert.Contains("are not valid immunization administrations", html);
        Assert.Contains("Not recorded", html);
        Assert.DoesNotContain("Record-entering user", html);
        Assert.DoesNotContain("<vaccine>", html);
        Assert.Equal("All", f.RequestedStatus);
        Assert.Equal(["audit", "history", "pdf"], f.Events);
        Assert.True(html.IndexOf("Recent &lt;vaccine&gt;", StringComparison.Ordinal) < html.IndexOf("Old historical vaccine", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EmptyHistoryProducesPatientSpecificSummary()
    {
        var f = new Fixture();
        Assert.NotNull(await f.Service.GenerateAsync(f.PatientUid, "trace"));
        Assert.Contains("Jane Example", f.Pdf.Html!);
        Assert.Contains("No immunizations recorded.", f.Pdf.Html!);
        Assert.DoesNotContain("<tbody>", f.Pdf.Html!);
    }

    [Fact]
    public async Task MissingDemographicsAreExplicitAndOrphanHealthCardVersionIsNotUsed()
    {
        var f = new Fixture();
        f.Patient.FirstName = ""; f.Patient.LastName = "";
        f.Patient.DateOfBirth = default; f.Patient.HealthCardNumber = null;
        await f.Service.GenerateAsync(f.PatientUid, "trace");
        Assert.Contains("Patient:</dt><dd>Not recorded", f.Pdf.Html!);
        Assert.Contains("DOB:</dt><dd>Not recorded", f.Pdf.Html!);
        Assert.Contains("Health Card:</dt><dd>Not recorded", f.Pdf.Html!);
        Assert.DoesNotContain("Not recorded AB", f.Pdf.Html!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnknownPatientIncludingForeignTenantUidCannotProduceSummary(bool foreignTenant)
    {
        var f = new Fixture();
        // Tenant-scoped patient reads return null for UIDs not owned by this tenant.
        var requested = foreignTenant ? Guid.NewGuid() : Guid.Empty;
        Assert.Null(await f.Service.GenerateAsync(requested, "trace"));
        Assert.Empty(f.Events);
        Assert.Null(f.Pdf.Html);
    }

    [Fact]
    public async Task CrossPatientClinicalRowsPreventOutput()
    {
        var f = new Fixture();
        f.History = [f.Item("Other patient", new(2020, 1, 1), "Dr", patientUid: Guid.NewGuid())];
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.GenerateAsync(f.PatientUid, "trace"));
        Assert.Null(f.Pdf.Html);
    }

    [Fact]
    public async Task AuditFailurePreventsClinicalReadAndOutput()
    {
        var f = new Fixture { AuditFails = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.GenerateAsync(f.PatientUid, "trace"));
        Assert.Equal(["audit"], f.Events);
        Assert.Null(f.Pdf.Html);
    }

    [Fact]
    public async Task LongHistoryIsNotTruncatedAndUsesWrappingRepeatingTableHeaders()
    {
        var f = new Fixture();
        f.History = Enumerable.Range(1, 120).Select(i => f.Item($"Vaccine {i:D3} {new string('V', 180)}",
            new DateOnly(2000, 1, 1).AddDays(i), new string('C', 200))).ToArray();
        await f.Service.GenerateAsync(f.PatientUid, "trace");
        Assert.Equal(120, f.Pdf.Html!.Split("<tr><td>").Length - 1);
        Assert.Contains("Vaccine 001", f.Pdf.Html!); Assert.Contains("Vaccine 120", f.Pdf.Html!);
        Assert.Contains("overflow-wrap: anywhere", f.Pdf.Html!);
        Assert.Contains("display: table-header-group", f.Pdf.Html!);
    }

    [Fact]
    public async Task ApiAndWebReturnInlinePdfNoStoreAndNotFound()
    {
        var f = new Fixture();
        var api = new ApiController(Proxy<IPatientImmunizationService>(_ => throw new NotSupportedException()),
            NullLogger<ApiController>.Instance) { ControllerContext = new() { HttpContext = new DefaultHttpContext { TraceIdentifier = "trace" } } };
        var result = Assert.IsType<FileContentResult>(await api.Summary(f.PatientUid, f.Service, default));
        Assert.Equal("application/pdf", result.ContentType); Assert.Empty(result.FileDownloadName);
        Assert.Equal("no-store", api.Response.Headers.CacheControl.ToString());
        Assert.IsType<NotFoundResult>(await api.Summary(Guid.NewGuid(), f.Service, default));
        var client = Proxy<IPatientImmunizationApiClient>(call => Task.FromResult<byte[]?>(call.Args[0]!.Equals(f.PatientUid) ? f.Pdf.Bytes : null));
        var web = new WebController(client, NullLogger<WebController>.Instance)
            { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        Assert.Equal("application/pdf", Assert.IsType<FileContentResult>(await web.Summary(f.PatientUid, default)).ContentType);
        Assert.Equal("no-store", web.Response.Headers.CacheControl.ToString());
        Assert.IsType<NotFoundResult>(await web.Summary(Guid.NewGuid(), default));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task WebPreservesApiAccessDenial(HttpStatusCode status)
    {
        var client = Proxy<IPatientImmunizationApiClient>(_ => throw new HttpRequestException("Denied", null, status));
        var web = new WebController(client, NullLogger<WebController>.Instance)
            { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        Assert.Equal((int)status, Assert.IsType<StatusCodeResult>(await web.Summary(Guid.NewGuid(), default)).StatusCode);
    }

    [Fact]
    public async Task SummaryRoutesRequireAuthenticatedPatientReadPermission()
    {
        Assert.Contains(typeof(ApiController).GetCustomAttributes<AuthorizeAttribute>(), a => a.Policy == "Permission:" + PermissionKeys.PatientsView);
        Assert.Contains(typeof(WebController).GetCustomAttributes<AuthorizeAttribute>(), a => a.Policy == "Permission:" + PermissionKeys.PatientsView);
        Assert.Empty(typeof(ApiController).GetMethod("Summary")!.GetCustomAttributes<AllowAnonymousAttribute>());
        var denied = Proxy<ICurrentUserPermissionService>(_ => Task.FromResult(false));
        var requirement = new MicroEMR.Api.Authorization.PermissionRequirement(PermissionKeys.PatientsView);
        var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(new ClaimsIdentity([], "test")), null);
        await new MicroEMR.Api.Authorization.PermissionAuthorizationHandler(denied,
            NullLogger<MicroEMR.Api.Authorization.PermissionAuthorizationHandler>.Instance).HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task WebClientForwardsBearerAndPatientScopedPdfBytes()
    {
        var handler = new PdfHandler();
        var auth = Proxy<IAuthenticationService>(_ => Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity([], "test")), TokenProperties(), "test"))));
        var httpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().AddSingleton(auth).BuildServiceProvider() };
        var client = new PatientImmunizationApiClient(new HttpClient(handler) { BaseAddress = new("https://test.invalid/") },
            new HttpContextAccessor { HttpContext = httpContext });
        var patientUid = Guid.NewGuid();
        Assert.Equal(handler.Bytes, await client.SummaryAsync(patientUid));
        Assert.Equal($"/api/patients/{patientUid}/immunizations/summary/pdf", handler.Path);
        Assert.Equal("Bearer access", handler.Authorization);
    }

    private static AuthenticationProperties TokenProperties()
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = "access" }]);
        return properties;
    }

    private sealed class PdfHandler : HttpMessageHandler
    {
        public byte[] Bytes = Encoding.ASCII.GetBytes("%PDF-test");
        public string? Path, Authorization;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Path = request.RequestUri!.AbsolutePath; Authorization = request.Headers.Authorization!.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Bytes) });
        }
    }

    private sealed class Fixture
    {
        public Guid PatientUid { get; } = Guid.NewGuid();
        public PatientDetailsResponse Patient { get; }
        public IReadOnlyList<PatientImmunizationResponse> History = [];
        public List<string> Events = [];
        public bool AuditFails;
        public string? RequestedStatus;
        public CapturingPdf Pdf { get; }
        public PatientImmunizationSummaryService Service { get; }
        public Fixture()
        {
            Patient = new() { PatientUid = PatientUid, FirstName = "Jane", LastName = "Example", DateOfBirth = new(1970, 1, 5), HealthCardNumber = "1234567890", HealthCardVersion = "AB" };
            Pdf = new(Events);
            Service = new(Proxy<IPatientService>(call => Task.FromResult<PatientDetailsResponse?>(call.Args[0]!.Equals(PatientUid) ? Patient : null)),
                Proxy<IPatientImmunizationService>(call => { Assert.Equal(PatientUid, call.Args[0]); RequestedStatus = (string)call.Args[1]!; Events.Add("history"); return Task.FromResult(History); }),
                Proxy<IClinicConfigurationService>(_ => Task.FromResult(new ClinicConfigurationResponse("Clinic", "UTC", null, null, null, null, null, null, null, null, null, null, null, null, null, null))),
                new ClinicalPrintLayoutRenderer(), Pdf,
                Proxy<IPatientChartReadAuditService>(call => { Assert.Equal(PatientUid, call.Args[0]); Assert.Equal("trace", call.Args[1]); Events.Add("audit"); if (AuditFails) throw new InvalidOperationException("Audit unavailable"); return Task.FromResult(Guid.NewGuid()); }),
                TimeProvider.System);
        }
        public PatientImmunizationResponse Item(string name, DateOnly date, string? clinician, string status = "Completed", Guid? patientUid = null) =>
            new() { ImmunizationUid = Guid.NewGuid(), PatientUid = patientUid ?? PatientUid, VaccineName = name,
                AdministrationDate = date, AdministeredByName = clinician, SourceType = "HistoricalExternal", Status = status,
                CreatedByDisplayName = "Record-entering user", RowVersion = "" };
    }

    private sealed class CapturingPdf(List<string> events) : IPdfRenderer
    {
        public string? Html;
        public byte[] Bytes = Encoding.ASCII.GetBytes("%PDF-test");
        public Task<byte[]> RenderAsync(string html, CancellationToken token = default)
        { Html = html; events.Add("pdf"); return Task.FromResult(Bytes); }
    }

    public class StubProxy : DispatchProxy
    {
        public Func<(string Name, object?[] Args), object?> Handler = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler((method!.Name, args ?? []));
    }
    private static T Proxy<T>(Func<(string Name, object?[] Args), object?> handler) where T : class
    { var proxy = DispatchProxy.Create<T, StubProxy>(); ((StubProxy)(object)proxy).Handler = handler; return proxy; }
}
