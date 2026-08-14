using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MicroEMR.Application.AccessProfiles;

namespace MicroEMR.Infrastructure.Tenancy;

public sealed class SqlAccessProfileRepository(IConfiguration configuration) : IAccessProfileRepository
{
    private readonly string connectionString=PlatformDatabaseConnection.GetConnectionString(configuration);
    public async Task<IReadOnlyList<AccessProfileSummary>> ListAsync(Guid tenantUid,CancellationToken token=default)
    {
        await using var c=new SqlConnection(connectionString); await using var cmd=Command(c,"dbo.AccessProfile_List"); Add(cmd,"@TenantUid",SqlDbType.UniqueIdentifier,tenantUid); await c.OpenAsync(token); await using var r=await cmd.ExecuteReaderAsync(token); var list=new List<AccessProfileSummary>();
        while(await r.ReadAsync(token)) list.Add(new(r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetBoolean(3),r.GetBoolean(4),r.GetInt32(5),r.GetInt32(6),Convert.ToBase64String((byte[])r[7]))); return list;
    }
    public async Task<AccessProfileDetails?> GetAsync(Guid tenantUid,Guid uid,CancellationToken token=default)
    {
        await using var c=new SqlConnection(connectionString); await using var cmd=Command(c,"dbo.AccessProfile_Get"); Add(cmd,"@TenantUid",SqlDbType.UniqueIdentifier,tenantUid); Add(cmd,"@AccessProfileUid",SqlDbType.UniqueIdentifier,uid); await c.OpenAsync(token); await using var r=await cmd.ExecuteReaderAsync(token); AccessProfileDetails? item=null; var keys=new List<string>();
        while(await r.ReadAsync(token)){ if(item is null)item=new(r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetBoolean(3),r.GetBoolean(4),keys,r.GetInt32(6),Convert.ToBase64String((byte[])r[7])); if(!r.IsDBNull(5))keys.Add(r.GetString(5)); } return item;
    }
    public async Task<IReadOnlyDictionary<string,UserAccessProfile>> GetAssignmentsAsync(Guid tenantUid,IReadOnlyCollection<string> userIds,CancellationToken token=default)
    {
        if(userIds.Count==0)return new Dictionary<string,UserAccessProfile>(); await using var c=new SqlConnection(connectionString); await using var cmd=Command(c,"dbo.AccessProfile_GetUserAssignments"); Add(cmd,"@TenantUid",SqlDbType.UniqueIdentifier,tenantUid); await c.OpenAsync(token); await using var r=await cmd.ExecuteReaderAsync(token); var wanted=userIds.ToHashSet(StringComparer.Ordinal); var result=new Dictionary<string,UserAccessProfile>(StringComparer.Ordinal);
        while(await r.ReadAsync(token)){var id=r.GetString(0);if(wanted.Contains(id))result[id]=new(id,r.IsDBNull(1)?null:r.GetGuid(1),r.IsDBNull(2)?null:r.GetString(2),r.IsDBNull(3)?null:r.GetBoolean(3),Convert.ToBase64String((byte[])r[4]));} return result;
    }
    public Task UpdatePermissionsAsync(Guid tenantUid,Guid uid,IReadOnlyCollection<string> keys,string version,string actor,CancellationToken token=default)=>ExecuteAsync("dbo.AccessProfile_ReplacePermissions",token,("@TenantUid",SqlDbType.UniqueIdentifier,tenantUid),("@AccessProfileUid",SqlDbType.UniqueIdentifier,uid),("@PermissionKeys",SqlDbType.NVarChar,string.Join(',',keys)),("@ExpectedRowVersion",SqlDbType.Timestamp,Version(version)),("@ActorUserId",SqlDbType.NVarChar,actor));
    public Task AssignAsync(Guid tenantUid,string user,Guid uid,string version,string actor,CancellationToken token=default)=>ExecuteAsync("dbo.AccessProfile_AssignUser",token,("@TenantUid",SqlDbType.UniqueIdentifier,tenantUid),("@UserId",SqlDbType.NVarChar,user),("@AccessProfileUid",SqlDbType.UniqueIdentifier,uid),("@ExpectedMembershipRowVersion",SqlDbType.Timestamp,Version(version)),("@ActorUserId",SqlDbType.NVarChar,actor));
    public async Task<(string,IReadOnlyCollection<string>)> GetEffectiveAsync(Guid tenantUid,string user,CancellationToken token=default)
    { await using var c=new SqlConnection(connectionString);await using var cmd=Command(c,"dbo.AccessProfile_GetEffective");Add(cmd,"@TenantUid",SqlDbType.UniqueIdentifier,tenantUid);Add(cmd,"@UserId",SqlDbType.NVarChar,user);await c.OpenAsync(token);await using var r=await cmd.ExecuteReaderAsync(token);string status="Missing";var keys=new List<string>();while(await r.ReadAsync(token)){status=r.GetString(0);if(!r.IsDBNull(1)&&PermissionCatalog.IsKnown(r.GetString(1)))keys.Add(r.GetString(1));}return(status,keys);}
    public async Task<UserPermissionAccessData?> GetUserAccessAsync(Guid tenantUid,string user,CancellationToken token=default)
    {
        await using var c=new SqlConnection(connectionString);await using var cmd=Command(c,"dbo.UserPermissionAccess_Get");Add(cmd,"@TenantUid",SqlDbType.UniqueIdentifier,tenantUid);Add(cmd,"@UserId",SqlDbType.NVarChar,user);await c.OpenAsync(token);await using var r=await cmd.ExecuteReaderAsync(token);
        if(!await r.ReadAsync(token))return null;var status=r.GetString(0);Guid? profileUid=r.IsDBNull(1)?null:r.GetGuid(1);var profileName=r.IsDBNull(2)?null:r.GetString(2);var version=Convert.ToBase64String((byte[])r[3]);var profileKeys=new HashSet<string>(StringComparer.Ordinal);var overrides=new Dictionary<string,PermissionOverrideState>(StringComparer.Ordinal);
        if(await r.NextResultAsync(token))while(await r.ReadAsync(token)){var key=r.IsDBNull(0)?null:r.GetString(0);if(key is null||!PermissionCatalog.IsKnown(key))continue;if(!r.IsDBNull(1)&&r.GetBoolean(1))profileKeys.Add(key);if(!r.IsDBNull(2)&&Enum.TryParse<PermissionOverrideState>(r.GetString(2),out var state))overrides[key]=state;}
        return new(status,profileUid,profileName,version,profileKeys,overrides);
    }
    public Task SetUserOverrideAsync(Guid tenantUid,string user,string key,PermissionOverrideState state,string version,string actor,CancellationToken token=default)=>ExecuteAsync("dbo.UserPermissionOverride_Set",token,("@TenantUid",SqlDbType.UniqueIdentifier,tenantUid),("@UserId",SqlDbType.NVarChar,user),("@PermissionKey",SqlDbType.NVarChar,key),("@OverrideState",SqlDbType.VarChar,state.ToString()),("@ExpectedMembershipRowVersion",SqlDbType.Timestamp,Version(version)),("@ActorUserId",SqlDbType.NVarChar,actor));
    private async Task ExecuteAsync(string name,CancellationToken token,params (string,SqlDbType,object)[] values){await using var c=new SqlConnection(connectionString);await using var cmd=Command(c,name);foreach(var x in values)Add(cmd,x.Item1,x.Item2,x.Item3);try{await c.OpenAsync(token);await cmd.ExecuteNonQueryAsync(token);}catch(SqlException ex)when(ex.Number is 51303 or 51401 or 51501){throw new KeyNotFoundException("The tenant membership or access profile was not found.",ex);}catch(SqlException ex)when(ex.Number is 51307 or 51402 or 51502){throw new InvalidOperationException("The access configuration was changed by another administrator.",ex);}catch(SqlException ex)when(ex.Number==51503){throw new ArgumentException("The permission override is invalid.",ex);}catch(SqlException ex)when(ex.Number==51504){throw new InvalidOperationException("The last active clinic administrator must retain access management.",ex);}}
    private static SqlCommand Command(SqlConnection c,string n)=>new(n,c){CommandType=CommandType.StoredProcedure};
    private static void Add(SqlCommand c,string n,SqlDbType t,object v){var p=c.Parameters.Add(n,t);if(t==SqlDbType.NVarChar)p.Size=-1;else if(t==SqlDbType.VarChar)p.Size=20;p.Value=v;}
    private static byte[] Version(string value){var x=Convert.FromBase64String(value);if(x.Length!=8)throw new FormatException("Invalid row version.");return x;}
}
