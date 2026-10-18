using System.ComponentModel.DataAnnotations;
namespace MicroEMR.Web.Models.PatientAllergies;
public sealed class ResolvePatientAllergyRequest
{ [Required] public string RowVersion {get;set;}=""; [StringLength(500)] public string? ResolveReason { get; set; } }
