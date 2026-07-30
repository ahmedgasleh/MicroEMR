using System.Data;
using MicroEMR.Application.Tenancy;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace MicroEMR.Infrastructure.Tenancy;

public sealed class SqlUserTenantMembershipRepository
    : IUserTenantMembershipRepository
{
    private readonly string _connectionString;

    public SqlUserTenantMembershipRepository(IConfiguration configuration)
    {
        _connectionString =
            PlatformDatabaseConnection.GetConnectionString(configuration);
    }

    public async Task<IReadOnlyList<UserTenantMembershipInfo>>
        GetActiveMembershipsAsync(
            string userId,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        return await ExecuteAsync(
            "dbo.UserTenantMembership_GetActiveByUserId",
            userId,
            tenantUid: null,
            cancellationToken);
    }

    public async Task<UserTenantMembershipInfo?> GetMembershipAsync(
        string userId,
        Guid tenantUid,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var memberships = await ExecuteAsync(
            "dbo.UserTenantMembership_GetActiveByUserAndTenant",
            userId,
            tenantUid,
            cancellationToken);

        return memberships.Count == 0 ? null : memberships[0];
    }

    private async Task<IReadOnlyList<UserTenantMembershipInfo>> ExecuteAsync(
        string storedProcedure,
        string userId,
        Guid? tenantUid,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(storedProcedure, connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add(
            new SqlParameter("@UserId", SqlDbType.NVarChar, 450)
            {
                Value = userId.Trim()
            });

        if (tenantUid.HasValue)
        {
            command.Parameters.Add(
                new SqlParameter("@TenantUid", SqlDbType.UniqueIdentifier)
                {
                    Value = tenantUid.Value
                });
        }

        await connection.OpenAsync(cancellationToken);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        return await AggregateAsync(reader, cancellationToken);
    }

    private static async Task<IReadOnlyList<UserTenantMembershipInfo>>
        AggregateAsync(
            SqlDataReader reader,
            CancellationToken cancellationToken)
    {
        var memberships = new Dictionary<MembershipKey, MembershipBuilder>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var membershipStatus =
                reader.GetString(reader.GetOrdinal("MembershipStatus"));

            if (!string.Equals(
                    membershipStatus,
                    "Active",
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Unsupported active-membership status '{membershipStatus}' in the platform database.");
            }

            var key = new MembershipKey(
                reader.GetString(reader.GetOrdinal("UserId")),
                reader.GetGuid(reader.GetOrdinal("TenantUid")));

            if (!memberships.TryGetValue(key, out var membership))
            {
                membership = new MembershipBuilder(
                    key.UserId,
                    key.TenantUid,
                    reader.GetString(reader.GetOrdinal("TenantKey")),
                    reader.GetString(reader.GetOrdinal("TenantDisplayName")),
                    membershipStatus,
                    reader.GetBoolean(reader.GetOrdinal("IsDefaultTenant")));

                memberships.Add(key, membership);
            }

            var roleOrdinal = reader.GetOrdinal("RoleName");
            if (!reader.IsDBNull(roleOrdinal))
            {
                membership.Roles.Add(reader.GetString(roleOrdinal));
            }
        }

        return memberships.Values
            .OrderByDescending(membership => membership.IsDefaultTenant)
            .ThenBy(membership => membership.TenantDisplayName, StringComparer.Ordinal)
            .ThenBy(membership => membership.TenantUid)
            .Select(membership => membership.Build())
            .ToArray();
    }

    private readonly record struct MembershipKey(string UserId, Guid TenantUid);

    private sealed class MembershipBuilder
    {
        public MembershipBuilder(
            string userId,
            Guid tenantUid,
            string tenantKey,
            string tenantDisplayName,
            string membershipStatus,
            bool isDefaultTenant)
        {
            UserId = userId;
            TenantUid = tenantUid;
            TenantKey = tenantKey;
            TenantDisplayName = tenantDisplayName;
            MembershipStatus = membershipStatus;
            IsDefaultTenant = isDefaultTenant;
        }

        public string UserId { get; }
        public Guid TenantUid { get; }
        public string TenantKey { get; }
        public string TenantDisplayName { get; }
        public string MembershipStatus { get; }
        public bool IsDefaultTenant { get; }
        public SortedSet<string> Roles { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public UserTenantMembershipInfo Build()
        {
            return new UserTenantMembershipInfo(
                UserId,
                TenantUid,
                TenantKey,
                TenantDisplayName,
                MembershipStatus,
                IsDefaultTenant,
                Roles.ToArray());
        }
    }
}
