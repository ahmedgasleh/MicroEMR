using Microsoft.Extensions.Configuration;
using MicroEMR.Infrastructure.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class TenantDatabaseSecretProviderTests
{
    [Fact]
    public async Task KnownReferenceResolves()
    {
        var provider = Provider(new Dictionary<string, string?>
        {
            ["TenantDatabaseSecrets:development:MicroEMR_Db"] =
                "Server=localhost;Database=MicroEMR_Db;Integrated Security=true"
        });

        var secret = await provider.ResolveAsync("development:MicroEMR_Db");

        Assert.Contains("MicroEMR_Db", secret.ConnectionString);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task BlankReferenceIsRejected(string? reference)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            Provider(new Dictionary<string, string?>()).ResolveAsync(reference!));
    }

    [Fact]
    public async Task UnknownOrBlankConfiguredSecretIsRejected()
    {
        var provider = Provider(new Dictionary<string, string?>
        {
            ["TenantDatabaseSecrets:blank"] = " "
        });

        await Assert.ThrowsAsync<TenantDatabaseConnectionException>(() =>
            provider.ResolveAsync("unknown"));
        await Assert.ThrowsAsync<TenantDatabaseConnectionException>(() =>
            provider.ResolveAsync("blank"));
    }

    private static ConfigurationTenantDatabaseSecretProvider Provider(
        IDictionary<string, string?> values) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
}
