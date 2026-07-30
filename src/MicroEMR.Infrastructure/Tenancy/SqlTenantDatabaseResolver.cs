using System.Data;
using MicroEMR.Application.Tenancy;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace MicroEMR.Infrastructure.Tenancy;

public sealed class SqlTenantDatabaseResolver : ITenantDatabaseResolver
{
    private readonly string _connectionString;

    public SqlTenantDatabaseResolver(IConfiguration configuration)
    {
        _connectionString =
            PlatformDatabaseConnection.GetConnectionString(configuration);
    }

    public async Task<TenantDatabaseInfo?> ResolveAsync(
        Guid tenantUid,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command =
            new SqlCommand("dbo.TenantDatabase_GetByTenantUid", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

        command.Parameters.Add(
            new SqlParameter("@TenantUid", SqlDbType.UniqueIdentifier)
            {
                Value = tenantUid
            });

        await connection.OpenAsync(cancellationToken);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new TenantDatabaseInfo(
            reader.GetGuid(reader.GetOrdinal("TenantUid")),
            reader.GetString(reader.GetOrdinal("DatabaseServerKey")),
            reader.GetString(reader.GetOrdinal("DatabaseName")),
            reader.GetString(reader.GetOrdinal("SecretReference")),
            reader.GetString(reader.GetOrdinal("DatabaseStatus")));
    }
}
