using System.Data;
using Microsoft.Data.SqlClient;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.Provisioning;

public sealed class SqlTenantMigrationStatusReader(
    ITenantDatabaseSecretProvider secretProvider) : ITenantMigrationStatusReader
{
    public async Task<TenantMigrationDatabaseSnapshot> ReadAsync(
        TenantMigrationStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var secret = await secretProvider.ResolveAsync(request.SecretReference, cancellationToken);
        var builder = TenantSqlConnectionFactory.ValidateConnectionString(
            secret.ConnectionString, request.DatabaseName);
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string tableSql = """
            SELECT
                CASE WHEN OBJECT_ID(N'dbo.TenantDatabaseIdentity', N'U') IS NULL THEN 0 ELSE 1 END,
                CASE WHEN OBJECT_ID(N'dbo.SchemaMigration', N'U') IS NULL THEN 0 ELSE 1 END;
            """;
        await using var tableCommand = new SqlCommand(tableSql, connection);
        await using var tableReader = await tableCommand.ExecuteReaderAsync(cancellationToken);
        await tableReader.ReadAsync(cancellationToken);
        var identityExists = tableReader.GetInt32(0) == 1;
        var migrationExists = tableReader.GetInt32(1) == 1;
        await tableReader.CloseAsync();

        var identities = new List<Guid>();
        if (identityExists)
        {
            await using var identityCommand = new SqlCommand(
                "SELECT TenantUid FROM dbo.TenantDatabaseIdentity;", connection);
            await using var identityReader = await identityCommand.ExecuteReaderAsync(cancellationToken);
            while (await identityReader.ReadAsync(cancellationToken)) identities.Add(identityReader.GetGuid(0));
        }

        var migrations = new List<AppliedTenantMigration>();
        if (migrationExists && identities.Count == 1 && identities[0] == request.TenantUid)
        {
            const string migrationSql = """
                SELECT MigrationId, SchemaVersion, ScriptHash, AppliedAt, AppliedBy
                FROM dbo.SchemaMigration
                ORDER BY AppliedAt, MigrationId;
                """;
            await using var command = new SqlCommand(migrationSql, connection) { CommandType = CommandType.Text };
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                migrations.Add(new(
                    reader.GetString(0), reader.GetString(1), reader.GetString(2),
                    new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc)),
                    reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        return new(identityExists, migrationExists, identities, migrations);
    }
}
