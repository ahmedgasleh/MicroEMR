using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MicroEMR.Application.PlatformAdministration;

namespace MicroEMR.Infrastructure.Tenancy;

public sealed class SqlIdentityUserLookup : IIdentityUserLookup
{
    private readonly string? _connectionString;
    public SqlIdentityUserLookup(IConfiguration configuration) =>
        _connectionString = configuration.GetConnectionString("AuthDatabase");
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
}
