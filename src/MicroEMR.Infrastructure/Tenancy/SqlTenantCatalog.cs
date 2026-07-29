using System.Data;
using MicroEMR.Application.Tenancy;
using MicroEMR.Core.Tenancy;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace MicroEMR.Infrastructure.Tenancy;

public sealed class SqlTenantCatalog : ITenantCatalog
{
    private readonly string _connectionString;

    public SqlTenantCatalog(IConfiguration configuration)
    {
        _connectionString =
            PlatformDatabaseConnection.GetConnectionString(configuration);
    }

    public Task<Tenant?> GetByUidAsync(
        Guid tenantUid,
        CancellationToken cancellationToken = default)
    {
        return GetAsync(
            "dbo.Tenant_GetByUid",
            new SqlParameter("@TenantUid", SqlDbType.UniqueIdentifier)
            {
                Value = tenantUid
            },
            cancellationToken);
    }

    public Task<Tenant?> GetByKeyAsync(
        string tenantKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantKey);

        return GetAsync(
            "dbo.Tenant_GetByKey",
            new SqlParameter("@TenantKey", SqlDbType.NVarChar, 50)
            {
                Value = tenantKey.Trim()
            },
            cancellationToken);
    }

    private async Task<Tenant?> GetAsync(
        string storedProcedure,
        SqlParameter parameter,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(storedProcedure, connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add(parameter);

        await connection.OpenAsync(cancellationToken);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken)
            ? MapTenant(reader)
            : null;
    }

    private static Tenant MapTenant(SqlDataReader reader)
    {
        var statusValue = reader.GetString(reader.GetOrdinal("TenantStatus"));

        var status = statusValue switch
        {
            "Provisioning" => TenantStatus.Provisioning,
            "Active" => TenantStatus.Active,
            "Suspended" => TenantStatus.Suspended,
            "Archived" => TenantStatus.Archived,
            _ => throw new InvalidDataException(
                $"Unsupported tenant status '{statusValue}' in the platform database.")
        };

        return new Tenant(
            reader.GetGuid(reader.GetOrdinal("TenantUid")),
            reader.GetString(reader.GetOrdinal("TenantKey")),
            reader.GetString(reader.GetOrdinal("DisplayName")),
            status,
            reader.GetString(reader.GetOrdinal("DefaultTimeZoneId")),
            GetUtcDateTimeOffset(reader, "CreatedAt"),
            GetNullableUtcDateTimeOffset(reader, "ActivatedAt"),
            GetNullableUtcDateTimeOffset(reader, "SuspendedAt"));
    }

    private static DateTimeOffset GetUtcDateTimeOffset(
        SqlDataReader reader,
        string columnName)
    {
        var value = reader.GetDateTime(reader.GetOrdinal(columnName));
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    private static DateTimeOffset? GetNullableUtcDateTimeOffset(
        SqlDataReader reader,
        string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal)
            ? null
            : new DateTimeOffset(
                DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc));
    }
}
