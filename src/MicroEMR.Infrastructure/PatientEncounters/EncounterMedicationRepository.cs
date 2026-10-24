using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.PatientEncounters;
using MicroEMR.Application.PatientMedications.Contracts;
using MicroEMR.Infrastructure.PatientMedications;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.PatientEncounters;

public sealed class EncounterMedicationRepository(ITenantSqlConnectionFactory connections) : IEncounterMedicationRepository
{
    public async Task<PatientMedicationDetailsResponse?> SaveAsync(Guid patient, Guid encounter, SaveEncounterMedicationRequest input,
        long actor, CancellationToken token = default)
    {
        await using var connection = await connections.OpenConnectionAsync(token);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(token);
        SqlCommand Query(string sql)
        {
            var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.Add("@Patient", SqlDbType.UniqueIdentifier).Value = patient;
            command.Parameters.Add("@Encounter", SqlDbType.UniqueIdentifier).Value = encounter;
            return command;
        }
        async Task<PatientMedicationDetailsResponse?> Read(Guid uid)
        {
            await using var command = new SqlCommand("dbo.PatientMedication_GetByUid", connection, transaction) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("@MedicationUid", SqlDbType.UniqueIdentifier).Value = uid;
            var row = await ReadResult(command, token);
            if (row is not null && row.PatientUid != patient) throw new UnauthorizedAccessException("Medication context is unavailable.");
            return row;
        }
        try
        {
            // Same patient -> encounter order as the existing encounter diagnosis/procedure workflow.
            await using (var live = Query("SELECT PatientId FROM dbo.Patient WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@Patient AND IsDeleted=0"))
                if (await live.ExecuteScalarAsync(token) is null) return null;
            await using (var context = Query("SELECT EncounterStatus,RowVersion FROM dbo.PatientEncounter WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@Patient AND EncounterUid=@Encounter"))
            {
                await using var reader = await context.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) return null;
                if (reader.GetString(0) != "Open" || !((byte[])reader[1]).SequenceEqual(Convert.FromBase64String(input.EncounterRowVersion)))
                    throw new EncounterMedicationConflictException("The encounter changed or is read-only. Reload before saving.");
            }
            await using (var user = Query("SELECT UserId FROM dbo.ApplicationUser WHERE UserId=@Actor AND IsActive=1"))
            {
                user.Parameters.Add("@Actor", SqlDbType.BigInt).Value = actor;
                if (await user.ExecuteScalarAsync(token) is null) throw new UnauthorizedAccessException("The clinical actor is unavailable.");
            }
            var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input)));
            var action = "CppMedication" + input.Action;
            // Existing encounter history supplies a durable receipt; the encounter lock serializes replays.
            await using (var receipt = Query("SELECT ActionType,ActionDescription FROM dbo.PatientEncounterHistory WHERE PatientUid=@Patient AND EncounterUid=@Encounter AND CreatedBy=@Actor AND ActionType IN(N'CppMedicationCreate',N'CppMedicationUpdate',N'CppMedicationDiscontinue') AND ActionDescription LIKE @Prefix"))
            {
                receipt.Parameters.Add("@Actor", SqlDbType.BigInt).Value = actor;
                receipt.Parameters.Add("@Prefix", SqlDbType.NVarChar, 50).Value = input.RequestUid.ToString("N") + "|%";
                string? recorded = null, recordedAction = null;
                await using (var reader = await receipt.ExecuteReaderAsync(token))
                {
                    if (await reader.ReadAsync(token)) { recordedAction = reader.GetString(0); recorded = reader.GetString(1); }
                }
                if (recorded is not null)
                {
                    var parts = recorded.Split('|');
                    if (recordedAction != action || parts.Length != 3 || parts[1] != hash || !Guid.TryParseExact(parts[2], "N", out var uid))
                        throw new EncounterMedicationConflictException("That request identifier has already been used. Reload before saving.");
                    var previous = await Read(uid) ?? throw new EncounterMedicationConflictException("The recorded medication is unavailable.");
                    await transaction.CommitAsync(token);
                    return previous;
                }
            }
            if (input.Action != "Create")
            {
                await using var source = Query("SELECT MedicationStatus,RowVersion FROM dbo.PatientMedication WITH(UPDLOCK,HOLDLOCK) WHERE PatientUid=@Patient AND MedicationUid=@Medication");
                source.Parameters.Add("@Medication", SqlDbType.UniqueIdentifier).Value = input.MedicationUid!.Value;
                await using var reader = await source.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) return null;
                if (reader.GetString(0) != "Active" || !((byte[])reader[1]).SequenceEqual(Convert.FromBase64String(input.MedicationRowVersion!)))
                    throw new EncounterMedicationConflictException("The medication changed or is no longer active. Reload before saving.");
            }
            await using var mutation = input.Action switch
            {
                "Create" => PatientMedicationRepository.CreateCommand(connection, patient, input.Medication!, actor, null),
                "Update" => PatientMedicationRepository.UpdateCommand(connection, patient, input.MedicationUid!.Value, Update(input), actor),
                "Discontinue" => PatientMedicationRepository.DiscontinueCommand(connection, patient, input.MedicationUid!.Value,
                    new() { RowVersion = input.MedicationRowVersion!, DiscontinueReason = input.DiscontinueReason }, actor),
                _ => throw new ArgumentException("Select a medication action.")
            };
            mutation.Transaction = transaction;
            var saved = await ReadResult(mutation, token) ?? throw new InvalidOperationException("Medication operation returned no record.");
            if (saved.PatientUid != patient) throw new UnauthorizedAccessException("Medication context is unavailable.");
            await using var provenance = new SqlCommand("dbo.PatientEncounterHistory_Create", connection, transaction) { CommandType = CommandType.StoredProcedure };
            provenance.Parameters.Add("@EncounterUid", SqlDbType.UniqueIdentifier).Value = encounter;
            provenance.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patient;
            provenance.Parameters.Add("@ActionType", SqlDbType.NVarChar, 50).Value = action;
            provenance.Parameters.Add("@ActionDescription", SqlDbType.NVarChar, 500).Value = $"{input.RequestUid:N}|{hash}|{saved.MedicationUid:N}";
            provenance.Parameters.Add("@OldStatus", SqlDbType.NVarChar, 50).Value = "Open";
            provenance.Parameters.Add("@NewStatus", SqlDbType.NVarChar, 50).Value = "Open";
            provenance.Parameters.Add("@CreatedBy", SqlDbType.BigInt).Value = actor;
            provenance.Parameters.Add("@ReturnResult", SqlDbType.Bit).Value = false;
            await provenance.ExecuteNonQueryAsync(token);
            await transaction.CommitAsync(token);
            return saved;
        }
        catch (Exception error)
        {
            // A nested domain procedure may already have rolled back the whole transaction.
            try { await transaction.RollbackAsync(CancellationToken.None); }
            catch (InvalidOperationException) { }
            catch (SqlException) { }
            if (error is SqlException sql)
            {
                if (sql.Number == 51057) throw new EncounterMedicationConflictException("The medication changed. Reload before saving.");
                if (sql.Number is 52900 or 52902 or 53100 or 51056 or 51062) throw new ArgumentException(sql.Message, sql);
                if (sql.Number == 52901) throw new UnauthorizedAccessException("The clinical actor is unavailable.", sql);
            }
            throw;
        }
    }

    private static UpdatePatientMedicationRequest Update(SaveEncounterMedicationRequest input)
    {
        var row = input.Medication!;
        return new() { RowVersion = input.MedicationRowVersion!, Status = "Active", MedicationName = row.MedicationName,
            Classification = row.Classification, Strength = row.Strength, DosageForm = row.DosageForm, Route = row.Route,
            Directions = row.Directions, Frequency = row.Frequency, StartDate = row.StartDate, EndDate = row.EndDate,
            Indication = row.Indication, PrescriberName = row.PrescriberName, Notes = row.Notes,
            PlanFields = row.PlanFields, ComplianceAssessment = row.ComplianceAssessment };
    }

    private static async Task<PatientMedicationDetailsResponse?> ReadResult(SqlCommand command, CancellationToken token)
    {
        await using var reader = await command.ExecuteReaderAsync(token);
        PatientMedicationDetailsResponse? result = null;
        do { while (await reader.ReadAsync(token)) result = PatientMedicationRepository.MapDetails(reader); }
        while (await reader.NextResultAsync(token));
        return result;
    }
}
