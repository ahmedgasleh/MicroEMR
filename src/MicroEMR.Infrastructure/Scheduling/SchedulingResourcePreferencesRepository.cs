using System.Data;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.Scheduling.Preferences;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.Scheduling;

public sealed class SchedulingResourcePreferencesRepository(ITenantSqlConnectionFactory connections)
    : ISchedulingResourcePreferencesRepository
{
    public async Task<SchedulingResourcePreferenceData?> GetAsync(long userId, CancellationToken token = default)
    {
        await using var connection = await connections.OpenConnectionAsync(token);
        await using var command = new SqlCommand("dbo.SchedulingResourcePreference_Get", connection) { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("@UserId", SqlDbType.BigInt).Value = userId;
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? Map(reader) : null;
    }

    public async Task<SchedulingResourcePreferenceData> SaveAsync(long userId, string resourceUidsJson,
        string? expectedRowVersion, CancellationToken token = default)
    {
        await using var connection = await connections.OpenConnectionAsync(token);
        await using var command = new SqlCommand("dbo.SchedulingResourcePreference_Save", connection) { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("@UserId", SqlDbType.BigInt).Value = userId;
        command.Parameters.Add("@ResourceUidsJson", SqlDbType.NVarChar, -1).Value = resourceUidsJson;
        command.Parameters.Add("@ExpectedRowVersion", SqlDbType.Binary, 8).Value =
            expectedRowVersion is null ? DBNull.Value : Convert.FromBase64String(expectedRowVersion);
        try
        {
            await using var reader = await command.ExecuteReaderAsync(token);
            return await reader.ReadAsync(token) ? Map(reader)
                : throw new InvalidOperationException("Resource preference save returned no record.");
        }
        catch (SqlException error) when (error.Number == 53901) { throw new SchedulingResourcePreferencesConcurrencyException(); }
        catch (SqlException error) when (error.Number == 53902) { throw new ClinicalUserResolutionException("An active clinical user is required."); }
        catch (SqlException error) when (error.Number == 53903) { throw new ArgumentException("Choose between 1 and 200 available scheduling resources in the current tenant."); }
    }

    private static SchedulingResourcePreferenceData Map(SqlDataReader reader) =>
        new(reader.GetString(reader.GetOrdinal("ResourceUidsJson")), Convert.ToBase64String((byte[])reader["RowVersion"]));
}
