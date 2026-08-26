using System.Data;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.Cds;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.Cds;

public sealed class CdsRepository(ITenantSqlConnectionFactory connectionFactory) : ICdsRepository
{
    public async Task<bool> PatientExistsAsync(Guid patientUid, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = Command(connection, "dbo.CdsAlert_PatientExists");
        Add(command, "@PatientUid", SqlDbType.UniqueIdentifier, patientUid);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<CdsAlertResponse>> ListAsync(Guid patientUid, bool includeHistory,
        CancellationToken cancellationToken)
    {
        var items = new List<CdsAlertResponse>();
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = Command(connection, "dbo.CdsAlert_List");
        Add(command, "@PatientUid", SqlDbType.UniqueIdentifier, patientUid);
        Add(command, "@IncludeHistory", SqlDbType.Bit, includeHistory);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) items.Add(MapAlert(reader));
        return items;
    }

    public async Task<IReadOnlyList<CdsAlertHistoryResponse>> GetHistoryAsync(Guid patientUid, Guid alertUid,
        CancellationToken cancellationToken)
    {
        var items = new List<CdsAlertHistoryResponse>();
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = Command(connection, "dbo.CdsAlertHistory_List");
        Add(command, "@PatientUid", SqlDbType.UniqueIdentifier, patientUid);
        Add(command, "@CdsAlertUid", SqlDbType.UniqueIdentifier, alertUid);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new CdsAlertHistoryResponse
            {
                CdsAlertHistoryUid = reader.GetGuid(reader.GetOrdinal("CdsAlertHistoryUid")),
                CdsAlertUid = reader.GetGuid(reader.GetOrdinal("CdsAlertUid")),
                EventType = reader.GetString(reader.GetOrdinal("EventType")),
                ActorUserId = NullableInt64(reader, "ActorUserId"),
                ActorDisplayName = NullableString(reader, "ActorDisplayName"),
                OccurredAtUtc = reader.GetDateTime(reader.GetOrdinal("OccurredAtUtc")),
                ReasonCode = NullableString(reader, "ReasonCode"),
                Comment = NullableString(reader, "Comment"),
                RuleKey = reader.GetString(reader.GetOrdinal("RuleKey")),
                RuleVersion = reader.GetInt32(reader.GetOrdinal("RuleVersion"))
            });
        }
        return items;
    }

    public async Task<CdsAlertResponse> RecordFindingAsync(PersistedCdsFinding finding,
        CancellationToken cancellationToken)
    {
        return await ExecuteAlertAsync("dbo.CdsAlert_RecordFinding", command =>
        {
            Add(command, "@PatientUid", SqlDbType.UniqueIdentifier, finding.PatientUid);
            Add(command, "@RuleKey", SqlDbType.NVarChar, finding.RuleKey, 100);
            Add(command, "@RuleVersion", SqlDbType.Int, finding.RuleVersion);
            Add(command, "@FindingFingerprint", SqlDbType.Char, finding.Fingerprint, 64);
            Add(command, "@Severity", SqlDbType.NVarChar, finding.Severity, 20);
            Add(command, "@Title", SqlDbType.NVarChar, finding.Title, 200);
            Add(command, "@Explanation", SqlDbType.NVarChar, finding.Explanation, 1000);
            Add(command, "@SuggestedAction", SqlDbType.NVarChar, finding.SuggestedAction, 1000);
            Add(command, "@RuleSourceReference", SqlDbType.NVarChar, finding.SourceReference, 500);
        }, cancellationToken) ?? throw new InvalidOperationException("CDS finding was not persisted.");
    }

    public async Task ResolveRuleFindingsAsync(Guid patientUid, string ruleKey, int ruleVersion,
        string? exceptFingerprint, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = Command(connection, "dbo.CdsAlert_ResolveRuleFindings");
        Add(command, "@PatientUid", SqlDbType.UniqueIdentifier, patientUid);
        Add(command, "@RuleKey", SqlDbType.NVarChar, ruleKey, 100);
        Add(command, "@RuleVersion", SqlDbType.Int, ruleVersion);
        Add(command, "@ExceptFingerprint", SqlDbType.Char, exceptFingerprint, 64);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task<CdsAlertResponse?> AcknowledgeAsync(Guid patientUid, Guid alertUid, byte[] rowVersion,
        long actorUserId, CancellationToken cancellationToken) => RespondAsync("dbo.CdsAlert_Acknowledge", command =>
    {
        CommonResponse(command, patientUid, alertUid, actorUserId, rowVersion);
    }, cancellationToken);

    public Task<CdsAlertResponse?> DismissAsync(Guid patientUid, Guid alertUid, string reasonCode,
        string? comment, byte[] rowVersion, long actorUserId, CancellationToken cancellationToken) =>
        RespondAsync("dbo.CdsAlert_Dismiss", command =>
        {
            CommonResponse(command, patientUid, alertUid, actorUserId, rowVersion);
            Add(command, "@ReasonCode", SqlDbType.NVarChar, reasonCode, 50);
            Add(command, "@Comment", SqlDbType.NVarChar, comment, 500);
        }, cancellationToken);

    private async Task<CdsAlertResponse?> RespondAsync(string procedure, Action<SqlCommand> parameters,
        CancellationToken cancellationToken)
    {
        try { return await ExecuteAlertAsync(procedure, parameters, cancellationToken); }
        catch (SqlException ex) when (ex.Number == 51401) { throw new CdsInvalidTransitionException(); }
        catch (SqlException ex) when (ex.Number == 51402) { throw new CdsConcurrencyException(); }
        catch (SqlException ex) when (ex.Number == 51403) { throw new CdsInvalidDismissReasonException(); }
    }

    private async Task<CdsAlertResponse?> ExecuteAlertAsync(string procedure, Action<SqlCommand> parameters,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = Command(connection, procedure);
        parameters(command);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapAlert(reader) : null;
    }

    private static void CommonResponse(SqlCommand command, Guid patientUid, Guid alertUid,
        long actorUserId, byte[] rowVersion)
    {
        Add(command, "@PatientUid", SqlDbType.UniqueIdentifier, patientUid);
        Add(command, "@CdsAlertUid", SqlDbType.UniqueIdentifier, alertUid);
        Add(command, "@ActorUserId", SqlDbType.BigInt, actorUserId);
        Add(command, "@ExpectedRowVersion", SqlDbType.Binary, rowVersion, 8);
    }

    private static CdsAlertResponse MapAlert(SqlDataReader reader) => new()
    {
        CdsAlertUid = reader.GetGuid(reader.GetOrdinal("CdsAlertUid")),
        PatientUid = reader.GetGuid(reader.GetOrdinal("PatientUid")),
        RuleKey = reader.GetString(reader.GetOrdinal("RuleKey")),
        RuleVersion = reader.GetInt32(reader.GetOrdinal("RuleVersion")),
        FindingFingerprint = reader.GetString(reader.GetOrdinal("FindingFingerprint")),
        Severity = reader.GetString(reader.GetOrdinal("Severity")),
        Status = reader.GetString(reader.GetOrdinal("Status")),
        Title = reader.GetString(reader.GetOrdinal("Title")),
        Explanation = reader.GetString(reader.GetOrdinal("Explanation")),
        SuggestedAction = reader.GetString(reader.GetOrdinal("SuggestedAction")),
        RuleSourceReference = NullableString(reader, "RuleSourceReference"),
        FirstDetectedAtUtc = reader.GetDateTime(reader.GetOrdinal("FirstDetectedAtUtc")),
        LastEvaluatedAtUtc = reader.GetDateTime(reader.GetOrdinal("LastEvaluatedAtUtc")),
        AcknowledgedBy = NullableInt64(reader, "AcknowledgedBy"),
        AcknowledgedAtUtc = NullableDateTime(reader, "AcknowledgedAtUtc"),
        DismissedBy = NullableInt64(reader, "DismissedBy"),
        DismissedAtUtc = NullableDateTime(reader, "DismissedAtUtc"),
        DismissReasonCode = NullableString(reader, "DismissReasonCode"),
        DismissComment = NullableString(reader, "DismissComment"),
        ResolvedAtUtc = NullableDateTime(reader, "ResolvedAtUtc"),
        RowVersion = Convert.ToBase64String((byte[])reader["RowVersion"])
    };

    private static SqlCommand Command(SqlConnection connection, string procedure) =>
        new(procedure, connection) { CommandType = CommandType.StoredProcedure };
    private static void Add(SqlCommand command, string name, SqlDbType type, object? value, int size = 0) =>
        command.Parameters.Add(new SqlParameter(name, type, size) { Value = value ?? DBNull.Value });
    private static string? NullableString(SqlDataReader reader, string name) { var i=reader.GetOrdinal(name); return reader.IsDBNull(i)?null:reader.GetString(i); }
    private static long? NullableInt64(SqlDataReader reader, string name) { var i=reader.GetOrdinal(name); return reader.IsDBNull(i)?null:reader.GetInt64(i); }
    private static DateTime? NullableDateTime(SqlDataReader reader, string name) { var i=reader.GetOrdinal(name); return reader.IsDBNull(i)?null:reader.GetDateTime(i); }
}
