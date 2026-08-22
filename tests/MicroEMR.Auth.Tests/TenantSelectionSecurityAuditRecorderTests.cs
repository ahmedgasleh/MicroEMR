using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.SecurityAudit;
using MicroEMR.Auth.Services.SecurityAudit;
using Xunit;

namespace MicroEMR.Auth.Tests;

public sealed class TenantSelectionSecurityAuditRecorderTests
{
    [Fact]
    public async Task ValidRejectedSelectionRecordsOneExactEventPerRequest()
    {
        var repository = new RecordingRepository();
        var recorder = Recorder(repository);
        var requestedTenantUid = Guid.NewGuid();
        var context = Context("opaque-auth-subject", "trace-tenant-selection");

        await recorder.TryRecordInvalidMembershipAsync(
            context, "opaque-auth-subject", requestedTenantUid);
        await recorder.TryRecordInvalidMembershipAsync(
            context, "opaque-auth-subject", requestedTenantUid);

        var securityEvent = Assert.Single(repository.Events);
        Assert.Equal("opaque-auth-subject", securityEvent.ActorSubject);
        Assert.Equal(requestedTenantUid, securityEvent.RequestedTenantUid);
        Assert.Equal(SecurityAuditSourceApplications.Auth, securityEvent.SourceApplication);
        Assert.Equal("trace-tenant-selection", securityEvent.RequestCorrelationId);
        Assert.Equal(1, repository.Attempts);
    }

    [Fact]
    public async Task SeparateRejectedPostsEachRecordOneEvent()
    {
        var repository = new RecordingRepository();
        var recorder = Recorder(repository);
        var requestedTenantUid = Guid.NewGuid();

        await recorder.TryRecordInvalidMembershipAsync(
            Context("subject", "trace-one"), "subject", requestedTenantUid);
        await recorder.TryRecordInvalidMembershipAsync(
            Context("subject", "trace-two"), "subject", requestedTenantUid);

        Assert.Equal(2, repository.Events.Count);
        Assert.Equal(["trace-one", "trace-two"],
            repository.Events.Select(value => value.RequestCorrelationId));
    }

    [Fact]
    public async Task AnonymousMalformedOrMissingIdentityInputsAreNotRecorded()
    {
        var repository = new RecordingRepository();
        var recorder = Recorder(repository);
        var anonymous = new DefaultHttpContext { TraceIdentifier = "anonymous" };

        await recorder.TryRecordInvalidMembershipAsync(anonymous, "subject", Guid.NewGuid());
        await recorder.TryRecordInvalidMembershipAsync(Context("subject", "empty-subject"), " ", Guid.NewGuid());
        await recorder.TryRecordInvalidMembershipAsync(Context("subject", "empty-tenant"), "subject", Guid.Empty);

        Assert.Empty(repository.Events);
        Assert.Equal(0, repository.Attempts);
    }

    [Fact]
    public async Task PersistenceFailureIsContainedAfterOneAttempt()
    {
        var repository = new RecordingRepository { Failure = new InvalidOperationException("platform unavailable") };
        var recorder = Recorder(repository);
        var context = Context("subject", "trace-failure");

        await recorder.TryRecordInvalidMembershipAsync(context, "subject", Guid.NewGuid());
        await recorder.TryRecordInvalidMembershipAsync(context, "subject", Guid.NewGuid());

        Assert.Equal(1, repository.Attempts);
        Assert.Empty(repository.Events);
    }

    [Fact]
    public void ControllerOwnsOnlyTheGovernedPostMembershipMismatchBoundary()
    {
        var controller = Read("src", "MicroEMR.Auth", "Controllers", "AccountController.cs");
        var post = controller[controller.IndexOf(
            "public async Task<IActionResult> SelectTenant(TenantSelectionViewModel model)",
            StringComparison.Ordinal)..];
        var pendingValidation = post.IndexOf("pending is null", StringComparison.Ordinal);
        var membershipResolution = post.IndexOf("GetActiveMembershipsAsync", StringComparison.Ordinal);
        var mismatch = post.IndexOf("if (selected is null)", StringComparison.Ordinal);
        var recorder = post.IndexOf("TryRecordInvalidMembershipAsync", StringComparison.Ordinal);
        var existingError = post.IndexOf("The selected clinic is unavailable. Please try again.", StringComparison.Ordinal);
        var continuation = post.IndexOf("StoreContinuationAsync", StringComparison.Ordinal);

        Assert.True(pendingValidation >= 0 && pendingValidation < membershipResolution);
        Assert.True(membershipResolution < mismatch);
        Assert.True(mismatch < recorder);
        Assert.True(recorder < existingError);
        Assert.True(existingError < continuation);
        Assert.Equal(1, Count(post, "TryRecordInvalidMembershipAsync"));
        Assert.Contains("model.SelectedTenantUid is Guid requestedTenantUid && requestedTenantUid != Guid.Empty", post);
        Assert.Contains("user.Id", post);
        Assert.Contains("return View(new TenantSelectionViewModel", post);
    }

    [Fact]
    public void RuntimeDoesNotWireApiTenantMiddlewareOrClinicalResolution()
    {
        var recorder = Read("src", "MicroEMR.Auth", "Services", "SecurityAudit",
            "TenantSelectionSecurityAuditRecorder.cs");
        var apiTenantMiddleware = Read("src", "MicroEMR.Api", "Middleware", "TenantResolutionMiddleware.cs");

        Assert.DoesNotContain("ITenantContext", recorder);
        Assert.DoesNotContain("IClinicalUser", recorder);
        Assert.DoesNotContain("TargetTenantUid", recorder);
        Assert.DoesNotContain("RecordInvalidTenantMembershipAsync", apiTenantMiddleware);
        Assert.DoesNotContain("CrossTenantAccess", recorder);
    }

    private static TenantSelectionSecurityAuditRecorder Recorder(IPlatformSecurityAuditRepository repository) =>
        new(repository, NullLogger<TenantSelectionSecurityAuditRecorder>.Instance);

    private static DefaultHttpContext Context(string subject, string traceIdentifier) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, subject)], "Test")),
        TraceIdentifier = traceIdentifier
    };

    private sealed class RecordingRepository : IPlatformSecurityAuditRepository
    {
        public List<InvalidTenantMembershipSecurityEvent> Events { get; } = [];
        public int Attempts { get; private set; }
        public Exception? Failure { get; init; }

        public Task RecordInvalidTenantMembershipAsync(
            InvalidTenantMembershipSecurityEvent securityEvent,
            CancellationToken cancellationToken = default)
        {
            Attempts++;
            if (Failure is not null) throw Failure;
            Events.Add(securityEvent);
            return Task.CompletedTask;
        }

        public Task RecordMissingPermissionAsync(MissingPermissionSecurityEvent securityEvent,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task RecordCrossPatientOwnershipAsync(CrossPatientOwnershipSecurityEvent securityEvent,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task RecordUnresolvedClinicalActorAsync(UnresolvedClinicalActorSecurityEvent securityEvent,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static int Count(string value, string fragment) =>
        value.Split(fragment, StringSplitOptions.None).Length - 1;

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([Root(), .. parts]));

    private static string Root() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
