namespace MicroEMR.Web.Models.PatientAllergies;

public sealed record AllergyDocumentationStateResponse(string State, NoKnownAllergiesAssertionResponse? NoKnownAllergies);
public sealed record NoKnownAllergiesAssertionResponse(Guid AssertionUid, Guid PatientUid, string Status, long VerifiedBy,
    string VerifiedByDisplayName, DateTime VerifiedAtUtc, long? RevokedBy, DateTime? RevokedAtUtc, string? RevocationReason, string RowVersion);

public sealed class RevokeNoKnownAllergiesRequest
{
    public Guid? AssertionUid {get;set;}
    public string RowVersion { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public sealed record AllergySnapshot(IReadOnlyList<PatientAllergyListItemResponse> Items,AllergyDocumentationStateResponse Documentation);
