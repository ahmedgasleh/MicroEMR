using Microsoft.Extensions.Configuration;

namespace MicroEMR.Infrastructure.Tenancy;

public sealed class ConfigurationTenantDatabaseSecretProvider
    : ITenantDatabaseSecretProvider
{
    public const string SectionName = "TenantDatabaseSecrets";

    private readonly IConfiguration _configuration;

    public ConfigurationTenantDatabaseSecretProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<TenantDatabaseSecret> ResolveAsync(
        string secretReference,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretReference);
        cancellationToken.ThrowIfCancellationRequested();

        var connectionString = _configuration[$"{SectionName}:{secretReference}"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new TenantDatabaseConnectionException(
                "The tenant database secret reference could not be resolved.");
        }

        return Task.FromResult(new TenantDatabaseSecret(connectionString));
    }
}
