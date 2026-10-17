using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.PatientRiskFactors;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.PatientRiskFactors;

public sealed class PatientRiskFactorRepository(ITenantSqlConnectionFactory connections,
    ILogger<PatientRiskFactorRepository> logger) : IPatientRiskFactorRepository
{
    public Task<IReadOnlyList<PatientRiskFactorResponse>> ListAsync(Guid patientUid, string status, CancellationToken token = default) =>
        Query("List", patientUid, null, null, null, null, status, token);
    public async Task<PatientRiskFactorResponse?> GetAsync(Guid patientUid, Guid riskFactorUid, CancellationToken token = default) =>
        (await Query("Get", patientUid, riskFactorUid, null, null, null, null, token)).SingleOrDefault();
    public async Task<PatientRiskFactorResponse> CreateAsync(Guid patientUid, CreatePatientRiskFactorRequest request, long actor, CancellationToken token = default) =>
        (await Query("Create", patientUid, null, request, null, actor, null, token)).Single();
    public async Task<PatientRiskFactorResponse?> UpdateAsync(Guid patientUid, Guid riskFactorUid, UpdatePatientRiskFactorRequest request, long actor, CancellationToken token = default) =>
        (await Query("Update", patientUid, riskFactorUid, request, request.RowVersion, actor, null, token)).SingleOrDefault();
    public async Task<PatientRiskFactorResponse?> ArchiveAsync(Guid patientUid, Guid riskFactorUid, string rowVersion, long actor, CancellationToken token = default) =>
        (await Query("Archive", patientUid, riskFactorUid, null, rowVersion, actor, null, token)).SingleOrDefault();

    private async Task<IReadOnlyList<PatientRiskFactorResponse>> Query(string operation, Guid patient,
        Guid? uid, SavePatientRiskFactorRequest? request, string? version, long? actor, string? status, CancellationToken token)
    {
        try
        {
            await using var connection = await connections.OpenConnectionAsync(token);
            await using var command = new SqlCommand("dbo.PatientRiskFactor_" + operation, connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patient;
            if (uid.HasValue) command.Parameters.Add("@RiskFactorUid", SqlDbType.UniqueIdentifier).Value = uid.Value;
            if (actor.HasValue) command.Parameters.Add("@Actor", SqlDbType.BigInt).Value = actor.Value;
            if (version is not null) command.Parameters.Add("@ExpectedRowVersion", SqlDbType.Binary, 8).Value = PatientRiskFactorService.ValidateVersion(version);
            if (status is not null) Str("@Status", status, 20);
            if (request is not null)
            {
                Str("@RiskFactor", request.RiskFactor, 200); Str("@ExposureDetails", request.ExposureDetails, 1000);
                Str("@LifeStageCode", request.LifeStageCode, 10);
                Str("@Notes", request.Notes, 1000);
            }
            var rows = new List<PatientRiskFactorResponse>();
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) rows.Add(new()
            {
                PatientUid = reader.GetGuid(reader.GetOrdinal("PatientUid")), RiskFactorUid = reader.GetGuid(reader.GetOrdinal("RiskFactorUid")),
                RiskFactor = Text("RiskFactor")!, ExposureDetails = Text("ExposureDetails")!,
                LifeStageCode = Text("LifeStageCode"), Notes = Text("Notes"), Status = Text("Status")!,
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")), CreatedBy = reader.GetInt64(reader.GetOrdinal("CreatedBy")),
                CreatedByDisplayName = Text("CreatedByDisplayName"), UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
                UpdatedBy = reader.IsDBNull(reader.GetOrdinal("UpdatedBy")) ? null : reader.GetInt64(reader.GetOrdinal("UpdatedBy")), UpdatedByDisplayName = Text("UpdatedByDisplayName"),
                RowVersion = Convert.ToBase64String((byte[])reader["RowVersion"])
            });
            return rows;
            string? Text(string name) => reader.IsDBNull(reader.GetOrdinal(name)) ? null : reader.GetString(reader.GetOrdinal(name));
            void Str(string name, string? value, int size) => command.Parameters.Add(name, SqlDbType.NVarChar, size).Value = (object?)value ?? DBNull.Value;
        }
        catch (SqlException e) when (e.Number == 52800) { throw new PatientRiskFactorNotFoundException(); }
        catch (SqlException e) when (e.Number == 52801) { throw new PatientRiskFactorConcurrencyException(); }
        catch (SqlException e) when (e.Number == 52802) { throw new PatientRiskFactorArchivedException(); }
        catch (SqlException e) when (e.Number == 52803) { throw new UnauthorizedAccessException("An active clinical actor is required."); }
        catch (SqlException e) when (e.Number == 52804) { throw new ArgumentException("Risk factor information is invalid."); }
        catch (SqlException e) { logger.LogError(e, "Risk factor {Operation} failed.", operation); throw; }
    }
}
