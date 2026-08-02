using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MicroEMR.Application.PlatformAdministration;

namespace MicroEMR.Infrastructure.Tenancy;

public sealed class SqlIdentityUserLookup : IIdentityUserLookup, IIdentityUserProfileLookup
{
    private readonly string? _connectionString;
    public SqlIdentityUserLookup(IConfiguration configuration) =>
        _connectionString = configuration.GetConnectionString("AuthDatabase")
            ?? configuration.GetConnectionString("AuthServerConnection");
    public bool IsAvailable => !string.IsNullOrWhiteSpace(_connectionString);

    public async Task<bool> ExistsAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        if (!IsAvailable) throw new InvalidOperationException("Identity user validation is not configured.");
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand("SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.AspNetUsers WHERE Id = @UserId) THEN 1 ELSE 0 END", connection);
        command.Parameters.AddWithValue("@UserId", userId.Trim());
        await connection.OpenAsync(cancellationToken);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    public async Task<IdentityUserProfile?> GetByIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        if (!IsAvailable) throw new InvalidOperationException("Identity user validation is not configured.");
        await using var connection = new SqlConnection(_connectionString);
        const string sql = """
            SELECT Id, UserName, FullName, Email, IsActive
            FROM dbo.AspNetUsers
            WHERE Id = @UserId;
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@UserId", System.Data.SqlDbType.NVarChar, 450).Value = userId;
        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var username = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
        var displayName = reader.IsDBNull(2) ? username : reader.GetString(2);
        return new(
            reader.GetString(0), username, displayName,
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetBoolean(4));
    }
}
