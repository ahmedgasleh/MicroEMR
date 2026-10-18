using System.ComponentModel.DataAnnotations;
using MicroEMR.Application.PatientAllergies.Contracts;
namespace MicroEMR.Web.Models.PatientAllergies;
public sealed class EditPatientAllergyViewModel : AllergySaveRequest
{
 public Guid PatientUid {get;set;} public Guid AllergyUid {get;set;}
 [Required,StringLength(30)] public string Status {get;set;}="Active";
 [Required] public string RowVersion {get;set;}="";
 public bool ClinicalFieldsIncluded {get;set;}
}
