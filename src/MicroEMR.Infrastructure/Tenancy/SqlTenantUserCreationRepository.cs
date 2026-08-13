using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MicroEMR.Application.TenantUserAdministration;

namespace MicroEMR.Infrastructure.Tenancy;

public sealed class SqlTenantUserCreationRepository(IConfiguration configuration) : ITenantUserCreationRepository
{
    private readonly string _connectionString = PlatformDatabaseConnection.GetConnectionString(configuration);

    public async Task CreateAsync(string authUserId, Guid tenantUid, string initialRole, string actorAuthUserId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand("dbo.PlatformMembership_CreateWithInitialRole", connection)
            { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("@UserId", SqlDbType.NVarChar, 450).Value = authUserId;
        command.Parameters.Add("@TenantUid", SqlDbType.UniqueIdentifier).Value = tenantUid;
        command.Parameters.Add("@RoleName", SqlDbType.NVarChar, 100).Value = initialRole;
        command.Parameters.Add("@ActorUserId", SqlDbType.NVarChar, 450).Value = actorAuthUserId;
        try
        {
            await connection.OpenAsync(cancellationToken);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException ex) when (ex.Number == 51301)
        { throw new TenantMembershipAlreadyExistsException("This user already belongs to this clinic.", ex); }
        catch (SqlException ex) when (ex.Number is 51302 or 51310)
        { throw new TenantUserCreationException("The clinic or initial role is not available.", ex); }
        catch (SqlException ex)
        { throw new TenantUserCreationException("The tenant membership could not be created.", ex); }
    }
}
