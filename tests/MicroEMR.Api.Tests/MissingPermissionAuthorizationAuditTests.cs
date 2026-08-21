using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.SecurityAudit;
using MicroEMR.Application.Tenancy;
using ApiResultHandler = MicroEMR.Api.Authorization.MissingPermissionAuthorizationResultHandler;
using WebResultHandler = MicroEMR.Web.Authorization.MissingPermissionAuthorizationResultHandler;
using Xunit;
using System.Reflection;

namespace MicroEMR.Api.Tests;

public sealed class MissingPermissionAuthorizationAuditTests
{
    [Fact]
    public async Task ApiForbiddenPermissionRecordsOneGovernedEventAndPreservesForbid()
    {
        var repository = new RecordingRepository();
        var tenantUid = Guid.NewGuid();
        var tenant = new TenantContextAccessor();
        tenant.SetTenant(new Tenant(tenantUid));
        var handler = new ApiResultHandler(repository, tenant, NullLogger<ApiResultHandler>.Instance);
        var authentication = new RecordingAuthenticationService();
        var context = Context(SecurityAuditCapabilities.PatientFileDownload, authentication, "actor-opaque", "api-trace");
        var policy = Policy(new PermissionRequirement(PermissionKeys.DocumentsView),
            new PermissionRequirement(PermissionKeys.PatientsView));

        await handler.HandleAsync(_ => Task.CompletedTask, context, policy, PolicyAuthorizationResult.Forbid());
        await handler.HandleAsync(_ => Task.CompletedTask, context, policy, PolicyAuthorizationResult.Forbid());

        var securityEvent = Assert.Single(repository.Events);
        Assert.Equal("actor-opaque", securityEvent.ActorSubject);
        Assert.Null(securityEvent.ClinicalUserId);
        Assert.Equal(tenantUid, securityEvent.TrustedTenantUid);
        Assert.Equal(SecurityAuditCapabilities.PatientFileDownload, securityEvent.Capability);
        Assert.Equal(PermissionKeys.DocumentsView, securityEvent.RequiredPermission);
        Assert.Equal(SecurityAuditSourceApplications.Api, securityEvent.SourceApplication);
        Assert.Equal("api-trace", securityEvent.RequestCorrelationId);
        Assert.Equal(2, authentication.ForbidCalls);
    }

    [Fact]
    public async Task WebForbiddenPermissionRecordsWebEventWithoutUntrustedTenantEnrichment()
    {
        var repository = new RecordingRepository();
        var handler = new WebResultHandler(Services(repository), NullLogger<WebResultHandler>.Instance);
        var authentication = new RecordingAuthenticationService();
        var context = Context(SecurityAuditCapabilities.AppointmentReportExport, authentication, "web-subject", "web-trace");
        var policy = Policy(new MicroEMR.Web.Authorization.WebPermissionRequirement(PermissionKeys.ReportsExport));

        await handler.HandleAsync(_ => Task.CompletedTask, context, policy, PolicyAuthorizationResult.Forbid());

        var securityEvent = Assert.Single(repository.Events);
        Assert.Equal(SecurityAuditCapabilities.AppointmentReportExport, securityEvent.Capability);
        Assert.Equal(PermissionKeys.ReportsExport, securityEvent.RequiredPermission);
        Assert.Equal(SecurityAuditSourceApplications.Web, securityEvent.SourceApplication);
        Assert.Null(securityEvent.TrustedTenantUid);
        Assert.Null(securityEvent.ClinicalUserId);
        Assert.Equal(1, authentication.ForbidCalls);
    }

    [Fact]
    public async Task SuccessfulAuthorizationCreatesNoDenialAndContinuesPipeline()
    {
        var repository = new RecordingRepository();
        var tenant = new TenantContextAccessor();
        var handler = new ApiResultHandler(repository, tenant, NullLogger<ApiResultHandler>.Instance);
        var context = Context(SecurityAuditCapabilities.PatientChartView,
            new RecordingAuthenticationService(), "subject", "success-trace");
        var nextCalls = 0;

        await handler.HandleAsync(_ => { nextCalls++; return Task.CompletedTask; }, context,
            Policy(new PermissionRequirement(PermissionKeys.PatientsView)), PolicyAuthorizationResult.Success());

        Assert.Empty(repository.Events);
        Assert.Equal(1, nextCalls);
    }

    [Fact]
    public async Task PersistenceFailureDoesNotReplaceOriginalDenial()
    {
        var handler = new ApiResultHandler(new RecordingRepository { Failure = new InvalidOperationException("unavailable") },
            new TenantContextAccessor(), NullLogger<ApiResultHandler>.Instance);
        var authentication = new RecordingAuthenticationService();
        var context = Context(SecurityAuditCapabilities.EncounterView, authentication, "subject", "failure-trace");

        await handler.HandleAsync(_ => Task.CompletedTask, context,
            Policy(new PermissionRequirement(PermissionKeys.EncountersView)), PolicyAuthorizationResult.Forbid());

        Assert.Equal(1, authentication.ForbidCalls);
    }

    [Fact]
    public async Task MissingSubjectOrMismatchedPermissionCreatesNoMalformedEvent()
    {
        var repository = new RecordingRepository();
        var handler = new ApiResultHandler(repository, new TenantContextAccessor(), NullLogger<ApiResultHandler>.Instance);
        var authentication = new RecordingAuthenticationService();
        var noSubject = Context(SecurityAuditCapabilities.PatientDocumentView, authentication, null, "trace-1");
        var mismatch = Context(SecurityAuditCapabilities.PatientDocumentView, authentication, "subject", "trace-2");

        await handler.HandleAsync(_ => Task.CompletedTask, noSubject,
            Policy(new PermissionRequirement(PermissionKeys.DocumentsView)), PolicyAuthorizationResult.Forbid());
        await handler.HandleAsync(_ => Task.CompletedTask, mismatch,
            Policy(new PermissionRequirement(PermissionKeys.PatientsView)), PolicyAuthorizationResult.Forbid());

        Assert.Empty(repository.Events);
    }

    [Theory]
    [InlineData(SecurityAuditCapabilities.PatientChartView, PermissionKeys.PatientsView)]
    [InlineData(SecurityAuditCapabilities.EncounterView, PermissionKeys.EncountersView)]
    [InlineData(SecurityAuditCapabilities.PatientDocumentView, PermissionKeys.DocumentsView)]
    [InlineData(SecurityAuditCapabilities.PatientFileDownload, PermissionKeys.DocumentsView)]
    [InlineData(SecurityAuditCapabilities.AppointmentReportRun, PermissionKeys.ReportsView)]
    [InlineData(SecurityAuditCapabilities.AppointmentReportExport, PermissionKeys.ReportsExport)]
    public void GovernedCapabilityMappingUsesPermissionConstants(string capability, string permission)
    {
        Assert.True(SensitiveCapabilityCatalog.TryGetRequiredPermission(capability, out var mapped));
        Assert.Equal(permission, mapped);
    }

    [Theory]
    [InlineData(typeof(MicroEMR.Api.Controllers.PatientsController), "GetByUid", SecurityAuditCapabilities.PatientChartView)]
    [InlineData(typeof(MicroEMR.Api.Controllers.PatientEncountersController), "GetEncounter", SecurityAuditCapabilities.EncounterView)]
    [InlineData(typeof(MicroEMR.Api.Controllers.PatientDocumentsController), "GetDocument", SecurityAuditCapabilities.PatientDocumentView)]
    [InlineData(typeof(MicroEMR.Api.Controllers.PatientFilesController), "Content", SecurityAuditCapabilities.PatientFileDownload)]
    [InlineData(typeof(MicroEMR.Api.Controllers.AppointmentReportsController), "Get", SecurityAuditCapabilities.AppointmentReportRun)]
    [InlineData(typeof(MicroEMR.Api.Controllers.AppointmentReportsController), "Csv", SecurityAuditCapabilities.AppointmentReportExport)]
    [InlineData(typeof(MicroEMR.Web.Controllers.PatientsController), "Details", SecurityAuditCapabilities.PatientChartView)]
    [InlineData(typeof(MicroEMR.Web.Controllers.PatientEncountersController), "Details", SecurityAuditCapabilities.EncounterView)]
    [InlineData(typeof(MicroEMR.Web.Controllers.PatientDocumentsController), "Details", SecurityAuditCapabilities.PatientDocumentView)]
    [InlineData(typeof(MicroEMR.Web.Controllers.PatientFilesController), "Content", SecurityAuditCapabilities.PatientFileDownload)]
    [InlineData(typeof(MicroEMR.Web.Controllers.ReportsController), "AppointmentStatus", SecurityAuditCapabilities.AppointmentReportRun)]
    [InlineData(typeof(MicroEMR.Web.Controllers.ReportsController), "AppointmentStatusCsv", SecurityAuditCapabilities.AppointmentReportExport)]
    public void ApprovedEndpointsCarryControlledSemanticCapability(Type controller, string action, string capability)
    {
        var methods = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.Name == action).ToArray();
        Assert.NotEmpty(methods);
        Assert.Contains(methods, method => method.GetCustomAttribute<SensitiveCapabilityAttribute>()?.Capability == capability);
    }

    private static DefaultHttpContext Context(string capability,
        IAuthenticationService authentication, string? subject, string traceIdentifier)
    {
        var services = new ServiceCollection().AddSingleton(authentication).BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services, TraceIdentifier = traceIdentifier };
        var claims = subject is null ? Array.Empty<Claim>() : [new Claim("sub", subject)];
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new SensitiveCapabilityAttribute(capability)), "sensitive-test"));
        return context;
    }

    private static AuthorizationPolicy Policy(params IAuthorizationRequirement[] requirements) =>
        new(requirements, ["test"]);

    private static IServiceProvider Services(IPlatformSecurityAuditRepository repository) =>
        new ServiceCollection().AddSingleton(repository).BuildServiceProvider();

    private sealed class RecordingRepository : IPlatformSecurityAuditRepository
    {
        public List<MissingPermissionSecurityEvent> Events { get; } = [];
        public Exception? Failure { get; init; }

        public Task RecordMissingPermissionAsync(MissingPermissionSecurityEvent securityEvent,
            CancellationToken cancellationToken = default)
        {
            if (Failure is not null) throw Failure;
            Events.Add(securityEvent);
            return Task.CompletedTask;
        }

        public Task RecordCrossPatientOwnershipAsync(CrossPatientOwnershipSecurityEvent securityEvent,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingAuthenticationService : IAuthenticationService
    {
        public int ForbidCalls { get; private set; }
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            ForbidCalls++;
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal,
            AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme,
            AuthenticationProperties? properties) => Task.CompletedTask;
    }

    private sealed record Tenant(Guid TenantUid) : ITenantContext
    {
        public string TenantKey => "test";
        public string DisplayName => "Test";
    }
}
