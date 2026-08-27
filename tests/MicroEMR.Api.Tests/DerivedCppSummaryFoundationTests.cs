using Microsoft.AspNetCore.Authorization;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientCpp;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class DerivedCppSummaryFoundationTests
{
    [Fact]
    public void ContractUsesSafeStatesAndPurposeBuiltProjections()
    {
        Assert.Equal("HasEntries", PatientCppSectionStates.HasEntries);
        Assert.Equal("NotDocumented", PatientCppSectionStates.NotDocumented);
        Assert.Equal("NotAuthorized", PatientCppSectionStates.NotAuthorized);
        Assert.Equal("Unavailable", PatientCppSectionStates.Unavailable);
        Assert.Equal("ExplicitlyNone", PatientCppSectionStates.ExplicitlyNone);
        Assert.DoesNotContain(typeof(PatientCppProblem).GetProperties(), x => x.Name.Contains("Note", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(PatientCppDocument).GetProperties(), x => x.Name.Contains("Content", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(PatientCppEncounter).GetProperties(), x => x.Name.Contains("Note", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EmptyAndRestrictedSectionsDoNotLeakCountsOrClaimExplicitNone()
    {
        var empty = PatientCppSection<PatientCppAllergy>.From([], 0);
        var restricted = PatientCppSection<PatientCppResult>.NotAuthorized();
        Assert.Equal(PatientCppSectionStates.NotDocumented, empty.State);
        Assert.Empty(empty.Items);
        Assert.Equal(0, empty.TotalCount);
        Assert.Equal(PatientCppSectionStates.NotAuthorized, restricted.State);
        Assert.Null(restricted.TotalCount);
        Assert.Empty(restricted.Items);
        Assert.NotEqual(PatientCppSectionStates.ExplicitlyNone, empty.State);
    }

    [Fact]
    public void EndpointIsPatientScopedAndRequiresBasePatientAccess()
    {
        var controller = typeof(PatientCppController);
        var route = Assert.Single(controller.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.RouteAttribute), true)
            .Cast<Microsoft.AspNetCore.Mvc.RouteAttribute>());
        Assert.Equal("api/patients/{patientUid:guid}/cpp", route.Template);
        var policies = controller.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Select(x => x.Policy).ToArray();
        Assert.Contains(PermissionPolicyProvider.Prefix + PermissionKeys.PatientsView, policies);
    }

    [Fact]
    public void AggregatorPermissionFiltersSensitiveSectionsBeforeLoad()
    {
        var source = Source("src", "MicroEMR.Application", "PatientCpp", "PatientCppService.cs");
        foreach (var mapping in new[]
        {
            (PermissionKeys.ResultsView, "LoadResults", "PatientCppResult"),
            (PermissionKeys.EncountersView, "LoadEncounters", "PatientCppEncounter"),
            (PermissionKeys.ReferralsView, "LoadReferrals", "PatientCppReferral"),
            (PermissionKeys.DocumentsView, "LoadDocuments", "PatientCppDocument")
        })
        {
            Assert.Contains($"Authorized(effective, PermissionKeys.{KeyName(mapping.Item1)})", source);
            Assert.Contains($"PatientCppSection<{mapping.Item3}>.NotAuthorized()", source);
        }
    }

    [Fact]
    public void AggregatorAppliesLifecycleFiltersAndLimits()
    {
        var source = Source("src", "MicroEMR.Application", "PatientCpp", "PatientCppService.cs");
        Assert.Contains("ProblemStatus, \"Active\"", source);
        Assert.Contains("x.Status, \"Active\"", source);
        Assert.Contains("PrescriptionStatuses.Finalized", source);
        Assert.Contains("x.Status, \"Completed\"", source);
        Assert.Contains("x.LifecycleStatus, \"Current\"", source);
        Assert.Contains("x.Status, \"Signed\"", source);
        Assert.Contains("private const int ClinicalLimit = 5", source);
        Assert.Contains("private const int ContextLimit = 1", source);
    }

    [Fact]
    public void ResultsProjectionIncludesAbnormalityReviewAndProvenanceWithoutInterpretation()
    {
        var properties = typeof(PatientCppResult).GetProperties().Select(x => x.Name).ToArray();
        Assert.Contains("Abnormality", properties);
        Assert.Contains("ReviewStatus", properties);
        Assert.Contains("Provenance", properties);
        var source = Source("src", "MicroEMR.Application", "PatientCpp", "PatientCppService.cs");
        Assert.DoesNotContain("ReferenceRange", source);
        Assert.DoesNotContain("Critical", source);
    }

    [Fact]
    public void OptionalFailuresAreUnavailableAndAuditIsSingleFailClosedBoundary()
    {
        var source = Source("src", "MicroEMR.Application", "PatientCpp", "PatientCppService.cs");
        Assert.Equal(1, Count(source, "readAudit.RecordOpenedAsync"));
        Assert.True(source.IndexOf("readAudit.RecordOpenedAsync", StringComparison.Ordinal) <
                    source.IndexOf("LoadProblems", StringComparison.Ordinal));
        Assert.Contains("return PatientCppSection<T>.Unavailable()", source);
        Assert.DoesNotContain("RecordStructuredReadAsync", source);
    }

    [Fact]
    public void WebUsesOneCppClientAndSafeClinicalEmptyStateLanguage()
    {
        var controller = Source("src", "MicroEMR.Web", "Controllers", "PatientsController.cs");
        var details = controller[controller.IndexOf("Task<IActionResult> Details", StringComparison.Ordinal)..];
        Assert.Equal(1, Count(details, "_patientCppApiClient.GetAsync"));
        Assert.DoesNotContain("RecordChartOpenedAsync", details);

        var view = Source("src", "MicroEMR.Web", "Views", "Patients", "Details.cshtml");
        Assert.Contains("Allergy status not documented.", view);
        Assert.Contains("Medication status not documented.", view);
        Assert.Contains("No active problem records documented.", view);
        Assert.Contains("No Known Allergies", view);
        Assert.DoesNotContain("No Current Medications", view);
        foreach (var tab in new[] { "problems", "allergies", "medications", "results", "vitals", "immunizations", "encounters", "referrals", "documents" })
            Assert.Contains($"data-tab-target=\"{tab}\"", view);
    }

    [Fact]
    public void StepIsMigrationFreeAndDoesNotAddCdsOrCdmClinicalFacts()
    {
        var manifest = Source("db", "tenant-clinical", "manifest.json");
        Assert.Contains("0054-results-provenance-correction-foundation", manifest);
        Assert.Contains("0055-verified-negative-allergy-assertion", manifest);
        Assert.False(File.Exists(Path.Combine(Root(), "db", "tenant-clinical", "migrations", "0055-cpp.sql")));
        var model = Source("src", "MicroEMR.Application", "PatientCpp", "PatientCppModels.cs");
        Assert.DoesNotContain("Cds", model, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Cdm", model, StringComparison.OrdinalIgnoreCase);
    }

    private static string KeyName(string value) => value switch
    {
        "Results.View" => "ResultsView",
        "Encounters.View" => "EncountersView",
        "Referrals.View" => "ReferralsView",
        "Documents.View" => "DocumentsView",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
    private static int Count(string source, string value) => source.Split(value, StringSplitOptions.None).Length - 1;
    private static string Source(params string[] parts) => File.ReadAllText(Path.Combine([Root(), .. parts]));
    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", ".."));
}
