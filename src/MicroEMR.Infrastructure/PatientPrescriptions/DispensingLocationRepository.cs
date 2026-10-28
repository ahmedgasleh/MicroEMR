using System.Data;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.PatientPrescriptions;

public sealed class DispensingLocationRepository(ITenantSqlConnectionFactory connections):IDispensingLocationRepository
{
    public Task<IReadOnlyList<DispensingLocation>> SearchAsync(string query,bool includeInactive,int offset,int take,CancellationToken token)=>Rows("dbo.DispensingLocation_Search",q=>{Add(q,"@Query",query);q.Parameters.AddWithValue("@IncludeInactive",includeInactive);q.Parameters.AddWithValue("@Offset",offset);q.Parameters.AddWithValue("@Take",take);},token);
    public async Task<DispensingLocation?> GetAsync(Guid? uid,PrescriptionDispensingLocationReference? identity,CancellationToken token)=>(await Rows("dbo.DispensingLocation_Get",q=>{q.Parameters.AddWithValue("@LocationUid",uid??(object)DBNull.Value);Add(q,"@System",identity?.System);Add(q,"@Identifier",identity?.Identifier);},token)).SingleOrDefault();
    public async Task<DispensingLocation> SaveAsync(Guid? uid,string mode,SaveDispensingLocation r,long actor,CancellationToken token)=>(await Rows("dbo.DispensingLocation_Save",q=>{q.Parameters.AddWithValue("@LocationUid",uid??(object)DBNull.Value);Add(q,"@Mode",mode);Add(q,"@System",r.System);Add(q,"@Identifier",r.Identifier);Add(q,"@FacilityName",r.FacilityName);Add(q,"@FacilityAddress",r.FacilityAddress);Add(q,"@Phone",r.Phone);q.Parameters.AddWithValue("@ActorUserId",actor);q.Parameters.Add("@RowVersion",SqlDbType.VarBinary,-1).Value=mode=="Create"?DBNull.Value:DispensingLocationDirectory.Version(r.RowVersion);},token)).Single();
    private async Task<IReadOnlyList<DispensingLocation>> Rows(string procedure,Action<SqlCommand> configure,CancellationToken token)
    {
        await using var c=await connections.OpenConnectionAsync(token);await using var q=new SqlCommand(procedure,c){CommandType=CommandType.StoredProcedure};configure(q);
        try{await using var r=await q.ExecuteReaderAsync(token);var rows=new List<DispensingLocation>();while(await r.ReadAsync(token))rows.Add(new(r.GetGuid(r.GetOrdinal("LocationUid")),r.GetString(r.GetOrdinal("System")),r.GetString(r.GetOrdinal("Identifier")),r.GetString(r.GetOrdinal("FacilityName")),r.GetString(r.GetOrdinal("FacilityAddress")),r["Phone"] is DBNull?null:(string)r["Phone"],(bool)r["IsActive"],(DateTime)r["CreatedAt"],(long)r["CreatedBy"],r["UpdatedAt"] is DBNull?null:(DateTime)r["UpdatedAt"],r["UpdatedBy"] is DBNull?null:(long)r["UpdatedBy"],Convert.ToBase64String((byte[])r["RowVersion"])));return rows;}
        catch(SqlException e)when(e.Number is 2601 or 2627 or 53101){throw new DispensingLocationConflictException("Location identity already exists, is immutable, or the record changed. Reload and try again.");}
        catch(SqlException e)when(e.Number==53100){throw new ArgumentException("Invalid dispensing location details.");}
    }
    private static void Add(SqlCommand q,string name,string? value)=>q.Parameters.Add(name,SqlDbType.NVarChar,-1).Value=value??(object)DBNull.Value;
}
