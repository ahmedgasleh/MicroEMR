using MicroEMR.Application.PatientEncounters;
using MicroEMR.Application.PatientEncounters.Contracts;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class EncounterSigningValidatorTests
{
    [Fact]
    public void Validate_AcceptsCompleteDraftEncounter()
    {
        var encounter = CompleteEncounter();

        Assert.Empty(EncounterSigningValidator.Validate(encounter));
    }

    [Fact]
    public void Validate_RejectsMissingClinicalFields()
    {
        var encounter = CompleteEncounter();
        encounter.ProviderName = null;
        encounter.ReasonForVisit = null;
        encounter.SubjectiveNote = null;

        var errors = EncounterSigningValidator.Validate(encounter);

        Assert.Contains("A responsible provider is required before signing.", errors);
        Assert.Contains("A reason for visit is required before signing.", errors);
        Assert.Contains("A clinical note is required before signing.", errors);
    }

    [Fact]
    public void Validate_RejectsAlreadySignedEncounter()
    {
        var encounter = CompleteEncounter();
        encounter.Status = EncounterStatuses.Signed;

        Assert.Contains("Only a draft encounter can be signed.",
            EncounterSigningValidator.Validate(encounter));
    }

    private static PatientEncounterDetailsResponse CompleteEncounter() => new()
    {
        EncounterDateUtc = DateTime.UtcNow,
        EncounterType = "Office Visit",
        ProviderName = "Dr. Example",
        ReasonForVisit = "Follow-up",
        SubjectiveNote = "Patient reports improvement.",
        Status = EncounterStatuses.Draft
    };
}
