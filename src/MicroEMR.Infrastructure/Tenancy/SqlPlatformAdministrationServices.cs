using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.PlatformAdministration;

namespace MicroEMR.Infrastructure.Tenancy;

public sealed class SqlPlatformTenantAdministrationService : IPlatformTenantAdministrationService
{
    private readonly string _connectionString;
    private readonly string _actor;
    private readonly ILogger<SqlPlatformTenantAdministrationService> _logger;

    public SqlPlatformTenantAdministrationService(IConfiguration configuration, ILogger<SqlPlatformTenantAdministrationService> logger)
    {
        _connectionString = PlatformDatabaseConnection.GetConnectionString(configuration);
        _actor = configuration["PlatformAdministration:ActorId"]?.Trim() ?? "local-cli";
        _logger = logger;
    }

    public Task<IReadOnlyList<PlatformTenantSummary>> GetTenantsAsync(CancellationToken cancellationToken = default) => QuerySummariesAsync(cancellationToken);
    public Task<PlatformTenantDetails?> GetTenantAsync(Guid tenantUid, CancellationToken cancellationToken = default) => QueryDetailsAsync("dbo.PlatformTenant_GetByUid", "@TenantUid", tenantUid, cancellationToken);
    public Task<PlatformTenantDetails?> GetTenantByKeyAsync(string tenantKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantKey);
        return QueryDetailsAsync("dbo.PlatformTenant_GetByKey", "@TenantKey", NormalizeKey(tenantKey), cancellationToken);
    }

    public async Task<PlatformTenantDetails> CreateTenantAsync(CreatePlatformTenantRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TenantUid == Guid.Empty) throw new ArgumentException("Tenant UID must not be empty.");
        var key = NormalizeKey(request.TenantKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DefaultTimeZoneId);
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(request.DefaultTimeZoneId.Trim(), out _)) throw new ArgumentException("The time zone is not recognized.");
        await ExecuteAsync("dbo.PlatformTenant_Create", cancellationToken,
            ("@TenantUid", request.TenantUid), ("@TenantKey", key), ("@DisplayName", request.DisplayName.Trim()),
            ("@DefaultTimeZoneId", request.DefaultTimeZoneId.Trim()), ("@ActorUserId", _actor));
        return await GetTenantAsync(request.TenantUid, cancellationToken) ?? throw new InvalidOperationException("The tenant could not be read after creation.");
    }

    public async Task<PlatformTenantDetails> UpdateDatabaseAssignmentAsync(UpdateTenantDatabaseAssignmentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TenantUid == Guid.Empty) throw new ArgumentException("Tenant UID must not be empty.");
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DatabaseServerKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DatabaseName);
        ValidateSecretReference(request.SecretReference);
        await ExecuteAsync("dbo.PlatformTenantDatabase_UpsertProvisioning", cancellationToken,
            ("@TenantUid", request.TenantUid), ("@DatabaseServerKey", request.DatabaseServerKey.Trim()),
            ("@DatabaseName", request.DatabaseName.Trim()), ("@SecretReference", request.SecretReference.Trim()), ("@ActorUserId", _actor));
        return await GetTenantAsync(request.TenantUid, cancellationToken) ?? throw new InvalidOperationException("The tenant could not be read after assignment.");
    }

    public Task SuspendTenantAsync(Guid tenantUid, CancellationToken cancellationToken = default) => TransitionAsync(tenantUid, "Suspended", cancellationToken);
    public Task ActivateTenantAsync(Guid tenantUid, CancellationToken cancellationToken = default) => TransitionAsync(tenantUid, "Active", cancellationToken);
    public Task ArchiveTenantAsync(Guid tenantUid, CancellationToken cancellationToken = default) => TransitionAsync(tenantUid, "Archived", cancellationToken);

    private Task TransitionAsync(Guid tenantUid, string status, CancellationToken cancellationToken)
    {
        if (tenantUid == Guid.Empty) throw new ArgumentException("Tenant UID must not be empty.");
        return ExecuteAsync("dbo.PlatformTenant_SetStatus", cancellationToken, ("@TenantUid", tenantUid), ("@NewStatus", status), ("@ActorUserId", _actor));
    }

    internal static string NormalizeKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var value = key.Trim().ToLowerInvariant();
        if (value.Length > 50 || value.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '-'))) throw new ArgumentException("Tenant key may contain only lowercase letters, digits, and hyphens.");
        return value;
    }

    internal static void ValidateSecretReference(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Contains(';') || normalized.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Server=", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Password=", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("User Id=", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Secret reference must be an opaque identifier, not a connection string.");
    }

    private async Task ExecuteAsync(string procedure, CancellationToken cancellationToken, params (string Name, object Value)[] parameters)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand(procedure, connection) { CommandType = CommandType.StoredProcedure };
            foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
            await connection.OpenAsync(cancellationToken);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException exception)
        {
            _logger.LogWarning(exception, "Platform administration operation failed for a tenant metadata command.");
            throw new InvalidOperationException(SafeMessage(exception.Number), exception);
        }
    }

    private static string SafeMessage(int number) => number switch
    {
        51201 or 2601 or 2627 => "A tenant with this UID or key already exists.",
        51203 => "An active database assignment cannot be overwritten.",
        51204 => "The tenant database must be active and current before the tenant can be activated.",
        51205 => "The requested tenant status transition is not allowed.",
        _ => "The platform administration operation could not be completed."
    };

    private async Task<IReadOnlyList<PlatformTenantSummary>> QuerySummariesAsync(CancellationToken cancellationToken)
    {
        var result = new List<PlatformTenantSummary>();
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand("dbo.PlatformTenant_List", connection) { CommandType = CommandType.StoredProcedure };
        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), NullableString(reader, 5), NullableString(reader, 6), NullableDate(reader, 7)));
        return result;
    }

    private async Task<PlatformTenantDetails?> QueryDetailsAsync(string procedure, string parameterName, object value, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(procedure, connection) { CommandType = CommandType.StoredProcedure };
        command.Parameters.AddWithValue(parameterName, value);
        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), Utc(reader.GetDateTime(5)), NullableDate(reader, 6), NullableDate(reader, 7), NullableString(reader, 8), NullableString(reader, 9), NullableString(reader, 10), NullableString(reader, 11), NullableDate(reader, 12), NullableDate(reader, 13));
    }

    private static string? NullableString(SqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
    private static DateTimeOffset? NullableDate(SqlDataReader r, int i) => r.IsDBNull(i) ? null : Utc(r.GetDateTime(i));
    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}

public sealed class SqlPlatformMembershipAdministrationService : IPlatformMembershipAdministrationService
{
    private readonly string _connectionString;
    private readonly string _actor;
    private readonly IIdentityUserLookup _users;
    public SqlPlatformMembershipAdministrationService(IConfiguration configuration, IIdentityUserLookup users)
    { _connectionString = PlatformDatabaseConnection.GetConnectionString(configuration); _actor = configuration["PlatformAdministration:ActorId"]?.Trim() ?? "local-cli"; _users = users; }

    public Task<IReadOnlyList<PlatformMembershipInfo>> GetMembershipsAsync(string userId, CancellationToken cancellationToken = default) => QueryAsync("dbo.PlatformMembership_ListByUser", "@UserId", RequireUser(userId), cancellationToken);
    public Task<IReadOnlyList<PlatformMembershipInfo>> GetTenantMembershipsAsync(Guid tenantUid, CancellationToken cancellationToken = default) => QueryAsync("dbo.PlatformMembership_ListByTenant", "@TenantUid", tenantUid, cancellationToken);
    public async Task AddMembershipAsync(AddUserTenantMembershipRequest request, CancellationToken cancellationToken = default)
    {
        var user = RequireUser(request.UserId);
        if (!_users.IsAvailable) throw new InvalidOperationException("Identity user validation is not configured; membership was not created.");
        if (!await _users.ExistsAsync(user, cancellationToken)) throw new InvalidOperationException("The specified Identity user could not be found.");
        await ExecuteAsync("dbo.PlatformMembership_Add", cancellationToken, ("@UserId", user), ("@TenantUid", request.TenantUid), ("@IsDefaultTenant", request.IsDefaultTenant), ("@ActorUserId", _actor));
    }
    public Task SetMembershipStatusAsync(SetUserTenantMembershipStatusRequest request, CancellationToken cancellationToken = default) =>
        ExecuteAsync("dbo.PlatformMembership_SetStatus", cancellationToken, ("@UserId", RequireUser(request.UserId)), ("@TenantUid", request.TenantUid), ("@MembershipStatus", NormalizeStatus(request.MembershipStatus)), ("@ActorUserId", _actor));
    public Task SetDefaultAsync(SetDefaultTenantRequest request, CancellationToken cancellationToken = default) =>
        ExecuteAsync("dbo.PlatformMembership_SetDefault", cancellationToken, ("@UserId", RequireUser(request.UserId)), ("@TenantUid", request.TenantUid), ("@IsDefaultTenant", request.IsDefaultTenant), ("@ActorUserId", _actor));
    public Task AddRoleAsync(AddUserTenantRoleRequest request, CancellationToken cancellationToken = default) =>
        ExecuteAsync("dbo.PlatformTenantRole_Add", cancellationToken, ("@UserId", RequireUser(request.UserId)), ("@TenantUid", request.TenantUid), ("@RoleName", TenantRoleCatalog.Normalize(request.RoleName)), ("@ActorUserId", _actor));
    public Task RemoveRoleAsync(RemoveUserTenantRoleRequest request, CancellationToken cancellationToken = default) =>
        ExecuteAsync("dbo.PlatformTenantRole_Remove", cancellationToken, ("@UserId", RequireUser(request.UserId)), ("@TenantUid", request.TenantUid), ("@RoleName", TenantRoleCatalog.Normalize(request.RoleName)), ("@ActorUserId", _actor));

    private static string RequireUser(string value) { ArgumentException.ThrowIfNullOrWhiteSpace(value); return value.Trim(); }
    private static string NormalizeStatus(string value) => value.Trim().ToLowerInvariant() switch { "active" => "Active", "suspended" => "Suspended", "revoked" => "Revoked", _ => throw new ArgumentException("Membership status must be Active, Suspended, or Revoked.") };
    private async Task ExecuteAsync(string procedure, CancellationToken cancellationToken, params (string, object)[] parameters)
    {
        try { await using var c = new SqlConnection(_connectionString); await using var cmd = new SqlCommand(procedure, c) { CommandType = CommandType.StoredProcedure }; foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Item1, p.Item2); await c.OpenAsync(cancellationToken); await cmd.ExecuteNonQueryAsync(cancellationToken); }
        catch (SqlException ex) { throw new InvalidOperationException(ex.Number switch { 51301 => "The user already has a membership in this tenant.", 51302 => "The tenant is not active.", 51303 => "The membership was not found.", _ => "The membership administration operation could not be completed." }, ex); }
    }
    private async Task<IReadOnlyList<PlatformMembershipInfo>> QueryAsync(string procedure, string name, object value, CancellationToken token)
    {
        var builders = new Dictionary<(string, Guid), (string Key, string Name, string Status, bool Default, List<string> Roles, DateTimeOffset? UpdatedAt, string? RowVersion)>();
        await using var c = new SqlConnection(_connectionString); await using var cmd = new SqlCommand(procedure, c) { CommandType = CommandType.StoredProcedure }; cmd.Parameters.AddWithValue(name, value); await c.OpenAsync(token); await using var r = await cmd.ExecuteReaderAsync(token);
        while (await r.ReadAsync(token)) { var k = (r.GetString(0), r.GetGuid(1)); if (!builders.TryGetValue(k, out var b)) b = (r.GetString(2), r.GetString(3), r.GetString(4), r.GetBoolean(5), [], r.IsDBNull(7)?null:new DateTimeOffset(DateTime.SpecifyKind(r.GetDateTime(7),DateTimeKind.Utc)), Convert.ToBase64String((byte[])r[8])); if (!r.IsDBNull(6)) b.Roles.Add(r.GetString(6)); builders[k] = b; }
        return builders.Select(x => new PlatformMembershipInfo(x.Key.Item1, x.Key.Item2, x.Value.Key, x.Value.Name, x.Value.Status, x.Value.Default, x.Value.Roles.Distinct(StringComparer.Ordinal).Order().ToArray(),x.Value.UpdatedAt,x.Value.RowVersion)).ToArray();
    }
}
