using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.PatientFamilyHistory;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.PatientFamilyHistory;

public sealed class PatientFamilyHistoryRepository(ITenantSqlConnectionFactory connections,
    ILogger<PatientFamilyHistoryRepository> logger) : IPatientFamilyHistoryRepository
{
    public Task<IReadOnlyList<PatientFamilyHistoryResponse>> ListAsync(Guid patientUid, string status, CancellationToken token = default) =>
        Query("List", patientUid, null, null, null, null, status, token);
    public async Task<PatientFamilyHistoryResponse?> GetAsync(Guid patientUid, Guid familyHistoryUid, CancellationToken token = default) =>
        (await Query("Get", patientUid, familyHistoryUid, null, null, null, null, token)).SingleOrDefault();
    public async Task<PatientFamilyHistoryResponse> CreateAsync(Guid patientUid, CreatePatientFamilyHistoryRequest request, long actor, CancellationToken token = default) =>
        (await Query("Create", patientUid, null, request, null, actor, null, token)).Single();
    public async Task<PatientFamilyHistoryResponse?> UpdateAsync(Guid patientUid, Guid familyHistoryUid, UpdatePatientFamilyHistoryRequest request, long actor, CancellationToken token = default) =>
        (await Query("Update", patientUid, familyHistoryUid, request, request.RowVersion, actor, null, token)).SingleOrDefault();
    public async Task<PatientFamilyHistoryResponse?> ArchiveAsync(Guid patientUid, Guid familyHistoryUid, string rowVersion, long actor, CancellationToken token = default) =>
        (await Query("Archive", patientUid, familyHistoryUid, null, rowVersion, actor, null, token)).SingleOrDefault();

    private async Task<IReadOnlyList<PatientFamilyHistoryResponse>> Query(string operation, Guid patient,
        Guid? uid, SavePatientFamilyHistoryRequest? request, string? version, long? actor, string? status, CancellationToken token)
    {
        try
        {
            await using var connection = await connections.OpenConnectionAsync(token);
            await using var command = new SqlCommand("dbo.PatientFamilyMedicalHistory_" + operation, connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patient;
            if (uid.HasValue) command.Parameters.Add("@FamilyHistoryUid", SqlDbType.UniqueIdentifier).Value = uid.Value;
            if (actor.HasValue) command.Parameters.Add("@Actor", SqlDbType.BigInt).Value = actor.Value;
            if (version is not null) command.Parameters.Add("@ExpectedRowVersion", SqlDbType.Binary, 8).Value = PatientFamilyHistoryService.ValidateVersion(version);
            if (status is not null) Str("@Status", status, 20);
            if (request is not null)
            {
                Str("@Relationship", request.Relationship, 100); Str("@ConditionDescription", request.ConditionDescription, 1000);
                Str("@AgeAtOnset", request.AgeAtOnset, 100); Str("@LifeStageCode", request.LifeStageCode, 10);
                Str("@Treatment", request.Treatment, 1000); Str("@Notes", request.Notes, 1000);
            }
            var rows = new List<PatientFamilyHistoryResponse>();
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) rows.Add(new()
            {
                PatientUid = reader.GetGuid(reader.GetOrdinal("PatientUid")), FamilyHistoryUid = reader.GetGuid(reader.GetOrdinal("FamilyHistoryUid")),
                Relationship = Text("Relationship")!, ConditionDescription = Text("ConditionDescription")!, AgeAtOnset = Text("AgeAtOnset"),
                LifeStageCode = Text("LifeStageCode"), Treatment = Text("Treatment"), Notes = Text("Notes"), Status = Text("Status")!,
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")), CreatedBy = reader.GetInt64(reader.GetOrdinal("CreatedBy")),
                CreatedByDisplayName = Text("CreatedByDisplayName"), UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
                UpdatedBy = reader.IsDBNull(reader.GetOrdinal("UpdatedBy")) ? null : reader.GetInt64(reader.GetOrdinal("UpdatedBy")), UpdatedByDisplayName = Text("UpdatedByDisplayName"),
                RowVersion = Convert.ToBase64String((byte[])reader["RowVersion"])
            });
            return rows;
            string? Text(string name) => reader.IsDBNull(reader.GetOrdinal(name)) ? null : reader.GetString(reader.GetOrdinal(name));
            void Str(string name, string? value, int size) => command.Parameters.Add(name, SqlDbType.NVarChar, size).Value = (object?)value ?? DBNull.Value;
        }
        catch (SqlException e) when (e.Number == 52700) { throw new PatientFamilyHistoryNotFoundException(); }
        catch (SqlException e) when (e.Number == 52701) { throw new PatientFamilyHistoryConcurrencyException(); }
        catch (SqlException e) when (e.Number == 52702) { throw new PatientFamilyHistoryArchivedException(); }
        catch (SqlException e) when (e.Number == 52703) { throw new UnauthorizedAccessException("An active clinical actor is required."); }
        catch (SqlException e) when (e.Number == 52704) { throw new ArgumentException("Family history information is invalid."); }
        catch (SqlException e) { logger.LogError(e, "Family history {Operation} failed.", operation); throw; }
    }
}
