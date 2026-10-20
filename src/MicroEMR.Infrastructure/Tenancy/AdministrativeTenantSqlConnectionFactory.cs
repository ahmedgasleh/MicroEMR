using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.Tenancy;

namespace MicroEMR.Infrastructure.Tenancy;

public sealed class AdministrativeTenantSqlConnectionFactory : IAdministrativeTenantSqlConnectionFactory
{
    private readonly TenantSqlConnectionFactory factory;

    public AdministrativeTenantSqlConnectionFactory(ITenantContext tenantContext,
        ITenantDatabaseResolver resolver, IConfiguration configuration, ILogger<TenantSqlConnectionFactory> logger)
    {
        // Reuse all assignment, connection and database-identity checks; no retry/fallback.
        factory = new(tenantContext, resolver, new AdministrativeTenantDatabaseSecretProvider(configuration), logger);
    }

    public Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default) =>
        factory.OpenConnectionAsync(cancellationToken);
}

internal sealed class AdministrativeTenantDatabaseSecretProvider(IConfiguration configuration) : ITenantDatabaseSecretProvider
{
    public const string SectionName = "AdministrativeTenantDatabaseSecrets";

    public Task<TenantDatabaseSecret> ResolveAsync(string secretReference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretReference);
        cancellationToken.ThrowIfCancellationRequested();
        var setting = $"{SectionName}:{secretReference}";
        var connection = AdministrativeConnectionConfiguration.Validate(configuration[setting],
            configuration[$"{ConfigurationTenantDatabaseSecretProvider.SectionName}:{secretReference}"], setting);
        return Task.FromResult(new TenantDatabaseSecret(connection));
    }
}
