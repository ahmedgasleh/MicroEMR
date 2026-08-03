using Microsoft.Extensions.Configuration;
using MicroEMR.Infrastructure.Provisioning;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PatientDocumentCreatePatientIdMigrationTests
{
    [Fact]
    public async Task CreateProcedureResolvesAndInsertsBothPatientIdentifiers()
    {
        var source = new FileTenantDatabaseMigrationSource(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenantProvisioning:SqlAssetsPath"] = Path.Combine(AppContext.BaseDirectory, "database")
            }).Build());
        var sql = Assert.Single(await source.GetAvailableMigrationsAsync(),
            item => item.MigrationId == "0024-patient-document-create-patient-id").Script;

        Assert.Contains("SELECT @PatientId = PatientId", sql);
        Assert.Contains("WHERE PatientUid = @PatientUid AND IsDeleted = 0", sql);
        Assert.Contains("PatientDocumentUid, PatientId, PatientUid", sql);
        Assert.Contains("@DocumentUid, @PatientId, @PatientUid", sql);
        Assert.Contains("VALUES (@CreatedBy, @PatientId", sql);
    }
}
