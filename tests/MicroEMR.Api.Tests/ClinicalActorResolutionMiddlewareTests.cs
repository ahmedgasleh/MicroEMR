using System.Security.Claims;
using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Api.Middleware;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.SecurityAudit;
using MicroEMR.Application.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ClinicalActorResolutionMiddlewareTests
{
    [Fact]
    public async Task UnmappedAuthenticatedMutationReturns403AndDoesNotExecute()
    {
        var executed = false;
        var securityAudit = new RecordingSecurityAuditRepository();
        var tenantUid = Guid.NewGuid();
        var middleware = new ClinicalUserActorResolutionMiddleware(
            _ => { executed = true; return Task.CompletedTask; },
            NullLogger<ClinicalUserActorResolutionMiddleware>.Instance);
        var context = Context("unmapped", "trace-unresolved");

        await middleware.InvokeAsync(
            context,
            new RejectingAccessor(completedUnresolved: true),
            new TenantContext(tenantUid, "tenant", "Tenant"),
            securityAudit);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(executed);
        var securityEvent = Assert.Single(securityAudit.UnresolvedEvents);
        Assert.Equal("unmapped", securityEvent.ActorSubject);
        Assert.Equal(tenantUid, securityEvent.TrustedTenantUid);
        Assert.Equal(SecurityAuditCapabilities.EncounterEdit, securityEvent.Capability);
        Assert.Equal("Encounters.Edit", securityEvent.RequiredPermission);
        Assert.Equal(SecurityAuditSourceApplications.Api, securityEvent.SourceApplication);
        Assert.Equal("trace-unresolved", securityEvent.RequestCorrelationId);
    }

    [Fact]
    public async Task ReenteredMiddlewareRecordsOneEventAndNeverExecutesEndpoint()
    {
        var executed = false;
        var securityAudit = new RecordingSecurityAuditRepository();
        var middleware = new ClinicalUserActorResolutionMiddleware(
            _ => { executed = true; return Task.CompletedTask; },
            NullLogger<ClinicalUserActorResolutionMiddleware>.Instance);
        var context = Context("inactive-subject", "trace-duplicate");
        var tenant = new TenantContext(Guid.NewGuid(), "tenant", "Tenant");

        await middleware.InvokeAsync(context, new RejectingAccessor(true), tenant, securityAudit);
        context.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(context, new RejectingAccessor(true), tenant, securityAudit);

        Assert.False(executed);
        Assert.Single(securityAudit.UnresolvedEvents);
    }

    [Fact]
    public async Task OperationalResolutionFailureIsNotMisclassified()
    {
        var securityAudit = new RecordingSecurityAuditRepository();
        var middleware = new ClinicalUserActorResolutionMiddleware(
            _ => Task.CompletedTask,
            NullLogger<ClinicalUserActorResolutionMiddleware>.Instance);
        var context = Context("subject", "trace-operational");

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(
            context,
            new ThrowingAccessor(),
            new TenantContext(Guid.NewGuid(), "tenant", "Tenant"),
            securityAudit));

        Assert.Empty(securityAudit.UnresolvedEvents);
    }

    [Fact]
    public async Task UnrelatedMutationRetains403WithoutUnresolvedEvent()
    {
        var securityAudit = new RecordingSecurityAuditRepository();
        var middleware = new ClinicalUserActorResolutionMiddleware(
            _ => Task.CompletedTask,
            NullLogger<ClinicalUserActorResolutionMiddleware>.Instance);
        var context = Context("subject", "trace-unrelated", includeCapability: false);

        await middleware.InvokeAsync(context, new RejectingAccessor(true),
            new TenantContext(Guid.NewGuid(), "tenant", "Tenant"), securityAudit);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Empty(securityAudit.UnresolvedEvents);
    }

    [Fact]
    public async Task AuditPersistenceFailurePreservesGeneric403AndNoExecution()
    {
        var executed = false;
        var securityAudit = new RecordingSecurityAuditRepository { Failure = new InvalidOperationException("down") };
        var middleware = new ClinicalUserActorResolutionMiddleware(
            _ => { executed = true; return Task.CompletedTask; },
            NullLogger<ClinicalUserActorResolutionMiddleware>.Instance);
        var context = Context("subject", "trace-persistence");

        await middleware.InvokeAsync(context, new RejectingAccessor(true),
            new TenantContext(Guid.NewGuid(), "tenant", "Tenant"), securityAudit);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(executed);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("Clinical user access required", body);
        Assert.DoesNotContain("subject", body);
        Assert.Equal(1, securityAudit.UnresolvedAttempts);
    }

    [Fact]
    public async Task ResolvedActorExecutesEndpointWithAttributionAndNoDenialEvent()
    {
        long? actorAtEndpoint = null;
        var securityAudit = new RecordingSecurityAuditRepository();
        var middleware = new ClinicalUserActorResolutionMiddleware(
            context =>
            {
                actorAtEndpoint = ClinicalUserActorContext.GetRequired(context);
                return Task.CompletedTask;
            },
            NullLogger<ClinicalUserActorResolutionMiddleware>.Instance);
        var context = Context("mapped-subject", "trace-resolved");

        await middleware.InvokeAsync(context, new ResolvedAccessor(73),
            new TenantContext(Guid.NewGuid(), "tenant", "Tenant"), securityAudit);

        Assert.Equal(73, actorAtEndpoint);
        Assert.Empty(securityAudit.UnresolvedEvents);
        Assert.Equal(0, securityAudit.UnresolvedAttempts);
    }

    private static DefaultHttpContext Context(string subject, string traceIdentifier, bool includeCapability = true)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", subject)], "Test")),
            TraceIdentifier = traceIdentifier,
            Request = { Method = HttpMethods.Post },
            Response = { Body = new MemoryStream() }
        };
        if (includeCapability)
            context.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
                new EndpointMetadataCollection(
                    new SensitiveCapabilityAttribute(SecurityAuditCapabilities.EncounterEdit)),
                "encounter-addendum-post"));
        return context;
    }

    private sealed class RejectingAccessor(bool completedUnresolved) : IAuthenticatedClinicalUserAccessor
    {
        public Task<long> GetRequiredUserIdAsync(CancellationToken cancellationToken = default) =>
            throw new ClinicalUserResolutionException("Unmapped.", completedUnresolved);
    }

    private sealed class ThrowingAccessor : IAuthenticatedClinicalUserAccessor
    {
        public Task<long> GetRequiredUserIdAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Repository unavailable.");
    }

    private sealed class ResolvedAccessor(long userId) : IAuthenticatedClinicalUserAccessor
    {
        public Task<long> GetRequiredUserIdAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(userId);
    }

    private sealed class RecordingSecurityAuditRepository : IPlatformSecurityAuditRepository
    {
        public List<UnresolvedClinicalActorSecurityEvent> UnresolvedEvents { get; } = [];
        public int UnresolvedAttempts { get; private set; }
        public Exception? Failure { get; init; }

        public Task RecordUnresolvedClinicalActorAsync(UnresolvedClinicalActorSecurityEvent securityEvent,
            CancellationToken cancellationToken = default)
        {
            UnresolvedAttempts++;
            if (Failure is not null) throw Failure;
            UnresolvedEvents.Add(securityEvent);
            return Task.CompletedTask;
        }

        public Task RecordMissingPermissionAsync(MissingPermissionSecurityEvent securityEvent,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task RecordCrossPatientOwnershipAsync(CrossPatientOwnershipSecurityEvent securityEvent,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
