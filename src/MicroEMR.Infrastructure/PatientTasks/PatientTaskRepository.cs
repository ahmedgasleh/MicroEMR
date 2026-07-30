using System.Data;
using Microsoft.Data.SqlClient;
using MicroEMR.Infrastructure.Tenancy;
using MicroEMR.Application.PatientTasks;

namespace MicroEMR.Infrastructure.PatientTasks;

public sealed class PatientTaskRepository : IPatientTaskRepository
{
    private readonly ITenantSqlConnectionFactory _connectionFactory;
    public PatientTaskRepository(ITenantSqlConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<PatientTaskResponse>> GetByPatientUidAsync(Guid patientUid, string statusFilter, CancellationToken cancellationToken = default)
    {
        var items = new List<PatientTaskResponse>();
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = Command(connection, "dbo.PatientTask_GetByPatientUid");
        Add(command, "@PatientUid", SqlDbType.UniqueIdentifier, patientUid);
        Add(command, "@StatusFilter", SqlDbType.NVarChar, statusFilter, 50);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) items.Add(Map(reader));
        return items;
    }

    public Task<PatientTaskResponse?> GetByUidAsync(Guid patientUid, Guid patientTaskUid, CancellationToken cancellationToken = default) =>
        ExecuteAsync("dbo.PatientTask_GetByUid", patientUid, patientTaskUid, null, null, null, cancellationToken);

    public Task<PatientTaskResponse?> CreateAsync(Guid patientUid, CreatePatientTaskRequest request, long? userId, CancellationToken cancellationToken = default) =>
        ExecuteAsync("dbo.PatientTask_Create", patientUid, null, request, null, userId, cancellationToken);

    public Task<PatientTaskResponse?> UpdateAsync(Guid patientUid, Guid patientTaskUid, UpdatePatientTaskRequest request, long? userId, CancellationToken cancellationToken = default) =>
        ExecuteAsync("dbo.PatientTask_Update", patientUid, patientTaskUid, request, null, userId, cancellationToken);

    public Task<PatientTaskResponse?> CompleteAsync(Guid patientUid, Guid patientTaskUid, CompletePatientTaskRequest request, long? userId, CancellationToken cancellationToken = default) =>
        ExecuteAsync("dbo.PatientTask_Complete", patientUid, patientTaskUid, null, request, userId, cancellationToken);

    public Task<PatientTaskResponse?> ReopenAsync(Guid patientUid, Guid patientTaskUid, long? userId, CancellationToken cancellationToken = default) =>
        ExecuteAsync("dbo.PatientTask_Reopen", patientUid, patientTaskUid, null, null, userId, cancellationToken);

    public async Task<IReadOnlyList<PatientDashboardTaskResponse>> GetOpenForDashboardAsync(long? assignedTo, int maxRows, CancellationToken cancellationToken = default)
    {
        var items = new List<PatientDashboardTaskResponse>();
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = Command(connection, "dbo.PatientTask_GetOpenForDashboard");
        Add(command, "@AssignedTo", SqlDbType.BigInt, assignedTo);
        Add(command, "@MaxRows", SqlDbType.Int, maxRows);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new PatientDashboardTaskResponse
            {
                PatientTaskUid = reader.GetGuid(reader.GetOrdinal("PatientTaskUid")), PatientUid = reader.GetGuid(reader.GetOrdinal("PatientUid")),
                PatientDisplayName = reader.GetString(reader.GetOrdinal("PatientDisplayName")), ChartNumber = String(reader, "ChartNumber"),
                DateOfBirth = Date(reader, "DateOfBirth"), TaskTitle = reader.GetString(reader.GetOrdinal("TaskTitle")),
                TaskDescription = String(reader, "TaskDescription"), TaskType = reader.GetString(reader.GetOrdinal("TaskType")),
                TaskPriority = reader.GetString(reader.GetOrdinal("TaskPriority")), TaskStatus = reader.GetString(reader.GetOrdinal("TaskStatus")),
                DueAt = Date(reader, "DueAt"), CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
            });
        }
        return items;
    }

    private async Task<PatientTaskResponse?> ExecuteAsync(string procedure, Guid patientUid, Guid? taskUid, SavePatientTaskRequest? save, CompletePatientTaskRequest? complete, long? userId, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = Command(connection, procedure);
        Add(command, "@PatientUid", SqlDbType.UniqueIdentifier, patientUid);
        if (taskUid.HasValue) Add(command, "@PatientTaskUid", SqlDbType.UniqueIdentifier, taskUid.Value);
        if (save is not null)
        {
            Add(command, "@TaskTitle", SqlDbType.NVarChar, save.TaskTitle, 200); Add(command, "@TaskDescription", SqlDbType.NVarChar, save.TaskDescription, 1000);
            Add(command, "@TaskType", SqlDbType.NVarChar, save.TaskType, 50); Add(command, "@TaskPriority", SqlDbType.NVarChar, save.TaskPriority, 50);
            Add(command, "@DueAt", SqlDbType.DateTime2, save.DueAt); Add(command, "@AssignedTo", SqlDbType.BigInt, save.AssignedTo);
        }
        if (complete is not null) Add(command, "@CompletionNote", SqlDbType.NVarChar, complete.CompletionNote, 1000);
        if (procedure.EndsWith("Create", StringComparison.Ordinal)) Add(command, "@CreatedBy", SqlDbType.BigInt, userId);
        else if (procedure.EndsWith("Complete", StringComparison.Ordinal)) Add(command, "@CompletedBy", SqlDbType.BigInt, userId);
        else if (!procedure.EndsWith("GetByUid", StringComparison.Ordinal)) Add(command, "@UpdatedBy", SqlDbType.BigInt, userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    private static PatientTaskResponse Map(SqlDataReader reader) => new()
    {
        PatientTaskUid=reader.GetGuid(reader.GetOrdinal("PatientTaskUid")),PatientUid=reader.GetGuid(reader.GetOrdinal("PatientUid")),TaskTitle=reader.GetString(reader.GetOrdinal("TaskTitle")),
        TaskDescription=String(reader,"TaskDescription"),TaskType=reader.GetString(reader.GetOrdinal("TaskType")),TaskPriority=reader.GetString(reader.GetOrdinal("TaskPriority")),
        TaskStatus=reader.GetString(reader.GetOrdinal("TaskStatus")),DueAt=Date(reader,"DueAt"),AssignedTo=Long(reader,"AssignedTo"),AssignedToDisplayName=String(reader,"AssignedToDisplayName"),
        CompletedAt=Date(reader,"CompletedAt"),CompletedBy=Long(reader,"CompletedBy"),CompletedByDisplayName=String(reader,"CompletedByDisplayName"),CompletionNote=String(reader,"CompletionNote"),
        CreatedAt=reader.GetDateTime(reader.GetOrdinal("CreatedAt")),CreatedBy=Long(reader,"CreatedBy"),CreatedByDisplayName=String(reader,"CreatedByDisplayName"),
        UpdatedAt=Date(reader,"UpdatedAt"),UpdatedBy=Long(reader,"UpdatedBy"),UpdatedByDisplayName=String(reader,"UpdatedByDisplayName"),RowVersion=Convert.ToBase64String((byte[])reader["RowVersion"])
    };
    private static SqlCommand Command(SqlConnection connection, string procedure) => new(procedure, connection) { CommandType = CommandType.StoredProcedure };
    private static void Add(SqlCommand command, string name, SqlDbType type, object? value, int size = 0) => command.Parameters.Add(new SqlParameter(name, type, size) { Value = value ?? DBNull.Value });
    private static string? String(SqlDataReader reader, string name) { var i=reader.GetOrdinal(name); return reader.IsDBNull(i)?null:reader.GetString(i); }
    private static DateTime? Date(SqlDataReader reader, string name) { var i=reader.GetOrdinal(name); return reader.IsDBNull(i)?null:reader.GetDateTime(i); }
    private static long? Long(SqlDataReader reader, string name) { var i=reader.GetOrdinal(name); return reader.IsDBNull(i)?null:reader.GetInt64(i); }
}
