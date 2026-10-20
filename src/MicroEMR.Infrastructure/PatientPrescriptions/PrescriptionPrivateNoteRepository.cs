using System.Data;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.PatientPrescriptions;

public sealed class PrescriptionPrivateNoteRepository(ITenantSqlConnectionFactory connections) : IPrescriptionPrivateNoteRepository
{
    public async Task<PrescriptionPrivateNoteDetail?> ReadAsync(Guid patient, Guid prescription, long actor,
        string correlation, bool canPrescribe, CancellationToken token)
    {
        await using var c = await connections.OpenConnectionAsync(token);
        await using var q = ReadCommand(c, patient, prescription, actor, correlation, false, canPrescribe);
        await using var r = await q.ExecuteReaderAsync(token);
        if (!await r.ReadAsync(token)) return null;
        return new(r.GetGuid(0), r.GetGuid(1), r.IsDBNull(2) ? null : r.GetString(2), r.GetString(3),
            Convert.ToBase64String(r.GetFieldValue<byte[]>(4)), r.GetBoolean(5));
    }
    public async Task<IReadOnlyList<PrescriptionPrivateNoteRevision>?> HistoryAsync(Guid patient, Guid prescription,
        long actor, string correlation, CancellationToken token)
    {
        await using var c = await connections.OpenConnectionAsync(token);
        await using var q = ReadCommand(c, patient, prescription, actor, correlation, true, false);
        await using var r = await q.ExecuteReaderAsync(token);
        if (!await r.ReadAsync(token)) return null;
        await r.NextResultAsync(token);
        var rows = new List<PrescriptionPrivateNoteRevision>();
        while (await r.ReadAsync(token))
            rows.Add(new(r.GetGuid(0), r.GetInt32(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3), r.GetInt64(4), r.GetDateTime(5)));
        return rows;
    }
    public async Task RecordDeniedAsync(string subject, long? actor, Guid? patient, Guid? prescription,
        string correlation, int status, CancellationToken token)
    {
        await using var c = await connections.OpenConnectionAsync(token);
        await using var q = new SqlCommand("dbo.PatientPrescriptionPrivateNote_RecordDenied", c) { CommandType = CommandType.StoredProcedure };
        q.Parameters.Add("@Subject", SqlDbType.NVarChar, 450).Value = subject;
        q.Parameters.Add("@ActorUserId", SqlDbType.BigInt).Value = actor ?? (object)DBNull.Value;
        q.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patient ?? (object)DBNull.Value;
        q.Parameters.Add("@PrescriptionUid", SqlDbType.UniqueIdentifier).Value = prescription ?? (object)DBNull.Value;
        q.Parameters.Add("@Correlation", SqlDbType.NVarChar, 100).Value = correlation;
        q.Parameters.Add("@Status", SqlDbType.Int).Value = status;
        await q.ExecuteNonQueryAsync(token);
    }
    private static SqlCommand ReadCommand(SqlConnection c, Guid patient, Guid prescription, long actor,
        string correlation, bool history, bool canPrescribe)
    {
        var q = new SqlCommand("dbo.PatientPrescriptionPrivateNote_Read", c) { CommandType = CommandType.StoredProcedure };
        q.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patient;
        q.Parameters.Add("@PrescriptionUid", SqlDbType.UniqueIdentifier).Value = prescription;
        q.Parameters.Add("@ActorUserId", SqlDbType.BigInt).Value = actor;
        q.Parameters.Add("@Correlation", SqlDbType.NVarChar, 100).Value = correlation;
        q.Parameters.Add("@History", SqlDbType.Bit).Value = history;
        q.Parameters.Add("@CanPrescribe", SqlDbType.Bit).Value = canPrescribe;
        return q;
    }
}
