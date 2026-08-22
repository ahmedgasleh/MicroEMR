using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.PlatformEntitlements;
using MicroEMR.Application.SecurityAudit;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Controllers;
using MicroEMR.Web.Models.SecurityAudit;
using MicroEMR.Web.Services.SecurityAudit;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class SecurityAuditUiTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task InitialPageUsesApiDefaultsAndFixedPageSizeWithoutDetailPrefetch()
    {
        var client = new StubClient(Page([]));
        var result = Assert.IsType<ViewResult>(await Controller(client).Index(default));
        var model = Assert.IsType<SecurityAuditIndexViewModel>(result.Model);

        var request = Assert.Single(client.SearchRequests);
        Assert.Null(request.FromUtc);
        Assert.Null(request.ToUtc);
        Assert.Equal(25, request.PageSize);
        Assert.Empty(model.Results!.Items);
        Assert.Equal(0, client.DetailCalls);
    }

    [Fact]
    public async Task ApplyMapsAllExactFiltersAndClearsOldContinuation()
    {
        var client = new StubClient(Page([]));
        var tenant = Guid.NewGuid();
        var filters = new SecurityAuditSearchForm
        {
            FromUtc = Now.AddDays(-1).UtcDateTime, ToUtc = Now.UtcDateTime,
            DenialReason = "MissingPermission", Capability = "PatientChartView",
            SourceApplication = "MicroEMR.Api", TargetTenantUid = tenant,
            RequestCorrelationId = "exact-correlation", ActorSubject = "exact-subject",
            ContinuationToken = "old-token"
        };
        var result = Assert.IsType<ViewResult>(await Controller(client).Search(filters, default));
        var request = Assert.Single(client.SearchRequests);
        Assert.Null(request.ContinuationToken);
        Assert.Equal("MissingPermission", request.DenialReason);
        Assert.Equal("PatientChartView", request.Capability);
        Assert.Equal("MicroEMR.Api", request.SourceApplication);
        Assert.Equal(tenant, request.TargetTenantUid);
        Assert.Equal("exact-correlation", request.RequestCorrelationId);
        Assert.Equal("exact-subject", request.ActorSubject);
        Assert.Null(filters.ContinuationToken);
        Assert.Null(Assert.IsType<SecurityAuditIndexViewModel>(result.Model).Filters.ActorSubject);
    }

    [Fact]
    public async Task OlderUsesProtectedContinuationAndCurrentFilters()
    {
        var client = new StubClient(Page([]));
        var protector = new StubPagingProtector();
        var state = new SecurityAuditSearchForm
        {
            FromUtc = Now.AddDays(-1).UtcDateTime, ToUtc = Now.UtcDateTime,
            DenialReason = "CrossPatientOwnership", ContinuationToken = "protected-token"
        };
        await Controller(client, protector).Older(protector.Protect(state), default);
        var request = Assert.Single(client.SearchRequests);
        Assert.Equal("protected-token", request.ContinuationToken);
        Assert.Equal("CrossPatientOwnership", request.DenialReason);
    }

    [Fact]
    public async Task InvalidRangeIsShownWithoutApiDisclosureRequest()
    {
        var client = new StubClient(Page([]));
        var result = Assert.IsType<ViewResult>(await Controller(client).Search(new SecurityAuditSearchForm
        {
            FromUtc = Now.AddDays(-32).UtcDateTime, ToUtc = Now.UtcDateTime
        }, default));
        var model = Assert.IsType<SecurityAuditIndexViewModel>(result.Model);
        Assert.True(model.IsValidationError);
        Assert.Contains("31 days", model.ErrorMessage);
        Assert.Empty(client.SearchRequests);
    }

    [Fact]
    public async Task DetailIsFetchedOnlyOnExplicitDetailsAction()
    {
        var detail = Detail(Guid.NewGuid());
        var client = new StubClient(Page([])) { Detail = detail };
        var result = Assert.IsType<ViewResult>(await Controller(client).Details(detail.SecurityAuditEventUid, default));
        Assert.Same(detail, Assert.IsType<SecurityAuditDetailViewModel>(result.Model).Detail);
        Assert.Equal(1, client.DetailCalls);
        Assert.Empty(client.SearchRequests);
    }

    [Fact]
    public void ControllerAndNavigationUseExactEntitlementWithoutRoleFallback()
    {
        var attribute = Assert.Single(typeof(PlatformSecurityAuditController)
            .GetCustomAttributes(typeof(RequirePlatformEntitlementAttribute), true)
            .Cast<RequirePlatformEntitlementAttribute>());
        Assert.Equal(PlatformEntitlementPolicies.SecurityAuditView, attribute.Policy);
        var sidebar = Read("src", "MicroEMR.Web", "Views", "Shared", "_Sidebar.cshtml");
        Assert.Contains("PlatformEntitlements.HasAsync", sidebar);
        Assert.Contains("PlatformEntitlementKeys.SecurityAuditView", sidebar);
        var platformSection = sidebar[sidebar.IndexOf("Platform Administration", StringComparison.Ordinal)..];
        Assert.DoesNotContain("IsInRole", platformSection);
        Assert.DoesNotContain("PermissionKeys", platformSection);
    }

    [Fact]
    public void ListIsMinimizedAndDetailClearlyLabelsUntrustedRequestedTenant()
    {
        var list = Read("src", "MicroEMR.Web", "Views", "PlatformSecurityAudit", "Index.cshtml");
        Assert.Contains("MaskedActorSubject", list);
        Assert.Contains("Trusted Tenant", list);
        Assert.DoesNotContain("RequestedPatientUid", list);
        Assert.DoesNotContain("AuthoritativePatientUid", list);
        Assert.DoesNotContain("ResourceUid", list);
        Assert.DoesNotContain("RequestedTenantUid", list);
        var detail = Read("src", "MicroEMR.Web", "Views", "PlatformSecurityAudit", "Details.cshtml");
        Assert.Contains("Requested Tenant (untrusted)", detail);
        Assert.Contains("RequestedPatientUid", detail);
        Assert.Contains("AuthoritativePatientUid", detail);
        Assert.DoesNotContain("PatientName", detail);
        Assert.DoesNotContain("DocumentTitle", detail);
    }

    [Fact]
    public void UiHasLoadingEmptyErrorPagingAndNoExportOrMutationActions()
    {
        var list = Read("src", "MicroEMR.Web", "Views", "PlatformSecurityAudit", "Index.cshtml");
        Assert.Contains("data-security-audit-loading", list);
        Assert.Contains("No security audit events were found", list);
        Assert.Contains("ErrorMessage", list);
        Assert.Contains("PagingStateToken", list);
        Assert.Contains("Older events", list);
        Assert.DoesNotContain("pageNumber", list, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("type=\"hidden\" name=\"ActorSubject\"", list, StringComparison.OrdinalIgnoreCase);
        foreach (var forbidden in new[] { "Export", "CSV", "Download", "Delete", "Edit", "Mark reviewed" })
            Assert.DoesNotContain(forbidden, list, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApiClientCallsOnlyStep23AEndpointsAndClassifiesUnauthorized()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.Forbidden));
        var client = new SecurityAuditApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") });
        var exception = await Assert.ThrowsAsync<SecurityAuditApiException>(() => client.SearchAsync(new()));
        Assert.Equal(SecurityAuditApiFailure.Unauthorized, exception.Failure);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("api/platform/security-audit/search", request.RequestUri!.PathAndQuery.TrimStart('/'));
    }

    private static PlatformSecurityAuditController Controller(
        ISecurityAuditApiClient client, ISecurityAuditPagingStateProtector? protector = null) =>
        new(client, protector ?? new StubPagingProtector(),
            NullLogger<PlatformSecurityAuditController>.Instance);

    private static SecurityAuditSearchPage Page(IReadOnlyList<SecurityAuditListItem> items) =>
        new(items, null, Now.AddDays(-1), Now, 25);

    private static SecurityAuditDetail Detail(Guid uid) => new(uid, "SecurityAccessDenied", "Denied",
        "InvalidTenantMembership", "subject", null, null, Guid.NewGuid(), "TenantSelection", null,
        "MicroEMR.Auth", "correlation", null, null, null, null, Now);

    private sealed class StubClient(SecurityAuditSearchPage page) : ISecurityAuditApiClient
    {
        public List<SecurityAuditSearchRequest> SearchRequests { get; } = [];
        public int DetailCalls { get; private set; }
        public SecurityAuditDetail? Detail { get; init; }
        public Task<SecurityAuditSearchPage> SearchAsync(SecurityAuditSearchRequest request,
            CancellationToken cancellationToken = default)
        { SearchRequests.Add(request); return Task.FromResult(page); }
        public Task<SecurityAuditDetail?> GetAsync(Guid securityAuditEventUid,
            CancellationToken cancellationToken = default)
        { DetailCalls++; return Task.FromResult(Detail); }
    }

    private sealed class RecordingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new HttpRequestMessage(request.Method, request.RequestUri));
            return Task.FromResult(response);
        }
    }

    private sealed class StubPagingProtector : ISecurityAuditPagingStateProtector
    {
        private readonly Dictionary<string, SecurityAuditSearchForm> _states = [];
        public string Protect(SecurityAuditSearchForm state)
        { var token = Guid.NewGuid().ToString("N"); _states[token] = state; return token; }
        public bool TryUnprotect(string token, out SecurityAuditSearchForm state) =>
            _states.TryGetValue(token, out state!);
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root(), .. parts]));
    private static string Root() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
        "..", "..", "..", "..", ".."));
}
