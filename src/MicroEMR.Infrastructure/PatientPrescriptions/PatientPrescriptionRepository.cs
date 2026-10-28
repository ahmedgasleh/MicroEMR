using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.PatientPrescriptions;

public sealed partial class PatientPrescriptionRepository(ITenantSqlConnectionFactory connections):IPatientPrescriptionRepository
{
 public async Task<IReadOnlyList<PatientPrescriptionResponse>> ListAsync(Guid patientUid,CancellationToken token=default){await using var c=await connections.OpenConnectionAsync(token);await using var q=Cmd(c,"dbo.PatientPrescription_GetByPatientUid");Uid(q,"@PatientUid",patientUid);await using var r=await q.ExecuteReaderAsync(token);var x=new List<PatientPrescriptionResponse>();while(await r.ReadAsync(token))x.Add(Map(r));return x;}
 public async Task<PatientPrescriptionResponse?> GetAsync(Guid p,Guid u,CancellationToken t=default)=>await One("dbo.PatientPrescription_GetByUid",p,u,null,null,t);
 public async Task<PatientPrescriptionResponse> CreateAsync(Guid p,CreatePrescriptionDraftRequest r,long a,CancellationToken t=default)=>await Mutate("dbo.PatientPrescription_CreateDraft",p,null,r,a,null,r.SupersedesPrescriptionUid,t)??throw new InvalidOperationException("Prescription creation returned no record.");
 public async Task<PatientPrescriptionResponse?> UpdateAsync(Guid p,Guid u,PrescriptionDraftRequest r,long a,CancellationToken t=default)=>await Mutate("dbo.PatientPrescription_UpdateDraft",p,u,r,a,r.RowVersion,null,t);
 public async Task<PatientPrescriptionResponse?> FinalizeAsync(Guid p,Guid u,string v,long a,CancellationToken t=default)=>await One("dbo.PatientPrescription_Finalize",p,u,v,(q)=>Actor(q,a),t);
 public async Task<PatientPrescriptionResponse?> CancelAsync(Guid p,Guid u,string v,string? reason,long a,CancellationToken t=default)=>await One("dbo.PatientPrescription_Cancel",p,u,v,q=>{Actor(q,a);Str(q,"@Reason",reason,500);},t);
 public async Task<PrescriptionArtifact?> GetArtifactAsync(Guid p,Guid u,CancellationToken t=default){await using var c=await connections.OpenConnectionAsync(t);await using var q=Cmd(c,"dbo.PatientPrescription_GetArtifact");Uid(q,"@PatientUid",p);Uid(q,"@PrescriptionUid",u);await using var r=await q.ExecuteReaderAsync(t);return await r.ReadAsync(t)?new(r.GetGuid(0),r.GetString(2)):null;}
 private async Task<PatientPrescriptionResponse?> Mutate(string sp,Guid p,Guid? u,PrescriptionDraftRequest r,long a,string? version,Guid? supersedes,CancellationToken t){if(r.OrderFields?.ClinicalError() is {} clinicalError)throw new ArgumentException(clinicalError);PrescriptionTextContract.Validate(r.ProductDisplayText);PrescriptionTextContract.Validate(r.OrderFields?.TreatmentType,false);await using var c=await connections.OpenConnectionAsync(t);await using var q=Cmd(c,sp);Uid(q,"@PatientUid",p);if(u.HasValue)Uid(q,"@PrescriptionUid",u.Value);if(r.OrderFields is not null)Str(q,"@OrderFields",JsonSerializer.Serialize(r.OrderFields),-1);Str(q,"@ProductName",r.ProductName,200,false);Str(q,"@ProductIdentifierNamespace",r.ProductIdentifierNamespace,100);Str(q,"@ProductIdentifierValue",r.ProductIdentifierValue,100);Str(q,"@ProductDisplayText",r.ProductDisplayText,300,false);Dec(q,"@StrengthValue",r.StrengthValue,18,6);Str(q,"@StrengthUnit",r.StrengthUnit,50);Dec(q,"@DoseAmount",r.DoseAmount,18,6);Str(q,"@DoseUnit",r.DoseUnit,50);Str(q,"@Route",r.Route,100,false);Str(q,"@FrequencyCode",r.FrequencyCode,40,false);Str(q,"@FrequencyDisplay",PrescriptionFrequencies.Values[r.FrequencyCode],100,false);q.Parameters.Add("@Prn",SqlDbType.Bit).Value=r.Prn;Str(q,"@Directions",r.Directions,1000,false);Dec(q,"@Quantity",r.Quantity,18,3);Str(q,"@QuantityUnit",r.QuantityUnit,50,false);q.Parameters.Add("@InitialDaysSupply",SqlDbType.Int).Value=r.InitialDaysSupply??(object)DBNull.Value;Dec(q,"@RefillQuantity",r.RefillQuantity,18,3);Str(q,"@RefillQuantityUnit",r.RefillQuantityUnit,50);q.Parameters.Add("@RefillDaysSupply",SqlDbType.Int).Value=r.RefillDaysSupply??(object)DBNull.Value;q.Parameters.Add("@AuthorizedRepeats",SqlDbType.Int).Value=r.AuthorizedRepeats;Str(q,"@Indication",r.Indication,500);if(r.GenericName?.Length>300)throw new ArgumentException("Generic name must be 300 characters or fewer.");Str(q,"@GenericName",r.GenericName,300);q.Parameters.Add("@GenericNameSpecified",SqlDbType.Bit).Value=r.GenericNameSpecified;Str(q,"@MedicationForm",r.MedicationForm,100);Str(q,"@NotesToPharmacist",r.NotesToPharmacist,1000);q.Parameters.Add("@PrescribedDate",SqlDbType.Date).Value=r.PrescribedDate?.ToDateTime(TimeOnly.MinValue)??(object)DBNull.Value;q.Parameters.Add("@StartDate",SqlDbType.Date).Value=r.StartDate?.ToDateTime(TimeOnly.MinValue)??(object)DBNull.Value;Dates(q,r);Pickup(q,r);FacilityName(q,r);FacilityAddress(q,r);ReasonCoding(q,r);DispensingLocationReference(q,r);ProtocolReference(q,r);PrivateNote(q,r);Actor(q,a);if(supersedes.HasValue)Uid(q,"@SupersedesPrescriptionUid",supersedes.Value);else if(sp.EndsWith("CreateDraft",StringComparison.Ordinal))q.Parameters.Add("@SupersedesPrescriptionUid",SqlDbType.UniqueIdentifier).Value=DBNull.Value;if(version is not null)Version(q,version);try{await using var rd=await q.ExecuteReaderAsync(t);return await rd.ReadAsync(t)?Map(rd):null;}catch(SqlException e)when(e.Number==53028){throw new DispensingLocationReferenceValidationException("Dispensing location reference requires system and identifier, valid single-line Unicode and at most 200 UTF-16 units each.");}catch(SqlException e)when(e.Number==53026){throw new ProtocolReferenceValidationException("Protocol reference requires system and identifier, valid single-line Unicode and at most 200 UTF-16 units each.");}catch(SqlException e)when(e.Number==53024){throw new ReasonCodingValidationException("Coded reason requires system/code, optional version/display, valid single-line Unicode and approved length limits.");}catch(SqlException e)when(e.Number==53022){throw new FacilityAddressValidationException("Target dispensing facility address must be valid Unicode text of at most 1,000 UTF-16 code units, with only supported line breaks.");}catch(SqlException e)when(e.Number==53020){throw new FacilityNameValidationException("Target dispensing facility name must be a single line of at most 200 UTF-16 code units.");}catch(SqlException e)when(e.Number==53011){throw new PrescriptionPrivateNoteAccessException();}catch(SqlException e)when(e.Number==51502){throw new PrescriberProviderMappingException(e);}catch(SqlException e)when(e.Number is 51504 or 51505){throw new PatientPrescriptionConcurrencyException("The prescription changed or is no longer available. Reload and try again.",e);}}
 private async Task<PatientPrescriptionResponse?> One(string sp,Guid p,Guid u,string? version,Action<SqlCommand>? extra,CancellationToken t){await using var c=await connections.OpenConnectionAsync(t);await using var q=Cmd(c,sp);Uid(q,"@PatientUid",p);Uid(q,"@PrescriptionUid",u);if(version is not null)Version(q,version);extra?.Invoke(q);try{await using var r=await q.ExecuteReaderAsync(t);return await r.ReadAsync(t)?Map(r):null;}catch(SqlException e)when(e.Number==53029){throw new DispensingLocationReferenceReviewException();}catch(SqlException e)when(e.Number==53027){throw new ProtocolReferenceReviewException();}catch(SqlException e)when(e.Number==53025){throw new ReasonCodingReviewException();}catch(SqlException e)when(e.Number==53023){throw new FacilityAddressReviewException();}catch(SqlException e)when(e.Number==53021){throw new FacilityNameReviewException();}catch(SqlException e)when(e.Number==51502 && (sp=="dbo.PatientPrescription_Finalize" || sp=="dbo.PatientPrescription_SignAndFinalize")){throw new PrescriberProviderMappingException(e);}catch(SqlException e)when(e.Number is 51504 or 51505){throw new PatientPrescriptionConcurrencyException("The prescription changed or is no longer available. Reload and try again.",e);}}
 public Task<PatientPrescriptionResponse?> FinalizeReviewedAsync(Guid p,Guid u,string v,long a,bool reviewed,CancellationToken t=default)=>One("dbo.PatientPrescription_Finalize",p,u,v,q=>{Actor(q,a);if(reviewed)q.Parameters.Add("@PickupReviewed",SqlDbType.Bit).Value=true;},t);
 private static void Pickup(SqlCommand command,PrescriptionDraftRequest request)
 {
  if(!request.EarliestPickupSpecified)return;
  request.EarliestPickup?.ValidateShape();
  command.Parameters.Add("@EarliestPickupSpecified",SqlDbType.Bit).Value=true;
  command.Parameters.Add("@EarliestPickup",SqlDbType.NVarChar,-1).Value=request.EarliestPickup is null?DBNull.Value:JsonSerializer.Serialize(request.EarliestPickup);
 }
 private static void DispensingLocationReference(SqlCommand command,PrescriptionDraftRequest request)
 {
  if(request.DispensingLocationReferenceSpecified){
   var value=PrescriptionDispensingLocationReference.Normalize(request.DispensingLocationReference);
   command.Parameters.Add("@DispensingLocationReferenceSpecified",SqlDbType.Bit).Value=true;
   command.Parameters.Add("@DispensingLocationReference",SqlDbType.NVarChar,-1).Value=value is null?DBNull.Value:JsonSerializer.Serialize(value);
  }
  if(request.DispensingLocationReferenceReviewed)command.Parameters.Add("@DispensingLocationReferenceReviewed",SqlDbType.Bit).Value=true;
 }
 private static void ProtocolReference(SqlCommand command,PrescriptionDraftRequest request)
 {
  if(request.ProtocolReferenceSpecified){
   var value=PrescriptionProtocolReference.Normalize(request.ProtocolReference);
   command.Parameters.Add("@ProtocolReferenceSpecified",SqlDbType.Bit).Value=true;
   command.Parameters.Add("@ProtocolReference",SqlDbType.NVarChar,-1).Value=value is null?DBNull.Value:JsonSerializer.Serialize(value);
  }
  if(request.ProtocolReferenceReviewed)command.Parameters.Add("@ProtocolReferenceReviewed",SqlDbType.Bit).Value=true;
 }
 private static void ReasonCoding(SqlCommand command,PrescriptionDraftRequest request)
 {
  if(request.ReasonCodingSpecified){
   var value=PrescriptionReasonCoding.Normalize(request.ReasonCoding);
   command.Parameters.Add("@ReasonCodingSpecified",SqlDbType.Bit).Value=true;
   command.Parameters.Add("@ReasonCoding",SqlDbType.NVarChar,-1).Value=value is null?DBNull.Value:JsonSerializer.Serialize(value);
  }
  if(request.ReasonCodingReviewed)command.Parameters.Add("@ReasonCodingReviewed",SqlDbType.Bit).Value=true;
 }
 private static void FacilityAddress(SqlCommand command,PrescriptionDraftRequest request)
 {
  if(request.TargetDispensingFacilityAddressSpecified)
  {
   command.Parameters.Add("@TargetDispensingFacilityAddressSpecified",SqlDbType.Bit).Value=true;
   command.Parameters.Add("@TargetDispensingFacilityAddress",SqlDbType.NVarChar,-1).Value=
    TargetDispensingFacilityAddress.Normalize(request.TargetDispensingFacilityAddress) is {} name?name:DBNull.Value;
  }
  if(request.FacilityAddressReviewed)command.Parameters.Add("@FacilityAddressReviewed",SqlDbType.Bit).Value=true;
 }
 private static void FacilityName(SqlCommand command,PrescriptionDraftRequest request)
 {
  if(request.TargetDispensingFacilityNameSpecified)
  {
   command.Parameters.Add("@TargetDispensingFacilityNameSpecified",SqlDbType.Bit).Value=true;
   command.Parameters.Add("@TargetDispensingFacilityName",SqlDbType.NVarChar,-1).Value=
    TargetDispensingFacilityName.Normalize(request.TargetDispensingFacilityName) is {} name?name:DBNull.Value;
  }
  if(request.FacilityNameReviewed)command.Parameters.Add("@FacilityNameReviewed",SqlDbType.Bit).Value=true;
 }
 private static void PrivateNote(SqlCommand q,PrescriptionDraftRequest r)
 {
  // Legacy callers and pre-feature isolated fixtures retain their original contract.
  if(!r.PrivateNoteSpecified)return;
  if(r.PrivateNote?.Length>1000)throw new ArgumentException("Private note exceeds the supported length.");
  q.Parameters.Add("@PrivateNoteSpecified",SqlDbType.Bit).Value=true;
  q.Parameters.Add("@PrivateNote",SqlDbType.NVarChar,-1).Value=string.IsNullOrWhiteSpace(r.PrivateNote)?DBNull.Value:r.PrivateNote.Trim();
  q.Parameters.Add("@PrivateNoteCorrelationId",SqlDbType.NVarChar,100).Value=r.PrivateNoteCorrelationId??throw new PrescriptionPrivateNoteAccessException();
 }
 private static string? ReadOptionalString(SqlDataReader reader,string name) { for(var i=0;i<reader.FieldCount;i++)if(reader.GetName(i)==name)return reader.IsDBNull(i)?null:reader.GetString(i);return null; }
 private static PrescriptionDateFields? ReadDates(SqlDataReader reader)
 {
  // Older isolated fixtures and pre-upgrade full-date reads do not project this column.
  for(var i=0;i<reader.FieldCount;i++)
   if(reader.GetName(i)=="DateFields") return reader.IsDBNull(i)?null:JsonSerializer.Deserialize<PrescriptionDateFields>(reader.GetString(i));
  return null;
 }
 private static void Dates(SqlCommand command,PrescriptionDraftRequest request)
 {
  command.Parameters.Add("@DateFields",SqlDbType.NVarChar,-1).Value=request.DateFields is null?DBNull.Value:JsonSerializer.Serialize(request.DateFields);
  command.Parameters.Add("@PrescribedDateSpecified",SqlDbType.Bit).Value=request.PrescribedDateSpecified;
  command.Parameters.Add("@StartDateSpecified",SqlDbType.Bit).Value=request.StartDateSpecified;
 }
 private static SqlCommand Cmd(SqlConnection c,string sp)=>new(sp,c){CommandType=CommandType.StoredProcedure};private static void Uid(SqlCommand q,string n,Guid v)=>q.Parameters.Add(n,SqlDbType.UniqueIdentifier).Value=v;private static void Actor(SqlCommand q,long a)=>q.Parameters.Add("@ActorUserId",SqlDbType.BigInt).Value=a;private static void Version(SqlCommand q,string v){var bytes=Convert.FromBase64String(v);if(bytes.Length!=8)throw new FormatException("A reviewed eight-byte row version is required.");q.Parameters.Add("@RowVersion",SqlDbType.VarBinary,-1).Value=bytes;}private static void Str(SqlCommand q,string n,string? v,int s,bool nullable=true)=>q.Parameters.Add(n,SqlDbType.NVarChar,s).Value=string.IsNullOrWhiteSpace(v)&&nullable?DBNull.Value:v!.Trim();private static void Dec(SqlCommand q,string n,decimal? v,byte p,byte s){var x=q.Parameters.Add(n,SqlDbType.Decimal);x.Precision=p;x.Scale=s;x.Value=v??(object)DBNull.Value;}
 private static int? ReadOptionalInt(SqlDataReader r,string name){for(var i=0;i<r.FieldCount;i++)if(r.GetName(i)==name)return r.IsDBNull(i)?null:r.GetInt32(i);return null;}
 private static bool? ReadOptionalBoolean(SqlDataReader r,string name){for(var i=0;i<r.FieldCount;i++)if(r.GetName(i)==name)return r.IsDBNull(i)?null:r.GetBoolean(i);return null;}
 private static PatientPrescriptionResponse Map(SqlDataReader r){T? N<T>(string n)where T:struct{var i=r.GetOrdinal(n);return r.IsDBNull(i)?null:r.GetFieldValue<T>(i);}string? S(string n){var i=r.GetOrdinal(n);return r.IsDBNull(i)?null:r.GetString(i);}return new(){DispensingLocationReference=ReadOptionalString(r,"DispensingLocationReference") is {} dispensingLocation?JsonSerializer.Deserialize<PrescriptionDispensingLocationReference>(dispensingLocation):null,DispensingLocationReferenceReviewed=ReadOptionalBoolean(r,"DispensingLocationReferenceReviewed")==true,DispensingLocationReferenceReviewRequired=ReadOptionalBoolean(r,"DispensingLocationReferenceReviewRequired")==true,ProtocolReference=ReadOptionalString(r,"ProtocolReference") is {} protocol?JsonSerializer.Deserialize<PrescriptionProtocolReference>(protocol):null,ProtocolReferenceReviewed=ReadOptionalBoolean(r,"ProtocolReferenceReviewed")==true,ProtocolReferenceReviewRequired=ReadOptionalBoolean(r,"ProtocolReferenceReviewRequired")==true,ReasonCoding=ReadOptionalString(r,"ReasonCoding") is {} reason?JsonSerializer.Deserialize<PrescriptionReasonCoding>(reason):null,ReasonCodingReviewed=ReadOptionalBoolean(r,"ReasonCodingReviewed")==true,ReasonCodingReviewRequired=ReadOptionalBoolean(r,"ReasonCodingReviewRequired")==true,TargetDispensingFacilityAddress=ReadOptionalString(r,"TargetDispensingFacilityAddress"),FacilityAddressReviewed=ReadOptionalBoolean(r,"FacilityAddressReviewed")==true,FacilityAddressReviewRequired=ReadOptionalBoolean(r,"FacilityAddressReviewRequired")==true,TargetDispensingFacilityName=ReadOptionalString(r,"TargetDispensingFacilityName"),FacilityNameReviewed=ReadOptionalBoolean(r,"FacilityNameReviewed")==true,FacilityNameReviewRequired=ReadOptionalBoolean(r,"FacilityNameReviewRequired")==true,EarliestPickup=ReadOptionalString(r,"EarliestPickup") is {} pickup?JsonSerializer.Deserialize<EarliestPickup>(pickup):null,PickupReviewRequired=ReadOptionalBoolean(r,"PickupReviewRequired")==true,PaperCopyRequiredBeforeDispense=ReadOptionalBoolean(r,"PaperCopyRequiredBeforeDispense"),MinimumDispenseIntervalDays=ReadOptionalInt(r,"MinimumDispenseIntervalDays"),SubstitutionProhibited=ReadOptionalBoolean(r,"SubstitutionProhibited"),TreatmentType=ReadOptionalString(r,"TreatmentType"),PrescriberFirstNameSnapshot=ReadOptionalString(r,"PrescriberFirstNameSnapshot"),PrescriberLastNameSnapshot=ReadOptionalString(r,"PrescriberLastNameSnapshot"),PrescriberOhipSnapshot=ReadOptionalString(r,"PrescriberOhipSnapshot"),GenericName=ReadOptionalString(r,"GenericName"),MedicationForm=S("MedicationForm"),NotesToPharmacist=S("NotesToPharmacist"),PrescriptionUid=r.GetGuid(r.GetOrdinal("PrescriptionUid")),PatientUid=r.GetGuid(r.GetOrdinal("PatientUid")),Status=S("Status")!,ProductName=S("ProductName")!,ProductIdentifierNamespace=S("ProductIdentifierNamespace"),ProductIdentifierValue=S("ProductIdentifierValue"),ProductDisplayText=S("ProductDisplayText")!,StrengthValue=N<decimal>("StrengthValue"),StrengthUnit=S("StrengthUnit"),DoseAmount=N<decimal>("DoseAmount"),DoseUnit=S("DoseUnit"),Route=S("Route")!,FrequencyCode=S("FrequencyCode")!,FrequencyDisplay=S("FrequencyDisplay")!,Prn=r.GetBoolean(r.GetOrdinal("Prn")),Directions=S("Directions")!,Quantity=r.GetDecimal(r.GetOrdinal("Quantity")),QuantityUnit=S("QuantityUnit")!,InitialDaysSupply=N<int>("InitialDaysSupply"),RefillQuantity=N<decimal>("RefillQuantity"),RefillQuantityUnit=S("RefillQuantityUnit"),RefillDaysSupply=N<int>("RefillDaysSupply"),AuthorizedRepeats=r.GetInt32(r.GetOrdinal("AuthorizedRepeats")),Indication=S("Indication"),DateFields=ReadDates(r),PrescribedDate=N<DateTime>("PrescribedDate") is {} wd?DateOnly.FromDateTime(wd):null,StartDate=N<DateTime>("StartDate") is { } sd?DateOnly.FromDateTime(sd):null,CreatedBy=r.GetInt64(r.GetOrdinal("CreatedBy")),CreatedAtUtc=r.GetDateTime(r.GetOrdinal("CreatedAtUtc")),UpdatedBy=N<long>("UpdatedBy"),UpdatedAtUtc=N<DateTime>("UpdatedAtUtc"),PrescriberUserId=r.GetInt64(r.GetOrdinal("PrescriberUserId")),PrescriberProviderUid=r.GetGuid(r.GetOrdinal("PrescriberProviderUid")),PrescriberDisplayNameSnapshot=S("PrescriberDisplayNameSnapshot"),PrescriberCredentialSnapshot=S("PrescriberCredentialSnapshot"),ProductDisplaySnapshot=S("ProductDisplaySnapshot"),FinalizedBy=N<long>("FinalizedBy"),FinalizedAtUtc=N<DateTime>("FinalizedAtUtc"),CancelledBy=N<long>("CancelledBy"),CancelledAtUtc=N<DateTime>("CancelledAtUtc"),CancellationReason=S("CancellationReason"),SupersedesPrescriptionUid=N<Guid>("SupersedesPrescriptionUid"),SupersededByPrescriptionUid=N<Guid>("SupersededByPrescriptionUid"),ArtifactUid=N<Guid>("ArtifactUid"),RowVersion=Convert.ToBase64String((byte[])r["RowVersion"])};}
}
