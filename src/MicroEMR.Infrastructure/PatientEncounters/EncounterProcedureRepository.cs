using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.PatientClinicalHistory;
using MicroEMR.Application.PatientEncounters;
using MicroEMR.Infrastructure.PatientClinicalHistory;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.PatientEncounters;

public sealed class EncounterProcedureRepository(ITenantSqlConnectionFactory connections) : IEncounterProcedureRepository
{
    public async Task<PatientClinicalHistoryResponse?> SaveAsync(Guid patient, Guid encounter, SaveEncounterProcedureRequest input,
        long actor, CancellationToken token = default)
    {
        try
        {
            await using var connection = await connections.OpenConnectionAsync(token);
            await using var command = new SqlCommand("dbo.PatientEncounterProcedure_Save", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patient;
            command.Parameters.Add("@EncounterUid", SqlDbType.UniqueIdentifier).Value = encounter;
            command.Parameters.Add("@HistoryUid", SqlDbType.UniqueIdentifier).Value = input.HistoryUid;
            command.Parameters.Add("@IsNew", SqlDbType.Bit).Value = input.IsNew;
            command.Parameters.Add("@ExpectedEncounterRowVersion", SqlDbType.Binary, 8).Value = Convert.FromBase64String(input.EncounterRowVersion);
            command.Parameters.Add("@ExpectedHistoryRowVersion", SqlDbType.VarBinary, -1).Value = input.IsNew ? DBNull.Value : Convert.FromBase64String(input.HistoryRowVersion!);
            command.Parameters.Add("@Description", SqlDbType.NVarChar, 1000).Value = input.Description;
            command.Parameters.Add("@ClinicalFields", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(new HistoryClinicalFields { ProcedureDate = input.ProcedureDate, Notes = input.Notes });
            command.Parameters.Add("@Actor", SqlDbType.BigInt).Value = actor;
            await using var reader = await command.ExecuteReaderAsync(token);
            PatientClinicalHistoryResponse? result = null;
            // Existing History procedures emit a row inside the outer transaction. Drain every result
            // before returning so a late provenance/COMMIT failure cannot be reported as success.
            do
            {
                while (await reader.ReadAsync(token)) result = PatientClinicalHistoryRepository.Map(reader);
            } while (await reader.NextResultAsync(token));
            return result;
        }
        catch (SqlException error) when (error.Number is 53101 or 53102 or 52105 or 52106)
        { throw new EncounterProcedureConflictException("The encounter or procedure changed or is read-only. Reload before saving."); }
        catch (SqlException error) when (error.Number is 52100 or 52101 or 52102 or 52107)
        { throw new ArgumentException(error.Message, error); }
        catch (SqlException error) when (error.Number == 52104)
        { throw new UnauthorizedAccessException("The clinical actor is unavailable.", error); }
    }
}
