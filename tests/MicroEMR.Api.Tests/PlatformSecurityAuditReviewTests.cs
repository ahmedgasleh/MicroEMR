using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MicroEMR.Application.SecurityAudit;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.PlatformEntitlements;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PlatformSecurityAuditReviewTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SearchDefaultsToBoundedDayAndTwentyFiveAndAuditsZeroResultsOnce()
    {
        var repository = new StubRepository();
        var service = Service(repository);

        var page = await service.SearchAsync(new(), "reviewer", Guid.NewGuid());

        Assert.Equal(Now.AddHours(-24), page.FromUtc);
        Assert.Equal(Now, page.ToUtc);
        Assert.Equal(25, page.PageSize);
        Assert.Empty(page.Items);
        var audit = Assert.Single(repository.Audits);
        Assert.Equal("SecurityAuditSearched", audit.Action);
        Assert.Equal(0, audit.ResultCount);
    }

    [Theory]
    [InlineData(101)]
    [InlineData(0)]
    public async Task SearchRejectsUnboundedPageSizes(int pageSize) =>
        await Assert.ThrowsAsync<SecurityAuditReviewValidationException>(() =>
            Service(new()).SearchAsync(new(PageSize: pageSize), "reviewer", Guid.NewGuid()));

    [Fact]
    public async Task SearchAcceptsMaximumPageAndRejectsOverThirtyOneDaysAndInvalidRange()
    {
        var repository = new StubRepository();
        var service = Service(repository);
        var result = await service.SearchAsync(
            new(Now.AddDays(-31), Now, 100), "reviewer", Guid.NewGuid());
        Assert.Equal(100, result.PageSize);
        await Assert.ThrowsAsync<SecurityAuditReviewValidationException>(() => service.SearchAsync(
            new(Now.AddDays(-31).AddTicks(-1), Now), "reviewer", Guid.NewGuid()));
        await Assert.ThrowsAsync<SecurityAuditReviewValidationException>(() => service.SearchAsync(
            new(Now, Now), "reviewer", Guid.NewGuid()));
    }

    [Fact]
    public async Task SearchPassesApprovedExactCombinedFiltersWithoutTenantContext()
    {
        var repository = new StubRepository();
        var tenant = Guid.NewGuid();
        await Service(repository).SearchAsync(new(
            DenialReason: "InvalidTenantMembership", Capability: "TenantSelection",
            SourceApplication: "MicroEMR.Auth", TargetTenantUid: tenant,
            RequestCorrelationId: "exact-correlation", ActorSubject: "exact-subject"),
            "reviewer", Guid.NewGuid());
        var criteria = Assert.IsType<SecurityAuditSearchCriteria>(repository.LastCriteria);
        Assert.Equal("InvalidTenantMembership", criteria.DenialReason);
        Assert.Equal("TenantSelection", criteria.Capability);
        Assert.Equal("MicroEMR.Auth", criteria.SourceApplication);
        Assert.Equal(tenant, criteria.TargetTenantUid);
        Assert.Equal("exact-correlation", criteria.RequestCorrelationId);
        Assert.Equal("exact-subject", criteria.ActorSubject);
    }

    [Fact]
    public async Task ListIsMinimizedMaskedOrderedAndOneAuditCoversTwentyFiveRows()
    {
        var rows = Enumerable.Range(0, 26).Select(i => ListItem(
            Guid.Parse($"00000000-0000-0000-0000-{(1000 - i):D12}"), Now.AddMinutes(-i),
            (i % 4) switch { 0 => "MissingPermission", 1 => "CrossPatientOwnership",
                2 => "UnresolvedClinicalActor", _ => "InvalidTenantMembership" })).ToArray();
        var repository = new StubRepository(rows);
        var page = await Service(repository).SearchAsync(new(), "reviewer", Guid.NewGuid());
        Assert.Equal(25, page.Items.Count);
        Assert.NotNull(page.ContinuationToken);
        Assert.All(page.Items, item => Assert.Equal("acto...ject", item.MaskedActorSubject));
        Assert.DoesNotContain("RequestedPatientUid", JsonSerializer.Serialize(page));
        Assert.DoesNotContain("ResourceUid", JsonSerializer.Serialize(page));
        Assert.Single(repository.Audits);
        Assert.Equal(25, repository.Audits[0].ResultCount);
    }

    [Fact]
    public async Task ContinuationIsBoundToFiltersAndUsesTimeAndUidKey()
    {
        var repository = new StubRepository([ListItem(Guid.NewGuid(), Now.AddMinutes(-1))]);
        var protector = new StubProtector();
        var service = Service(repository, protector);
        var token = protector.Protect(new(Now.AddMinutes(-2), Guid.NewGuid(),
            Fingerprint(new(Now.AddHours(-24), Now, 25, null, null, null, null, null, null, null, null))));
        await service.SearchAsync(new(ContinuationToken: token), "reviewer", Guid.NewGuid());
        Assert.Equal(Now.AddMinutes(-2), repository.LastCriteria!.CursorOccurredAtUtc);
        Assert.NotNull(repository.LastCriteria.CursorSecurityAuditEventUid);
        await Assert.ThrowsAsync<SecurityAuditReviewValidationException>(() => service.SearchAsync(
            new(ContinuationToken: token, DenialReason: "MissingPermission"), "reviewer", Guid.NewGuid()));
        await Assert.ThrowsAsync<SecurityAuditReviewValidationException>(() => service.SearchAsync(
            new(ContinuationToken: "malformed"), "reviewer", Guid.NewGuid()));
    }

    [Fact]
    public async Task DetailAuditsOnlyKnownDisclosureAndIncludesOnlyApprovedIdentifiers()
    {
        var known = Detail(Guid.NewGuid(), Now, "CrossPatientOwnership");
        var repository = new StubRepository([], known);
        var service = Service(repository);
        Assert.Same(known, await service.GetByUidAsync(known.SecurityAuditEventUid, "reviewer", Guid.NewGuid()));
        Assert.Equal("SecurityAuditViewed", Assert.Single(repository.Audits).Action);
        repository.Detail = null;
        Assert.Null(await service.GetByUidAsync(Guid.NewGuid(), "reviewer", Guid.NewGuid()));
        Assert.Single(repository.Audits);
        var json = JsonSerializer.Serialize(known);
        Assert.DoesNotContain("PatientName", json);
        Assert.DoesNotContain("DocumentTitle", json);
    }

    [Fact]
    public async Task AuditFailureFailsClosedForSearchAndDetail()
    {
        var repository = new StubRepository([ListItem(Guid.NewGuid(), Now)])
        { Detail = Detail(Guid.NewGuid(), Now), FailAudit = true };
        var service = Service(repository);
        await Assert.ThrowsAsync<SecurityAuditDisclosureUnavailableException>(() =>
            service.SearchAsync(new(), "reviewer", Guid.NewGuid()));
        await Assert.ThrowsAsync<SecurityAuditDisclosureUnavailableException>(() =>
            service.GetByUidAsync(repository.Detail.SecurityAuditEventUid, "reviewer", Guid.NewGuid()));
    }

    [Fact]
    public void MigrationCreatesOnlyBoundedKeysetReadsAndExplicitColumnReviewAudit()
    {
        var sql = Read("db", "platform", "019_platform_security_audit_review.sql");
        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.PlatformSecurityAudit_Search", sql);
        Assert.Contains("TOP (@PageSize + 1)", sql);
        Assert.Contains("OccurredAtUtc DESC, SecurityAuditEventUid DESC", sql);
        Assert.Contains("OccurredAtUtc < @CursorOccurredAtUtc", sql);
        Assert.DoesNotContain("OFFSET", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EXEC(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.PlatformSecurityAudit_GetByUid", sql);
        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.PlatformAudit_RecordSecurityAuditReview", sql);
        Assert.Contains("INSERT dbo.PlatformAuditEvent\n    (", sql.Replace("\r\n", "\n"));
        Assert.DoesNotContain("INSERT dbo.PlatformAuditEvent VALUES", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER TABLE dbo.PlatformAuditEvent", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT dbo.PlatformSecurityAuditEvent", sql, StringComparison.OrdinalIgnoreCase);
        var search = sql[sql.IndexOf("CREATE OR ALTER PROCEDURE dbo.PlatformSecurityAudit_Search", StringComparison.Ordinal)
            ..sql.IndexOf("CREATE OR ALTER PROCEDURE dbo.PlatformSecurityAudit_GetByUid", StringComparison.Ordinal)];
        var projection = search[search.IndexOf("SELECT TOP", StringComparison.Ordinal)
            ..search.IndexOf("FROM dbo.PlatformSecurityAuditEvent", StringComparison.Ordinal)];
        Assert.DoesNotContain("\n        ActorSubject,", projection);
        Assert.DoesNotContain("ClinicalUserId", projection);
        Assert.DoesNotContain("RequestedPatientUid", projection);
        Assert.DoesNotContain("AuthoritativePatientUid", projection);
        Assert.DoesNotContain("ResourceUid", projection);
        Assert.Contains("AS MaskedActorSubject", projection);
    }

    [Fact]
    public void ApiIsPostSearchGetDetailNoStoreAndExactEntitlementOnly()
    {
        var controller = typeof(PlatformSecurityAuditController);
        var entitlement = Assert.Single(controller.GetCustomAttributes(
            typeof(RequirePlatformEntitlementAttribute), true).Cast<RequirePlatformEntitlementAttribute>());
        Assert.Equal(PlatformEntitlementPolicies.SecurityAuditView, entitlement.Policy);
        var caching = Assert.Single(controller.GetCustomAttributes(
            typeof(ResponseCacheAttribute), true).Cast<ResponseCacheAttribute>());
        Assert.True(caching.NoStore);
        Assert.NotNull(controller.GetMethod(nameof(PlatformSecurityAuditController.Search))!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpPostAttribute), true).Single());
        Assert.NotNull(controller.GetMethod(nameof(PlatformSecurityAuditController.Get))!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpGetAttribute), true).Single());
        Assert.All(controller.GetConstructors().Single().GetParameters(), parameter =>
            Assert.DoesNotContain(parameter.ParameterType.Name, new[] { "ITenantContext", "IAuthenticatedClinicalUserAccessor" }));
    }

    [Fact]
    public void RepositoryUsesOnlyThreeGovernedStoredProceduresAndNoAdHocDml()
    {
        var source = Read("src", "MicroEMR.Infrastructure", "SecurityAudit",
            "SqlPlatformSecurityAuditReviewRepository.cs");
        Assert.Contains("dbo.PlatformSecurityAudit_Search", source);
        Assert.Contains("dbo.PlatformSecurityAudit_GetByUid", source);
        Assert.Contains("dbo.PlatformAudit_RecordSecurityAuditReview", source);
        Assert.Contains("CommandType = CommandType.StoredProcedure", source);
        Assert.DoesNotContain("SELECT ", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT ", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE ", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE ", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MigrationSequenceAndImmutablePredecessorsRemainSafe()
    {
        var platform = Directory.GetFiles(Path.Combine(Root(), "db", "platform"), "*.sql")
            .Select(Path.GetFileName).Where(x => int.TryParse(x![..3], out _)).ToArray();
        Assert.Single(platform, x => x!.StartsWith("019_", StringComparison.Ordinal));
        Assert.Equal(21, platform.Max(x => int.Parse(x![..3])));
        var tenant = Directory.GetFiles(Path.Combine(Root(), "db", "tenant-clinical", "migrations"), "*.sql")
            .Max(x => int.Parse(Path.GetFileName(x)[..4]));
        Assert.Equal(50, tenant);
        Assert.Equal("59191CC39EACA18C81303B72FFA7A99DB1C728B682612917C3E3A668E211615A",
            Hash("db", "platform", "018_platform_entitlement_foundation.sql"));
    }

    private static PlatformSecurityAuditReviewService Service(
        StubRepository repository, StubProtector? protector = null) =>
        new(repository, protector ?? new(), new FixedTimeProvider(Now));

    private static SecurityAuditDetail Detail(Guid uid, DateTimeOffset occurred, string reason = "MissingPermission") =>
        new(uid, "SecurityAccessDenied", "Denied", reason, "actor-subject", null,
            reason == "InvalidTenantMembership" ? null : Guid.NewGuid(),
            reason == "InvalidTenantMembership" ? Guid.NewGuid() : null,
            reason == "InvalidTenantMembership" ? "TenantSelection" : "PatientChartView",
            reason == "InvalidTenantMembership" ? null : "Patients.View", "MicroEMR.Api", "correlation",
            reason == "CrossPatientOwnership" ? Guid.NewGuid() : null,
            reason == "CrossPatientOwnership" ? Guid.NewGuid() : null,
            reason == "CrossPatientOwnership" ? "Encounter" : null,
            reason == "CrossPatientOwnership" ? Guid.NewGuid() : null, occurred);

    private static SecurityAuditListItem ListItem(Guid uid, DateTimeOffset occurred,
        string reason = "MissingPermission") => new(uid, occurred, reason,
        reason == "InvalidTenantMembership" ? "TenantSelection" : "PatientChartView",
        reason == "InvalidTenantMembership" ? null : "Patients.View", "MicroEMR.Api",
        reason == "InvalidTenantMembership" ? null : Guid.NewGuid(), "correlation", "acto...ject");

    private static string Fingerprint(SecurityAuditSearchCriteria value)
    {
        var canonical = string.Join('|', value.FromUtc.ToString("O"), value.ToUtc.ToString("O"), value.PageSize,
            value.DenialReason, value.Capability, value.SourceApplication, value.TargetTenantUid,
            value.RequestCorrelationId, value.ActorSubject);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    { public override DateTimeOffset GetUtcNow() => now; }

    private sealed class StubProtector : ISecurityAuditContinuationTokenProtector
    {
        private readonly Dictionary<string, SecurityAuditContinuation> _values = [];
        public string Protect(SecurityAuditContinuation continuation)
        { var token = Guid.NewGuid().ToString("N"); _values[token] = continuation; return token; }
        public bool TryUnprotect(string token, out SecurityAuditContinuation continuation) =>
            _values.TryGetValue(token, out continuation!);
    }

    private sealed class StubRepository(
        IReadOnlyList<SecurityAuditListItem>? rows = null, SecurityAuditDetail? detail = null)
        : IPlatformSecurityAuditReviewRepository
    {
        public SecurityAuditSearchCriteria? LastCriteria { get; private set; }
        public SecurityAuditDetail? Detail { get; set; } = detail;
        public bool FailAudit { get; set; }
        public List<(string Action, int? ResultCount)> Audits { get; } = [];
        public Task<IReadOnlyList<SecurityAuditListItem>> SearchAsync(SecurityAuditSearchCriteria criteria,
            CancellationToken cancellationToken = default)
        { LastCriteria = criteria; return Task.FromResult(rows ?? (IReadOnlyList<SecurityAuditListItem>)[]); }
        public Task<SecurityAuditDetail?> GetByUidAsync(Guid securityAuditEventUid,
            CancellationToken cancellationToken = default) => Task.FromResult(Detail);
        public Task RecordReviewAsync(string actorSubject, string action, Guid correlationId,
            Guid? securityAuditEventUid, int? resultCount, string? filterSummary,
            CancellationToken cancellationToken = default)
        {
            if (FailAudit) throw new InvalidOperationException("synthetic audit failure");
            Audits.Add((action, resultCount)); return Task.CompletedTask;
        }
    }

    private static string Hash(params string[] parts) => Convert.ToHexString(
        SHA256.HashData(File.ReadAllBytes(Path.Combine([Root(), .. parts]))));
    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root(), .. parts]));
    private static string Root() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
        "..", "..", "..", "..", ".."));
}
