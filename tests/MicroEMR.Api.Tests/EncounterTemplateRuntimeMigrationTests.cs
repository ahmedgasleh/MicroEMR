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

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MicroEMR.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
