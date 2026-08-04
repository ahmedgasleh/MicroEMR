using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.ClinicConfiguration;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.ClinicConfiguration;

public sealed class ClinicProfileRepository(
    ITenantSqlConnectionFactory connectionFactory,
    ILogger<ClinicProfileRepository> logger) : IClinicProfileRepository
{
    public async Task<ClinicProfileData?> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = Command(connection, "dbo.ClinicProfile_Get");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task<ClinicProfileData> SaveAsync(SaveClinicConfigurationRequest request, long actorUserId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = Command(connection, "dbo.ClinicProfile_Save");
        AddText(command, "@LegalName", 200, request.LegalName);
        AddText(command, "@Phone", 50, request.Phone);
        AddText(command, "@Fax", 50, request.Fax);
        AddText(command, "@Email", 254, request.Email);
        AddText(command, "@AddressLine1", 200, request.AddressLine1);
        AddText(command, "@AddressLine2", 200, request.AddressLine2);
        AddText(command, "@City", 100, request.City);
        AddText(command, "@ProvinceState", 100, request.ProvinceState);
        AddText(command, "@PostalCode", 30, request.PostalCode);
        AddText(command, "@Country", 100, request.Country);
        command.Parameters.Add("@DefaultAppointmentDurationMinutes", SqlDbType.Int).Value =
            (object?)request.DefaultAppointmentDurationMinutes ?? DBNull.Value;
        command.Parameters.Add("@UpdatedBy", SqlDbType.BigInt).Value = actorUserId;
        command.Parameters.Add("@ExpectedRowVersion", SqlDbType.Timestamp).Value = DecodeRowVersion(request.RowVersion);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException("Clinic profile save returned no record.");
            return Map(reader);
        }
        catch (SqlException ex) when (ex.Number == 51801)
        {
            throw new ClinicConfigurationConcurrencyException(
                "The clinic configuration was changed by another user.", ex);
        }
        catch (SqlException ex)
        {
            logger.LogError(ex, "Failed to save clinic configuration.");
            throw;
        }
    }

    private static object DecodeRowVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return DBNull.Value;
        var bytes = Convert.FromBase64String(value);
        if (bytes.Length != 8) throw new FormatException("The row version must contain exactly 8 bytes.");
        return bytes;
    }

    private static SqlCommand Command(SqlConnection connection, string name) =>
        new(name, connection) { CommandType = CommandType.StoredProcedure };

    private static void AddText(SqlCommand command, string name, int size, string? value) =>
        command.Parameters.Add(name, SqlDbType.NVarChar, size).Value =
            string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private static ClinicProfileData Map(SqlDataReader reader) => new(
        Text(reader, "LegalName"), Text(reader, "Phone"), Text(reader, "Fax"), Text(reader, "Email"),
        Text(reader, "AddressLine1"), Text(reader, "AddressLine2"), Text(reader, "City"),
        Text(reader, "ProvinceState"), Text(reader, "PostalCode"), Text(reader, "Country"),
        reader.IsDBNull(reader.GetOrdinal("DefaultAppointmentDurationMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("DefaultAppointmentDurationMinutes")),
        reader.GetDateTime(reader.GetOrdinal("UpdatedAtUtc")), reader.GetInt64(reader.GetOrdinal("UpdatedBy")),
        Convert.ToBase64String((byte[])reader["RowVersion"]));

    private static string? Text(SqlDataReader reader, string name) =>
        reader.IsDBNull(reader.GetOrdinal(name)) ? null : reader.GetString(reader.GetOrdinal(name));
}
