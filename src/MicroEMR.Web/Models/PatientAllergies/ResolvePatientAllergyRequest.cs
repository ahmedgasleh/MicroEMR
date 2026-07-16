using System.ComponentModel.DataAnnotations;
namespace MicroEMR.Web.Models.PatientAllergies;
public sealed class ResolvePatientAllergyRequest
{ [StringLength(500)] public string? ResolveReason { get; set; } }
