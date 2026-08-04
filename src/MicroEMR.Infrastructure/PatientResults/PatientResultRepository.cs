using System.Data;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.PatientResults;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.PatientResults;

public sealed class PatientResultRepository(ITenantSqlConnectionFactory connectionFactory) : IPatientResultRepository
{
    public async Task<int> GetUnreviewedCount(CancellationToken token = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(token);
        await using var command = Command(connection, "dbo.PatientResult_GetUnreviewedCount");
        return checked((int)Convert.ToInt64(await command.ExecuteScalarAsync(token)));
    }

    public async Task<IReadOnlyList<PatientResultResponse>> List(Guid patientUid, string status, CancellationToken token = default)
    {
        var results = new List<PatientResultResponse>();
        await using var connection = await connectionFactory.OpenConnectionAsync(token);
        await using var command = Command(connection, "dbo.PatientResult_GetByPatientUid");
        Parameter(command, "@PatientUid", SqlDbType.UniqueIdentifier, patientUid);
        Parameter(command, "@StatusFilter", SqlDbType.NVarChar, status, 50);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) results.Add(Map(reader));
        return results;
    }

    public Task<PatientResultResponse?> Get(Guid patientUid, Guid uid, CancellationToken token = default) =>
        Run("dbo.PatientResult_GetByUid", patientUid, uid, null, null, null, token);
    public Task<PatientResultResponse?> Create(Guid patientUid, CreatePatientResultRequest request, long? user, CancellationToken token = default) =>
        Run("dbo.PatientResult_Create", patientUid, null, request, null, user, token);
    public Task<PatientResultResponse?> Update(Guid patientUid, Guid uid, UpdatePatientResultRequest request, long? user, CancellationToken token = default) =>
        Run("dbo.PatientResult_Update", patientUid, uid, request, null, user, token);
    public Task<PatientResultResponse?> Review(Guid patientUid, Guid uid, MarkPatientResultReviewedRequest request, long? user, CancellationToken token = default) =>
        Run("dbo.PatientResult_MarkReviewed", patientUid, uid, null, request, user, token);

    private async Task<PatientResultResponse?> Run(string procedure, Guid patientUid, Guid? uid,
        SavePatientResultRequest? request, MarkPatientResultReviewedRequest? review, long? user, CancellationToken token)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(token);
        await using var command = Command(connection, procedure);
        Parameter(command, "@PatientUid", SqlDbType.UniqueIdentifier, patientUid);
        if (uid.HasValue) Parameter(command, "@PatientResultUid", SqlDbType.UniqueIdentifier, uid.Value);
        if (request is not null)
        {
            Parameter(command, "@ResultType", SqlDbType.NVarChar, request.ResultType, 50);
            Parameter(command, "@ResultName", SqlDbType.NVarChar, request.ResultName, 200);
            Parameter(command, "@ResultDate", SqlDbType.DateTime2, request.ResultDate);
            Parameter(command, "@ResultSummary", SqlDbType.NVarChar, request.ResultSummary, -1);
            Parameter(command, "@ResultValue", SqlDbType.NVarChar, request.ResultValue, 500);
            Parameter(command, "@ResultUnit", SqlDbType.NVarChar, request.ResultUnit, 100);
            Parameter(command, "@ReferenceRange", SqlDbType.NVarChar, request.ReferenceRange, 200);
        }
        if (review is not null) Parameter(command, "@ReviewNote", SqlDbType.NVarChar, review.ReviewNote, 1000);
        if (request is not null || review is not null)
            Parameter(command, procedure.EndsWith("Create") ? "@CreatedBy" : procedure.EndsWith("Reviewed") ? "@ReviewedBy" : "@UpdatedBy", SqlDbType.BigInt, user);
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? Map(reader) : null;
    }

    private static SqlCommand Command(SqlConnection connection, string procedure) => new(procedure, connection) { CommandType = CommandType.StoredProcedure };
    private static void Parameter(SqlCommand command, string name, SqlDbType type, object? value, int size = 0) =>
        command.Parameters.Add(new SqlParameter(name, type, size) { Value = value ?? DBNull.Value });
    private static PatientResultResponse Map(SqlDataReader reader) => new()
    {
        PatientResultUid=reader.GetGuid(reader.GetOrdinal("PatientResultUid")),PatientUid=reader.GetGuid(reader.GetOrdinal("PatientUid")),
        ResultType=reader.GetString(reader.GetOrdinal("ResultType")),ResultName=reader.GetString(reader.GetOrdinal("ResultName")),ResultDate=reader.GetDateTime(reader.GetOrdinal("ResultDate")),
        ResultSummary=String(reader,"ResultSummary"),ResultValue=String(reader,"ResultValue"),ResultUnit=String(reader,"ResultUnit"),ReferenceRange=String(reader,"ReferenceRange"),
        ResultStatus=reader.GetString(reader.GetOrdinal("ResultStatus")),ReviewedAt=Date(reader,"ReviewedAt"),ReviewedBy=Long(reader,"ReviewedBy"),ReviewedByDisplayName=String(reader,"ReviewedByDisplayName"),ReviewNote=String(reader,"ReviewNote"),
        CreatedAt=reader.GetDateTime(reader.GetOrdinal("CreatedAt")),CreatedBy=Long(reader,"CreatedBy"),CreatedByDisplayName=String(reader,"CreatedByDisplayName"),UpdatedAt=Date(reader,"UpdatedAt"),UpdatedBy=Long(reader,"UpdatedBy"),UpdatedByDisplayName=String(reader,"UpdatedByDisplayName"),RowVersion=Convert.ToBase64String((byte[])reader["RowVersion"])
    };
    private static string? String(SqlDataReader reader,string name){var ordinal=reader.GetOrdinal(name);return reader.IsDBNull(ordinal)?null:reader.GetString(ordinal);}
    private static long? Long(SqlDataReader reader,string name){var ordinal=reader.GetOrdinal(name);return reader.IsDBNull(ordinal)?null:reader.GetInt64(ordinal);}
    private static DateTime? Date(SqlDataReader reader,string name){var ordinal=reader.GetOrdinal(name);return reader.IsDBNull(ordinal)?null:reader.GetDateTime(ordinal);}
}
