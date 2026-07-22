using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.PatientProblems.Contracts;
using MicroEMR.Application.PatientProblems.Repositories;
using MicroEMR.Application.PatientProblems;

namespace MicroEMR.Infrastructure.PatientProblems;

public sealed class PatientProblemRepository : IPatientProblemRepository
{
    private readonly string _connectionString;
    private readonly ILogger<PatientProblemRepository> _logger;

    public PatientProblemRepository(IConfiguration configuration, ILogger<PatientProblemRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("MicroEmrDatabase")
            ?? throw new InvalidOperationException("Connection string 'MicroEmrDatabase' was not found.");
        _logger = logger;
    }

    public async Task<IReadOnlyList<PatientProblemResponse>> GetByPatientUidAsync(Guid patientUid, string statusFilter, CancellationToken cancellationToken = default)
    {
        var results = new List<PatientProblemResponse>();
        await using var connection = new SqlConnection(_connectionString);
        await using var command = CreateCommand(connection, "dbo.PatientProblem_GetByPatientUid");
        command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
        command.Parameters.Add("@StatusFilter", SqlDbType.NVarChar, 50).Value = statusFilter;
        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) results.Add(Map(reader));
        return results;
    }

    public async Task<PatientProblemResponse?> GetByUidAsync(Guid patientUid, Guid patientProblemUid, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = CreateCommand(connection, "dbo.PatientProblem_GetByUid");
        command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
        command.Parameters.Add("@PatientProblemUid", SqlDbType.UniqueIdentifier).Value = patientProblemUid;
        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public Task<PatientProblemResponse> CreateAsync(Guid patientUid, CreatePatientProblemRequest request, long? createdBy, CancellationToken cancellationToken = default)
        => ExecuteRequiredAsync("dbo.PatientProblem_Create", command =>
        {
            command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
            AddString(command, "@ProblemName", 200, request.ProblemName, false);
            AddString(command, "@ProblemDescription", 1000, request.ProblemDescription, true);
            AddDate(command, "@OnsetDate", request.OnsetDate);
            AddLong(command, "@CreatedBy", createdBy);
        }, patientUid, cancellationToken);

    public Task<PatientProblemResponse?> UpdateAsync(Guid patientUid, Guid patientProblemUid, UpdatePatientProblemRequest request, long? updatedBy, CancellationToken cancellationToken = default)
        => ExecuteOptionalAsync("dbo.PatientProblem_Update", command =>
        {
            command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
            command.Parameters.Add("@PatientProblemUid", SqlDbType.UniqueIdentifier).Value = patientProblemUid;
            AddString(command, "@ProblemName", 200, request.ProblemName, false);
            AddString(command, "@ProblemDescription", 1000, request.ProblemDescription, true);
            AddDate(command, "@OnsetDate", request.OnsetDate);
            AddLong(command, "@UpdatedBy", updatedBy);
        }, patientUid, cancellationToken);

    public Task<PatientProblemResponse?> ResolveAsync(Guid patientUid, Guid patientProblemUid, ResolvePatientProblemRequest request, long? resolvedBy, CancellationToken cancellationToken = default)
        => ExecuteOptionalAsync("dbo.PatientProblem_Resolve", command =>
        {
            command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
            command.Parameters.Add("@PatientProblemUid", SqlDbType.UniqueIdentifier).Value = patientProblemUid;
            AddString(command, "@ResolutionReason", 500, request.ResolutionReason, true);
            AddLong(command, "@ResolvedBy", resolvedBy);
        }, patientUid, cancellationToken);

    private async Task<PatientProblemResponse> ExecuteRequiredAsync(string procedure, Action<SqlCommand> configure, Guid patientUid, CancellationToken cancellationToken)
        => await ExecuteOptionalAsync(procedure, configure, patientUid, cancellationToken)
            ?? throw new InvalidOperationException($"{procedure} returned no problem record.");

    private async Task<PatientProblemResponse?> ExecuteOptionalAsync(string procedure, Action<SqlCommand> configure, Guid patientUid, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = CreateCommand(connection, procedure);
        configure(command);
        await connection.OpenAsync(cancellationToken);
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
        }
        catch (SqlException exception) when (exception.Number == 51072)
        {
            throw new PatientProblemResolvedException("Resolved problems cannot be edited.", exception);
        }
        catch (SqlException exception)
        {
            _logger.LogError(exception, "Patient problem database operation {Procedure} failed for patient {PatientUid}.", procedure, patientUid);
            throw;
        }
    }

    private static SqlCommand CreateCommand(SqlConnection connection, string procedure) => new(procedure, connection) { CommandType = CommandType.StoredProcedure };
    private static void AddString(SqlCommand command, string name, int size, string? value, bool nullable) =>
        command.Parameters.Add(name, SqlDbType.NVarChar, size).Value = nullable && string.IsNullOrWhiteSpace(value) ? DBNull.Value : value!.Trim();
    private static void AddDate(SqlCommand command, string name, DateTime? value) =>
        command.Parameters.Add(name, SqlDbType.Date).Value = value.HasValue ? value.Value.Date : DBNull.Value;
    private static void AddLong(SqlCommand command, string name, long? value) =>
        command.Parameters.Add(name, SqlDbType.BigInt).Value = value.HasValue ? value.Value : DBNull.Value;
    private static T? Nullable<T>(SqlDataReader reader, string name) where T : struct
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<T>(ordinal);
    }
    private static string? NullableString(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
    private static PatientProblemResponse Map(SqlDataReader reader) => new()
    {
        PatientProblemUid = reader.GetGuid(reader.GetOrdinal("PatientProblemUid")),
        PatientUid = reader.GetGuid(reader.GetOrdinal("PatientUid")),
        ProblemName = reader.GetString(reader.GetOrdinal("ProblemName")),
        ProblemDescription = NullableString(reader, "ProblemDescription"),
        OnsetDate = Nullable<DateTime>(reader, "OnsetDate"),
        ProblemStatus = reader.GetString(reader.GetOrdinal("ProblemStatus")),
        ResolvedAt = Nullable<DateTime>(reader, "ResolvedAt"),
        ResolvedBy = Nullable<long>(reader, "ResolvedBy"),
        ResolvedByDisplayName = NullableString(reader, "ResolvedByDisplayName"),
        ResolutionReason = NullableString(reader, "ResolutionReason"),
        CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
        CreatedBy = Nullable<long>(reader, "CreatedBy"),
        CreatedByDisplayName = NullableString(reader, "CreatedByDisplayName"),
        UpdatedAt = Nullable<DateTime>(reader, "UpdatedAt"),
        UpdatedBy = Nullable<long>(reader, "UpdatedBy"),
        RowVersion = Convert.ToBase64String((byte[])reader["RowVersion"])
    };
}
