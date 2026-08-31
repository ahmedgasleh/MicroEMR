using System.Reflection;
using System.Text.Json;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientAllergies.Contracts;
using MicroEMR.Application.PatientCpp;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class NoKnownAllergiesAssertionTests
{
    private static readonly string Sql = Source("db", "tenant-clinical", "migrations", "0055-verified-negative-allergy-assertion.sql");

    [Fact] public void Migration0055IsExactlyOnceAndAfter0054()
    {
        using var json = JsonDocument.Parse(Source("db", "tenant-clinical", "manifest.json"));
        var ids = json.RootElement.EnumerateArray().Select(x => x.GetProperty("migrationId").GetString()).ToArray();
        Assert.Equal("0054-results-provenance-correction-foundation", ids[^2]);
        Assert.Equal("0055-verified-negative-allergy-assertion", ids[^1]);
        Assert.Single(ids, x => x == "0055-verified-negative-allergy-assertion");
    }

    [Fact] public void ModelPreservesProvenanceHistoryAndHasNoBackfill()
    {
        foreach (var value in new[] { "AssertionUid", "PatientUid", "NoKnownAllergies", "VerifiedBy", "VerifiedAtUtc", "RevokedBy", "RevokedAtUtc", "RevocationReason", "RowVersion" }) Assert.Contains(value, Sql);
        Assert.Contains("Status IN (N'Active', N'Revoked')", Sql);
        Assert.DoesNotContain("INSERT dbo.PatientAllergyAssertion SELECT", Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM", Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public void PatientLockAndUniqueIndexPreventContradictionsAndDuplicates()
    {
        Assert.Contains("UX_PatientAllergyAssertion_ActiveNka", Sql);
        Assert.True(Count(Sql, "dbo.Patient WITH(UPDLOCK,HOLDLOCK)") >= 3);
        var create = Procedure("dbo.PatientAllergy_Create");
        Assert.Contains("@ConfirmReplaceNoKnownAllergies", create);
        Assert.Contains("NoKnownAllergiesRevoked", create);
        Assert.Contains("INSERT dbo.PatientAllergy", create);
        Assert.Contains("BEGIN TRANSACTION", create);
        Assert.Contains("COMMIT", create);
    }

    [Fact] public void AssertionIsIdempotentActorBoundAndAuditedOnce()
    {
        var assertion = Procedure("dbo.PatientAllergy_AssertNoKnownAllergies");
        Assert.Contains("ApplicationUser", assertion);
        Assert.Contains("IsActive=1", assertion);
        Assert.Contains("AllergyStatus=N'Active'", assertion);
        Assert.Equal(1, Count(assertion, "NoKnownAllergiesAsserted"));
        Assert.DoesNotContain("UPDATE dbo.PatientAllergyAssertion SET Verified", assertion);
    }

    [Fact] public void ApiIsSemanticAuthorizedAndDoesNotAcceptActor()
    {
        var controller = typeof(PatientAllergiesController);
        foreach (var methodName in new[] { nameof(PatientAllergiesController.AssertNoKnownAllergies), nameof(PatientAllergiesController.RevokeNoKnownAllergies) })
            Assert.Contains(controller.GetMethod(methodName)!.GetCustomAttributes<RequirePermissionAttribute>(), x => x.Policy?.Contains(PermissionKeys.ClinicalDataManage) == true);
        Assert.Null(typeof(RevokeNoKnownAllergiesRequest).GetProperty("RevokedBy"));
        Assert.Null(typeof(CreatePatientAllergyRequest).GetProperty("CreatedBy"));
    }

    [Fact] public void PermissionHandlerDoesNotResolveTenantPermissionsForAnonymousRequests()
    {
        var source = Source("src", "MicroEMR.Api", "Authorization", "PermissionAuthorization.cs");
        Assert.Contains("context.User.Identity?.IsAuthenticated != true", source);
        Assert.True(source.IndexOf("IsAuthenticated != true", StringComparison.Ordinal) <
                    source.IndexOf("permissions.HasPermissionAsync", StringComparison.Ordinal));
    }

    [Fact] public void CppEnablesExplicitNoneForAllergiesOnly()
    {
        var service = Source("src", "MicroEMR.Application", "PatientCpp", "PatientCppService.cs");
        var allergyStart = service.IndexOf("private async Task<PatientCppSection<PatientCppAllergy>> LoadAllergies", StringComparison.Ordinal);
        var medicationStart = service.IndexOf("private async Task<PatientCppSection<PatientCppMedication>> LoadMedications", allergyStart, StringComparison.Ordinal);
        Assert.Contains("ExplicitlyNone", service[allergyStart..medicationStart]);
        Assert.DoesNotContain("ExplicitlyNone", service[medicationStart..]);
        Assert.Equal(PatientCppSectionStates.ExplicitlyNone, PatientCppSection<PatientCppAllergy>.ExplicitlyNone().State);
    }

    private static string Procedure(string name) { var start = Sql.IndexOf("CREATE OR ALTER PROCEDURE " + name, StringComparison.Ordinal); var end = Sql.IndexOf("\nGO", start, StringComparison.Ordinal); return Sql[start..end]; }
    private static int Count(string source, string value) => source.Split(value, StringSplitOptions.None).Length - 1;
    private static string Source(params string[] parts) => File.ReadAllText(Path.Combine([Root(), .. parts]));
    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string source = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", ".."));
}
