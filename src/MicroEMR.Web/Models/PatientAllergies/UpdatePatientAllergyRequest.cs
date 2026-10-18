using MicroEMR.Application.PatientAllergies.Contracts;
namespace MicroEMR.Web.Models.PatientAllergies;
public sealed class UpdatePatientAllergyRequest : AllergySaveRequest
{ [System.ComponentModel.DataAnnotations.Required] public string RowVersion {get;set;}=""; public string Status {get;set;}="Active"; }
