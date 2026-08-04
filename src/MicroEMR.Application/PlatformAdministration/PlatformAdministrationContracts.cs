namespace MicroEMR.Application.PlatformAdministration;

public static class PlatformRoles
{
    public const string Administrator = "PlatformAdministrator";
    public const string Operator = "PlatformOperator";
}

public static class TenantRoleCatalog
{
    public static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.Ordinal)
    {
        "Physician", "Nurse", "MedicalAssistant", "Scheduler", "ClinicAdministrator"
    };

    public static string Normalize(string roleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);
        return Allowed.FirstOrDefault(x => string.Equals(x, roleName.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("The tenant role is not recognized.", nameof(roleName));
    }
}

public sealed record CreatePlatformTenantRequest(Guid TenantUid, string TenantKey, string DisplayName, string DefaultTimeZoneId);
public sealed record UpdateTenantDatabaseAssignmentRequest(Guid TenantUid, string DatabaseServerKey, string DatabaseName, string SecretReference);
public sealed record AddUserTenantMembershipRequest(string UserId, Guid TenantUid, bool IsDefaultTenant);
public sealed record SetUserTenantMembershipStatusRequest(string UserId, Guid TenantUid, string MembershipStatus);
public sealed record SetDefaultTenantRequest(string UserId, Guid TenantUid, bool IsDefaultTenant);
public sealed record AddUserTenantRoleRequest(string UserId, Guid TenantUid, string RoleName);
public sealed record RemoveUserTenantRoleRequest(string UserId, Guid TenantUid, string RoleName);

public sealed record PlatformTenantSummary(Guid TenantUid, string TenantKey, string DisplayName, string TenantStatus,
    string DefaultTimeZoneId, string? DatabaseStatus, string? CurrentSchemaVersion, DateTimeOffset? LastMigrationAt);

public sealed record PlatformTenantDetails(Guid TenantUid, string TenantKey, string DisplayName, string TenantStatus,
    string DefaultTimeZoneId, DateTimeOffset CreatedAt, DateTimeOffset? ActivatedAt, DateTimeOffset? SuspendedAt,
    string? DatabaseServerKey, string? DatabaseName, string? DatabaseStatus, string? CurrentSchemaVersion,
    DateTimeOffset? LastMigrationAt, DateTimeOffset? UpdatedAt);

public sealed record PlatformMembershipInfo(string UserId, Guid TenantUid, string TenantKey, string TenantDisplayName,
    string MembershipStatus, bool IsDefaultTenant, IReadOnlyCollection<string> Roles,
    DateTimeOffset? UpdatedAt = null, string? RowVersion = null);

public interface IPlatformTenantAdministrationService
{
    Task<IReadOnlyList<PlatformTenantSummary>> GetTenantsAsync(CancellationToken cancellationToken = default);
    Task<PlatformTenantDetails?> GetTenantAsync(Guid tenantUid, CancellationToken cancellationToken = default);
    Task<PlatformTenantDetails?> GetTenantByKeyAsync(string tenantKey, CancellationToken cancellationToken = default);
    Task<PlatformTenantDetails> CreateTenantAsync(CreatePlatformTenantRequest request, CancellationToken cancellationToken = default);
    Task<PlatformTenantDetails> UpdateDatabaseAssignmentAsync(UpdateTenantDatabaseAssignmentRequest request, CancellationToken cancellationToken = default);
    Task SuspendTenantAsync(Guid tenantUid, CancellationToken cancellationToken = default);
    Task ActivateTenantAsync(Guid tenantUid, CancellationToken cancellationToken = default);
    Task ArchiveTenantAsync(Guid tenantUid, CancellationToken cancellationToken = default);
}

public interface IPlatformMembershipAdministrationService
{
    Task<IReadOnlyList<PlatformMembershipInfo>> GetMembershipsAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlatformMembershipInfo>> GetTenantMembershipsAsync(Guid tenantUid, CancellationToken cancellationToken = default);
    Task AddMembershipAsync(AddUserTenantMembershipRequest request, CancellationToken cancellationToken = default);
    Task SetMembershipStatusAsync(SetUserTenantMembershipStatusRequest request, CancellationToken cancellationToken = default);
    Task SetDefaultAsync(SetDefaultTenantRequest request, CancellationToken cancellationToken = default);
    Task AddRoleAsync(AddUserTenantRoleRequest request, CancellationToken cancellationToken = default);
    Task RemoveRoleAsync(RemoveUserTenantRoleRequest request, CancellationToken cancellationToken = default);
}

public interface IIdentityUserLookup
{
    bool IsAvailable { get; }
    Task<bool> ExistsAsync(string userId, CancellationToken cancellationToken = default);
}
