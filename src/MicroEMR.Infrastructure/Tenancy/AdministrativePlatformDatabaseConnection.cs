using Microsoft.Extensions.Configuration;

namespace MicroEMR.Infrastructure.Tenancy;

internal static class AdministrativePlatformDatabaseConnection
{
    public const string ConnectionStringName = "AdministrativePlatformDatabase";

    public static string GetConnectionString(IConfiguration configuration) =>
        AdministrativeConnectionConfiguration.Validate(
            configuration.GetConnectionString(ConnectionStringName),
            PlatformDatabaseConnection.GetConnectionString(configuration),
            $"ConnectionStrings:{ConnectionStringName}");
}
