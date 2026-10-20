using Microsoft.Data.SqlClient;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.Provisioning;

// Only registered by the explicit deployment/tool composition root.
internal sealed class DeploymentTenantSqlConnectionFactory(ITenantSqlConnectionFactory factory)
    : IAdministrativeTenantSqlConnectionFactory
{
    public Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default) =>
        factory.OpenConnectionAsync(cancellationToken);
}
