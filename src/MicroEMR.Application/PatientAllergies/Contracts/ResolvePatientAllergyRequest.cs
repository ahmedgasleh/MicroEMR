using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace MicroEMR.Application.PatientAllergies.Contracts;
public sealed class ResolvePatientAllergyRequest
{
    [StringLength(500)] public string? ResolveReason { get; set; }
    [Required] public string RowVersion { get; set; }="";
    [JsonIgnore] public string? RequestCorrelationId { get; set; }
}
