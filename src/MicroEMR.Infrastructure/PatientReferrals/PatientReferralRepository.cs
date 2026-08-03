using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.PatientReferrals;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.PatientReferrals;

public sealed class PatientReferralRepository(
    ITenantSqlConnectionFactory connectionFactory,
    ILogger<PatientReferralRepository> logger) : IPatientReferralRepository
{
    public async Task<IReadOnlyList<PatientReferral>> GetByPatientUidAsync(
        Guid patientUid,
        CancellationToken cancellationToken = default)
    {
        var referrals = new List<PatientReferral>();
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = CreateCommand(connection, "dbo.PatientReferral_GetByPatientUid");
        command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            referrals.Add(Map(reader));
        }

        return referrals;
    }

    public async Task<PatientReferral?> GetByUidAsync(
        Guid patientUid,
        Guid referralUid,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = CreateCommand(connection, "dbo.PatientReferral_GetByUid");
        command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
        command.Parameters.Add("@ReferralUid", SqlDbType.UniqueIdentifier).Value = referralUid;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task<PatientReferral> CreateAsync(
        Guid patientUid,
        CreatePatientReferralRequest request,
        long createdBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = CreateCommand(connection, "dbo.PatientReferral_Create");
        command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
        AddRequiredString(command, "@RecipientName", 200, request.RecipientName);
        AddNullableString(command, "@RecipientOrganization", 200, request.RecipientOrganization);
        AddNullableString(command, "@RecipientPhone", 30, request.RecipientPhone);
        AddNullableString(command, "@RecipientFax", 30, request.RecipientFax);
        AddRequiredString(command, "@Reason", 1000, request.Reason);
        AddNullableString(command, "@ClinicalSummary", -1, request.ClinicalSummary);
        command.Parameters.Add("@CreatedBy", SqlDbType.BigInt).Value = createdBy;

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return Map(reader);
            }

            throw new InvalidOperationException("PatientReferral_Create returned no referral record.");
        }
        catch (SqlException exception)
        {
            logger.LogError(
                exception,
                "Failed to create referral for patient {PatientUid} by clinical user {CreatedBy}.",
                patientUid,
                createdBy);
            throw;
        }
    }

    public Task<PatientReferral?> MarkSentAsync(
        Guid patientUid, Guid referralUid, string rowVersion, long updatedBy,
        CancellationToken cancellationToken = default) =>
        TransitionAsync("dbo.PatientReferral_MarkSent", patientUid, referralUid, rowVersion, updatedBy, cancellationToken);

    public Task<PatientReferral?> MarkResponseReceivedAsync(
        Guid patientUid, Guid referralUid, string rowVersion, long updatedBy,
        CancellationToken cancellationToken = default) =>
        TransitionAsync("dbo.PatientReferral_MarkResponseReceived", patientUid, referralUid, rowVersion, updatedBy, cancellationToken);

    public Task<PatientReferral?> CloseAsync(
        Guid patientUid, Guid referralUid, string rowVersion, long updatedBy,
        CancellationToken cancellationToken = default) =>
        TransitionAsync("dbo.PatientReferral_Close", patientUid, referralUid, rowVersion, updatedBy, cancellationToken);

    private async Task<PatientReferral?> TransitionAsync(
        string procedure, Guid patientUid, Guid referralUid, string rowVersion, long updatedBy,
        CancellationToken cancellationToken)
    {
        byte[] expectedRowVersion;
        try { expectedRowVersion = Convert.FromBase64String(rowVersion); }
        catch (FormatException exception) { throw new ArgumentException("RowVersion is invalid.", nameof(rowVersion), exception); }
        if (expectedRowVersion.Length != 8)
            throw new ArgumentException("RowVersion is invalid.", nameof(rowVersion));

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = CreateCommand(connection, procedure);
        command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
        command.Parameters.Add("@ReferralUid", SqlDbType.UniqueIdentifier).Value = referralUid;
        command.Parameters.Add("@ExpectedRowVersion", SqlDbType.Timestamp, 8).Value = expectedRowVersion;
        command.Parameters.Add("@UpdatedBy", SqlDbType.BigInt).Value = updatedBy;
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
        }
        catch (SqlException exception) when (exception.Number == 51510)
        {
            return null;
        }
        catch (SqlException exception) when (exception.Number == 51511)
        {
            throw new PatientReferralTransitionException(
                "The referral is no longer in the expected status. Refresh and try again.");
        }
        catch (SqlException exception) when (exception.Number == 51512)
        {
            throw new PatientReferralConcurrencyException();
        }
    }

    private static SqlCommand CreateCommand(SqlConnection connection, string procedure) =>
        new(procedure, connection) { CommandType = CommandType.StoredProcedure };

    private static void AddRequiredString(SqlCommand command, string name, int size, string value) =>
        command.Parameters.Add(name, SqlDbType.NVarChar, size).Value = value;

    private static void AddNullableString(SqlCommand command, string name, int size, string? value) =>
        command.Parameters.Add(name, SqlDbType.NVarChar, size).Value =
            string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private static PatientReferral Map(SqlDataReader reader) => new()
    {
        ReferralUid = reader.GetGuid(reader.GetOrdinal("ReferralUid")),
        PatientUid = reader.GetGuid(reader.GetOrdinal("PatientUid")),
        RecipientName = reader.GetString(reader.GetOrdinal("RecipientName")),
        RecipientOrganization = GetNullableString(reader, "RecipientOrganization"),
        RecipientPhone = GetNullableString(reader, "RecipientPhone"),
        RecipientFax = GetNullableString(reader, "RecipientFax"),
        Reason = reader.GetString(reader.GetOrdinal("Reason")),
        ClinicalSummary = GetNullableString(reader, "ClinicalSummary"),
        Status = Enum.Parse<ReferralStatus>(reader.GetString(reader.GetOrdinal("Status")), true),
        CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
        CreatedBy = reader.GetInt64(reader.GetOrdinal("CreatedBy")),
        UpdatedAt = GetNullableDateTime(reader, "UpdatedAt"),
        UpdatedBy = GetNullableInt64(reader, "UpdatedBy"),
        SentAt = GetNullableDateTime(reader, "SentAt"),
        ResponseReceivedAt = GetNullableDateTime(reader, "ResponseReceivedAt"),
        ClosedAt = GetNullableDateTime(reader, "ClosedAt"),
        RowVersion = Convert.ToBase64String((byte[])reader["RowVersion"])
    };

    private static string? GetNullableString(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static DateTime? GetNullableDateTime(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    private static long? GetNullableInt64(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
    }
}
