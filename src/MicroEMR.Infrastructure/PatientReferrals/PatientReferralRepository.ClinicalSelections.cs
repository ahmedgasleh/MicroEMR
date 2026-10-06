using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.PatientReferrals;

namespace MicroEMR.Infrastructure.PatientReferrals;

public sealed partial class PatientReferralRepository
{
    public async Task<PatientReferralClinicalSelectionsResponse?> GetClinicalSelectionsAsync(
        Guid patientUid, Guid referralUid, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = CreateCommand(connection, "dbo.PatientReferralClinicalSelection_Get");
        command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
        command.Parameters.Add("@ReferralUid", SqlDbType.UniqueIdentifier).Value = referralUid;
        return await ReadClinicalSelectionsAsync(command, patientUid, referralUid, cancellationToken);
    }

    public async Task<PatientReferralClinicalSelectionsResponse?> ReplaceDraftClinicalSelectionsAsync(
        Guid patientUid, Guid referralUid, ReplacePatientReferralClinicalSelectionsRequest request, long updatedBy,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = CreateCommand(connection, "dbo.PatientReferralClinicalSelection_ReplaceDraft");
        command.Parameters.Add("@PatientUid", SqlDbType.UniqueIdentifier).Value = patientUid;
        command.Parameters.Add("@ReferralUid", SqlDbType.UniqueIdentifier).Value = referralUid;
        command.Parameters.Add("@ExpectedRowVersion", SqlDbType.Binary, 8).Value = ParseVersion(request.RowVersion);
        command.Parameters.Add("@SelectionsJson", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(request.Selections);
        command.Parameters.Add("@UpdatedBy", SqlDbType.BigInt).Value = updatedBy;
        try { return await ReadClinicalSelectionsAsync(command, patientUid, referralUid, cancellationToken); }
        catch (SqlException e) when (e.Number == 51700) { return null; }
        catch (SqlException e) when (e.Number == 51701)
        { throw new PatientReferralTransitionException("Clinical selections can only change while the referral is Draft."); }
        catch (SqlException e) when (e.Number == 51702) { throw new PatientReferralConcurrencyException(); }
        catch (SqlException e) when (e.Number == 51705)
        { throw new MicroEMR.Application.ClinicalUsers.ClinicalUserResolutionException("Active clinical user not found."); }
        catch (SqlException e) when (e.Number is 51703 or 51704)
        { throw new ReferralClinicalSelectionRuleException(e.Number == 51704
            ? "Selected source is unavailable for this patient." : "Invalid clinical selection references."); }
    }

    private static async Task<PatientReferralClinicalSelectionsResponse?> ReadClinicalSelectionsAsync(
        SqlCommand command, Guid patientUid, Guid referralUid, CancellationToken token)
    {
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) return null;
        var rowVersion = Convert.ToBase64String((byte[])reader["RowVersion"]);
        if (!await reader.NextResultAsync(token)) throw new InvalidOperationException("Clinical selection rows were not returned.");
        var selections = new List<PatientReferralClinicalSelectionResponse>();
        while (await reader.ReadAsync(token)) selections.Add(new(
            reader.GetGuid(reader.GetOrdinal("SelectionUid")), reader.GetString(reader.GetOrdinal("SelectionKind")),
            GetNullableString(reader, "CppCategoryCode"), GetNullableGuid(reader, "EncounterUid"), GetNullableGuid(reader, "ResultUid"),
            reader.GetDateTime(reader.GetOrdinal("CreatedAt")), reader.GetInt64(reader.GetOrdinal("CreatedBy"))));
        // Consume completion so a transaction/commit failure cannot be hidden after the result sets.
        while (await reader.NextResultAsync(token)) { }
        return new(patientUid, referralUid, rowVersion, selections);
    }
}
