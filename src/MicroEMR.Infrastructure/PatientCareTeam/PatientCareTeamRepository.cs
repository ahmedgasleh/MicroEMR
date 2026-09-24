using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.PatientCareTeam;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.PatientCareTeam;

public sealed class PatientCareTeamRepository(
    ITenantSqlConnectionFactory connections,
    ILogger<PatientCareTeamRepository> logger) : IPatientCareTeamRepository
{
    public async Task<IReadOnlyList<PatientCareTeamRelationship>> ListAsync(
        Guid patientUid, CancellationToken cancellationToken = default)
    {
        var items = new List<PatientCareTeamRelationship>();
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        await using var command = Command(connection, "dbo.PatientProviderRelationship_List");
        GuidParameter(command, "@PatientUid", patientUid);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) items.Add(Map(reader));
        return items;
    }

    public async Task<IReadOnlyList<CareTeamRelationshipType>> ListActiveTypesAsync(
        CancellationToken cancellationToken = default)
    {
        var items = new List<CareTeamRelationshipType>();
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        await using var command = Command(connection, "dbo.PatientProviderRelationshipType_ListActive");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            items.Add(new(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3)));
        return items;
    }

    public Task<PatientCareTeamRelationship> AddAsync(Guid patientUid,
        AddPatientCareTeamRelationshipRequest request, long actorUserId, CancellationToken cancellationToken = default) =>
        WriteAsync("dbo.PatientProviderRelationship_Create", patientUid, null, null, actorUserId,
            command =>
            {
                GuidParameter(command, "@ProviderUid", request.ProviderUid);
                command.Parameters.Add("@RelationshipTypeCode", SqlDbType.NVarChar, 30).Value = request.RelationshipTypeCode;
                command.Parameters.Add("@IsPrimary", SqlDbType.Bit).Value = request.IsPrimary;
                command.Parameters.Add("@StartDate", SqlDbType.Date).Value = request.StartDate.ToDateTime(TimeOnly.MinValue);
            }, cancellationToken);

    public Task<PatientCareTeamRelationship> UpdateAsync(Guid patientUid, Guid relationshipUid,
        UpdatePatientCareTeamRelationshipRequest request, long actorUserId, CancellationToken cancellationToken = default) =>
        WriteAsync("dbo.PatientProviderRelationship_Update", patientUid, relationshipUid, request.RowVersion, actorUserId,
            command =>
            {
                command.Parameters.Add("@IsPrimary", SqlDbType.Bit).Value = request.IsPrimary;
                command.Parameters.Add("@StartDate", SqlDbType.Date).Value = request.StartDate.ToDateTime(TimeOnly.MinValue);
            }, cancellationToken);

    public Task<PatientCareTeamRelationship> EndAsync(Guid patientUid, Guid relationshipUid,
        EndPatientCareTeamRelationshipRequest request, long actorUserId, CancellationToken cancellationToken = default) =>
        WriteAsync("dbo.PatientProviderRelationship_End", patientUid, relationshipUid, request.RowVersion, actorUserId,
            command => command.Parameters.Add("@EndDate", SqlDbType.Date).Value = request.EndDate.ToDateTime(TimeOnly.MinValue),
            cancellationToken);

    private async Task<PatientCareTeamRelationship> WriteAsync(string procedure, Guid patientUid,
        Guid? relationshipUid, string? rowVersion, long actorUserId, Action<SqlCommand> parameters,
        CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        await using var command = Command(connection, procedure);
        GuidParameter(command, "@PatientUid", patientUid);
        if (relationshipUid.HasValue) GuidParameter(command, "@RelationshipUid", relationshipUid.Value);
        if (rowVersion is not null)
            command.Parameters.Add("@ExpectedRowVersion", SqlDbType.Binary, 8).Value = Convert.FromBase64String(rowVersion);
        command.Parameters.Add("@ActorUserId", SqlDbType.BigInt).Value = actorUserId;
        parameters(command);
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken)) return Map(reader);
            throw new InvalidOperationException("Care-team procedure returned no relationship.");
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Care-team write failed for patient {PatientUid} in {Procedure}.", patientUid, procedure);
            throw;
        }
    }

    private static SqlCommand Command(SqlConnection connection, string name) =>
        new(name, connection) { CommandType = CommandType.StoredProcedure };

    private static void GuidParameter(SqlCommand command, string name, Guid value) =>
        command.Parameters.Add(name, SqlDbType.UniqueIdentifier).Value = value;

    private static PatientCareTeamRelationship Map(SqlDataReader reader) => new(
        reader.GetGuid(reader.GetOrdinal("RelationshipUid")),
        reader.GetGuid(reader.GetOrdinal("PatientUid")),
        reader.GetGuid(reader.GetOrdinal("ProviderUid")),
        reader.GetString(reader.GetOrdinal("ProviderDisplayName")),
        reader.GetString(reader.GetOrdinal("RelationshipTypeCode")),
        reader.GetString(reader.GetOrdinal("RelationshipTypeDisplayName")),
        reader.GetBoolean(reader.GetOrdinal("IsPrimary")),
        DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("StartDate"))),
        reader.IsDBNull(reader.GetOrdinal("EndDate")) ? null : DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("EndDate"))),
        reader.GetBoolean(reader.GetOrdinal("IsActive")),
        reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
        reader.GetInt64(reader.GetOrdinal("CreatedBy")),
        reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
        reader.IsDBNull(reader.GetOrdinal("UpdatedBy")) ? null : reader.GetInt64(reader.GetOrdinal("UpdatedBy")),
        Convert.ToBase64String((byte[])reader["RowVersion"]));
}
