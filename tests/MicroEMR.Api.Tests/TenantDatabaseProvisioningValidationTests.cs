using MicroEMR.Infrastructure.Provisioning;
using MicroEMR.Infrastructure.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class TenantDatabaseProvisioningValidationTests
{
    [Fact]
    public void EmptyOrSameTenantIdentityIsRetrySafe()
    {
        var tenantUid = Guid.NewGuid();

        TenantDatabaseMigrationRunner.ValidateIdentity([], tenantUid);
        TenantDatabaseMigrationRunner.ValidateIdentity([tenantUid], tenantUid);
    }

    [Fact]
    public void DifferentTenantOrEmptyRequestedUidIsRejected()
    {
        Assert.Throws<TenantDatabaseConnectionException>(() =>
            TenantDatabaseMigrationRunner.ValidateIdentity(
                [Guid.NewGuid()],
                Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() =>
            TenantDatabaseMigrationRunner.ValidateIdentity([], Guid.Empty));
    }

    [Fact]
    public void ChangedAppliedHashIsRejected()
    {
        var migration = new TenantDatabaseMigration(
            "0001", "1.0.0", "one.sql", new string('A', 64), "SELECT 1;");

        Assert.Throws<TenantDatabaseConnectionException>(() =>
            TenantDatabaseMigrationRunner.ValidateAppliedHashes(
                new Dictionary<string, string> { ["0001"] = new string('B', 64) },
                [migration]));
    }
}
