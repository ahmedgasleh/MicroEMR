using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientPrescriptions;
using Xunit;

namespace MicroEMR.Api.Tests;
public sealed class PatientPrescriptionFoundationTests
{
 private static string Root()=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..",".."));
 private static string Sql()=>File.ReadAllText(Path.Combine(Root(),"db","tenant-clinical","migrations","0050-patient-prescription-foundation.sql"));
 [Fact]public void Migration0050RemainsExactlyOnceAndIsFollowedBy0051(){using var d=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(),"db","tenant-clinical","manifest.json")));var ids=d.RootElement.EnumerateArray().Select(x=>x.GetProperty("migrationId").GetString()).ToArray();Assert.Equal("0050-patient-prescription-foundation",ids[^7]);Assert.Equal("0051-result-review-acknowledgement-hardening",ids[^6]);Assert.Equal("0052-cds-foundation",ids[^5]);Assert.Equal("0053-cdm-enrollment-foundation",ids[^4]);Assert.Equal("0054-results-provenance-correction-foundation",ids[^3]);Assert.Equal(1,ids.Count(x=>x=="0050-patient-prescription-foundation"));Assert.False(File.Exists(Path.Combine(Root(),"db","tenant-clinical","migrations","0051-patient-prescription-foundation.sql")));}
 [Fact]public void SeparateAggregateHasStatusConstraintsIndexesAndNoDelete(){var s=Sql();Assert.Contains("CREATE TABLE dbo.PatientPrescription",s);Assert.Contains("Draft',N'Finalized',N'Cancelled',N'Superseded",s);Assert.Contains("RowVersion ROWVERSION",s);Assert.Contains("IX_PatientPrescription_Patient_Status_Date",s);Assert.DoesNotContain("CREATE OR ALTER PROCEDURE dbo.PatientPrescription_Delete",s);}
 [Fact]public void ApprovedFrequencySetIsExactAndPrnSeparate(){var expected=new Dictionary<string,string>{{"ONCE","Once"},{"ONCE_DAILY","Once daily"},{"TWICE_DAILY","Twice daily"},{"THREE_TIMES_DAILY","Three times daily"},{"FOUR_TIMES_DAILY","Four times daily"},{"EVERY_MORNING","Every morning"},{"EVERY_EVENING","Every evening"},{"AT_BEDTIME","At bedtime"},{"EVERY_4_HOURS","Every 4 hours"},{"EVERY_6_HOURS","Every 6 hours"},{"EVERY_8_HOURS","Every 8 hours"},{"EVERY_12_HOURS","Every 12 hours"},{"ONCE_WEEKLY","Once weekly"},{"OTHER","Other"}};Assert.Equal(expected,PrescriptionFrequencies.Values);Assert.Contains("Prn BIT",Sql());}
 [Theory][InlineData("BAD")][InlineData("BID")][InlineData("PRN")]public void UnknownOrShorthandFrequencyRejected(string code){var x=Valid();x.FrequencyCode=code;Assert.Contains(Validate(x),e=>e.MemberNames.Contains(nameof(x.FrequencyCode)));}
 [Fact]public void PairQuantityRepeatAndDirectionsValidationWorks(){var x=Valid();x.StrengthValue=500;x.StrengthUnit=null;x.DoseAmount=0;x.Quantity=0;x.AuthorizedRepeats=-1;x.Directions="";var e=Validate(x);Assert.NotEmpty(e);}
 [Fact]public void PrescribingPermissionIsDedicatedAndKnown(){Assert.True(PermissionCatalog.IsKnown(PermissionKeys.PrescriptionsPrescribe));Assert.NotEqual(PermissionKeys.ClinicalDataManage,PermissionKeys.PrescriptionsPrescribe);}
 [Fact]public void ApiUsesPatientScopedCompoundRoutesAndDedicatedMutationPermission(){var methods=typeof(PatientPrescriptionsController).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.DeclaredOnly);Assert.All(methods.Where(x=>x.Name is "Create" or "Update" or "Finalize" or "Cancel" or "Correction"),m=>Assert.Contains(m.GetCustomAttributes<RequirePermissionAttribute>(),a=>a.Policy?.Contains(PermissionKeys.PrescriptionsPrescribe)==true));var routes=methods.SelectMany(m=>m.GetCustomAttributes().OfType<HttpMethodAttribute>()).Select(a=>a.Template).Where(x=>x is not null).ToArray();Assert.All(routes,r=>Assert.Contains("{patientUid:guid}/prescriptions",r));}
 [Fact]public void LifecycleAuditAndArtifactAreAtomicSqlContracts(){var s=Sql();foreach(var e in new[]{"PrescriptionDraftCreated","PrescriptionDraftUpdated","PrescriptionFinalized","PrescriptionCancelled","PrescriptionSuperseded"})Assert.Contains(e,s);Assert.Contains("BEGIN TRANSACTION",s);Assert.Contains("PatientPrescriptionArtifact",s);Assert.Contains("ArtifactJson",s);Assert.Contains("Status=N'Superseded'",s);Assert.Contains("IF @@ROWCOUNT<>1",s);}
 [Fact]public void ProviderIsRevalidatedAndActorCannotSpoofPrescriber(){var s=Sql();Assert.Contains("JOIN dbo.Provider p ON p.ProviderId=u.ProviderId AND p.IsActive=1",s);Assert.Contains("u.UserId=@ActorUserId AND u.IsActive=1",s);Assert.DoesNotContain("@PrescriberUserId",s);}
 [Fact]public void MedicationListIsNotMutated(){var s=Sql();Assert.DoesNotContain("UPDATE dbo.PatientMedication",s);Assert.DoesNotContain("INSERT dbo.PatientMedication",s);}
 [Fact]public void CompoundLookupProtectsEveryReadAndMutation(){var s=Sql();Assert.Contains("WHERE PatientUid=@PatientUid AND PrescriptionUid=@PrescriptionUid",s);Assert.Contains("a.PatientUid=@PatientUid AND a.PrescriptionUid=@PrescriptionUid",s);}
 private static CreatePrescriptionDraftRequest Valid()=>new(){ProductName="Acetaminophen",ProductDisplayText="Acetaminophen 500 mg tablet",StrengthValue=500,StrengthUnit="mg",DoseAmount=1,DoseUnit="tablet",Route="Oral",FrequencyCode="ONCE_DAILY",Directions="Take one tablet by mouth once daily.",Quantity=30,QuantityUnit="tablet",AuthorizedRepeats=0,PrescribedDate=DateOnly.FromDateTime(DateTime.Today)};
 private static List<ValidationResult> Validate(object x){var e=new List<ValidationResult>();System.ComponentModel.DataAnnotations.Validator.TryValidateObject(x,new ValidationContext(x),e,true);return e;}
}
