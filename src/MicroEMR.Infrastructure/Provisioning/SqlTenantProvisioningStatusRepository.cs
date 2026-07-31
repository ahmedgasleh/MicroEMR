using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.Provisioning;

public sealed class SqlTenantProvisioningStatusRepository
    : ITenantProvisioningStatusRepository
{
    private readonly string _connectionString;

    public SqlTenantProvisioningStatusRepository(IConfiguration configuration)
    {
        _connectionString = PlatformDatabaseConnection.GetConnectionString(configuration);
    }

    public Task MarkStartedAsync(Guid tenantUid, CancellationToken cancellationToken = default) =>
        ExecuteAsync("dbo.TenantDatabase_ProvisioningStarted", tenantUid, null, cancellationToken);

    public Task MarkCompletedAsync(
        Guid tenantUid,
        string schemaVersion,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync("dbo.TenantDatabase_ProvisioningCompleted", tenantUid, schemaVersion, cancellationToken);

    public Task MarkFailedAsync(Guid tenantUid, CancellationToken cancellationToken = default) =>
        ExecuteAsync("dbo.TenantDatabase_ProvisioningFailed", tenantUid, null, cancellationToken);

    private async Task ExecuteAsync(
        string procedure,
        Guid tenantUid,
        string? schemaVersion,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(procedure, connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@TenantUid", SqlDbType.UniqueIdentifier).Value = tenantUid;
        if (schemaVersion is not null)
            command.Parameters.Add("@CurrentSchemaVersion", SqlDbType.NVarChar, 50).Value = schemaVersion;
        await connection.OpenAsync(cancellationToken);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
