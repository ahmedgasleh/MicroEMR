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

    public async Task<IReadOnlyList<UnreviewedPatientResultResponse>> ListUnreviewed(CancellationToken token = default)
    {
        var results = new List<UnreviewedPatientResultResponse>();
        await using var connection = await connectionFactory.OpenConnectionAsync(token);
        await using var command = Command(connection, "dbo.PatientResult_GetUnreviewed");
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            results.Add(new UnreviewedPatientResultResponse
            {
                PatientResultUid = reader.GetGuid(reader.GetOrdinal("PatientResultUid")),
                PatientUid = reader.GetGuid(reader.GetOrdinal("PatientUid")),
                PatientDisplayName = reader.GetString(reader.GetOrdinal("PatientDisplayName")),
                ChartNumber = reader.GetString(reader.GetOrdinal("ChartNumber")),
                ResultType = reader.GetString(reader.GetOrdinal("ResultType")),
                ResultName = reader.GetString(reader.GetOrdinal("ResultName")),
                ResultDate = reader.GetDateTime(reader.GetOrdinal("ResultDate")),
                ResultSummary = String(reader, "ResultSummary"),
                ResultValue = String(reader, "ResultValue"),
                ResultUnit = String(reader, "ResultUnit"),
                ReferenceRange = String(reader, "ReferenceRange"),
                ResultStatus = reader.GetString(reader.GetOrdinal("ResultStatus")),
                SourceType = reader.GetString(reader.GetOrdinal("SourceType")),
                SourceOrganization = String(reader,"SourceOrganization"), SourceSystem = String(reader,"SourceSystem"),
                Abnormality = reader.GetString(reader.GetOrdinal("Abnormality")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                RowVersion = Convert.ToBase64String((byte[])reader["RowVersion"])
            });
        }
        return results;
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
    public async Task<IReadOnlyList<PatientResultResponse>> History(Guid patientUid,Guid uid,CancellationToken token=default){var result=new List<PatientResultResponse>();await using var connection=await connectionFactory.OpenConnectionAsync(token);await using var command=Command(connection,"dbo.PatientResult_GetHistory");Parameter(command,"@PatientUid",SqlDbType.UniqueIdentifier,patientUid);Parameter(command,"@PatientResultUid",SqlDbType.UniqueIdentifier,uid);await using var reader=await command.ExecuteReaderAsync(token);while(await reader.ReadAsync(token))result.Add(Map(reader));return result;}
    public Task<PatientResultResponse?> Create(Guid patientUid, CreatePatientResultRequest request, long user, CancellationToken token = default) =>
        Run("dbo.PatientResult_Create", patientUid, null, request, null, user, token);
    public Task<PatientResultResponse?> Update(Guid patientUid, Guid uid, UpdatePatientResultRequest request, long user, CancellationToken token = default) =>
        Run("dbo.PatientResult_Update", patientUid, uid, request, null, user, token);
    public Task<PatientResultResponse?> Correct(Guid patientUid,Guid uid,CorrectPatientResultRequest request,long user,CancellationToken token=default)=>Run("dbo.PatientResult_Correct",patientUid,uid,request,null,user,token);
    public Task<PatientResultResponse?> MarkEnteredInError(Guid patientUid,Guid uid,MarkPatientResultEnteredInErrorRequest request,long user,CancellationToken token=default)=>Run("dbo.PatientResult_MarkEnteredInError",patientUid,uid,null,null,user,token,request);
    public Task<PatientResultResponse?> Review(Guid patientUid, Guid uid, MarkPatientResultReviewedRequest request, long user, CancellationToken token = default) =>
        Run("dbo.PatientResult_MarkReviewed", patientUid, uid, null, request, user, token);

    private async Task<PatientResultResponse?> Run(string procedure, Guid patientUid, Guid? uid,
        SavePatientResultRequest? request, MarkPatientResultReviewedRequest? review, long? user, CancellationToken token,MarkPatientResultEnteredInErrorRequest? error=null)
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
            Parameter(command,"@SourceType",SqlDbType.NVarChar,request.SourceType,20);Parameter(command,"@SourceOrganization",SqlDbType.NVarChar,request.SourceOrganization,200);Parameter(command,"@SourceSystem",SqlDbType.NVarChar,request.SourceSystem,200);Parameter(command,"@ExternalResultId",SqlDbType.NVarChar,request.ExternalResultId,200);Parameter(command,"@ReceivedAtUtc",SqlDbType.DateTime2,request.ReceivedAtUtc);Parameter(command,"@Abnormality",SqlDbType.NVarChar,request.Abnormality,20);
            if(request is UpdatePatientResultRequest update)Parameter(command,"@ExpectedRowVersion",SqlDbType.Binary,Convert.FromBase64String(update.ExpectedRowVersion),8);
        }
        if (review is not null)
        {
            Parameter(command, "@ReviewNote", SqlDbType.NVarChar, review.ReviewNote, 1000);
            Parameter(command, "@ExpectedRowVersion", SqlDbType.Binary, Convert.FromBase64String(review.ExpectedRowVersion), 8);
        }
        if(error is not null){Parameter(command,"@ExpectedRowVersion",SqlDbType.Binary,Convert.FromBase64String(error.ExpectedRowVersion),8);Parameter(command,"@Reason",SqlDbType.NVarChar,error.Reason,500);Parameter(command,"@Actor",SqlDbType.BigInt,user);}
        if (request is not null || review is not null)
            Parameter(command, procedure.EndsWith("Create") ? "@CreatedBy" : procedure.EndsWith("Reviewed") ? "@ReviewedBy" : procedure.EndsWith("Correct")?"@CorrectedBy":"@UpdatedBy", SqlDbType.BigInt, user);
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
        ResultStatus=reader.GetString(reader.GetOrdinal("ResultStatus")),LifecycleStatus=reader.GetString(reader.GetOrdinal("LifecycleStatus")),SourceType=reader.GetString(reader.GetOrdinal("SourceType")),SourceOrganization=String(reader,"SourceOrganization"),SourceSystem=String(reader,"SourceSystem"),ExternalResultId=String(reader,"ExternalResultId"),ReceivedAtUtc=Date(reader,"ReceivedAtUtc"),Abnormality=reader.GetString(reader.GetOrdinal("Abnormality")),PreviousResultUid=GuidValue(reader,"PreviousResultUid"),EnteredInErrorAtUtc=Date(reader,"EnteredInErrorAtUtc"),EnteredInErrorBy=Long(reader,"EnteredInErrorBy"),EnteredInErrorByDisplayName=String(reader,"EnteredInErrorByDisplayName"),EnteredInErrorReason=String(reader,"EnteredInErrorReason"),ReviewedAt=Date(reader,"ReviewedAt"),ReviewedBy=Long(reader,"ReviewedBy"),ReviewedByDisplayName=String(reader,"ReviewedByDisplayName"),ReviewNote=String(reader,"ReviewNote"),
        CreatedAt=reader.GetDateTime(reader.GetOrdinal("CreatedAt")),CreatedBy=Long(reader,"CreatedBy"),CreatedByDisplayName=String(reader,"CreatedByDisplayName"),UpdatedAt=Date(reader,"UpdatedAt"),UpdatedBy=Long(reader,"UpdatedBy"),UpdatedByDisplayName=String(reader,"UpdatedByDisplayName"),RowVersion=Convert.ToBase64String((byte[])reader["RowVersion"]),ReviewWasApplied=Boolean(reader,"ReviewWasApplied")
    };
    private static string? String(SqlDataReader reader,string name){var ordinal=reader.GetOrdinal(name);return reader.IsDBNull(ordinal)?null:reader.GetString(ordinal);}
    private static long? Long(SqlDataReader reader,string name){var ordinal=reader.GetOrdinal(name);return reader.IsDBNull(ordinal)?null:reader.GetInt64(ordinal);}
    private static DateTime? Date(SqlDataReader reader,string name){var ordinal=reader.GetOrdinal(name);return reader.IsDBNull(ordinal)?null:reader.GetDateTime(ordinal);}
    private static Guid? GuidValue(SqlDataReader reader,string name){var ordinal=reader.GetOrdinal(name);return reader.IsDBNull(ordinal)?null:reader.GetGuid(ordinal);}
    private static bool Boolean(SqlDataReader reader,string name){for(var ordinal=0;ordinal<reader.FieldCount;ordinal++)if(string.Equals(reader.GetName(ordinal),name,StringComparison.OrdinalIgnoreCase))return !reader.IsDBNull(ordinal)&&reader.GetBoolean(ordinal);return false;}
}
