using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
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
 public PrescriptionOrderFields? OrderFields { get; set; }
 private string? privateNote;
 [JsonIgnore,StringLength(1000)] public string? PrivateNote { get => privateNote; set { privateNote=value; PrivateNoteSpecified=true; } }
 [JsonIgnore] public bool PrivateNoteSpecified { get; private set; }
 [JsonPropertyName("privateNote"),JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)]
 public JsonElement PrivateNoteInput
 {
  get => PrivateNoteSpecified ? JsonSerializer.SerializeToElement(PrivateNote) : default;
  set
  {
   if(value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))throw new JsonException("Private note must be text or null.");
   PrivateNote=value.ValueKind==JsonValueKind.Null?null:value.GetString();
  }
 }
 [JsonIgnore] public string? PrivateNoteCorrelationId { get; internal set; }
 private string? genericName;
 [JsonIgnore,StringLength(300)] public string? GenericName { get => genericName; set { genericName=value; GenericNameSpecified=true; } }
 [JsonIgnore] public bool GenericNameSpecified { get; private set; }
 // Undefined is omitted on the wire; explicit JSON null clears a draft value.
 // Preserve that distinction through the Web API client as well as incoming JSON.
 [JsonPropertyName("genericName"),JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)]
 public JsonElement GenericNameInput
 {
  get => GenericNameSpecified ? JsonSerializer.SerializeToElement(GenericName) : default;
  set
  {
   if(value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))throw new JsonException("Generic name must be text or null.");
   GenericName=value.ValueKind==JsonValueKind.Null?null:value.GetString();
  }
 }
 [StringLength(100)] public string? MedicationForm{get;set;}
 [StringLength(1000)] public string? NotesToPharmacist{get;set;}
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
 [Range(1,int.MaxValue)] public int? InitialDaysSupply{get;set;}
 [Range(typeof(decimal),"0.001","999999999999999.999")] public decimal? RefillQuantity{get;set;}
 [StringLength(50)] public string? RefillQuantityUnit{get;set;}
 [Range(1,int.MaxValue)] public int? RefillDaysSupply{get;set;}
 [Range(0,int.MaxValue)] public int AuthorizedRepeats{get;set;}
 [StringLength(500)] public string? Indication{get;set;}
 private DateOnly? prescribedDate, startDate;
 [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
 public DateOnly? PrescribedDate { get => prescribedDate; set { prescribedDate=value; PrescribedDateSpecified=true; } }
 [JsonIgnore] public bool PrescribedDateSpecified { get; private set; }
 [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public PrescriptionDateFields? DateFields { get; set; }
 [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public DateOnly? StartDate { get => startDate; set { startDate=value; StartDateSpecified=true; } }
 [JsonIgnore] public bool StartDateSpecified { get; private set; }
 public string? RowVersion{get;set;}
 public IEnumerable<ValidationResult> ValidateDispensingInstructions()
 {
  if(InitialDaysSupply is <=0)yield return new("Initial days' supply must be positive when supplied.",[nameof(InitialDaysSupply)]);
  if(RefillDaysSupply is <=0)yield return new("Refill days' supply must be positive when supplied.",[nameof(RefillDaysSupply)]);
  if(RefillQuantity.HasValue && (RefillQuantity.Value<0.001m || RefillQuantity.Value>999999999999999.999m || decimal.Round(RefillQuantity.Value,3)!=RefillQuantity.Value))yield return new("Refill quantity must be positive and have at most three decimal places.",[nameof(RefillQuantity)]);
  if(RefillQuantity.HasValue!=(!string.IsNullOrWhiteSpace(RefillQuantityUnit)))yield return new("Refill quantity and unit must be supplied together.",[nameof(RefillQuantity),nameof(RefillQuantityUnit)]);
  if(RefillQuantityUnit?.Trim().Length>50)yield return new("Refill quantity unit must be 50 characters or fewer.",[nameof(RefillQuantityUnit)]);
  if(AuthorizedRepeats==0 && (RefillQuantity.HasValue || !string.IsNullOrWhiteSpace(RefillQuantityUnit) || RefillDaysSupply.HasValue))yield return new("Remove refill instructions or authorize at least one refill.",[nameof(AuthorizedRepeats),nameof(RefillQuantity),nameof(RefillQuantityUnit),nameof(RefillDaysSupply)]);
 }
 public IEnumerable<ValidationResult> ValidateDates()
 {
  if(DateFields is not null)
  {
   foreach(var result in DateFields.Validate()) yield return result;
   if(PrescribedDateSpecified || StartDateSpecified) yield return new("Supply structured dates or legacy dates, not both.");
  }
  else if((this is CreatePrescriptionDraftRequest || PrescribedDateSpecified) && (PrescribedDate is null || PrescribedDate==default(DateOnly)))
   yield return new("Prescribed date is required.",[nameof(PrescribedDate)]);
 }
 public IEnumerable<ValidationResult> Validate(ValidationContext _)
 {
 if(PrescriptionTextContract.Error(ProductDisplayText) is {} descriptionError) yield return new(descriptionError,[nameof(ProductDisplayText)]);
 if(PrescriptionTextContract.Error(OrderFields?.TreatmentType,false) is {} treatmentError) yield return new(treatmentError,[nameof(OrderFields)]);
  foreach(var result in ValidateDispensingInstructions())yield return result;
  if((StrengthValue.HasValue)!=(!string.IsNullOrWhiteSpace(StrengthUnit)))yield return new("Strength value and unit must be supplied together.",[nameof(StrengthValue),nameof(StrengthUnit)]);
  if((DoseAmount.HasValue)!=(!string.IsNullOrWhiteSpace(DoseUnit)))yield return new("Dose amount and unit must be supplied together.",[nameof(DoseAmount),nameof(DoseUnit)]);
  if(string.IsNullOrWhiteSpace(ProductIdentifierNamespace)!=string.IsNullOrWhiteSpace(ProductIdentifierValue))yield return new("Product identifier namespace and value must be supplied together.",[nameof(ProductIdentifierNamespace),nameof(ProductIdentifierValue)]);
  if(!PrescriptionFrequencies.Values.ContainsKey(FrequencyCode))yield return new("Frequency code is not approved.",[nameof(FrequencyCode)]);
  foreach (var error in ValidateDates()) yield return error;
 }
}
public sealed class CreatePrescriptionDraftRequest:PrescriptionDraftRequest { public Guid? SupersedesPrescriptionUid{get;set;} }
public sealed class PrescriptionTransitionRequest { [Required] public string RowVersion{get;set;}=""; [StringLength(500)] public string? Reason{get;set;} }
public sealed class PatientPrescriptionResponse
{
 public bool? SubstitutionProhibited {get;set;} public string? TreatmentType {get;set;} public string? PrescriberFirstNameSnapshot {get;set;} public string? PrescriberLastNameSnapshot {get;set;} public string? PrescriberOhipSnapshot {get;set;}
 public string? GenericName { get; set; }
 public string? MedicationForm{get;set;} public string? NotesToPharmacist{get;set;}
 public Guid PrescriptionUid{get;set;} public Guid PatientUid{get;set;} public string Status{get;set;}=""; public string ProductName{get;set;}=""; public string? ProductIdentifierNamespace{get;set;} public string? ProductIdentifierValue{get;set;} public string ProductDisplayText{get;set;}="";
 public decimal? StrengthValue{get;set;} public string? StrengthUnit{get;set;} public decimal? DoseAmount{get;set;} public string? DoseUnit{get;set;} public string Route{get;set;}=""; public string FrequencyCode{get;set;}=""; public string FrequencyDisplay{get;set;}=""; public bool Prn{get;set;} public string Directions{get;set;}=""; public decimal Quantity{get;set;} public string QuantityUnit{get;set;}=""; public int? InitialDaysSupply{get;set;} public decimal? RefillQuantity{get;set;} public string? RefillQuantityUnit{get;set;} public int? RefillDaysSupply{get;set;} public int AuthorizedRepeats{get;set;} public string? Indication{get;set;} public DateOnly? PrescribedDate{get;set;} public DateOnly? StartDate{get;set;}
 public PrescriptionDateFields? DateFields { get; set; }
 public string PrescribedDateDisplay => DateFields?.WrittenDate?.Display ?? PrescribedDate?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture) ?? "Not recorded";
 public string StartDateDisplay => DateFields?.StartDate?.Display ?? StartDate?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture) ?? "Not recorded";
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
public sealed class PatientPrescriptionService(IPatientPrescriptionRepository repository,IPdfRenderer pdf, IPrescriptionPrivateNoteService? privateNotes=null):IPatientPrescriptionService
{
 public Task<IReadOnlyList<PatientPrescriptionResponse>> ListAsync(Guid p,CancellationToken t=default)=>repository.ListAsync(p,t); public Task<PatientPrescriptionResponse?> GetAsync(Guid p,Guid u,CancellationToken t=default)=>repository.GetAsync(p,u,t);
 public async Task<PatientPrescriptionResponse> CreateAsync(Guid p,CreatePrescriptionDraftRequest r,long a,CancellationToken t=default){await AuthorizePrivateInput(r,a,t);Normalize(r);return await repository.CreateAsync(p,r,a,t);} public async Task<PatientPrescriptionResponse?> UpdateAsync(Guid p,Guid u,PrescriptionDraftRequest r,long a,CancellationToken t=default){await AuthorizePrivateInput(r,a,t);if(string.IsNullOrWhiteSpace(r.RowVersion)||!Convert.TryFromBase64String(r.RowVersion,new byte[8],out var tokenLength)||tokenLength!=8)throw new FormatException("A reviewed row version is required.");Normalize(r);return await repository.UpdateAsync(p,u,r,a,t);}
 public Task<PatientPrescriptionResponse?> FinalizeAsync(Guid p,Guid u,string v,long a,CancellationToken t=default)=>repository.FinalizeAsync(p,u,v,a,t); public Task<PatientPrescriptionResponse?> CancelAsync(Guid p,Guid u,string v,string? reason,long a,CancellationToken t=default)=>repository.CancelAsync(p,u,v,reason,a,t);
 public async Task<byte[]?> RenderArtifactPdfAsync(Guid p,Guid u,CancellationToken t=default){var a=await repository.GetArtifactAsync(p,u,t);if(a is null)return null;using var d=JsonDocument.Parse(a.Json);var x=d.RootElement;string G(string n)=>WebUtility.HtmlEncode(x.TryGetProperty(n,out var v)&&v.ValueKind!=JsonValueKind.Null?v.ToString():"");// Old snapshots keep their original layout. Never infer new instructions from existing quantity/directions.
 var dispensing=x.TryGetProperty("RefillInstructionsVersion",out _)
  ? $"<dt>Initial dispense quantity</dt><dd>{G("Quantity")} {G("QuantityUnit")}</dd><dt>Initial days' supply</dt><dd>{Recorded("InitialDaysSupply")}</dd><dt>Authorized refills</dt><dd>{G("AuthorizedRepeats")}</dd><dt>Refill quantity</dt><dd>{Recorded("RefillQuantity")} {G("RefillQuantityUnit")}</dd><dt>Refill days' supply</dt><dd>{Recorded("RefillDaysSupply")}</dd>"
  : $"<dt>Quantity / repeats</dt><dd>{G("Quantity")} {G("QuantityUnit")} / {G("AuthorizedRepeats")}</dd>";
 string Recorded(string n)=>x.TryGetProperty(n,out var value)&&value.ValueKind!=JsonValueKind.Null?G(n):"Not recorded";
 var html=$"<!doctype html><html><head><meta charset='utf-8'><style>body{{font-family:Arial;margin:36px}}h1{{border-bottom:2px solid #222}}dt{{font-weight:bold}}dd{{margin:0 0 12px}}</style></head><body><h1>Prescription</h1><dl><dt>Patient</dt><dd>{G("PatientName")} (DOB {G("DateOfBirth")})</dd><dt>Prescribed date</dt><dd>{G("PrescribedDate")}</dd><dt>Product</dt><dd>{G("ProductDisplayText")}</dd>{(x.TryGetProperty("GenericNameVersion",out _)?$"<dt>Generic name</dt><dd>{Recorded("GenericName")}</dd>":"")}<dt>Directions</dt><dd>{G("Directions")}</dd><dt>Route / frequency</dt><dd>{G("Route")} / {G("FrequencyDisplay")}{(x.TryGetProperty("Prn",out var prn)&&prn.GetBoolean()?" / As needed":"")}</dd>{dispensing}<dt>Indication</dt><dd>{G("Indication")}</dd><dt>Prescriber</dt><dd>{G("PrescriberDisplayName")} — {G("PrescriberCredential")}</dd><dt>Prescription ID</dt><dd>{G("PrescriptionUid")}</dd></dl><p>Local structured prescription. Not electronically transmitted.</p></body></html>";return await pdf.RenderAsync(html,t);}
 private async Task AuthorizePrivateInput(PrescriptionDraftRequest r,long actor,CancellationToken token)
 {
  if(!r.PrivateNoteSpecified)return;
  if(privateNotes is null)throw new PrescriptionPrivateNoteAccessException();
  await privateNotes.AuthorizeWriteAsync(actor,token);
  r.PrivateNoteCorrelationId=privateNotes.CorrelationId;
 }
 private static void Normalize(PrescriptionDraftRequest r){PrescriptionTextContract.Validate(r.ProductDisplayText);PrescriptionTextContract.Validate(r.OrderFields?.TreatmentType,false);if(r.OrderFields is {} fields)fields.TreatmentType=string.IsNullOrWhiteSpace(fields.TreatmentType)?null:fields.TreatmentType.Trim();if(r.PrivateNote?.Length>1000)throw new ArgumentException("Private note exceeds the supported length.");if(r.PrivateNoteSpecified)r.PrivateNote=string.IsNullOrWhiteSpace(r.PrivateNote)?null:r.PrivateNote.Trim();if(r.GenericName?.Length>300)throw new ArgumentException("Generic name must be 300 characters or fewer.");if(r.GenericNameSpecified)r.GenericName=string.IsNullOrWhiteSpace(r.GenericName)?null:r.GenericName.Trim();var dateErrors=r.ValidateDates().ToArray();if(dateErrors.Length>0)throw new ArgumentException(dateErrors[0].ErrorMessage);if(r.MedicationForm?.Length>100 || r.NotesToPharmacist?.Length>1000)throw new ArgumentException("Prescription form or pharmacist notes exceed the supported length.");var errors=r.ValidateDispensingInstructions().ToArray();if(errors.Length>0)throw new ArgumentException(errors[0].ErrorMessage);r.RefillQuantityUnit=string.IsNullOrWhiteSpace(r.RefillQuantityUnit)?null:r.RefillQuantityUnit.Trim();r.ProductName=r.ProductName.Trim();r.ProductDisplayText=r.ProductDisplayText.Trim();r.Route=r.Route.Trim();r.Directions=r.Directions.Trim();r.QuantityUnit=r.QuantityUnit.Trim();if(!PrescriptionFrequencies.Values.TryGetValue(r.FrequencyCode,out _))throw new ArgumentException("Frequency code is not approved.");}
}
public sealed class PatientPrescriptionConcurrencyException(string message,Exception? inner=null):Exception(message,inner);
public sealed class PrescriberProviderMappingException(Exception? inner=null):Exception("Your account must be linked to an active Provider before you can prescribe. Ask a clinic administrator to link your user in Providers.",inner);
