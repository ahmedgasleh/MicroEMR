using System.Data;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.ClinicConfiguration;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.ClinicConfiguration;

public sealed class ClinicLogoRepository(ITenantSqlConnectionFactory read, IAdministrativeTenantSqlConnectionFactory write)
    : IClinicLogoRepository
{
    public async Task<ClinicLogoRecord?> GetAsync(CancellationToken token = default)
    {
        await using var connection = await read.OpenConnectionAsync(token);
        await using var command = new SqlCommand("dbo.ClinicBrandingLogo_Get", connection) { CommandType = CommandType.StoredProcedure };
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? Map(reader) : null;
    }

    public async Task<ClinicLogoRecord> SaveAsync(ClinicLogo? logo, string? rowVersion, long actor, CancellationToken token = default)
    {
        await using var connection = await write.OpenConnectionAsync(token);
        await using var command = new SqlCommand("dbo.ClinicBrandingLogo_Save", connection) { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("@PngBytes", SqlDbType.VarBinary, -1).Value = (object?)logo?.PngBytes ?? DBNull.Value;
        command.Parameters.Add("@Width", SqlDbType.Int).Value = (object?)logo?.Width ?? DBNull.Value;
        command.Parameters.Add("@Height", SqlDbType.Int).Value = (object?)logo?.Height ?? DBNull.Value;
        command.Parameters.Add("@UpdatedBy", SqlDbType.BigInt).Value = actor;
        command.Parameters.Add("@ExpectedRowVersion", SqlDbType.Binary, 8).Value = rowVersion is null ? DBNull.Value : Convert.FromBase64String(rowVersion);
        try
        {
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new InvalidOperationException("Branding save returned no record.");
            return Map(reader);
        }
        catch (SqlException ex) when (ex.Number == 51811)
        { throw new ClinicConfigurationConcurrencyException("Clinic branding has changed.", ex); }
    }

    private static ClinicLogoRecord Map(SqlDataReader reader) => new(
        reader.IsDBNull(0) ? null : new((byte[])reader[0], reader.GetInt32(1), reader.GetInt32(2)),
        Convert.ToBase64String((byte[])reader[3]));
}
