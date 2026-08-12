namespace MicroEMR.Api.Tests;

using Xunit;

public sealed class EncounterTemplateRuntimeMigrationTests
{
    [Fact]
    public void MigrationAddsNullableProvenanceAndConcurrencyCheckedStructuredSave()
    {
        var root = FindRoot();
        var sql = File.ReadAllText(Path.Combine(root, "db", "tenant-clinical", "migrations", "0036-encounter-template-runtime.sql"));
        Assert.Contains("ADD TemplateUid UNIQUEIDENTIFIER NULL", sql);
        Assert.Contains("ADD TemplateVersionUid UNIQUEIDENTIFIER NULL", sql);
        Assert.Contains("ADD StructuredDataJson NVARCHAR(MAX) NULL", sql);
        Assert.Contains("@ExpectedRowVersion BINARY(8)", sql);
        Assert.Contains("PatientEncounterHistory_Create", sql);
        Assert.Contains("AuditLog", sql);
    }

    [Fact]
    public void LegacySoapRemovalMigration_DropsOnlyTemplateInfrastructure()
    {
        var root = FindRoot();
        var sql = File.ReadAllText(Path.Combine(root, "db", "tenant-clinical", "migrations",
            "0037-remove-legacy-encounter-soap-templates.sql"));

        Assert.Contains("DROP TABLE IF EXISTS dbo.EncounterSoapTemplate", sql);
        Assert.Contains("DROP PROCEDURE IF EXISTS dbo.EncounterSoapTemplate_GetByUid", sql);
        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.PatientEncounter_Create", sql);
        Assert.DoesNotContain("DROP TABLE IF EXISTS dbo.PatientEncounter", sql);
        Assert.DoesNotContain("UPDATE dbo.PatientEncounter", sql);
    }

    [Fact]
    public void WebWorkflow_HasOneTemplateSelectorAndReopensCreatedEncounter()
    {
        var root = FindRoot();
        var patientView = File.ReadAllText(Path.Combine(root, "src", "MicroEMR.Web", "Views", "Patients", "Details.cshtml"));
        var createView = File.ReadAllText(Path.Combine(root, "src", "MicroEMR.Web", "Views", "PatientEncounters", "Create.cshtml"));
        var controller = File.ReadAllText(Path.Combine(root, "src", "MicroEMR.Web", "Controllers", "PatientEncountersController.cs"));
        var sidebar = File.ReadAllText(Path.Combine(root, "src", "MicroEMR.Web", "Views", "Shared", "_Sidebar.cshtml"));

        Assert.DoesNotContain("EncounterSoapTemplateUid", patientView);
        Assert.DoesNotContain("EncounterSoapTemplateUid", createView);
        Assert.DoesNotContain("EncounterSoapTemplates", sidebar);
        Assert.Contains("openEncounterUid = created.EncounterUid", controller);
        Assert.Contains("openEncounter(@Html.Raw", patientView);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MicroEMR.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
