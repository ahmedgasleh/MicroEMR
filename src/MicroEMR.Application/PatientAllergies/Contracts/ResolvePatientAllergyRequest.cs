using System.ComponentModel.DataAnnotations;
namespace MicroEMR.Application.PatientAllergies.Contracts;
public sealed class ResolvePatientAllergyRequest
{ [StringLength(500)] public string? ResolveReason { get; set; } }
