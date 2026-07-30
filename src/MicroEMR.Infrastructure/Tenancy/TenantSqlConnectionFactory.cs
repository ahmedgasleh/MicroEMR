using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.Tenancy;

namespace MicroEMR.Infrastructure.Tenancy;

public sealed class TenantSqlConnectionFactory : ITenantSqlConnectionFactory
{
    private readonly ITenantContext _tenantContext;
    private readonly ITenantDatabaseResolver _databaseResolver;
    private readonly ITenantDatabaseSecretProvider _secretProvider;
    private readonly ILogger<TenantSqlConnectionFactory> _logger;

    public TenantSqlConnectionFactory(
        ITenantContext tenantContext,
        ITenantDatabaseResolver databaseResolver,
        ITenantDatabaseSecretProvider secretProvider,
        ILogger<TenantSqlConnectionFactory> logger)
    {
        _tenantContext = tenantContext;
        _databaseResolver = databaseResolver;
        _secretProvider = secretProvider;
        _logger = logger;
    }

    public async Task<SqlConnection> OpenConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantUid = _tenantContext.TenantUid;
        if (tenantUid == Guid.Empty)
        {
            throw new TenantDatabaseConnectionException(
                "A tenant clinical database connection cannot be opened because no tenant context is available.");
        }

        var assignment = await _databaseResolver.ResolveAsync(
            tenantUid,
            cancellationToken);

        ValidateAssignment(assignment, tenantUid);
        var validAssignment = assignment!;

        _logger.LogInformation(
            "Tenant database metadata resolved. TenantUid: {TenantUid}; DatabaseServerKey: {DatabaseServerKey}; DatabaseName: {DatabaseName}; DatabaseStatus: {DatabaseStatus}",
            tenantUid,
            validAssignment.DatabaseServerKey,
            validAssignment.DatabaseName,
            validAssignment.DatabaseStatus);

        var secret = await _secretProvider.ResolveAsync(
            validAssignment.SecretReference,
            cancellationToken);
        var builder = ValidateConnectionString(
            secret.ConnectionString,
            validAssignment.DatabaseName);

        var connection = new SqlConnection(builder.ConnectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
            _logger.LogDebug(
                "Tenant database connection opened. TenantUid: {TenantUid}; DatabaseServerKey: {DatabaseServerKey}; DatabaseName: {DatabaseName}",
                tenantUid,
                validAssignment.DatabaseServerKey,
                validAssignment.DatabaseName);
            return connection;
        }
        catch (Exception exception)
        {
            await connection.DisposeAsync();
            _logger.LogError(
                exception,
                "Tenant database connection failed. TenantUid: {TenantUid}; DatabaseServerKey: {DatabaseServerKey}; DatabaseName: {DatabaseName}",
                tenantUid,
                validAssignment.DatabaseServerKey,
                validAssignment.DatabaseName);
            throw new TenantDatabaseConnectionException(
                "The tenant clinical database connection could not be opened.",
                exception);
        }
    }

    internal static void ValidateAssignment(
        TenantDatabaseInfo? assignment,
        Guid tenantUid)
    {
        if (assignment is null)
            throw new TenantDatabaseConnectionException("No tenant database assignment is available.");
        if (assignment.TenantUid != tenantUid)
            throw new TenantDatabaseConnectionException("The tenant database assignment does not match the current tenant.");
        if (!string.Equals(assignment.DatabaseStatus, "Active", StringComparison.Ordinal))
            throw new TenantDatabaseConnectionException("The tenant database assignment is not active.");
        if (string.IsNullOrWhiteSpace(assignment.DatabaseServerKey) ||
            string.IsNullOrWhiteSpace(assignment.DatabaseName) ||
            string.IsNullOrWhiteSpace(assignment.SecretReference))
            throw new TenantDatabaseConnectionException("The tenant database assignment is incomplete.");
    }

    internal static SqlConnectionStringBuilder ValidateConnectionString(
        string connectionString,
        string assignedDatabaseName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new TenantDatabaseConnectionException("The resolved tenant database secret is blank.");

        SqlConnectionStringBuilder builder;
        try
        {
            builder = new SqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException exception)
        {
            throw new TenantDatabaseConnectionException(
                "The resolved tenant database secret is not a valid SQL Server connection string.",
                exception);
        }

        if (string.IsNullOrWhiteSpace(builder.DataSource) ||
            string.IsNullOrWhiteSpace(builder.InitialCatalog))
            throw new TenantDatabaseConnectionException("The tenant database connection configuration is incomplete.");
        if (!string.IsNullOrWhiteSpace(builder.AttachDBFilename))
            throw new TenantDatabaseConnectionException("AttachDbFilename is not supported for tenant databases.");
        if (!string.Equals(
                builder.InitialCatalog,
                assignedDatabaseName,
                StringComparison.OrdinalIgnoreCase))
            throw new TenantDatabaseConnectionException(
                "The resolved database does not match the tenant database assignment.");

        return builder;
    }
}
