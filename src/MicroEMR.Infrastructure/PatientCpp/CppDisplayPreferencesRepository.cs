using System.Data;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.PatientCpp;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.PatientCpp;

public sealed class CppDisplayPreferencesRepository(ITenantSqlConnectionFactory connections) : ICppDisplayPreferencesRepository
{
    public async Task<CppDisplayPreferencesData?> GetAsync(long userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.CppDisplayPreference_Get", connection) { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("@UserId", SqlDbType.BigInt).Value = userId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task<CppDisplayPreferencesData> SaveAsync(long userId, string settingsJson, string? expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.CppDisplayPreference_Save", connection) { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("@UserId", SqlDbType.BigInt).Value = userId;
        command.Parameters.Add("@SettingsJson", SqlDbType.NVarChar, -1).Value = settingsJson;
        command.Parameters.Add("@ExpectedRowVersion", SqlDbType.Binary, 8).Value =
            expectedRowVersion is null ? DBNull.Value : Convert.FromBase64String(expectedRowVersion);
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? Map(reader)
                : throw new InvalidOperationException("CPP display preference save returned no record.");
        }
        catch (SqlException exception) when (exception.Number == 52601)
        {
            throw new CppDisplayPreferencesConcurrencyException();
        }
    }

    private static CppDisplayPreferencesData Map(SqlDataReader reader) =>
        new(reader.GetString(reader.GetOrdinal("SettingsJson")), Convert.ToBase64String((byte[])reader["RowVersion"]));
}
