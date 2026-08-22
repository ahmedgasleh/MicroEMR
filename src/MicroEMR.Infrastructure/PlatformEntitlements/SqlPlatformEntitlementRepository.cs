using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MicroEMR.Application.PlatformEntitlements;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.PlatformEntitlements;

public sealed class SqlPlatformEntitlementRepository(IConfiguration configuration)
    : IPlatformEntitlementRepository
{
    private readonly string _connectionString =
        PlatformDatabaseConnection.GetConnectionString(configuration);

    public async Task<IReadOnlyList<string>> GetActiveForUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = StoredProcedure(connection, "dbo.PlatformEntitlement_GetActiveForUser");
        AddText(command, "@UserId", 451, userId);
        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
            results.Add(reader.GetString(0));
        return results;
    }

    public async Task<long> GetAuthorizationVersionAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = StoredProcedure(connection, "dbo.PlatformAuthorization_GetVersionForUser");
        AddText(command, "@UserId", 451, userId);
        await connection.OpenAsync(cancellationToken);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    public Task<PlatformEntitlementChangeResult> AssignAsync(
        string userId,
        string entitlementKey,
        string actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken = default) =>
        ChangeAsync("dbo.PlatformEntitlement_AssignToUser", userId, entitlementKey,
            actorUserId, correlationId, cancellationToken);

    public Task<PlatformEntitlementChangeResult> RevokeAsync(
        string userId,
        string entitlementKey,
        string actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken = default) =>
        ChangeAsync("dbo.PlatformEntitlement_RevokeFromUser", userId, entitlementKey,
            actorUserId, correlationId, cancellationToken);

    private async Task<PlatformEntitlementChangeResult> ChangeAsync(
        string procedure,
        string userId,
        string entitlementKey,
        string actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = StoredProcedure(connection, procedure);
        AddText(command, "@UserId", 451, userId);
        AddText(command, "@EntitlementKey", 101, entitlementKey);
        AddText(command, "@ActorUserId", 451, actorUserId);
        command.Parameters.Add("@CorrelationId", SqlDbType.UniqueIdentifier).Value = correlationId;
        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("The platform entitlement operation returned no result.");
        return new(reader.GetGuid(0), reader.GetInt64(1));
    }

    private static SqlCommand StoredProcedure(SqlConnection connection, string name) =>
        new(name, connection) { CommandType = CommandType.StoredProcedure };

    private static void AddText(SqlCommand command, string name, int size, string value) =>
        command.Parameters.Add(name, SqlDbType.NVarChar, size).Value = value;
}
