using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MicroEMR.Application.SecurityAudit;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.SecurityAudit;

public sealed class SqlPlatformSecurityAuditReviewRepository(IConfiguration configuration)
    : IPlatformSecurityAuditReviewRepository
{
    private readonly string _connectionString = PlatformDatabaseConnection.GetConnectionString(configuration);

    public async Task<IReadOnlyList<SecurityAuditListItem>> SearchAsync(
        SecurityAuditSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = Command(connection, "dbo.PlatformSecurityAudit_Search");
        Add(command, "@FromUtc", SqlDbType.DateTime2, criteria.FromUtc.UtcDateTime);
        Add(command, "@ToUtc", SqlDbType.DateTime2, criteria.ToUtc.UtcDateTime);
        Add(command, "@PageSize", SqlDbType.Int, criteria.PageSize);
        Add(command, "@CursorOccurredAtUtc", SqlDbType.DateTime2, criteria.CursorOccurredAtUtc?.UtcDateTime);
        Add(command, "@CursorSecurityAuditEventUid", SqlDbType.UniqueIdentifier, criteria.CursorSecurityAuditEventUid);
        Add(command, "@DenialReason", SqlDbType.NVarChar, criteria.DenialReason, 50);
        Add(command, "@Capability", SqlDbType.NVarChar, criteria.Capability, 100);
        Add(command, "@SourceApplication", SqlDbType.NVarChar, criteria.SourceApplication, 50);
        Add(command, "@TargetTenantUid", SqlDbType.UniqueIdentifier, criteria.TargetTenantUid);
        Add(command, "@RequestCorrelationId", SqlDbType.NVarChar, criteria.RequestCorrelationId, 128);
        Add(command, "@ActorSubject", SqlDbType.NVarChar, criteria.ActorSubject, 450);
        await connection.OpenAsync(cancellationToken);
        var rows = new List<SecurityAuditListItem>(criteria.PageSize + 1);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) rows.Add(ReadListItem(reader));
        return rows;
    }

    public async Task<SecurityAuditDetail?> GetByUidAsync(
        Guid securityAuditEventUid, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = Command(connection, "dbo.PlatformSecurityAudit_GetByUid");
        Add(command, "@SecurityAuditEventUid", SqlDbType.UniqueIdentifier, securityAuditEventUid);
        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task RecordReviewAsync(
        string actorSubject, string action, Guid correlationId, Guid? securityAuditEventUid,
        int? resultCount, string? filterSummary, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = Command(connection, "dbo.PlatformAudit_RecordSecurityAuditReview");
        Add(command, "@ActorSubject", SqlDbType.NVarChar, actorSubject, 450);
        Add(command, "@Action", SqlDbType.NVarChar, action, 100);
        Add(command, "@CorrelationId", SqlDbType.UniqueIdentifier, correlationId);
        Add(command, "@SecurityAuditEventUid", SqlDbType.UniqueIdentifier, securityAuditEventUid);
        Add(command, "@ResultCount", SqlDbType.Int, resultCount);
        Add(command, "@FilterSummary", SqlDbType.NVarChar, filterSummary, 1000);
        await connection.OpenAsync(cancellationToken);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static SqlCommand Command(SqlConnection connection, string text) => new(text, connection)
    {
        CommandType = CommandType.StoredProcedure
    };

    private static void Add(SqlCommand command, string name, SqlDbType type, object? value, int size = 0)
    {
        var parameter = size == 0 ? command.Parameters.Add(name, type) : command.Parameters.Add(name, type, size);
        parameter.Value = value ?? DBNull.Value;
    }

    private static SecurityAuditDetail Read(SqlDataReader reader) => new(
        reader.GetGuid(reader.GetOrdinal("SecurityAuditEventUid")),
        reader.GetString(reader.GetOrdinal("EventType")),
        reader.GetString(reader.GetOrdinal("Outcome")),
        reader.GetString(reader.GetOrdinal("DenialReason")),
        reader.GetString(reader.GetOrdinal("ActorSubject")),
        Nullable<long>(reader, "ClinicalUserId"),
        Nullable<Guid>(reader, "TargetTenantUid"),
        Nullable<Guid>(reader, "RequestedTenantUid"),
        reader.GetString(reader.GetOrdinal("Capability")),
        Nullable<string>(reader, "RequiredPermission"),
        reader.GetString(reader.GetOrdinal("SourceApplication")),
        Nullable<string>(reader, "RequestCorrelationId"),
        Nullable<Guid>(reader, "RequestedPatientUid"),
        Nullable<Guid>(reader, "AuthoritativePatientUid"),
        Nullable<string>(reader, "ResourceType"),
        Nullable<Guid>(reader, "ResourceUid"),
        new DateTimeOffset(DateTime.SpecifyKind(
            reader.GetDateTime(reader.GetOrdinal("OccurredAtUtc")), DateTimeKind.Utc)));

    private static SecurityAuditListItem ReadListItem(SqlDataReader reader) => new(
        reader.GetGuid(reader.GetOrdinal("SecurityAuditEventUid")),
        new DateTimeOffset(DateTime.SpecifyKind(
            reader.GetDateTime(reader.GetOrdinal("OccurredAtUtc")), DateTimeKind.Utc)),
        reader.GetString(reader.GetOrdinal("DenialReason")),
        reader.GetString(reader.GetOrdinal("Capability")),
        Nullable<string>(reader, "RequiredPermission"),
        reader.GetString(reader.GetOrdinal("SourceApplication")),
        Nullable<Guid>(reader, "TargetTenantUid"),
        Nullable<string>(reader, "RequestCorrelationId"),
        reader.GetString(reader.GetOrdinal("MaskedActorSubject")));

    private static T? Nullable<T>(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? default : reader.GetFieldValue<T>(ordinal);
    }
}
