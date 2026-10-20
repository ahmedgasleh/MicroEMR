using Microsoft.Data.SqlClient;

namespace MicroEMR.Infrastructure.Tenancy;

// Infrastructure-only capability, deliberately separate from the clinical factory interface.
public interface IAdministrativeTenantSqlConnectionFactory
{
    Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
