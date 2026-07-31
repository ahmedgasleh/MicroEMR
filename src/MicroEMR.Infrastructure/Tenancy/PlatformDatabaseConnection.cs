using Microsoft.Extensions.Configuration;

namespace MicroEMR.Infrastructure.Tenancy;

internal static class PlatformDatabaseConnection
{
    public const string ConnectionStringName = "PlatformDatabase";

    public static string GetConnectionString(IConfiguration configuration)
    {
        return configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' was not found.");
    }
}
