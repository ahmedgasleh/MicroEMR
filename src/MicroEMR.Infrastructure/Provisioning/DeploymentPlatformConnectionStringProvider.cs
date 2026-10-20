using Microsoft.Extensions.Configuration;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.Provisioning;

// Explicit tool registration preserves its existing isolated operator configuration.
internal sealed class DeploymentPlatformConnectionStringProvider(IConfiguration configuration)
    : IAdministrativePlatformConnectionStringProvider
{
    public string GetConnectionString() => PlatformDatabaseConnection.GetConnectionString(configuration);
}
