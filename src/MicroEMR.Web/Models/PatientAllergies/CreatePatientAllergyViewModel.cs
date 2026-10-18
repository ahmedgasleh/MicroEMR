using MicroEMR.Application.PatientAllergies.Contracts;
namespace MicroEMR.Web.Models.PatientAllergies;
public sealed class CreatePatientAllergyViewModel : AllergySaveRequest
{
 public Guid PatientUid {get;set;} public string? ReturnTab {get;set;}
 public bool NoKnownAllergiesActive {get;set;} public bool ConfirmReplaceNoKnownAllergies {get;set;}
 public Guid? ExpectedAssertionUid {get;set;} public string? ExpectedAssertionRowVersion {get;set;}
 public bool ClinicalFieldsIncluded {get;set;}
}
