using MicroEMR.Application.PatientAllergies.Contracts;
namespace MicroEMR.Web.Models.PatientAllergies;
public sealed class CreatePatientAllergyRequest : AllergySaveRequest
{ public bool ConfirmReplaceNoKnownAllergies {get;set;} public Guid? ExpectedAssertionUid {get;set;} public string? ExpectedAssertionRowVersion {get;set;} }
