using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MicroEMR.Api.HealthChecks;

internal sealed class PlatformDatabaseHealthCheck : IHealthCheck
{
    private readonly string _connectionString;

    public PlatformDatabaseHealthCheck(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("PlatformDatabase")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:PlatformDatabase is required.");
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand("SELECT 1;", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return HealthCheckResult.Unhealthy();
        }
    }
}
