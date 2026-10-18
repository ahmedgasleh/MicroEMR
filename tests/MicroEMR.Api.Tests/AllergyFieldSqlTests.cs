using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.PatientAllergies.Contracts;
using MicroEMR.Application.PatientAllergies;
using MicroEMR.Application.PatientAllergies.Services;
using MicroEMR.Application.PatientCpp;
using MicroEMR.Infrastructure.PatientAllergies;
using MicroEMR.Core.Domain;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class AllergyFieldSqlTests(AllergyFieldDatabase db):IClassFixture<AllergyFieldDatabase>
{
    internal static string Route(Guid patient,Guid? uid=null)=>$"api/patients/{patient}/allergies"+(uid is null?"":$"/{uid}");
    internal static CreatePatientAllergyRequest Draft()=>new(){AllergenName="TEST ONLY drug allergy",AllergenType="Medication",Reaction="TEST ONLY reaction",Severity="Mild",Notes="TEST ONLY original note <script>",ClinicalFields=new(){DrugDIN="00123456",LifeStageCode="C",OnsetDate=new(){Year=2000,Month=2}}};
    internal static UpdatePatientAllergyRequest Edit(PatientAllergyDetailsResponse row,AllergyClinicalFields? fields)=>new(){AllergenName=row.AllergenName,AllergenType=row.AllergenType,Reaction=row.Reaction,Severity=row.Severity,Notes=row.Notes,Status=row.Status,RowVersion=row.RowVersion,ClinicalFields=fields};
    [Theory]
    [InlineData("N",2000,null,null)] [InlineData("I",2000,2,null)] [InlineData("C",2000,2,29)] [InlineData("T",null,null,null)] [InlineData("A",2001,3,4)]
    public async Task RealApiPersistsDinStagePrecisionAndNativeProvenanceWithCompleteAudit(string stage,int? year,int? month,int? day)
    {
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);
        await using var host=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,actor);using var client=host.Client(actor);
        var draft=Draft();draft.ClinicalFields!.LifeStageCode=stage;draft.ClinicalFields.OnsetDate=new(){Year=year,Month=month,Day=day};
        var from=await db.Clinical.Utc(false);var response=await client.PostAsJsonAsync(Route(patient),draft);Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var row=(await response.Content.ReadFromJsonAsync<PatientAllergyDetailsResponse>())!;var read=(await db.Repository().GetForPatientAsync(patient,row.AllergyUid))!;
        Assert.Equal("00123456",read.ClinicalFields!.DrugDIN);Assert.Equal(stage,read.ClinicalFields.LifeStageCode);Assert.Equal(year,read.ClinicalFields.OnsetDate?.Year);Assert.Equal(month,read.ClinicalFields.OnsetDate?.Month);Assert.Equal(day,read.ClinicalFields.OnsetDate?.Day);Assert.Equal(day.HasValue,read.OnsetDate.HasValue);
        Assert.InRange(read.CreatedAt,from.AddSeconds(-1),(await db.Clinical.Utc(false)).AddSeconds(1));Assert.Equal(actor.Actor,read.CreatedBy);
        var update=Edit(read,read.ClinicalFields);update.Notes="TEST ONLY amended";var save=await client.PutAsJsonAsync(Route(patient,row.AllergyUid),update);Assert.Equal(HttpStatusCode.OK,save.StatusCode);
        var edited=(await save.Content.ReadFromJsonAsync<PatientAllergyDetailsResponse>())!;Assert.NotEqual(row.RowVersion,edited.RowVersion);Assert.Equal(row.CreatedAt,edited.CreatedAt);
        var audits=await db.Clinical.Audit(false,row.AllergyUid,"PatientAllergy");Assert.Equal(2,audits.Count);using var before=JsonDocument.Parse(audits[1].Old!);using var after=JsonDocument.Parse(audits[1].New);
        Assert.Equal(draft.Notes,before.RootElement.GetProperty("Notes").GetString());Assert.Equal(update.Notes,after.RootElement.GetProperty("Notes").GetString());Assert.Equal(edited.RowVersion,after.RootElement.GetProperty("RowVersion").GetString());Assert.Equal(db.Clinical.Tenant(false).TenantUid,after.RootElement.GetProperty("TenantUid").GetGuid());Assert.Equal(actor.Actor,audits[1].Actor);Assert.Equal(actor.Subject,audits[1].Subject);Assert.False(string.IsNullOrWhiteSpace(after.RootElement.GetProperty("RequestCorrelationId").GetString()));
        Assert.Equal(new[]{"N","I","C","T","A"},FamilyHistoryLifeStages.Labels.Keys);Assert.Equal("Child",FamilyHistoryLifeStages.Display("C"));
        var resolve=await client.PostAsJsonAsync(Route(patient,row.AllergyUid)+"/resolve",new ResolvePatientAllergyRequest{RowVersion=edited.RowVersion,ResolveReason="TEST ONLY resolved"});Assert.Equal(HttpStatusCode.OK,resolve.StatusCode);
        var resolved=(await resolve.Content.ReadFromJsonAsync<PatientAllergyDetailsResponse>())!;Assert.Equal("Resolved",resolved.Status);Assert.Equal(row.CreatedAt,resolved.CreatedAt);Assert.Equal("00123456",resolved.ClinicalFields!.DrugDIN);
        var snapshot=await db.Snapshot(false);Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync(Route(patient,row.AllergyUid)+"/resolve",new ResolvePatientAllergyRequest{RowVersion=resolved.RowVersion})).StatusCode);Assert.Equal(snapshot,await db.Snapshot(false));
        var reactivate=Edit(resolved,resolved.ClinicalFields);reactivate.Status="Active";Assert.Equal(HttpStatusCode.OK,(await client.PutAsJsonAsync(Route(patient,row.AllergyUid),reactivate)).StatusCode);
    }
    [Fact]
    public async Task NonDrugUnknownAndAmbiguousDinBehaviorAndInvalidDatesNeverInferClinicalFacts()
    {
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);await using var host=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,actor);using var client=host.Client(actor);
        foreach(var type in new[]{"Food","Environmental","Latex","Contrast","Other",""})
        {
            var draft=Draft();draft.AllergenType=type;draft.ClinicalFields!.DrugDIN=null;draft.ClinicalFields.LifeStageCode=null;
            Assert.Equal(HttpStatusCode.Created,(await client.PostAsJsonAsync(Route(patient),draft)).StatusCode);
            draft.ClinicalFields.DrugDIN="00123456";var before=await db.Snapshot(false);Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(Route(patient),draft)).StatusCode);Assert.Equal(before,await db.Snapshot(false));
        }
        var unknown=Draft();unknown.ClinicalFields=new();var unknownRow=await db.Repository().CreateAsync(patient,unknown,actor.Actor,null);Assert.Null(unknownRow.ClinicalFields!.DrugDIN);Assert.Equal("Not recorded",unknownRow.OnsetDisplay);
        foreach(var change in new Action<CreatePatientAllergyRequest>[] {x=>x.ClinicalFields!.LifeStageCode="c",x=>x.ClinicalFields!.OnsetDate=new(){Year=2000,Month=2,Day=30},x=>x.ClinicalFields!.OnsetDate=new(){Month=2},x=>x.ClinicalFields!.DrugDIN=new('1',101),x=>x.OnsetDate=new(2000,1,1)})
        {var draft=Draft();change(draft);var before=await db.Snapshot(false);Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(Route(patient),draft)).StatusCode);Assert.Equal(before,await db.Snapshot(false));}
    }
    [Fact]
    public async Task UpgradeLegacyAndOldClientOmissionPreservePreciseNewFieldsAndAuditHistory()
    {
        Assert.Equal(db.LegacyAudit,await db.LegacyAuditSnapshot());var row=(await db.Repository().GetForPatientAsync(db.LegacyPatient,db.LegacyUid))!;Assert.Equal(db.LegacyToken,row.RowVersion);Assert.Equal("1998-03-04",row.OnsetDisplay);Assert.Null(row.ClinicalFields!.DrugDIN);
        var actor=await db.Clinical.SeedUser(false);var changed=(await db.Repository().UpdateAsync(db.LegacyPatient,row.AllergyUid,Edit(row,Draft().ClinicalFields),actor.Actor))!;
        var omitted=(await db.Repository().UpdateAsync(db.LegacyPatient,row.AllergyUid,Edit(changed,null),actor.Actor))!;Assert.Equal("2000-02",omitted.OnsetDisplay);Assert.Equal("00123456",omitted.ClinicalFields!.DrugDIN);
        var clearOld=Edit(omitted,null);clearOld.OnsetDate=null;var before=await db.Snapshot(false);await Assert.ThrowsAsync<PatientAllergyValidationException>(()=>db.Repository().UpdateAsync(db.LegacyPatient,row.AllergyUid,clearOld,actor.Actor));Assert.Equal(before,await db.Snapshot(false));
        var typeChange=Edit(omitted,null);typeChange.AllergenType="Food";await Assert.ThrowsAsync<PatientAllergyValidationException>(()=>db.Repository().UpdateAsync(db.LegacyPatient,row.AllergyUid,typeChange,actor.Actor));Assert.Equal(before,await db.Snapshot(false));
        var deliberate=Edit(omitted,new());deliberate.AllergenType="Food";var cleared=(await db.Repository().UpdateAsync(db.LegacyPatient,row.AllergyUid,deliberate,actor.Actor))!;Assert.Null(cleared.ClinicalFields!.DrugDIN);Assert.Equal("Not recorded",cleared.OnsetDisplay);Assert.Equal(row.CreatedAt,cleared.CreatedAt);
    }
    [Fact]
    public async Task NkaIsExplicitIdempotentAndReplacementAndRevocationAreBoundToReviewedAssertion()
    {
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);await using var host=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,actor);using var client=host.Client(actor);
        Assert.Equal("NotDocumented",(await db.Repository().GetSnapshotAsync(patient)).Documentation.State);
        var nka=(await client.PostAsJsonAsync(Route(patient)+"/no-known-allergies",new{}));Assert.Equal(HttpStatusCode.OK,nka.StatusCode);var first=(await nka.Content.ReadFromJsonAsync<NoKnownAllergiesAssertionResponse>())!;
        var before=await db.Snapshot(false);var again=await db.Repository().AssertNoKnownAllergiesAsync(patient,actor.Actor);Assert.Equal(first.AssertionUid,again.AssertionUid);Assert.Equal(first.VerifiedAtUtc,again.VerifiedAtUtc);Assert.Equal(before,await db.Snapshot(false));
        var draft=Draft();draft.ConfirmReplaceNoKnownAllergies=true;Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(Route(patient),draft)).StatusCode);Assert.Equal(before,await db.Snapshot(false));
        var revoke=new RevokeNoKnownAllergiesRequest{AssertionUid=first.AssertionUid,RowVersion=first.RowVersion,Reason="TEST ONLY"};await db.Repository().RevokeNoKnownAllergiesAsync(patient,revoke,actor.Actor);
        var second=await db.Repository().AssertNoKnownAllergiesAsync(patient,actor.Actor);draft.ExpectedAssertionUid=first.AssertionUid;draft.ExpectedAssertionRowVersion=first.RowVersion;before=await db.Snapshot(false);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(Route(patient),draft)).StatusCode);Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(Route(patient)+"/no-known-allergies/revoke",revoke)).StatusCode);Assert.Equal(before,await db.Snapshot(false));
        draft.ExpectedAssertionUid=second.AssertionUid;draft.ExpectedAssertionRowVersion=second.RowVersion;var response=await client.PostAsJsonAsync(Route(patient),draft);Assert.Equal(HttpStatusCode.Created,response.StatusCode);var row=(await response.Content.ReadFromJsonAsync<PatientAllergyDetailsResponse>())!;
        var snapshot=await db.Repository().GetSnapshotAsync(patient);Assert.Equal("HasEntries",snapshot.Documentation.State);Assert.Null(snapshot.Documentation.NoKnownAllergies);Assert.Equal(2,(await db.Clinical.Audit(false,second.AssertionUid,"PatientAllergyAssertion")).Count);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(Route(patient)+"/no-known-allergies",new{})).StatusCode);
        var resolved=(await db.Repository().ResolveAsync(patient,row.AllergyUid,new(){RowVersion=row.RowVersion},actor.Actor))!;Assert.Equal("NotDocumented",(await db.Repository().GetSnapshotAsync(patient)).Documentation.State);
        await db.Repository().AssertNoKnownAllergiesAsync(patient,actor.Actor);var active=Edit(resolved,resolved.ClinicalFields);active.Status="Active";before=await db.Snapshot(false);Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync(Route(patient,row.AllergyUid),active)).StatusCode);Assert.Equal(before,await db.Snapshot(false));
    }
    [Theory]
    [InlineData(false,false)] [InlineData(false,true)] [InlineData(true,true)]
    public async Task EditAndResolveRacesAllowOnlyOneCurrentVersionMutation(bool oneResolve,bool twoResolve)
    {
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);var row=await db.Repository().CreateAsync(patient,Draft(),actor.Actor,null);
        await using var host=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,actor);using var client=host.Client(actor);
        Task<HttpResponseMessage> Save(bool resolve)=>resolve?client.PostAsJsonAsync(Route(patient,row.AllergyUid)+"/resolve",new ResolvePatientAllergyRequest{RowVersion=row.RowVersion}):client.PutAsJsonAsync(Route(patient,row.AllergyUid),Edit(row,row.ClinicalFields));
        var results=await Task.WhenAll(Save(oneResolve),Save(twoResolve));Assert.Single(results,x=>x.StatusCode==HttpStatusCode.OK);Assert.Single(results,x=>x.StatusCode==HttpStatusCode.Conflict);Assert.Equal(2,(await db.Clinical.Audit(false,row.AllergyUid,"PatientAllergy")).Count);
    }
    [Theory]
    [InlineData(false,false)] [InlineData(false,true)] [InlineData(true,true)]
    public async Task NkaReplacementAndRevokeRacesRejectTheStaleAssertion(bool firstRevoke,bool secondRevoke)
    {
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);var nka=await db.Repository().AssertNoKnownAllergiesAsync(patient,actor.Actor);
        await using var host=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,actor);using var client=host.Client(actor);
        Task<HttpResponseMessage> Save(bool revoke)
        {var draft=Draft();draft.ConfirmReplaceNoKnownAllergies=true;draft.ExpectedAssertionUid=nka.AssertionUid;draft.ExpectedAssertionRowVersion=nka.RowVersion;return revoke?client.PostAsJsonAsync(Route(patient)+"/no-known-allergies/revoke",new RevokeNoKnownAllergiesRequest{AssertionUid=nka.AssertionUid,RowVersion=nka.RowVersion}):client.PostAsJsonAsync(Route(patient),draft);}
        var results=await Task.WhenAll(Save(firstRevoke),Save(secondRevoke));Assert.Single(results,x=>x.IsSuccessStatusCode);Assert.Single(results,x=>x.StatusCode==HttpStatusCode.Conflict);Assert.Equal(2,(await db.Clinical.Audit(false,nka.AssertionUid,"PatientAllergyAssertion")).Count);
    }
    [Fact]
    public async Task AssertAndCreateRaceNeverProduceContradictorySnapshot()
    {
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);await using var host=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,actor);using var client=host.Client(actor);
        var results=await Task.WhenAll(client.PostAsJsonAsync(Route(patient),Draft()),client.PostAsJsonAsync(Route(patient)+"/no-known-allergies",new{}));Assert.Single(results,x=>x.IsSuccessStatusCode);Assert.Single(results,x=>x.StatusCode==HttpStatusCode.Conflict);
        var data=await db.Repository().GetSnapshotAsync(patient);Assert.Equal(data.Items.Any(x=>x.Status=="Active"),data.Documentation.State=="HasEntries");Assert.False(data.Items.Any()&&data.Documentation.State=="ExplicitlyNone");
    }
    [Fact]
    public async Task ScopedReadsAreOwnedBeforeSuccessAuditAndDenialsLeaveBothStoresUnchanged()
    {
        var patient=await db.Clinical.SeedPatient(false);var wrong=await db.Clinical.SeedPatient(false);var pb=await db.Clinical.SeedPatient(true);var writer=await db.Clinical.SeedUser(false);var reader=await db.Clinical.SeedUser(false,false);var hidden=await db.Clinical.SeedUser(false,true,false);var wb=await db.Clinical.SeedUser(true);var disabled=await db.Clinical.SeedUser(false);await db.Execute(false,$"UPDATE dbo.ApplicationUser SET IsActive=0 WHERE UserId={disabled.Actor}");var missing=new FamilyHistoryAcceptanceDatabase.User("allergy-unresolved-"+Guid.NewGuid(),long.MaxValue,true);
        var row=await db.Repository().CreateAsync(patient,Draft(),writer.Actor,null);var foreign=await db.Repository(true).CreateAsync(pb,Draft(),wb.Actor,null);
        await using var ha=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,writer,reader,hidden,disabled,missing);await using var hb=await FamilyHistoryAcceptanceHost.Start(db.Clinical,true,wb);
        using var a=ha.Client(writer);using var b=hb.Client(wb);using var read=ha.Client(reader);using var noRead=ha.Client(hidden);using var inactive=ha.Client(disabled);using var unresolved=ha.Client(missing);
        var before=await db.Snapshot(false);var beforeB=await db.Snapshot(true);var readCount=await db.ReadAuditCount(false);
        foreach(var (client,p,uid) in new[]{(a,wrong,row.AllergyUid),(b,patient,row.AllergyUid),(a,pb,foreign.AllergyUid)})
        {Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(Route(p,uid))).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await client.PutAsJsonAsync(Route(p,uid),Edit(row,row.ClinicalFields))).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync(Route(p,uid)+"/resolve",new ResolvePatientAllergyRequest{RowVersion=row.RowVersion})).StatusCode);}
        Assert.Equal(readCount,await db.ReadAuditCount(false));Assert.Equal(HttpStatusCode.Forbidden,(await noRead.GetAsync(Route(patient,row.AllergyUid))).StatusCode);
        foreach(var denied in new[]{read,inactive,unresolved,noRead})
        {Assert.Equal(HttpStatusCode.Forbidden,(await denied.PostAsJsonAsync(Route(patient),Draft())).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await denied.PostAsJsonAsync(Route(patient,row.AllergyUid)+"/resolve",new ResolvePatientAllergyRequest{RowVersion=row.RowVersion})).StatusCode);}
        Assert.Equal(HttpStatusCode.OK,(await read.GetAsync(Route(patient,row.AllergyUid))).StatusCode);Assert.Equal(readCount+1,await db.ReadAuditCount(false));
        Assert.Equal(HttpStatusCode.BadRequest,(await a.GetAsync($"api/patient-allergies/{row.AllergyUid}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await a.PostAsJsonAsync(Route(patient,row.AllergyUid)+"/resolve",new{})).StatusCode);
        Assert.Equal(before,await db.Snapshot(false));Assert.Equal(beforeB,await db.Snapshot(true));
        await db.Execute(false,$"UPDATE dbo.Patient SET IsDeleted=1 WHERE PatientUid='{patient}'");var count=await db.ReadAuditCount(false);Assert.Equal(HttpStatusCode.NotFound,(await a.GetAsync(Route(patient,row.AllergyUid))).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await a.GetAsync(Route(patient)+"/snapshot")).StatusCode);Assert.Equal(count,await db.ReadAuditCount(false));
        await db.Execute(false,"CREATE TRIGGER dbo.AllergyReadFault ON dbo.AuditLog AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE ActionName=N'PatientChartOpened') THROW 52997,'Synthetic read audit failure.',1; END;");
        try{Assert.Equal(HttpStatusCode.ServiceUnavailable,(await a.GetAsync(Route(wrong)+"/snapshot")).StatusCode);}finally{await db.Execute(false,"DROP TRIGGER dbo.AllergyReadFault");}
    }
    [Theory]
    [InlineData("Create")] [InlineData("Update")] [InlineData("Resolve")] [InlineData("Assert")] [InlineData("Revoke")] [InlineData("Replace")]
    public async Task RequiredAuditFailureRollsBackEveryMutationIncludingBothReplacementDestinations(string action)
    {
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);PatientAllergyDetailsResponse? row=null;NoKnownAllergiesAssertionResponse? nka=null;
        if(action is "Update" or "Resolve")row=await db.Repository().CreateAsync(patient,Draft(),actor.Actor,null);
        if(action is "Revoke" or "Replace")nka=await db.Repository().AssertNoKnownAllergiesAsync(patient,actor.Actor);
        var before=await db.Snapshot(false);
        foreach(var entity in action=="Replace"?new[]{"PatientAllergy","PatientAllergyAssertion"}:new[]{action is "Assert" or "Revoke"?"PatientAllergyAssertion":"PatientAllergy"})
        {
            await db.Execute(false,$"CREATE TRIGGER dbo.AllergyAuditFault ON dbo.AuditLog AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE EntityName=N'{entity}') THROW 52997,'Synthetic mutation audit failure.',1; END;");
            try
            {
                await Assert.ThrowsAsync<SqlException>(async()=>
                {
                    if(action=="Assert")await db.Repository().AssertNoKnownAllergiesAsync(patient,actor.Actor);
                    else if(action=="Revoke")await db.Repository().RevokeNoKnownAllergiesAsync(patient,new(){AssertionUid=nka!.AssertionUid,RowVersion=nka.RowVersion},actor.Actor);
                    else if(action=="Update")await db.Repository().UpdateAsync(patient,row!.AllergyUid,Edit(row,row.ClinicalFields),actor.Actor);
                    else if(action=="Resolve")await db.Repository().ResolveAsync(patient,row!.AllergyUid,new(){RowVersion=row.RowVersion},actor.Actor);
                    else{var draft=Draft();if(nka is not null){draft.ConfirmReplaceNoKnownAllergies=true;draft.ExpectedAssertionUid=nka.AssertionUid;draft.ExpectedAssertionRowVersion=nka.RowVersion;}await db.Repository().CreateAsync(patient,draft,actor.Actor,null);}
                });
            }
            finally{await db.Execute(false,"DROP TRIGGER dbo.AllergyAuditFault");}
            Assert.Equal(before,await db.Snapshot(false));
        }
    }
    [Fact]
    public async Task CppPrintUsesOneCoherentOwnedSourceWithAllDetailsAndExplicitNkaOnly()
    {
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);
        for(var i=0;i<8;i++){var draft=Draft();draft.AllergenName=$"TEST ONLY allergen {i}";draft.Notes=string.Join("\n",Enumerable.Range(0,20).Select(n=>$"Line {n}"))+$"\nTail{i} <script>";await db.Repository().CreateAsync(patient,draft,actor.Actor,null);}
        var inactive=await db.Repository().CreateAsync(patient,Draft(),actor.Actor,null);var edit=Edit(inactive,inactive.ClinicalFields);edit.Status="Inactive";await db.Repository().UpdateAsync(patient,inactive.AllergyUid,edit,actor.Actor);
        var calls=0;var repo=db.Repository();var allergies=CppSelectivePrintTests.Stub<IPatientAllergyService>((method,args)=>{Assert.Equal("GetSnapshotAsync",method);calls++;return repo.GetSnapshotAsync((Guid)args[0]!);});
        var source=CppSelectivePrintTests.Source(patient,null!,CppSelectivePrintTests.Stub<MicroEMR.Application.ReadAudit.IPatientChartReadAuditService>((_,_)=>Task.FromResult(Guid.NewGuid())),allergies:allergies);
        var summary=(await source.GetAsync(patient,"test"))!;Assert.Equal(5,summary.Allergies.Items.Count);Assert.Equal(8,summary.Allergies.TotalCount);Assert.Equal("2000-02",summary.Allergies.Items[0].OnsetDisplay);Assert.Equal("Child",summary.Allergies.Items[0].LifeStage);Assert.Null(summary.AllergiesForPrint);
        var fixture=new CppSelectivePrintTests.Fixture{ActualSource=source};fixture.Patient.PatientUid=patient;var prior=calls;var html=System.Text.Encoding.UTF8.GetString((await fixture.Print("Allergies"))!);Assert.Equal(prior+1,calls);
        foreach(var text in new[]{"TEST ONLY allergen 7","2000-02","Child","TEST ONLY reaction","System Entry Recorded At","Tail7","&lt;script&gt;"})Assert.Contains(text,html);
        Assert.DoesNotContain("2000-02-01",html);Assert.DoesNotContain("00123456",html);Assert.DoesNotContain("No Known Allergies",html);Assert.DoesNotContain("<script>",html);
        await using(var renderer=new MicroEMR.Infrastructure.ClinicalOutput.PlaywrightPdfRenderer(NullLogger<MicroEMR.Infrastructure.ClinicalOutput.PlaywrightPdfRenderer>.Instance))
        {using var pdf=UglyToad.PdfPig.PdfDocument.Open(await renderer.RenderAsync(html));Assert.True(pdf.NumberOfPages>1);Assert.Contains(pdf.GetPages().SelectMany(x=>x.GetWords()),x=>x.Text=="Tail7");}
        var unselected=calls;await source.GetForPrintAsync(patient,["Problems"],"test");Assert.Equal(unselected,calls);
        var empty=await db.Clinical.SeedPatient(false);var emptySource=CppSelectivePrintTests.Source(empty,null!,CppSelectivePrintTests.Stub<MicroEMR.Application.ReadAudit.IPatientChartReadAuditService>((_,_)=>Task.FromResult(Guid.NewGuid())),allergies:allergies);Assert.Equal(PatientCppSectionStates.NotDocumented,(await emptySource.GetAsync(empty,"test"))!.Allergies.State);
        var nka=await repo.AssertNoKnownAllergiesAsync(empty,actor.Actor);Assert.Equal(PatientCppSectionStates.ExplicitlyNone,(await emptySource.GetAsync(empty,"test"))!.Allergies.State);
        fixture.ActualSource=emptySource;fixture.Patient.PatientUid=empty;Assert.Contains("No Known Allergies",System.Text.Encoding.UTF8.GetString((await fixture.Print("Allergies"))!));
        var foreign=CppSelectivePrintTests.Stub<IPatientAllergyService>((_,_)=>Task.FromResult(new AllergySnapshot([new(){PatientUid=Guid.NewGuid(),Status="Resolved"}],new("NotDocumented",null))));
        fixture.ActualSource=CppSelectivePrintTests.Source(empty,null!,CppSelectivePrintTests.Stub<MicroEMR.Application.ReadAudit.IPatientChartReadAuditService>((_,_)=>Task.FromResult(Guid.NewGuid())),allergies:foreign);await Assert.ThrowsAsync<CppPrintUnavailableException>(()=>fixture.Print("Allergies"));
    }

    [Fact]
    public async Task SpoofedAttributionAndDirectSqlInvalidFieldsCannotBypassTheContract()
    {
        var p=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);await using var host=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,actor);using var client=host.Client(actor);
        var result=await client.PostAsJsonAsync(Route(p),new{AllergenName="TEST ONLY actor",AllergenType="Medication",CreatedBy=long.MaxValue,CreatedByDisplayName="spoofed",RequestCorrelationId="spoofed",ClinicalFields=new{DrugDIN="00123456",LifeStageCode="C",OnsetDate=new{Year=2000}}});
        Assert.Equal(HttpStatusCode.Created,result.StatusCode);var row=(await result.Content.ReadFromJsonAsync<PatientAllergyDetailsResponse>())!;Assert.Equal(actor.Actor,row.CreatedBy);Assert.DoesNotContain("spoofed",Assert.Single(await db.Clinical.Audit(false,row.AllergyUid,"PatientAllergy")).New);
        foreach(var fields in new[]{"{\"DrugDIN\":\"00123456\",\"LifeStageCode\":\"bad\"}","{\"OnsetDate\":{\"Year\":2000,\"Month\":2,\"Day\":30}}"})
        {
            var before=await db.Snapshot(false);await Assert.ThrowsAsync<SqlException>(()=>db.Execute(false,$"EXEC dbo.PatientAllergy_Create @PatientUid='{p}',@AllergenName=N'TEST ONLY',@CreatedBy={actor.Actor},@ClinicalFields=N'{fields}'"));Assert.Equal(before,await db.Snapshot(false));
        }
        var bad=Edit(row,row.ClinicalFields);bad.RowVersion="not-base64";Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync(Route(p,row.AllergyUid),bad)).StatusCode);
    }

    [Fact]
    public async Task DirectSqlNullTokensAndMissingAuditCannotProduceFalseSuccess()
    {
        var p=await db.Clinical.SeedPatient(false);var a=await db.Clinical.SeedUser(false);var row=await db.Repository().CreateAsync(p,Draft(),a.Actor,null);var empty=await db.Clinical.SeedPatient(false);var nka=await db.Repository().AssertNoKnownAllergiesAsync(empty,a.Actor);var before=await db.Snapshot(false);
        foreach(var sql in new[]{ $"EXEC dbo.PatientAllergy_Resolve @PatientUid='{p}',@AllergyUid='{row.AllergyUid}',@ResolvedBy={a.Actor}", $"EXEC dbo.PatientAllergy_RevokeNoKnownAllergies @PatientUid='{empty}',@RevokedBy={a.Actor},@ExpectedAssertionUid='{nka.AssertionUid}',@ExpectedRowVersion=NULL",$"EXEC dbo.PatientAllergy_Update @PatientUid='{p}',@AllergyUid='{row.AllergyUid}',@AllergenName=N'TEST ONLY',@AllergyStatus=N'Active',@UpdatedBy={a.Actor},@RowVersion=NULL"})
        {await Assert.ThrowsAsync<SqlException>(()=>db.Execute(false,sql));Assert.Equal(before,await db.Snapshot(false));}
        await db.Execute(false,"EXEC sp_rename N'dbo.AuditLog',N'AllergyHeldAudit'");try{await Assert.ThrowsAsync<SqlException>(()=>db.Repository().CreateAsync(p,Draft(),a.Actor,null));}finally{await db.Execute(false,"EXEC sp_rename N'dbo.AllergyHeldAudit',N'AuditLog'");}Assert.Equal(before,await db.Snapshot(false));
        await db.Execute(false,$"BEGIN TRANSACTION; EXEC dbo.PatientAllergy_Create @PatientUid='{p}',@AllergenName=N'TEST ONLY outer rollback',@CreatedBy={a.Actor}; ROLLBACK;");Assert.Equal(before,await db.Snapshot(false));
    }
}

public sealed class AllergyFieldDatabase:IAsyncLifetime
{
    internal FamilyHistoryAcceptanceDatabase Clinical {get;}=new();
    internal Guid LegacyPatient,LegacyUid;internal string LegacyToken="",LegacyAudit="";
    public async Task InitializeAsync()
    {
        await Clinical.InitializeAsync();try
        {
            foreach(var other in new[]{false,true})
            {
                await Apply(other,"patient_allergy_stored_procedures.sql");
                foreach(var file in new[]{"0043-patient-chart-read-audit.sql","0041-patient-clinical-history.sql","0078-clinical-history-fields.sql","0055-verified-negative-allergy-assertion.sql"})await Apply(other,"tenant-clinical/migrations/"+file);
                if(!other)
                {
                    LegacyPatient=await Clinical.SeedPatient(false);var actor=await Clinical.SeedUser(false);await using var c=await Clinical.Open(false);
                    await using var q=new SqlCommand($"EXEC dbo.PatientAllergy_Create @PatientUid='{LegacyPatient}',@AllergenName=N'TEST ONLY legacy',@AllergenType=N'Medication',@OnsetDate='1998-03-04',@CreatedBy={actor.Actor}",c);
                    await using(var r=await q.ExecuteReaderAsync()){await r.ReadAsync();LegacyUid=(Guid)r["AllergyUid"];LegacyToken=Convert.ToBase64String((byte[])r["RowVersion"]);}LegacyAudit=await LegacyAuditSnapshot();
                }
                await Apply(other,"tenant-clinical/migrations/0081-allergy-fields-safeguards.sql");
            }
        }catch{await Clinical.DisposeAsync();throw;}
    }
    public Task DisposeAsync()=>Clinical.DisposeAsync();
    internal PatientAllergyRepository Repository(bool other=false)=>new(Clinical.Factory(other),NullLogger<PatientAllergyRepository>.Instance);
    internal async Task Apply(bool other,string file){foreach(var batch in Regex.Split(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"database",file)),@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase))if(!string.IsNullOrWhiteSpace(batch))await Execute(other,batch);}
    internal async Task Execute(bool other,string sql){await using var c=await Clinical.Open(other);await using var q=new SqlCommand(sql,c);await q.ExecuteNonQueryAsync();}
    internal async Task<string> Scalar(bool other,string sql){await using var c=await Clinical.Open(other);await using var q=new SqlCommand(sql,c);return Convert.ToString(await q.ExecuteScalarAsync())!;}
    internal Task<string> LegacyAuditSnapshot()=>Scalar(false,$"SELECT (SELECT * FROM dbo.AuditLog WHERE EntityName=N'PatientAllergy' AND EntityId='{LegacyUid}' ORDER BY AuditLogId FOR JSON PATH,INCLUDE_NULL_VALUES)");
    internal async Task<int> ReadAuditCount(bool other)=>int.Parse(await Scalar(other,"SELECT COUNT(*) FROM dbo.AuditLog WHERE ActionName=N'PatientChartOpened'"));
    internal async Task<string> Snapshot(bool other)
    {
        await using var c=await Clinical.Open(other);await using var q=new SqlCommand("SELECT (SELECT * FROM dbo.PatientAllergy ORDER BY AllergyUid FOR JSON PATH,INCLUDE_NULL_VALUES) Clinical,(SELECT * FROM dbo.PatientAllergyAssertion ORDER BY AssertionUid FOR JSON PATH,INCLUDE_NULL_VALUES) Assertions,(SELECT * FROM dbo.AuditLog WHERE EntityName IN(N'PatientAllergy',N'PatientAllergyAssertion') ORDER BY AuditLogId FOR JSON PATH,INCLUDE_NULL_VALUES) Audit FOR JSON PATH,WITHOUT_ARRAY_WRAPPER",c);
        await using var r=await q.ExecuteReaderAsync();var text=new System.Text.StringBuilder();while(await r.ReadAsync())text.Append(r.GetString(0));return text.ToString();
    }
}
