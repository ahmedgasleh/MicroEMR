using Microsoft.Extensions.Configuration;

namespace MicroEMR.Infrastructure.Tenancy;

public interface IAdministrativePlatformConnectionStringProvider
{
    string GetConnectionString();
}

internal sealed class AdministrativePlatformConnectionStringProvider(IConfiguration configuration)
    : IAdministrativePlatformConnectionStringProvider
{
    public string GetConnectionString() => AdministrativePlatformDatabaseConnection.GetConnectionString(configuration);
}
