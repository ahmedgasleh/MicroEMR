using System.Data;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.Scheduling;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.Scheduling;

public sealed class SchedulingResourceAdministrationRepository(ITenantSqlConnectionFactory connections) : ISchedulingResourceAdministrationRepository
{
    public async Task<IReadOnlyList<SchedulingResourceItem>> ListAsync(string status, CancellationToken token = default)
    {
        await using var connection = await connections.OpenConnectionAsync(token);
        await using var command = Command(connection, "dbo.ScheduleResource_AdminList");
        command.Parameters.Add("@Status", SqlDbType.NVarChar, 10).Value = status;
        await using var reader = await command.ExecuteReaderAsync(token);
        var items = new List<SchedulingResourceItem>();
        while (await reader.ReadAsync(token)) items.Add(Map(reader));
        return items;
    }
    public async Task<SchedulingResourceItem?> GetAsync(Guid uid, CancellationToken token = default)
    {
        await using var connection = await connections.OpenConnectionAsync(token);
        await using var command = Command(connection, "dbo.ScheduleResource_AdminList");
        command.Parameters.Add("@ResourceUid", SqlDbType.UniqueIdentifier).Value = uid;
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? Map(reader) : null;
    }
    public async Task<IReadOnlyList<SchedulingProviderChoice>> ProvidersAsync(CancellationToken token = default)
    {
        await using var connection = await connections.OpenConnectionAsync(token);
        await using var command = Command(connection, "dbo.ScheduleResource_ProviderChoices");
        await using var reader = await command.ExecuteReaderAsync(token);
        var choices = new List<SchedulingProviderChoice>();
        while (await reader.ReadAsync(token)) choices.Add(new(reader.GetGuid(0), reader.GetString(1)));
        return choices;
    }
    public Task<SchedulingResourceItem?> SaveAsync(Guid? uid, SaveSchedulingResourceRequest request, long actor, CancellationToken token = default) =>
        Mutate("dbo.ScheduleResource_AdminSave", uid, request.RowVersion, actor, command =>
        {
            command.Parameters.Add("@ResourceType", SqlDbType.NVarChar, 50).Value = request.ResourceType;
            command.Parameters.Add("@DisplayName", SqlDbType.NVarChar, 200).Value = request.DisplayName;
            command.Parameters.Add("@ColorCode", SqlDbType.NVarChar, 20).Value = (object?)request.ColorCode ?? DBNull.Value;
            command.Parameters.Add("@SortOrder", SqlDbType.Int).Value = request.SortOrder;
            command.Parameters.Add("@ProviderUid", SqlDbType.UniqueIdentifier).Value = (object?)request.ProviderUid ?? DBNull.Value;
        }, token);
    public Task<SchedulingResourceItem?> SetActiveAsync(Guid uid, bool active, string version, long actor, CancellationToken token = default) =>
        Mutate("dbo.ScheduleResource_AdminSetActive", uid, version, actor, command => command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = active, token);
    private async Task<SchedulingResourceItem?> Mutate(string procedure, Guid? uid, string? version, long actor, Action<SqlCommand> parameters, CancellationToken token)
    {
        await using var connection = await connections.OpenConnectionAsync(token);
        await using var command = Command(connection, procedure);
        command.Parameters.Add("@ResourceUid", SqlDbType.UniqueIdentifier).Value = (object?)uid ?? DBNull.Value;
        command.Parameters.Add("@ActorUserId", SqlDbType.BigInt).Value = actor;
        command.Parameters.Add("@ExpectedRowVersion", SqlDbType.Binary, 8).Value = version is null ? DBNull.Value : Convert.FromBase64String(version);
        parameters(command);
        try
        {
            await using var reader = await command.ExecuteReaderAsync(token);
            return await reader.ReadAsync(token) ? Map(reader) : null;
        }
        catch (SqlException exception) when (exception.Number == 51800) { throw new ArgumentException(exception.Message); }
        catch (SqlException exception) when (exception.Number is 51801 or 51803) { throw new SchedulingResourceConflictException(exception.Message); }
        catch (SqlException exception) when (exception.Number is 2601 or 2627) { throw new SchedulingResourceConflictException("This Provider already has an active scheduling resource."); }
        catch (SqlException exception) when (exception.Number == 51802) { throw new UnauthorizedAccessException("The clinical actor or tenant identity is unavailable."); }
    }
    private static SqlCommand Command(SqlConnection connection, string procedure) => new(procedure, connection) { CommandType = CommandType.StoredProcedure };
    private static SchedulingResourceItem Map(SqlDataReader reader) => new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetInt32(4), reader.GetBoolean(5),
        reader.IsDBNull(6) ? null : reader.GetGuid(6), reader.IsDBNull(7) ? null : reader.GetString(7), Convert.ToBase64String((byte[])reader[8]));
}
