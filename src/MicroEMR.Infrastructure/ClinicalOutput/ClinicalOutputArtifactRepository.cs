using System.Data;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.Tenancy;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.ClinicalOutput;

public sealed class ClinicalOutputArtifactRepository(ITenantSqlConnectionFactory connections)
    : IClinicalOutputArtifactRepository
{
    public async Task<ClinicalOutputArtifact?> GetFinalBySourceAsync(string sourceType, Guid sourceUid, CancellationToken token = default)
    {
        await using var connection = await connections.OpenConnectionAsync(token);
        await using var command = new SqlCommand("dbo.ClinicalOutputArtifact_GetFinalBySource", connection) { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("@SourceType", SqlDbType.NVarChar, 30).Value = sourceType;
        command.Parameters.Add("@SourceUid", SqlDbType.UniqueIdentifier).Value = sourceUid;
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? Map(reader) : null;
    }

    public async Task<ClinicalOutputArtifact> CreateAsync(CreateClinicalOutputArtifact artifact, CancellationToken token = default)
    {
        await using var connection = await connections.OpenConnectionAsync(token);
        await using var command = new SqlCommand("dbo.ClinicalOutputArtifact_Create", connection) { CommandType = CommandType.StoredProcedure };
        Add(command, "@ArtifactUid", artifact.ArtifactUid); Add(command, "@PatientUid", artifact.PatientUid);
        Text(command, "@SourceType", artifact.SourceType, 30); Add(command, "@SourceUid", artifact.SourceUid);
        Add(command, "@TemplateVersionUid", artifact.TemplateVersionUid); Text(command, "@ArtifactType", artifact.ArtifactType, 30);
        Text(command, "@StorageProvider", artifact.StorageProvider, 30); Text(command, "@StorageKey", artifact.StorageKey, 700);
        Text(command, "@MimeType", artifact.MimeType, 100); command.Parameters.Add("@FileSizeBytes", SqlDbType.BigInt).Value = artifact.FileSizeBytes;
        Text(command, "@Sha256", artifact.Sha256, 64); command.Parameters.Add("@CreatedBy", SqlDbType.BigInt).Value = (object?)artifact.CreatedBy ?? DBNull.Value;
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) throw new InvalidOperationException("ClinicalOutputArtifact_Create returned no artifact.");
        return Map(reader);
    }

    public async Task RecordFailureAsync(Guid patientUid, string sourceType, Guid sourceUid, Guid templateVersionUid,
        long? actorUserId, string failureCode, CancellationToken token = default)
    {
        await using var connection = await connections.OpenConnectionAsync(token);
        await using var command = new SqlCommand("dbo.ClinicalOutputArtifact_RecordFailure", connection) { CommandType = CommandType.StoredProcedure };
        Add(command, "@PatientUid", patientUid); Text(command, "@SourceType", sourceType, 30); Add(command, "@SourceUid", sourceUid);
        Add(command, "@TemplateVersionUid", templateVersionUid); command.Parameters.Add("@CreatedBy", SqlDbType.BigInt).Value = (object?)actorUserId ?? DBNull.Value;
        Text(command, "@FailureCode", failureCode, 100); await command.ExecuteNonQueryAsync(token);
    }

    private static ClinicalOutputArtifact Map(SqlDataReader r) => new(r.GetGuid(r.GetOrdinal("ArtifactUid")),
        r.GetGuid(r.GetOrdinal("PatientUid")), r.GetString(r.GetOrdinal("SourceType")), r.GetGuid(r.GetOrdinal("SourceUid")),
        r.GetGuid(r.GetOrdinal("TemplateVersionUid")), r.GetString(r.GetOrdinal("ArtifactType")), r.GetString(r.GetOrdinal("StorageProvider")),
        r.GetString(r.GetOrdinal("StorageKey")), r.GetString(r.GetOrdinal("MimeType")), r.GetInt64(r.GetOrdinal("FileSizeBytes")),
        r.GetString(r.GetOrdinal("Sha256")), r.GetString(r.GetOrdinal("ArtifactStatus")),
        r.IsDBNull(r.GetOrdinal("CreatedBy")) ? null : r.GetInt64(r.GetOrdinal("CreatedBy")), r.GetDateTime(r.GetOrdinal("CreatedAt")));
    private static void Add(SqlCommand c, string n, Guid v) => c.Parameters.Add(n, SqlDbType.UniqueIdentifier).Value = v;
    private static void Text(SqlCommand c, string n, string v, int size) => c.Parameters.Add(n, SqlDbType.NVarChar, size).Value = v;
}
