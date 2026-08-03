using System.Data;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.PatientReferrals;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.PatientReferrals;

public sealed class ReferralDocumentRepository(ITenantSqlConnectionFactory connections)
    : IReferralDocumentRepository
{
    public async Task<IReadOnlyList<ReferralDocumentLinkResponse>> GetByReferralUidAsync(
        Guid patientUid, Guid referralUid, CancellationToken cancellationToken = default)
    {
        var results = new List<ReferralDocumentLinkResponse>();
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        await using var command = Command(connection, "dbo.PatientReferralDocument_GetByReferralUid");
        AddIds(command, patientUid, referralUid);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) results.Add(Map(reader));
        return results;
    }

    public Task LinkAsync(Guid patientUid, Guid referralUid, Guid documentUid, string rowVersion,
        long linkedBy, CancellationToken cancellationToken = default) =>
        MutateAsync("dbo.PatientReferralDocument_Link", patientUid, referralUid, documentUid,
            rowVersion, linkedBy, cancellationToken);

    public Task UnlinkAsync(Guid patientUid, Guid referralUid, Guid documentUid, string rowVersion,
        long unlinkedBy, CancellationToken cancellationToken = default) =>
        MutateAsync("dbo.PatientReferralDocument_Unlink", patientUid, referralUid, documentUid,
            rowVersion, unlinkedBy, cancellationToken);

    private async Task MutateAsync(string procedure, Guid patientUid, Guid referralUid,
        Guid documentUid, string rowVersion, long actor, CancellationToken cancellationToken)
    {
        byte[] version;
        try { version = Convert.FromBase64String(rowVersion); }
        catch (FormatException e) { throw new ArgumentException("RowVersion is invalid.", nameof(rowVersion), e); }
        if (version.Length != 8) throw new ArgumentException("RowVersion is invalid.", nameof(rowVersion));
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        await using var command = Command(connection, procedure);
        AddIds(command, patientUid, referralUid);
        command.Parameters.Add("@DocumentUid", SqlDbType.UniqueIdentifier).Value = documentUid;
        command.Parameters.Add("@ExpectedRowVersion", SqlDbType.Timestamp, 8).Value = version;
        command.Parameters.Add("@Actor", SqlDbType.BigInt).Value = actor;
        try { await command.ExecuteNonQueryAsync(cancellationToken); }
        catch (SqlException e) when (e.Number is 51603) { throw new ReferralDocumentConcurrencyException(); }
        catch (SqlException e) when (e.Number is 51602 or 51605 or 51606)
        { throw new ReferralDocumentRuleException("The supporting-document change is not allowed."); }
        catch (SqlException e) when (e.Number is 51600 or 51601 or 51604)
        { throw new KeyNotFoundException("Referral, document, or link not found."); }
    }

    private static SqlCommand Command(SqlConnection connection, string name) =>
        new(name, connection) { CommandType = CommandType.StoredProcedure };
    private static void AddIds(SqlCommand command, Guid patientUid, Guid referralUid)
    {
        command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
        command.Parameters.Add("@ReferralUid", SqlDbType.UniqueIdentifier).Value = referralUid;
    }
    private static ReferralDocumentLinkResponse Map(SqlDataReader r) => new()
    {
        DocumentUid = r.GetGuid(r.GetOrdinal("DocumentUid")), Title = r.GetString(r.GetOrdinal("Title")),
        DocumentType = r.GetString(r.GetOrdinal("DocumentType")),
        DocumentStatus = r.GetString(r.GetOrdinal("DocumentStatus")),
        CreatedAtUtc = r.GetDateTime(r.GetOrdinal("CreatedAt")),
        CreatedBy = r.IsDBNull(r.GetOrdinal("CreatedBy")) ? null : r.GetInt64(r.GetOrdinal("CreatedBy")),
        CreatedByDisplayName = r.IsDBNull(r.GetOrdinal("CreatedByDisplayName")) ? null : r.GetString(r.GetOrdinal("CreatedByDisplayName")),
        LinkedAtUtc = r.GetDateTime(r.GetOrdinal("LinkedAt")), LinkedBy = r.GetInt64(r.GetOrdinal("LinkedBy")),
        LinkedByDisplayName = r.IsDBNull(r.GetOrdinal("LinkedByDisplayName")) ? null : r.GetString(r.GetOrdinal("LinkedByDisplayName"))
    };
}
