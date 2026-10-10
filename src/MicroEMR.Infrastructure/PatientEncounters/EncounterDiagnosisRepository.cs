using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.PatientEncounters;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.PatientEncounters;

public sealed class EncounterDiagnosisRepository(ITenantSqlConnectionFactory connections, ILogger<EncounterDiagnosisRepository> logger) : IEncounterDiagnosisRepository
{
    public Task<EncounterDiagnosesResponse?> GetAsync(Guid patient, Guid encounter, CancellationToken token = default)
        => Execute(patient,encounter,null,0,token);
    public Task<EncounterDiagnosesResponse?> SaveAsync(Guid patient, Guid encounter, SaveEncounterDiagnosesRequest request, long actor, CancellationToken token = default)
        => Execute(patient,encounter,request,actor,token);
    private async Task<EncounterDiagnosesResponse?> Execute(Guid patient, Guid encounter, SaveEncounterDiagnosesRequest? input, long actor, CancellationToken token)
    {
        await using var connection = await connections.OpenConnectionAsync(token);
        await using var command = new SqlCommand(input is null ? "dbo.PatientEncounterDiagnoses_Get" : "dbo.PatientEncounterDiagnoses_Save",connection)
            { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("@PatientUid",SqlDbType.UniqueIdentifier).Value = patient;
        command.Parameters.Add("@EncounterUid",SqlDbType.UniqueIdentifier).Value = encounter;
        if (input is not null)
        {
            command.Parameters.Add("@ExpectedRowVersion",SqlDbType.Binary,8).Value = Convert.FromBase64String(input.RowVersion);
            command.Parameters.Add("@SaveToCpp",SqlDbType.Bit).Value = input.SaveToCpp;
            command.Parameters.Add("@DiagnosesJson",SqlDbType.NVarChar,-1).Value = JsonSerializer.Serialize(input.Diagnoses);
            command.Parameters.Add("@Actor",SqlDbType.BigInt).Value = actor;
        }
        try
        {
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) return null;
            var patientUid = reader.GetGuid(0); var encounterUid = reader.GetGuid(1);
            var version = Convert.ToBase64String((byte[])reader[2]); var status = reader.GetString(3);
            var items = new List<EncounterDiagnosis>();
            await reader.NextResultAsync(token);
            while (await reader.ReadAsync(token)) items.Add(new(reader.GetGuid(0),reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),reader.IsDBNull(3) ? null : reader.GetDateTime(3),reader.IsDBNull(4) ? null : reader.GetGuid(4)));
            return new(patientUid,encounterUid,version,status,items);
        }
        catch (SqlException error) when (error.Number is 51200 or 51201 or 51202)
        { throw new EncounterDiagnosisConflictException(error.Number == 51200 ? "The encounter changed. Reload before saving diagnoses." : error.Number == 51201 ? "Diagnoses can only be edited in an open encounter." : "A diagnosis is unavailable for this encounter. Reload before saving."); }
        catch (SqlException error) { logger.LogError(error,"Encounter diagnosis database operation failed."); throw; }
    }
}
