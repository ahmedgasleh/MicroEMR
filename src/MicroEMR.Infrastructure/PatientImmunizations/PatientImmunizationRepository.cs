using System.Data;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.PatientImmunizations;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.PatientImmunizations;

public sealed class PatientImmunizationRepository(ITenantSqlConnectionFactory connections) : IPatientImmunizationRepository
{
    public async Task<IReadOnlyList<PatientImmunizationResponse>> ListAsync(Guid patientUid, string status, CancellationToken token = default)
    {
        var items = new List<PatientImmunizationResponse>();
        await using var connection = await connections.OpenConnectionAsync(token);
        await using var command = Command(connection, "dbo.PatientImmunization_ListByPatient");
        command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
        command.Parameters.Add("@Status", SqlDbType.NVarChar, 30).Value = status;
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) items.Add(Map(reader));
        return items;
    }

    public async Task<PatientImmunizationResponse?> GetAsync(Guid patientUid, Guid immunizationUid, CancellationToken token = default)
    {
        await using var connection = await connections.OpenConnectionAsync(token);
        await using var command = Command(connection, "dbo.PatientImmunization_GetByUid");
        command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
        command.Parameters.Add("@ImmunizationUid", SqlDbType.UniqueIdentifier).Value = immunizationUid;
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? Map(reader) : null;
    }

    public async Task<PatientImmunizationResponse> CreateAsync(Guid patientUid, CreatePatientImmunizationRequest request, long actor, CancellationToken token = default) =>
        await MutateAsync("dbo.PatientImmunization_Create", patientUid, null, request, null, actor, token)
        ?? throw new InvalidOperationException("PatientImmunization_Create returned no record.");

    public Task<PatientImmunizationResponse?> UpdateAsync(Guid patientUid, Guid immunizationUid, UpdatePatientImmunizationRequest request, long actor, CancellationToken token = default) =>
        MutateAsync("dbo.PatientImmunization_Update", patientUid, immunizationUid, request, null, actor, token);

    public Task<PatientImmunizationResponse?> MarkEnteredInErrorAsync(Guid patientUid, Guid immunizationUid, MarkImmunizationEnteredInErrorRequest request, long actor, CancellationToken token = default) =>
        MutateAsync("dbo.PatientImmunization_MarkEnteredInError", patientUid, immunizationUid, null, request, actor, token);

    private async Task<PatientImmunizationResponse?> MutateAsync(string procedure, Guid patientUid, Guid? immunizationUid,
        SavePatientImmunizationRequest? request, MarkImmunizationEnteredInErrorRequest? error, long actor, CancellationToken token)
    {
        try
        {
            await using var connection = await connections.OpenConnectionAsync(token);
            await using var command = Command(connection, procedure);
            command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
            if (immunizationUid.HasValue) command.Parameters.Add("@ImmunizationUid", SqlDbType.UniqueIdentifier).Value = immunizationUid.Value;
            if (request is not null)
            {
                command.Parameters.Add("@VaccineName", SqlDbType.NVarChar, 200).Value = request.VaccineName;
                command.Parameters.Add("@AdministrationDate", SqlDbType.Date).Value = request.AdministrationDate!.Value.ToDateTime(TimeOnly.MinValue);
                Add(command, "@DoseNumber", SqlDbType.Int, request.DoseNumber);
                Add(command, "@Route", SqlDbType.NVarChar, request.Route, 100);
                Add(command, "@Site", SqlDbType.NVarChar, request.Site, 100);
                Add(command, "@LotNumber", SqlDbType.NVarChar, request.LotNumber, 100);
                command.Parameters.Add("@SourceType", SqlDbType.NVarChar, 30).Value = request.SourceType;
                Add(command, "@SourceDescription", SqlDbType.NVarChar, request.SourceDescription, 500);
                Add(command, "@AdministeredByName", SqlDbType.NVarChar, request.AdministeredByName, 200);
                Add(command, "@EncounterUid", SqlDbType.UniqueIdentifier, request.EncounterUid);
                Add(command, "@Notes", SqlDbType.NVarChar, request.Notes, 1000);
            }
            if (request is UpdatePatientImmunizationRequest update)
                command.Parameters.Add("@ExpectedRowVersion", SqlDbType.Binary, 8).Value = Version(update.RowVersion);
            if (error is not null)
            {
                command.Parameters.Add("@Reason", SqlDbType.NVarChar, 500).Value = error.Reason;
                command.Parameters.Add("@ExpectedRowVersion", SqlDbType.Binary, 8).Value = Version(error.RowVersion);
            }
            command.Parameters.Add("@Actor", SqlDbType.BigInt).Value = actor;
            await using var reader = await command.ExecuteReaderAsync(token);
            return await reader.ReadAsync(token) ? Map(reader) : null;
        }
        catch (SqlException exception) when (exception.Number == 52308) { throw new PatientImmunizationConcurrencyException(); }
        catch (SqlException exception) when (exception.Number == 52309) { throw new PatientImmunizationTerminalException(); }
    }

    private static SqlCommand Command(SqlConnection connection, string name) => new(name, connection) { CommandType = CommandType.StoredProcedure };
    private static void Add(SqlCommand command, string name, SqlDbType type, object? value, int size = 0)
    {
        var parameter = size > 0 ? command.Parameters.Add(name, type, size) : command.Parameters.Add(name, type);
        parameter.Value = value ?? DBNull.Value;
    }
    private static byte[] Version(string value)
    {
        try { var bytes = Convert.FromBase64String(value); return bytes.Length == 8 ? bytes : throw new FormatException(); }
        catch (FormatException) { throw new PatientImmunizationConcurrencyException(); }
    }
    private static PatientImmunizationResponse Map(SqlDataReader reader) => new()
    {
        ImmunizationUid = reader.GetGuid(reader.GetOrdinal("ImmunizationUid")), PatientUid = reader.GetGuid(reader.GetOrdinal("PatientUid")),
        VaccineName = reader.GetString(reader.GetOrdinal("VaccineName")), AdministrationDate = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("AdministrationDate"))),
        DoseNumber = Number32(reader,"DoseNumber"), Route = Text(reader,"Route"), Site = Text(reader,"Site"), LotNumber = Text(reader,"LotNumber"),
        SourceType = reader.GetString(reader.GetOrdinal("SourceType")), SourceDescription = Text(reader,"SourceDescription"), AdministeredByName = Text(reader,"AdministeredByName"),
        EncounterUid = Uid(reader,"EncounterUid"), Notes = Text(reader,"Notes"), Status = reader.GetString(reader.GetOrdinal("Status")),
        CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc")), CreatedBy = reader.GetInt64(reader.GetOrdinal("CreatedBy")), CreatedByDisplayName = Text(reader,"CreatedByDisplayName"),
        UpdatedAtUtc = Time(reader,"UpdatedAtUtc"), UpdatedBy = Number64(reader,"UpdatedBy"), UpdatedByDisplayName = Text(reader,"UpdatedByDisplayName"),
        EnteredInErrorAtUtc = Time(reader,"EnteredInErrorAtUtc"), EnteredInErrorBy = Number64(reader,"EnteredInErrorBy"), EnteredInErrorByDisplayName = Text(reader,"EnteredInErrorByDisplayName"),
        EnteredInErrorReason = Text(reader,"EnteredInErrorReason"), RowVersion = Convert.ToBase64String((byte[])reader["RowVersion"])
    };
    private static string? Text(SqlDataReader r,string n){var i=r.GetOrdinal(n);return r.IsDBNull(i)?null:r.GetString(i);}
    private static int? Number32(SqlDataReader r,string n){var i=r.GetOrdinal(n);return r.IsDBNull(i)?null:r.GetInt32(i);}
    private static long? Number64(SqlDataReader r,string n){var i=r.GetOrdinal(n);return r.IsDBNull(i)?null:r.GetInt64(i);}
    private static DateTime? Time(SqlDataReader r,string n){var i=r.GetOrdinal(n);return r.IsDBNull(i)?null:r.GetDateTime(i);}
    private static Guid? Uid(SqlDataReader r,string n){var i=r.GetOrdinal(n);return r.IsDBNull(i)?null:r.GetGuid(i);}
}
