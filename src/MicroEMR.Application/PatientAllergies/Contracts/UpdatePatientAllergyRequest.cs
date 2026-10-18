using System.ComponentModel.DataAnnotations;
namespace MicroEMR.Application.PatientAllergies.Contracts;
public sealed class UpdatePatientAllergyRequest : AllergySaveRequest
{
    [Required,StringLength(30)] public string Status { get; set; }="Active";
    [Required] public string RowVersion { get; set; }="";
}
