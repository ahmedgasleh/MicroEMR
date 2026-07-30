using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.Provisioning;

public sealed class TenantDatabaseMigrationRunner
    : ITenantDatabaseMigrationRunner
{
    private readonly ITenantDatabaseMigrationSource _migrationSource;
    private readonly ITenantDatabaseSecretProvider _secretProvider;
    private readonly ITenantProvisioningStatusRepository _statusRepository;
    private readonly ILogger<TenantDatabaseMigrationRunner> _logger;

    public TenantDatabaseMigrationRunner(
        ITenantDatabaseMigrationSource migrationSource,
        ITenantDatabaseSecretProvider secretProvider,
        ITenantProvisioningStatusRepository statusRepository,
        ILogger<TenantDatabaseMigrationRunner> logger)
    {
        _migrationSource = migrationSource;
        _secretProvider = secretProvider;
        _statusRepository = statusRepository;
        _logger = logger;
    }

    public async Task<TenantDatabaseProvisioningResult> ProvisionAsync(
        TenantDatabaseProvisioningRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var stopwatch = Stopwatch.StartNew();
        await _statusRepository.MarkStartedAsync(request.TenantUid, cancellationToken);

        try
        {
            var migrations = await _migrationSource.GetAvailableMigrationsAsync(cancellationToken);
            if (migrations.Count == 0)
                throw new TenantDatabaseConnectionException("No tenant database migrations are available.");

            var secret = await _secretProvider.ResolveAsync(
                request.SecretReference,
                cancellationToken);
            var builder = TenantSqlConnectionFactory.ValidateConnectionString(
                secret.ConnectionString,
                request.DatabaseName);

            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            var lockResource = $"MicroEMR:TenantProvisioning:{request.DatabaseName}";
            await AcquireLockAsync(connection, lockResource, cancellationToken);
            try
            {
                await EnsureMetadataAsync(connection, migrations[0], cancellationToken);
                await ValidateOrCreateIdentityAsync(connection, request, cancellationToken);

                var applied = await ReadAppliedMigrationsAsync(connection, cancellationToken);
                ValidateAppliedHashes(applied, migrations);
                var initiallyAppliedCount = applied.Count;
                var newlyApplied = new List<string>();

                foreach (var migration in migrations)
                {
                    if (applied.ContainsKey(migration.MigrationId))
                        continue;

                    await ApplyMigrationAsync(connection, migration, cancellationToken);
                    newlyApplied.Add(migration.MigrationId);
                    _logger.LogInformation(
                        "Tenant database migration applied. TenantUid: {TenantUid}; TenantKey: {TenantKey}; DatabaseServerKey: {DatabaseServerKey}; DatabaseName: {DatabaseName}; MigrationId: {MigrationId}; SchemaVersion: {SchemaVersion}",
                        request.TenantUid,
                        request.TenantKey,
                        request.DatabaseServerKey,
                        request.DatabaseName,
                        migration.MigrationId,
                        migration.SchemaVersion);
                }

                await VerifyAsync(connection, request.TenantUid, migrations[^1], cancellationToken);
                var currentVersion = migrations[^1].SchemaVersion;
                await _statusRepository.MarkCompletedAsync(
                    request.TenantUid,
                    currentVersion,
                    cancellationToken);

                var status = newlyApplied.Count == 0
                    ? TenantDatabaseProvisioningStatus.AlreadyCurrent
                    : initiallyAppliedCount == 0
                        ? TenantDatabaseProvisioningStatus.Provisioned
                        : TenantDatabaseProvisioningStatus.Migrated;

                _logger.LogInformation(
                    "Tenant database provisioning completed. TenantUid: {TenantUid}; TenantKey: {TenantKey}; DatabaseServerKey: {DatabaseServerKey}; DatabaseName: {DatabaseName}; SchemaVersion: {SchemaVersion}; ProvisioningStatus: {ProvisioningStatus}; DurationMs: {DurationMs}",
                    request.TenantUid,
                    request.TenantKey,
                    request.DatabaseServerKey,
                    request.DatabaseName,
                    currentVersion,
                    status,
                    stopwatch.ElapsedMilliseconds);

                return new TenantDatabaseProvisioningResult(status, currentVersion, newlyApplied);
            }
            finally
            {
                await ReleaseLockAsync(connection, lockResource, CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Tenant database provisioning failed. TenantUid: {TenantUid}; TenantKey: {TenantKey}; DatabaseServerKey: {DatabaseServerKey}; DatabaseName: {DatabaseName}; ProvisioningStatus: Failed; DurationMs: {DurationMs}",
                request.TenantUid,
                request.TenantKey,
                request.DatabaseServerKey,
                request.DatabaseName,
                stopwatch.ElapsedMilliseconds);
            try
            {
                await _statusRepository.MarkFailedAsync(request.TenantUid, CancellationToken.None);
            }
            catch (Exception statusException)
            {
                _logger.LogError(
                    statusException,
                    "Platform provisioning failure status could not be recorded. TenantUid: {TenantUid}",
                    request.TenantUid);
            }
            throw;
        }
    }

    private static void ValidateRequest(TenantDatabaseProvisioningRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TenantUid == Guid.Empty)
            throw new ArgumentException("Tenant UID must not be empty.", nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TenantKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DatabaseServerKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DatabaseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SecretReference);
    }

    private static async Task AcquireLockAsync(
        SqlConnection connection,
        string resource,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource, 'Exclusive', 'Session', 30000; SELECT @result;",
            connection);
        command.Parameters.Add("@Resource", SqlDbType.NVarChar, 255).Value = resource;
        var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        if (result < 0)
            throw new TenantDatabaseConnectionException("The tenant database provisioning lock could not be acquired.");
    }

    private static async Task ReleaseLockAsync(
        SqlConnection connection,
        string resource,
        CancellationToken cancellationToken)
    {
        if (connection.State != ConnectionState.Open)
            return;
        await using var command = new SqlCommand(
            "EXEC sys.sp_releaseapplock @Resource, 'Session';",
            connection);
        command.Parameters.Add("@Resource", SqlDbType.NVarChar, 255).Value = resource;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureMetadataAsync(
        SqlConnection connection,
        TenantDatabaseMigration metadataMigration,
        CancellationToken cancellationToken)
    {
        foreach (var batch in SqlBatchParser.Parse(metadataMigration.Script))
        {
            await using var command = new SqlCommand(batch, connection) { CommandTimeout = 120 };
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task ValidateOrCreateIdentityAsync(
        SqlConnection connection,
        TenantDatabaseProvisioningRequest request,
        CancellationToken cancellationToken)
    {
        await using var read = new SqlCommand(
            "SELECT TenantUid FROM dbo.TenantDatabaseIdentity;",
            connection);
        var identities = new List<Guid>();
        await using (var reader = await read.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                identities.Add(reader.GetGuid(0));
        }

        ValidateIdentity(identities, request.TenantUid);

        if (identities.Count == 0)
        {
            await using var insert = new SqlCommand(
                "INSERT dbo.TenantDatabaseIdentity (TenantUid, TenantKey) VALUES (@TenantUid, @TenantKey);",
                connection);
            insert.Parameters.Add("@TenantUid", SqlDbType.UniqueIdentifier).Value = request.TenantUid;
            insert.Parameters.Add("@TenantKey", SqlDbType.NVarChar, 50).Value = request.TenantKey.Trim();
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    internal static void ValidateIdentity(
        IReadOnlyCollection<Guid> identities,
        Guid requestedTenantUid)
    {
        if (requestedTenantUid == Guid.Empty)
            throw new ArgumentException("Tenant UID must not be empty.", nameof(requestedTenantUid));
        if (identities.Count > 1 ||
            identities.Count == 1 && identities.Single() != requestedTenantUid)
            throw new TenantDatabaseConnectionException(
                "The clinical database is already assigned to a different tenant.");
    }

    private static async Task<Dictionary<string, string>> ReadAppliedMigrationsAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "SELECT MigrationId, ScriptHash FROM dbo.SchemaMigration;",
            connection);
        var applied = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            applied.Add(reader.GetString(0), reader.GetString(1));
        return applied;
    }

    internal static void ValidateAppliedHashes(
        IReadOnlyDictionary<string, string> applied,
        IReadOnlyCollection<TenantDatabaseMigration> available)
    {
        var availableById = available.ToDictionary(item => item.MigrationId, StringComparer.Ordinal);
        foreach (var item in applied)
        {
            if (!availableById.TryGetValue(item.Key, out var migration) ||
                !string.Equals(item.Value, migration.ScriptHash, StringComparison.OrdinalIgnoreCase))
                throw new TenantDatabaseConnectionException(
                    $"Applied tenant migration '{item.Key}' does not match the controlled migration asset.");
        }
    }

    private static async Task ApplyMigrationAsync(
        SqlConnection connection,
        TenantDatabaseMigration migration,
        CancellationToken cancellationToken)
    {
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var batch in SqlBatchParser.Parse(migration.Script))
            {
                await using var command = new SqlCommand(batch, connection, transaction)
                {
                    CommandTimeout = 120
                };
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var record = new SqlCommand(
                "INSERT dbo.SchemaMigration (MigrationId, SchemaVersion, ScriptHash, AppliedBy) VALUES (@MigrationId, @SchemaVersion, @ScriptHash, @AppliedBy);",
                connection,
                transaction);
            record.Parameters.Add("@MigrationId", SqlDbType.NVarChar, 100).Value = migration.MigrationId;
            record.Parameters.Add("@SchemaVersion", SqlDbType.NVarChar, 50).Value = migration.SchemaVersion;
            record.Parameters.Add("@ScriptHash", SqlDbType.Char, 64).Value = migration.ScriptHash;
            record.Parameters.Add("@AppliedBy", SqlDbType.NVarChar, 200).Value = Environment.MachineName;
            await record.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task VerifyAsync(
        SqlConnection connection,
        Guid tenantUid,
        TenantDatabaseMigration latest,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CASE WHEN
                (SELECT COUNT(*) FROM dbo.TenantDatabaseIdentity WHERE TenantUid = @TenantUid) = 1
                AND EXISTS (SELECT 1 FROM dbo.SchemaMigration WHERE MigrationId = @MigrationId AND ScriptHash = @ScriptHash)
                AND OBJECT_ID(N'dbo.Patient', N'U') IS NOT NULL
                AND OBJECT_ID(N'dbo.Patient_GetByUid', N'P') IS NOT NULL
                AND OBJECT_ID(N'dbo.ScheduleAppointment_GetByUid', N'P') IS NOT NULL
            THEN 1 ELSE 0 END;
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@TenantUid", SqlDbType.UniqueIdentifier).Value = tenantUid;
        command.Parameters.Add("@MigrationId", SqlDbType.NVarChar, 100).Value = latest.MigrationId;
        command.Parameters.Add("@ScriptHash", SqlDbType.Char, 64).Value = latest.ScriptHash;
        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) != 1)
            throw new TenantDatabaseConnectionException("Tenant database post-provisioning verification failed.");
    }
}
