using System.Text;
using Microsoft.AspNetCore.Authorization;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.Reporting;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Web.Authorization;
using MicroEMR.Application.Scheduling;
using MicroEMR.Application.Tenancy;
using MicroEMR.Core.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class AppointmentStatusReportTests
{
    private static readonly Guid TenantUid = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task TenantLocalDatesBecomeCorrectHalfOpenUtcRangeAndCountsAreZeroFilled()
    {
        var repository = new Repository(new AppointmentStatusReportData(
            [new("Scheduled", 1), new("Scheduled", 2), new("Cancelled", 1)],
            [Row(new DateTime(2026, 8, 1, 4, 0, 0, DateTimeKind.Utc), "Scheduled"),
             Row(new DateTime(2026, 9, 1, 3, 59, 0, DateTimeKind.Utc), "Cancelled")]));
        var service = Service(repository);

        var report = await service.GetAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

        Assert.Equal(new DateTime(2026, 8, 1, 4, 0, 0, DateTimeKind.Utc), repository.StartUtc);
        Assert.Equal(new DateTime(2026, 9, 1, 4, 0, 0, DateTimeKind.Utc), repository.EndUtc);
        Assert.Equal(2, report.TotalAppointments);
        Assert.Equal(Enum.GetValues<AppointmentStatus>().Length, report.StatusCounts.Count);
        Assert.Equal(1, report.StatusCounts.Single(x => x.Status == "Cancelled").Count);
        Assert.Equal(3, report.StatusCounts.Single(x => x.Status == "Scheduled").Count);
        Assert.Equal(0, report.StatusCounts.Single(x => x.Status == "Completed").Count);
        Assert.Equal(report.Appointments.OrderBy(x => x.StartAtUtc), report.Appointments);
    }

    [Fact]
    public async Task InvalidOrExcessiveRangesAreRejectedBeforeQuery()
    {
        var repository = new Repository(new([], []));
        var service = Service(repository);
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(new(2026, 8, 2), new(2026, 8, 1)));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(new(2025, 1, 1), new(2026, 1, 2)));
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public void CsvIsUtf8SafeEscapedAndFormulaNeutralized()
    {
        var report = new AppointmentStatusReport(new(2026, 8, 1), new(2026, 8, 1), "UTC", 1,
            [new("Scheduled", 1)], [new(Guid.NewGuid(), new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc),
                new(2026, 8, 1, 12, 30, 0, DateTimeKind.Utc), Guid.NewGuid(), "=SUM(1,2) \"Test\"\nName",
                "+123", "@Provider", "Scheduled")]);
        var csv = Service(new Repository(new([], []))).CreateCsv(report);
        var text = Encoding.UTF8.GetString(csv);
        Assert.Equal([0xEF, 0xBB, 0xBF], csv.Take(3).ToArray());
        Assert.StartsWith("\uFEFFAppointment Date,Start Time,End Time,Patient Name,Chart Number,Provider/Resource,Status", text);
        Assert.Contains("\"'=SUM(1,2) \"\"Test\"\"\nName\"", text);
        Assert.Contains("\"'+123\"", text);
        Assert.Contains("\"'@Provider\"", text);
        Assert.Equal(2, text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void ApiAndWebRequireEffectiveReportPermissionAndContractsAreNarrow()
    {
        var api = typeof(AppointmentReportsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>();
        var web = typeof(MicroEMR.Web.Controllers.ReportsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>();
        Assert.Contains(api, x => x.Policy == PermissionPolicyProvider.Prefix + PermissionKeys.ReportsView);
        Assert.Contains(web, x => x.Policy == WebPermissionPolicyProvider.Prefix + PermissionKeys.ReportsView);
        foreach (var method in new[] { nameof(AppointmentReportsController.Get), nameof(AppointmentReportsController.Csv) })
            Assert.DoesNotContain(typeof(AppointmentReportsController).GetMethod(method)!.GetParameters(),
                x => x.Name!.Contains("tenant", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MigrationUsesScheduledStartRangeIncludesCancelledAndReturnsPrivacyLimitedFields()
    {
        var sql = File.ReadAllText(Path.Combine(Root(), "db", "tenant-clinical", "migrations", "0029-appointment-status-report.sql"));
        Assert.Contains("a.StartDateTimeUtc>=@StartDateTimeUtc", sql);
        Assert.Contains("a.StartDateTimeUtc<@EndDateTimeUtc", sql);
        Assert.DoesNotContain("AppointmentStatus<>N'Cancelled'", sql.Replace(" ", ""), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Diagnosis", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Notes", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY a.StartDateTimeUtc", sql);
    }

    [Fact]
    public void WebPageHasFiltersSummaryDetailsExportAndEmptyStateWithoutCharts()
    {
        var view = File.ReadAllText(Path.Combine(Root(), "src", "MicroEMR.Web", "Views", "Reports", "AppointmentStatus.cshtml"));
        foreach (var text in new[] { "Start Date", "End Date", "Run Report", "Export CSV", "Summary", "No appointments found for the selected date range." })
            Assert.Contains(text, view);
        Assert.DoesNotContain("<canvas", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("chart.js", view, StringComparison.OrdinalIgnoreCase);
    }

    private static AppointmentStatusReportService Service(Repository repository) => new(
        new TenantContext(TenantUid, "tenant-a", "Tenant A"), new Catalog(), repository);
    private static AppointmentStatusReportRow Row(DateTime start, string status) => new(Guid.NewGuid(), start,
        start.AddMinutes(30), Guid.NewGuid(), "Patient", "C1", "Provider", status);
    private static string Root() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private sealed class Repository(AppointmentStatusReportData data) : IAppointmentStatusReportRepository
    {
        public DateTime StartUtc { get; private set; }
        public DateTime EndUtc { get; private set; }
        public int Calls { get; private set; }
        public Task<AppointmentStatusReportData> GetAsync(DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default)
        { Calls++; StartUtc = startUtc; EndUtc = endUtc; return Task.FromResult(data); }
    }
    private sealed class Catalog : ITenantCatalog
    {
        private static readonly Tenant Value = new(TenantUid, "tenant-a", "Tenant A", TenantStatus.Active,
            "America/Toronto", DateTimeOffset.UtcNow);
        public Task<Tenant?> GetByUidAsync(Guid tenantUid, CancellationToken cancellationToken = default) => Task.FromResult<Tenant?>(tenantUid == TenantUid ? Value : null);
        public Task<Tenant?> GetByKeyAsync(string tenantKey, CancellationToken cancellationToken = default) => Task.FromResult<Tenant?>(null);
    }
}
