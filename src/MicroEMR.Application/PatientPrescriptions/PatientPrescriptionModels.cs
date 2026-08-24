using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using System.Text.Json;
using MicroEMR.Application.ClinicalOutput;

namespace MicroEMR.Application.PatientPrescriptions;

public static class PrescriptionStatuses { public const string Draft="Draft", Finalized="Finalized", Cancelled="Cancelled", Superseded="Superseded"; }
public static class PrescriptionFrequencies
{
    public static readonly IReadOnlyDictionary<string,string> Values=new Dictionary<string,string>(StringComparer.Ordinal)
    { ["ONCE"]="Once",["ONCE_DAILY"]="Once daily",["TWICE_DAILY"]="Twice daily",["THREE_TIMES_DAILY"]="Three times daily",["FOUR_TIMES_DAILY"]="Four times daily",["EVERY_MORNING"]="Every morning",["EVERY_EVENING"]="Every evening",["AT_BEDTIME"]="At bedtime",["EVERY_4_HOURS"]="Every 4 hours",["EVERY_6_HOURS"]="Every 6 hours",["EVERY_8_HOURS"]="Every 8 hours",["EVERY_12_HOURS"]="Every 12 hours",["ONCE_WEEKLY"]="Once weekly",["OTHER"]="Other" };
}
public class PrescriptionDraftRequest:IValidatableObject
{
 [Required,StringLength(200)] public string ProductName{get;set;}="";
 [StringLength(100)] public string? ProductIdentifierNamespace{get;set;}
 [StringLength(100)] public string? ProductIdentifierValue{get;set;}
 [Required,StringLength(300)] public string ProductDisplayText{get;set;}="";
 [Range(typeof(decimal),"0.000001","999999999999.999999")] public decimal? StrengthValue{get;set;}
 [StringLength(50)] public string? StrengthUnit{get;set;}
 [Range(typeof(decimal),"0.000001","999999999999.999999")] public decimal? DoseAmount{get;set;}
 [StringLength(50)] public string? DoseUnit{get;set;}
 [Required,StringLength(100)] public string Route{get;set;}="";
 [Required,StringLength(40)] public string FrequencyCode{get;set;}="";
 public bool Prn{get;set;}
 [Required,StringLength(1000)] public string Directions{get;set;}="";
 [Range(typeof(decimal),"0.001","999999999999999.999")] public decimal Quantity{get;set;}
 [Required,StringLength(50)] public string QuantityUnit{get;set;}="";
 [Range(0,int.MaxValue)] public int AuthorizedRepeats{get;set;}
 [StringLength(500)] public string? Indication{get;set;}
 public DateOnly PrescribedDate{get;set;}
 public DateOnly? StartDate{get;set;}
 public string? RowVersion{get;set;}
 public IEnumerable<ValidationResult> Validate(ValidationContext _)
 {
  if((StrengthValue.HasValue)!=(!string.IsNullOrWhiteSpace(StrengthUnit)))yield return new("Strength value and unit must be supplied together.",[nameof(StrengthValue),nameof(StrengthUnit)]);
  if((DoseAmount.HasValue)!=(!string.IsNullOrWhiteSpace(DoseUnit)))yield return new("Dose amount and unit must be supplied together.",[nameof(DoseAmount),nameof(DoseUnit)]);
  if(string.IsNullOrWhiteSpace(ProductIdentifierNamespace)!=string.IsNullOrWhiteSpace(ProductIdentifierValue))yield return new("Product identifier namespace and value must be supplied together.",[nameof(ProductIdentifierNamespace),nameof(ProductIdentifierValue)]);
  if(!PrescriptionFrequencies.Values.ContainsKey(FrequencyCode))yield return new("Frequency code is not approved.",[nameof(FrequencyCode)]);
  if(PrescribedDate==default)yield return new("Prescribed date is required.",[nameof(PrescribedDate)]);
 }
}
public sealed class CreatePrescriptionDraftRequest:PrescriptionDraftRequest { public Guid? SupersedesPrescriptionUid{get;set;} }
public sealed class PrescriptionTransitionRequest { [Required] public string RowVersion{get;set;}=""; [StringLength(500)] public string? Reason{get;set;} }
public sealed class PatientPrescriptionResponse
{
 public Guid PrescriptionUid{get;set;} public Guid PatientUid{get;set;} public string Status{get;set;}=""; public string ProductName{get;set;}=""; public string? ProductIdentifierNamespace{get;set;} public string? ProductIdentifierValue{get;set;} public string ProductDisplayText{get;set;}="";
 public decimal? StrengthValue{get;set;} public string? StrengthUnit{get;set;} public decimal? DoseAmount{get;set;} public string? DoseUnit{get;set;} public string Route{get;set;}=""; public string FrequencyCode{get;set;}=""; public string FrequencyDisplay{get;set;}=""; public bool Prn{get;set;} public string Directions{get;set;}=""; public decimal Quantity{get;set;} public string QuantityUnit{get;set;}=""; public int AuthorizedRepeats{get;set;} public string? Indication{get;set;} public DateOnly PrescribedDate{get;set;} public DateOnly? StartDate{get;set;}
 public long CreatedBy{get;set;} public DateTime CreatedAtUtc{get;set;} public long? UpdatedBy{get;set;} public DateTime? UpdatedAtUtc{get;set;} public long PrescriberUserId{get;set;} public Guid PrescriberProviderUid{get;set;} public string? PrescriberDisplayNameSnapshot{get;set;} public string? PrescriberCredentialSnapshot{get;set;} public string? ProductDisplaySnapshot{get;set;} public long? FinalizedBy{get;set;} public DateTime? FinalizedAtUtc{get;set;} public long? CancelledBy{get;set;} public DateTime? CancelledAtUtc{get;set;} public string? CancellationReason{get;set;} public Guid? SupersedesPrescriptionUid{get;set;} public Guid? SupersededByPrescriptionUid{get;set;} public Guid? ArtifactUid{get;set;} public string RowVersion{get;set;}="";
}
public sealed record PrescriptionArtifact(Guid ArtifactUid,string Json);
public interface IPatientPrescriptionRepository
{
 Task<IReadOnlyList<PatientPrescriptionResponse>> ListAsync(Guid patientUid,CancellationToken token=default); Task<PatientPrescriptionResponse?> GetAsync(Guid patientUid,Guid uid,CancellationToken token=default);
 Task<PatientPrescriptionResponse> CreateAsync(Guid patientUid,CreatePrescriptionDraftRequest request,long actor,CancellationToken token=default); Task<PatientPrescriptionResponse?> UpdateAsync(Guid patientUid,Guid uid,PrescriptionDraftRequest request,long actor,CancellationToken token=default);
 Task<PatientPrescriptionResponse?> FinalizeAsync(Guid patientUid,Guid uid,string rowVersion,long actor,CancellationToken token=default); Task<PatientPrescriptionResponse?> CancelAsync(Guid patientUid,Guid uid,string rowVersion,string? reason,long actor,CancellationToken token=default); Task<PrescriptionArtifact?> GetArtifactAsync(Guid patientUid,Guid uid,CancellationToken token=default);
}
public interface IPatientPrescriptionService
{
 Task<IReadOnlyList<PatientPrescriptionResponse>> ListAsync(Guid patientUid,CancellationToken token=default); Task<PatientPrescriptionResponse?> GetAsync(Guid patientUid,Guid uid,CancellationToken token=default); Task<PatientPrescriptionResponse> CreateAsync(Guid patientUid,CreatePrescriptionDraftRequest request,long actor,CancellationToken token=default); Task<PatientPrescriptionResponse?> UpdateAsync(Guid patientUid,Guid uid,PrescriptionDraftRequest request,long actor,CancellationToken token=default); Task<PatientPrescriptionResponse?> FinalizeAsync(Guid patientUid,Guid uid,string version,long actor,CancellationToken token=default); Task<PatientPrescriptionResponse?> CancelAsync(Guid patientUid,Guid uid,string version,string? reason,long actor,CancellationToken token=default); Task<byte[]?> RenderArtifactPdfAsync(Guid patientUid,Guid uid,CancellationToken token=default);
}
public sealed class PatientPrescriptionService(IPatientPrescriptionRepository repository,IPdfRenderer pdf):IPatientPrescriptionService
{
 public Task<IReadOnlyList<PatientPrescriptionResponse>> ListAsync(Guid p,CancellationToken t=default)=>repository.ListAsync(p,t); public Task<PatientPrescriptionResponse?> GetAsync(Guid p,Guid u,CancellationToken t=default)=>repository.GetAsync(p,u,t);
 public Task<PatientPrescriptionResponse> CreateAsync(Guid p,CreatePrescriptionDraftRequest r,long a,CancellationToken t=default){Normalize(r);return repository.CreateAsync(p,r,a,t);} public Task<PatientPrescriptionResponse?> UpdateAsync(Guid p,Guid u,PrescriptionDraftRequest r,long a,CancellationToken t=default){Normalize(r);return repository.UpdateAsync(p,u,r,a,t);}
 public Task<PatientPrescriptionResponse?> FinalizeAsync(Guid p,Guid u,string v,long a,CancellationToken t=default)=>repository.FinalizeAsync(p,u,v,a,t); public Task<PatientPrescriptionResponse?> CancelAsync(Guid p,Guid u,string v,string? reason,long a,CancellationToken t=default)=>repository.CancelAsync(p,u,v,reason,a,t);
 public async Task<byte[]?> RenderArtifactPdfAsync(Guid p,Guid u,CancellationToken t=default){var a=await repository.GetArtifactAsync(p,u,t);if(a is null)return null;using var d=JsonDocument.Parse(a.Json);var x=d.RootElement;string G(string n)=>WebUtility.HtmlEncode(x.TryGetProperty(n,out var v)&&v.ValueKind!=JsonValueKind.Null?v.ToString():"");var html=$"<!doctype html><html><head><meta charset='utf-8'><style>body{{font-family:Arial;margin:36px}}h1{{border-bottom:2px solid #222}}dt{{font-weight:bold}}dd{{margin:0 0 12px}}</style></head><body><h1>Prescription</h1><dl><dt>Patient</dt><dd>{G("PatientName")} (DOB {G("DateOfBirth")})</dd><dt>Prescribed date</dt><dd>{G("PrescribedDate")}</dd><dt>Product</dt><dd>{G("ProductDisplayText")}</dd><dt>Directions</dt><dd>{G("Directions")}</dd><dt>Route / frequency</dt><dd>{G("Route")} / {G("FrequencyDisplay")}{(x.TryGetProperty("Prn",out var prn)&&prn.GetBoolean()?" / As needed":"")}</dd><dt>Quantity / repeats</dt><dd>{G("Quantity")} {G("QuantityUnit")} / {G("AuthorizedRepeats")}</dd><dt>Indication</dt><dd>{G("Indication")}</dd><dt>Prescriber</dt><dd>{G("PrescriberDisplayName")} — {G("PrescriberCredential")}</dd><dt>Prescription ID</dt><dd>{G("PrescriptionUid")}</dd></dl><p>Local structured prescription. Not electronically transmitted.</p></body></html>";return await pdf.RenderAsync(html,t);}
 private static void Normalize(PrescriptionDraftRequest r){r.ProductName=r.ProductName.Trim();r.ProductDisplayText=r.ProductDisplayText.Trim();r.Route=r.Route.Trim();r.Directions=r.Directions.Trim();r.QuantityUnit=r.QuantityUnit.Trim();if(!PrescriptionFrequencies.Values.TryGetValue(r.FrequencyCode,out _))throw new ArgumentException("Frequency code is not approved.");}
}
public sealed class PatientPrescriptionConcurrencyException(string message,Exception? inner=null):Exception(message,inner);
