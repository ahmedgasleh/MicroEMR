using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.DrugCatalog;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.DrugCatalog;

public sealed class DrugCatalogRepository(ITenantSqlConnectionFactory connections) : IDrugCatalogRepository
{
    public async Task<DrugImportResult> ImportAsync(DrugImportBatch batch, long actor, CancellationToken token)
    {
        await using var c = await connections.OpenConnectionAsync(token);
        await using var q = new SqlCommand("dbo.DrugCatalog_Import", c) { CommandType = CommandType.StoredProcedure, CommandTimeout = 120 };
        q.Parameters.Add("@Products", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(batch.Products);
        q.Parameters.Add("@ImportSha256", SqlDbType.VarChar, 64).Value = batch.Sha256;
        q.Parameters.AddWithValue("@ActorUserId", actor);
        await using var r = await q.ExecuteReaderAsync(token); await r.ReadAsync(token);
        return new(r.GetInt32(0), r.GetInt32(1), r.GetInt32(2));
    }
    public Task<IReadOnlyList<CatalogDrug>> SearchAsync(string query, int offset, int take, CancellationToken token) =>
        Read("dbo.DrugCatalog_Search", q => { q.Parameters.Add("@Query", SqlDbType.NVarChar, 200).Value = query; q.Parameters.AddWithValue("@Offset", offset); q.Parameters.AddWithValue("@Take", take); }, token);
    public async Task<CatalogDrug?> GetAsync(Guid uid, CancellationToken token) =>
        (await Read("dbo.DrugCatalog_Get", q => q.Parameters.AddWithValue("@ProductUid", uid), token)).SingleOrDefault();
    private async Task<IReadOnlyList<CatalogDrug>> Read(string procedure, Action<SqlCommand> parameters, CancellationToken token)
    {
        await using var c = await connections.OpenConnectionAsync(token);
        await using var q = new SqlCommand(procedure, c) { CommandType = CommandType.StoredProcedure }; parameters(q);
        await using var r = await q.ExecuteReaderAsync(token); var rows = new List<CatalogDrug>();
        while (await r.ReadAsync(token)) rows.Add(new(r.GetGuid(0), JsonSerializer.Deserialize<DrugFacts>(r.GetString(1))!, r.GetDateTime(2), r.GetString(3), Convert.ToBase64String((byte[])r[4])));
        return rows;
    }
}
