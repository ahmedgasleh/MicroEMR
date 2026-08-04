using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MicroEMR.Application.TenantUserAdministration;

namespace MicroEMR.Infrastructure.Tenancy;

public sealed class SqlTenantMembershipLifecycleRepository(IConfiguration configuration)
    : ITenantMembershipLifecycleRepository
{
    private readonly string _connectionString = PlatformDatabaseConnection.GetConnectionString(configuration);

    public Task<TenantMembershipLifecycleResult> DeactivateAsync(string authUserId, Guid tenantUid,
        string rowVersion, string actorAuthUserId, CancellationToken cancellationToken = default) =>
        ExecuteAsync("dbo.PlatformMembership_Deactivate", authUserId, tenantUid, rowVersion, actorAuthUserId, cancellationToken);

    public Task<TenantMembershipLifecycleResult> ActivateAsync(string authUserId, Guid tenantUid,
        string rowVersion, string actorAuthUserId, CancellationToken cancellationToken = default) =>
        ExecuteAsync("dbo.PlatformMembership_Activate", authUserId, tenantUid, rowVersion, actorAuthUserId, cancellationToken);

    private async Task<TenantMembershipLifecycleResult> ExecuteAsync(string procedure, string authUserId,
        Guid tenantUid, string rowVersion, string actorAuthUserId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorAuthUserId);
        if (tenantUid == Guid.Empty) throw new ArgumentException("Tenant UID is required.", nameof(tenantUid));
        var expected = Convert.FromBase64String(rowVersion);
        if (expected.Length != 8) throw new FormatException("The row version must contain exactly 8 bytes.");

        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(procedure, connection) { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("@UserId", SqlDbType.NVarChar, 450).Value = authUserId.Trim();
        command.Parameters.Add("@TenantUid", SqlDbType.UniqueIdentifier).Value = tenantUid;
        command.Parameters.Add("@ExpectedRowVersion", SqlDbType.Timestamp, 8).Value = expected;
        command.Parameters.Add("@ActorUserId", SqlDbType.NVarChar, 450).Value = actorAuthUserId.Trim();
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException("Membership lifecycle operation returned no result.");
            return new(reader.GetString(0),
                new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc)),
                Convert.ToBase64String((byte[])reader[2]));
        }
        catch (SqlException ex) when (ex.Number == 51303) { throw new TenantMembershipNotFoundException("The membership was not found in the active tenant.", ex); }
        catch (SqlException ex) when (ex.Number == 51305) { throw new TenantMembershipTransitionException("The membership is not in the required status.", ex); }
        catch (SqlException ex) when (ex.Number == 51306) { throw new TenantMembershipSelfDeactivationException("You cannot deactivate your own current clinic membership.", ex); }
        catch (SqlException ex) when (ex.Number == 51307) { throw new TenantMembershipConcurrencyException("The membership was changed by another administrator.", ex); }
        catch (SqlException ex) when (ex.Number == 51308) { throw new TenantMembershipLastAdministratorException("The last active clinic administrator cannot be deactivated.", ex); }
    }
}
