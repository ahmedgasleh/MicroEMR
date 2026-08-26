using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientResults;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ResultReviewAcknowledgementHardeningTests
{
    private static readonly string Sql = File.ReadAllText(Path.Combine(Root(), "db", "tenant-clinical", "migrations", "0051-result-review-acknowledgement-hardening.sql"));

    [Fact]
    public void Migration0051IsUniqueNextAndSupportsFreshProvisioning()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "db", "tenant-clinical", "manifest.json")));
        var ids = manifest.RootElement.EnumerateArray().Select(x => x.GetProperty("migrationId").GetString()).ToArray();
        Assert.Equal("0050-patient-prescription-foundation", ids[^5]);
        Assert.Equal("0051-result-review-acknowledgement-hardening", ids[^4]);
        Assert.Equal("0052-cds-foundation", ids[^3]);
        Assert.Equal("0053-cdm-enrollment-foundation", ids[^2]);
        Assert.Equal("0054-results-provenance-correction-foundation", ids[^1]);
        Assert.Single(ids, x => x == "0051-result-review-acknowledgement-hardening");
        Assert.False(File.Exists(Path.Combine(Root(), "db", "tenant-clinical", "migrations", "0052-result-review-acknowledgement-hardening.sql")));
    }

    [Fact]
    public void FirstReviewIsAtomicActorAttributedAndChangesOnlyNewUnreviewedRow()
    {
        Assert.Contains("SET XACT_ABORT ON", Sql);
        Assert.Contains("BEGIN TRANSACTION", Sql);
        Assert.Contains("WITH (UPDLOCK, HOLDLOCK)", Sql);
        Assert.Contains("ResultStatus = N'New'", Sql);
        Assert.Contains("ReviewedAt IS NULL", Sql);
        Assert.Contains("ReviewedBy = @ReviewedBy", Sql);
        Assert.Contains("UpdatedBy = @ReviewedBy", Sql);
        Assert.Contains("@ExpectedRowVersion BINARY(8)", Sql);
        Assert.Contains("RowVersion = @ExpectedRowVersion", Sql);
        Assert.Contains("THROW 51304", Sql);
        Assert.Contains("COMMIT", Sql);
    }

    [Fact]
    public void RepeatAndConcurrentReviewAreNoOpWithOneSuccessfulAudit()
    {
        Assert.Contains("DECLARE @ReviewWasApplied BIT = 0", Sql);
        Assert.Contains("IF @@ROWCOUNT = 1", Sql);
        Assert.Contains("SET @ReviewWasApplied = 1", Sql);
        Assert.Equal(1, Sql.Split("INSERT dbo.AuditLog", StringSplitOptions.None).Length - 1);
        Assert.Contains("@ReviewWasApplied AS ReviewWasApplied", Sql);
        Assert.DoesNotContain("COALESCE(ReviewedBy", Sql);
        Assert.DoesNotContain("COALESCE(ReviewedAt", Sql);
    }

    [Fact]
    public void ReviewAuditUsesExistingAuditLogAndMinimalPayload()
    {
        Assert.Contains("N'ResultReviewed'", Sql);
        Assert.Contains("N'PatientResult'", Sql);
        Assert.Contains("N'Status=Reviewed'", Sql);
        var audit = Sql[Sql.IndexOf("INSERT dbo.AuditLog", StringComparison.Ordinal)..Sql.IndexOf("SET @ReviewWasApplied", StringComparison.Ordinal)];
        Assert.DoesNotContain("ReviewNote", audit);
        Assert.DoesNotContain("ResultValue", audit);
        Assert.DoesNotContain("ResultSummary", audit);
    }

    [Fact]
    public void ActorIsServerResolvedAndCannotBeSubmittedByClient()
    {
        Assert.Null(typeof(MarkPatientResultReviewedRequest).GetProperty("ReviewedBy"));
        Assert.NotNull(typeof(MarkPatientResultReviewedRequest).GetProperty("ExpectedRowVersion"));
        Assert.Contains("ApplicationUser", Sql);
        Assert.Contains("IsActive = 1", Sql);
        var controller = File.ReadAllText(Path.Combine(Root(), "src", "MicroEMR.Api", "Controllers", "PatientResultsController.cs"));
        Assert.Contains("ClinicalUserActorContext.GetRequired(HttpContext)", controller);
    }

    [Fact]
    public void ApiPreservesViewAndReviewPermissionBoundary()
    {
        var type = typeof(PatientResultsController);
        Assert.Contains(type.GetCustomAttributes<RequirePermissionAttribute>(), x => x.Policy?.Contains(PermissionKeys.ResultsView) == true);
        var review = type.GetMethod(nameof(PatientResultsController.Review))!;
        Assert.Contains(review.GetCustomAttributes<RequirePermissionAttribute>(), x => x.Policy?.Contains(PermissionKeys.ResultsReview) == true);
        Assert.NotNull(type.GetMethod(nameof(PatientResultsController.Unreviewed))!.GetCustomAttribute<HttpGetAttribute>());
    }

    [Fact]
    public void CompoundPatientLookupAndTenantLocalQueuePreserveIsolation()
    {
        Assert.Contains("r.PatientUid = @PatientUid", Sql);
        Assert.Contains("r.PatientResultUid = @PatientResultUid", Sql);
        Assert.DoesNotContain("TenantUid", Sql);
        Assert.Contains("p.IsDeleted = 0", Sql);
        Assert.Contains("r.ResultStatus = N'New'", Sql);
        Assert.Contains("r.ReviewedAt IS NULL", Sql);
        Assert.Contains("ORDER BY r.ResultDate ASC, r.CreatedAt ASC", Sql);
    }

    [Fact]
    public void DashboardQueueIsActionableAndReusesPatientChartResultContext()
    {
        var dashboard = File.ReadAllText(Path.Combine(Root(), "src", "MicroEMR.Web", "Views", "Home", "Index.cshtml"));
        var queue = File.ReadAllText(Path.Combine(Root(), "src", "MicroEMR.Web", "Views", "PatientResults", "Unreviewed.cshtml"));
        var resultUi = File.ReadAllText(Path.Combine(Root(), "src", "MicroEMR.Web", "ClientApp", "patients", "patient-results.ts"));
        Assert.Contains("asp-action=\"Unreviewed\"", dashboard);
        Assert.Contains("asp-route-tab=\"results\"", queue);
        Assert.Contains("Not reviewed", queue);
        Assert.Contains("Reviewed by", resultUi);
        Assert.Contains("reviewedAt", resultUi);
    }

    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", ".."));
}
