using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.PatientEncounters;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.PatientEncounters;

public sealed class EncounterContributionRepository(ITenantSqlConnectionFactory connections,
    ILogger<EncounterContributionRepository> logger) : IEncounterContributionRepository
{
    public async Task<bool> SaveAsync(Guid patientUid, Guid encounterUid, SaveEncounterContributionRequest request,
        long actorId, CancellationToken token = default)
    {
        await using var connection = await connections.OpenConnectionAsync(token);
        await using var command = new SqlCommand("dbo.PatientEncounterContribution_Save", connection)
        { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
        command.Parameters.Add("@EncounterUid", SqlDbType.UniqueIdentifier).Value = encounterUid;
        command.Parameters.Add("@ContributionUid", SqlDbType.UniqueIdentifier).Value = (object?)request.ContributionUid ?? DBNull.Value;
        command.Parameters.Add("@SectionKey", SqlDbType.NVarChar, 100).Value = request.SectionKey;
        command.Parameters.Add("@FieldKey", SqlDbType.NVarChar, 100).Value = (object?)request.FieldKey ?? DBNull.Value;
        command.Parameters.Add("@Content", SqlDbType.NVarChar, -1).Value = request.Content;
        command.Parameters.Add("@ExpectedRowVersion", SqlDbType.Binary, 8).Value = Convert.FromBase64String(request.RowVersion);
        command.Parameters.Add("@ActorId", SqlDbType.BigInt).Value = actorId;
        try { return await command.ExecuteScalarAsync(token) is Guid; }
        catch (SqlException exception) when (exception.Number is 51071 or 51073)
        { throw new EncounterNoteNotEditableException("The encounter changed or cannot be edited. Reopen it before saving.", exception); }
        catch (SqlException exception)
        { logger.LogError(exception, "Contribution save failed for encounter {EncounterUid}.", encounterUid); throw; }
    }

    internal static async Task<List<EncounterContributionResponse>> ReadAsync(SqlConnection connection,
        SqlTransaction transaction, Guid patientUid, Guid encounterUid, CancellationToken token)
    {
        await using var command = new SqlCommand("dbo.PatientEncounterContribution_Get", connection, transaction)
        { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
        command.Parameters.Add("@EncounterUid", SqlDbType.UniqueIdentifier).Value = encounterUid;
        await using var reader = await command.ExecuteReaderAsync(token);
        var result = new List<EncounterContributionResponse>();
        while (await reader.ReadAsync(token))
        {
            var uid = reader.GetGuid(0);
            var contribution = result.LastOrDefault();
            if (contribution?.ContributionUid != uid)
            {
                contribution = new() { ContributionUid = uid, EncounterUid = encounterUid, PatientUid = patientUid,
                    SectionKey = reader.GetString(1), SectionTitle = reader.GetString(2),
                    OriginalContributorId = reader.GetInt64(3), OriginalContributorDisplayName = reader.GetString(4),
                    CreatedAtUtc = DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc),
                    FieldKey = reader.FieldCount > 11 && !reader.IsDBNull(11) ? reader.GetString(11) : null,
                    ValueKind = reader.FieldCount > 12 ? reader.GetString(12) : "Narrative",
                    PriorValueJson = reader.FieldCount > 13 && !reader.IsDBNull(13) ? reader.GetString(13) : null,
                    FieldLabel = reader.FieldCount > 14 && !reader.IsDBNull(14) ? reader.GetString(14) : null };
                result.Add(contribution);
            }
            contribution.Revisions.Add(new(reader.GetInt32(6), reader.GetString(7), reader.GetInt64(8),
                reader.GetString(9), DateTime.SpecifyKind(reader.GetDateTime(10), DateTimeKind.Utc)));
        }
        return result;
    }
}
