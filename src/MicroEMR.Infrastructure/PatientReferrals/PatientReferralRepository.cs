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
        command.Parameters.Add("@ReferringProviderUid", SqlDbType.UniqueIdentifier).Value = request.ReferringProviderUid;
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

    public async Task<PatientReferral?> UpdateDraftAsync(Guid patientUid, Guid referralUid,
        UpdatePatientReferralDraftRequest request, long updatedBy, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = CreateCommand(connection, "dbo.PatientReferral_UpdateDraft");
        command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
        command.Parameters.Add("@ReferralUid", SqlDbType.UniqueIdentifier).Value = referralUid;
        AddRequiredString(command, "@RecipientName", 200, request.RecipientName);
        AddNullableString(command, "@RecipientOrganization", 200, request.RecipientOrganization);
        AddNullableString(command, "@RecipientPhone", 30, request.RecipientPhone);
        AddNullableString(command, "@RecipientFax", 30, request.RecipientFax);
        AddRequiredString(command, "@Reason", 1000, request.Reason);
        AddNullableString(command, "@ClinicalSummary", -1, request.ClinicalSummary);
        command.Parameters.Add("@ReferringProviderUid", SqlDbType.UniqueIdentifier).Value = request.ReferringProviderUid;
        command.Parameters.Add("@ExpectedRowVersion", SqlDbType.Timestamp, 8).Value = ParseVersion(request.RowVersion);
        command.Parameters.Add("@UpdatedBy", SqlDbType.BigInt).Value = updatedBy;
        try { await using var reader = await command.ExecuteReaderAsync(cancellationToken); return await reader.ReadAsync(cancellationToken) ? Map(reader) : null; }
        catch (SqlException e) when (e.Number == 51510) { return null; }
        catch (SqlException e) when (e.Number == 51512) { throw new PatientReferralConcurrencyException(); }
        catch (SqlException e) when (e.Number == 51504) { throw new ArgumentException("The referring provider is unavailable."); }
    }

    public async Task<IReadOnlyList<ReferralProviderListItem>> GetActiveProvidersAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<ReferralProviderListItem>();
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = CreateCommand(connection, "dbo.PatientReferral_GetActiveProviders");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) results.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3)));
        return results;
    }

    public async Task<ReferralProvider?> GetProviderAsync(Guid providerUid, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = CreateCommand(connection, "dbo.PatientReferral_GetProvider");
        command.Parameters.Add("@ProviderUid", SqlDbType.UniqueIdentifier).Value = providerUid;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4)) : null;
    }

    public async Task<ReferralArtifactContent?> GetArtifactAsync(Guid patientUid, Guid referralUid, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = CreateCommand(connection, "dbo.PatientReferralArtifact_Get");
        command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
        command.Parameters.Add("@ReferralUid", SqlDbType.UniqueIdentifier).Value = referralUid;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
            (byte[])reader["PdfContent"], reader.GetInt64(4), reader.GetString(5), reader.GetString(6), reader.GetDateTime(7)) : null;
    }

    public Task<PatientReferral?> MarkSentAsync(Guid patientUid, Guid referralUid, string rowVersion, long updatedBy,
        CancellationToken cancellationToken = default) =>
        TransitionAsync("dbo.PatientReferral_MarkSent",patientUid,referralUid,rowVersion,updatedBy,cancellationToken);

    public async Task<PatientReferral?> SendWithArtifactAsync(Guid patientUid, Guid referralUid, string rowVersion,
        long updatedBy, ReferralArtifactWrite artifact, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = CreateCommand(connection, "dbo.PatientReferral_Send");
        command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
        command.Parameters.Add("@ReferralUid", SqlDbType.UniqueIdentifier).Value = referralUid;
        command.Parameters.Add("@ExpectedRowVersion", SqlDbType.Timestamp, 8).Value = ParseVersion(rowVersion);
        command.Parameters.Add("@UpdatedBy", SqlDbType.BigInt).Value = updatedBy;
        command.Parameters.Add("@ArtifactUid", SqlDbType.UniqueIdentifier).Value = artifact.ArtifactUid;
        command.Parameters.Add("@SentAt", SqlDbType.DateTime2).Value = artifact.SentAtUtc;
        command.Parameters.Add("@PdfContent", SqlDbType.VarBinary, -1).Value = artifact.PdfContent;
        AddRequiredString(command, "@FileName", 260, artifact.FileName);
        command.Parameters.Add("@Sha256", SqlDbType.Char, 64).Value = artifact.Sha256;
        AddRequiredString(command, "@SnapshotJson", -1, artifact.SnapshotJson);
        AddRequiredString(command, "@ProviderDisplayName", 200, artifact.ProviderDisplayName);
        AddNullableString(command, "@ProviderCredential", 200, artifact.ProviderCredential);
        try { await using var reader = await command.ExecuteReaderAsync(cancellationToken); return await reader.ReadAsync(cancellationToken) ? Map(reader) : null; }
        catch (SqlException e) when (e.Number == 51510) { return null; }
        catch (SqlException e) when (e.Number == 51511) { throw new PatientReferralTransitionException("The referral is no longer Draft. Refresh and try again."); }
        catch (SqlException e) when (e.Number == 51512) { throw new PatientReferralConcurrencyException(); }
    }

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

    private static byte[] ParseVersion(string rowVersion)
    {
        byte[] value;
        try { value = Convert.FromBase64String(rowVersion); }
        catch (FormatException e) { throw new ArgumentException("RowVersion is invalid.", nameof(rowVersion), e); }
        if (value.Length != 8) throw new ArgumentException("RowVersion is invalid.", nameof(rowVersion));
        return value;
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
        ReferringProviderUid = GetNullableGuid(reader, "ReferringProviderUid"),
        ReferringProviderDisplayNameSnapshot = GetNullableString(reader, "ReferringProviderDisplayNameSnapshot"),
        ReferringProviderCredentialSnapshot = GetNullableString(reader, "ReferringProviderCredentialSnapshot"),
        ArtifactUid = GetNullableGuid(reader, "ArtifactUid"),
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

    private static Guid? GetNullableGuid(SqlDataReader reader, string name)
    { var ordinal = reader.GetOrdinal(name); return reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal); }

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
