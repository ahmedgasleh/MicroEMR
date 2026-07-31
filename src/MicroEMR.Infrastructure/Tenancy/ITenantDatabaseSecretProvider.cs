namespace MicroEMR.Infrastructure.Tenancy;

public interface ITenantDatabaseSecretProvider
{
    Task<TenantDatabaseSecret> ResolveAsync(
        string secretReference,
        CancellationToken cancellationToken = default);
}

public sealed record TenantDatabaseSecret(string ConnectionString);
