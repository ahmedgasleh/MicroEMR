using Microsoft.Data.SqlClient;

namespace MicroEMR.Infrastructure.Tenancy;

public interface ITenantSqlConnectionFactory
{
    Task<SqlConnection> OpenConnectionAsync(
        CancellationToken cancellationToken = default);
}
