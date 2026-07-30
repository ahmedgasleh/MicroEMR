using MicroEMR.Application.Tenancy;
using MicroEMR.Infrastructure.Tenancy;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class TenantSqlConnectionFactoryTests
{
    private static readonly Guid TenantUid = Guid.NewGuid();

    [Fact]
    public void ActiveAssignmentAndMatchingDatabaseAreAccepted()
    {
        var assignment = Assignment();

        TenantSqlConnectionFactory.ValidateAssignment(assignment, TenantUid);
        var builder = TenantSqlConnectionFactory.ValidateConnectionString(
            "Server=localhost;Database=MicroEMR_Db;Integrated Security=true",
            assignment.DatabaseName);

        Assert.Equal("MicroEMR_Db", builder.InitialCatalog);
    }

    [Fact]
    public void MissingAssignmentIsRejected() =>
        Assert.Throws<TenantDatabaseConnectionException>(() =>
            TenantSqlConnectionFactory.ValidateAssignment(null, TenantUid));

    [Theory]
    [InlineData("Provisioning")]
    [InlineData("Unavailable")]
    [InlineData("MigrationFailed")]
    [InlineData("Archived")]
    public void InactiveAssignmentIsRejected(string status) =>
        Assert.Throws<TenantDatabaseConnectionException>(() =>
            TenantSqlConnectionFactory.ValidateAssignment(
                Assignment(status: status),
                TenantUid));

    [Fact]
    public void TenantMismatchIsRejected() =>
        Assert.Throws<TenantDatabaseConnectionException>(() =>
            TenantSqlConnectionFactory.ValidateAssignment(
                Assignment(tenantUid: Guid.NewGuid()),
                TenantUid));

    [Fact]
    public void BlankAssignmentFieldsAreRejected()
    {
        Assert.Throws<TenantDatabaseConnectionException>(() =>
            TenantSqlConnectionFactory.ValidateAssignment(
                Assignment(secretReference: " "),
                TenantUid));
    }

    [Fact]
    public void DatabaseMismatchAndAttachFileAreRejected()
    {
        Assert.Throws<TenantDatabaseConnectionException>(() =>
            TenantSqlConnectionFactory.ValidateConnectionString(
                "Server=localhost;Database=OtherDb;Integrated Security=true",
                "MicroEMR_Db"));
        Assert.Throws<TenantDatabaseConnectionException>(() =>
            TenantSqlConnectionFactory.ValidateConnectionString(
                "Server=localhost;Database=MicroEMR_Db;AttachDbFilename=C:\\db.mdf;Integrated Security=true",
                "MicroEMR_Db"));
    }

    [Fact]
    public async Task FactoryAlwaysResolvesUsingCurrentTenantUid()
    {
        var resolver = new CapturingResolver();
        var factory = new TenantSqlConnectionFactory(
            new TenantContext(TenantUid, "trusted-key", "Trusted Tenant"),
            resolver,
            new StubSecretProvider(),
            NullLogger<TenantSqlConnectionFactory>.Instance);

        await Assert.ThrowsAsync<TenantDatabaseConnectionException>(() =>
            factory.OpenConnectionAsync());

        Assert.Equal(TenantUid, resolver.RequestedTenantUid);
    }

    private static TenantDatabaseInfo Assignment(
        Guid? tenantUid = null,
        string status = "Active",
        string secretReference = "development:MicroEMR_Db") =>
        new(
            tenantUid ?? TenantUid,
            "local-sql",
            "MicroEMR_Db",
            secretReference,
            status);

    private sealed class CapturingResolver : ITenantDatabaseResolver
    {
        public Guid? RequestedTenantUid { get; private set; }

        public Task<TenantDatabaseInfo?> ResolveAsync(
            Guid tenantUid,
            CancellationToken cancellationToken = default)
        {
            RequestedTenantUid = tenantUid;
            return Task.FromResult<TenantDatabaseInfo?>(null);
        }
    }

    private sealed class StubSecretProvider : ITenantDatabaseSecretProvider
    {
        public Task<TenantDatabaseSecret> ResolveAsync(
            string secretReference,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The secret provider should not be reached.");
    }
}
