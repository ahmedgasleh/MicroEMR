using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MicroEMR.Application.TenantUserAdministration;

namespace MicroEMR.Infrastructure.Tenancy;

public sealed class SqlTenantRoleManagementRepository(IConfiguration configuration) : ITenantRoleManagementRepository
{
    private readonly string _connectionString = PlatformDatabaseConnection.GetConnectionString(configuration);

    public async Task<TenantRoleUpdateResult> ReplaceRolesAsync(string authUserId, Guid tenantUid,
        IReadOnlyCollection<string> roles, string rowVersion, string actorAuthUserId, CancellationToken cancellationToken = default)
    {
        var expected = Convert.FromBase64String(rowVersion);
        if (expected.Length != 8) throw new FormatException("The row version must contain exactly 8 bytes.");
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand("dbo.PlatformMembership_ReplaceRoles", connection) { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("@UserId", SqlDbType.NVarChar, 450).Value = authUserId;
        command.Parameters.Add("@TenantUid", SqlDbType.UniqueIdentifier).Value = tenantUid;
        command.Parameters.Add("@RoleNames", SqlDbType.NVarChar, 1000).Value = string.Join(',', roles);
        command.Parameters.Add("@ExpectedRowVersion", SqlDbType.Timestamp, 8).Value = expected;
        command.Parameters.Add("@ActorUserId", SqlDbType.NVarChar, 450).Value = actorAuthUserId;
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var resultRoles = new List<string>(); DateTimeOffset updatedAt = default; string? version = null;
            while (await reader.ReadAsync(cancellationToken))
            { resultRoles.Add(reader.GetString(0)); updatedAt = new(DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc)); version = Convert.ToBase64String((byte[])reader[2]); }
            return new(resultRoles, updatedAt, version ?? throw new InvalidOperationException("Role update returned no result."));
        }
        catch (SqlException ex) when (ex.Number == 51303) { throw new TenantMembershipNotFoundException("The membership was not found in the active tenant.", ex); }
        catch (SqlException ex) when (ex.Number == 51305) { throw new TenantRoleInactiveMembershipException("Roles can only be changed for an active membership.", ex); }
        catch (SqlException ex) when (ex.Number == 51307) { throw new TenantMembershipConcurrencyException("The roles were changed by another administrator.", ex); }
        catch (SqlException ex) when (ex.Number == 51308) { throw new TenantMembershipLastAdministratorException("The last active clinic administrator cannot lose that role.", ex); }
        catch (SqlException ex) when (ex.Number == 51309) { throw new TenantRoleSelfLockoutException("You cannot remove your own clinic administrator role.", ex); }
        catch (SqlException ex) when (ex.Number == 51310) { throw new TenantRoleValidationException("The submitted tenant role set is invalid.", ex); }
    }
}
