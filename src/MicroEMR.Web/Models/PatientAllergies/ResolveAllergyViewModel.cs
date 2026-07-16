using System.ComponentModel.DataAnnotations;
namespace MicroEMR.Web.Models.PatientAllergies;
public sealed class ResolveAllergyViewModel
{ public Guid PatientUid { get; set; } public Guid AllergyUid { get; set; } [StringLength(500)] public string? ResolveReason { get; set; } }
