using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.PatientMedications.Contracts;
using MicroEMR.Application.PatientAllergies.Contracts;
using MicroEMR.Application.PatientMedications;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class Step138PlanTests(Step138PlanDatabase fixture):IClassFixture<Step138PlanDatabase>
{
    private MedicationClassificationDatabase Db=>fixture.Db;
    [Fact]
    public async Task IndependentIntentOmissionClearAndPastMappingRemainAuditedAndVersioned()
    {
        var p=await Db.Clinical.SeedPatient(false);var actor=await Db.Clinical.SeedUser(false);var repo=Db.Repository();
        var row=await repo.CreateAsync(p,new(){MedicationName="TEST ONLY independent intent",Classification="Acute",PlanFields=new(){LongTermIntent=true}},actor.Actor,null);
        Assert.True(row.LongTermIntent);Assert.False(row.PastMedication);Assert.Equal("Acute",row.Classification);
        var request=MedicationClassificationTests.Update(row,"Ongoing");
        var saved=(await repo.UpdateAsync(p,row.MedicationUid,request,actor.Actor))!;
        Assert.True(saved.LongTermIntent);
        request.RowVersion=saved.RowVersion;request.PlanFields=new();
        var cleared=(await repo.UpdateAsync(p,row.MedicationUid,request,actor.Actor))!;Assert.Null(cleared.LongTermIntent);
        request.RowVersion=cleared.RowVersion;request.PlanFields=new(){LongTermIntent=false};
        saved=(await repo.UpdateAsync(p,row.MedicationUid,request,actor.Actor))!;Assert.False(saved.LongTermIntent);
        await Assert.ThrowsAsync<PatientMedicationConcurrencyException>(()=>repo.UpdateAsync(p,row.MedicationUid,request,actor.Actor));
        var before=await Db.Snapshot(false);
        await Db.Execute(false,"CREATE TRIGGER dbo.Step138AuditFail ON dbo.AuditLog AFTER INSERT AS BEGIN THROW 52997,'TEST ONLY audit failure',1; END");
        try
        {
            await Assert.ThrowsAsync<SqlException>(()=>repo.DiscontinueAsync(p,row.MedicationUid,new(){RowVersion=saved.RowVersion},actor.Actor));
            Assert.Equal(before,await Db.Snapshot(false));
        }
        finally{await Db.Execute(false,"DROP TRIGGER dbo.Step138AuditFail");}
        var stopped=(await repo.DiscontinueAsync(p,row.MedicationUid,new(){RowVersion=saved.RowVersion},actor.Actor))!;
        Assert.True(stopped.PastMedication);Assert.False(stopped.LongTermIntent);Assert.Equal(saved.Classification,stopped.Classification);
        var audits=await Db.Clinical.Audit(false,row.MedicationUid,"PatientMedication");
        using var old=JsonDocument.Parse(audits.Last().Old!);using var current=JsonDocument.Parse(audits.Last().New);
        Assert.Equal("Active",old.RootElement.GetProperty("MedicationStatus").GetString());
        Assert.False(current.RootElement.GetProperty("LongTermIntent").GetBoolean());
        Assert.Null(await Db.Repository(true).GetByUidAsync(row.MedicationUid));
        var otherPatient=await Db.Clinical.SeedPatient(false);
        Assert.Null(await repo.DiscontinueAsync(otherPatient,row.MedicationUid,new(){RowVersion=stopped.RowVersion},actor.Actor));
        var stable=await Db.Snapshot(false);
        await repo.DiscontinueAsync(p,row.MedicationUid,new(){RowVersion=stopped.RowVersion},actor.Actor);
        Assert.Equal(stable,await Db.Snapshot(false));
    }
    [Fact]
    public async Task LegacyUnknownAndInactiveActorsAreNotReinterpreted()
    {
        var legacy=(await Db.Repository().GetByUidAsync(Db.LegacyUid))!;
        Assert.Null(legacy.LongTermIntent);Assert.Equal(Db.LegacyToken,legacy.RowVersion);
        var p=await Db.Clinical.SeedPatient(false);var actor=await Db.Clinical.SeedUser(false);
        var row=await Db.Repository().CreateAsync(p,new(){MedicationName="TEST ONLY active actor"},actor.Actor,null);
        await Db.Execute(false,$"UPDATE dbo.ApplicationUser SET IsActive=0 WHERE UserId={actor.Actor}");
        Assert.Equal(52901,(await Assert.ThrowsAsync<SqlException>(()=>Db.Repository().DiscontinueAsync(p,row.MedicationUid,new(){RowVersion=row.RowVersion},actor.Actor))).Number);
        Assert.Equal(row.RowVersion,(await Db.Repository().GetByUidAsync(row.MedicationUid))!.RowVersion);
    }
    [Fact]
    public async Task DiscontinueCannotInventAnEndBeforeAFutureStart()
    {
        var patient=await Db.Clinical.SeedPatient(false);var actor=await Db.Clinical.SeedUser(false);
        var row=await Db.Repository().CreateAsync(patient,new(){MedicationName="TEST ONLY future plan",StartDate=DateTime.UtcNow.Date.AddDays(30)},actor.Actor,null);
        var before=await Db.Snapshot(false);
        await Assert.ThrowsAsync<ArgumentException>(()=>Db.Repository().DiscontinueAsync(patient,row.MedicationUid,new(){RowVersion=row.RowVersion},actor.Actor));
        Assert.Equal(before,await Db.Snapshot(false));
    }
}

public sealed class Step138AllergyTests(Step138AllergyDatabase fixture):IClassFixture<Step138AllergyDatabase>
{
    private AllergyFieldDatabase Db=>fixture.Db;
    [Theory] [InlineData("Drug")] [InlineData("Non-drug")] [InlineData("Unknown")]
    public async Task ExplicitApplicabilityDoesNotReclassifyLegacyTextAndOmissionPreservesIt(string applicability)
    {
        var patient=await Db.Clinical.SeedPatient(false);var actor=await Db.Clinical.SeedUser(false);
        await using var host=await FamilyHistoryAcceptanceHost.Start(Db.Clinical,false,actor);using var client=host.Client(actor);
        var request=AllergyFieldSqlTests.Draft();request.AllergenType="Contrast";
        request.ClinicalFields!.DrugApplicability=applicability;request.ClinicalFields.DrugDIN=applicability=="Drug"?"00123456":null;
        var response=await client.PostAsJsonAsync(AllergyFieldSqlTests.Route(patient),request);
        Assert.True(response.StatusCode==HttpStatusCode.Created,await response.Content.ReadAsStringAsync());
        var row=(await response.Content.ReadFromJsonAsync<PatientAllergyDetailsResponse>())!;
        Assert.Equal("Contrast",row.AllergenType);Assert.Equal(applicability,row.ClinicalFields!.DrugApplicability);
        var omitted=new AllergyClinicalFields{DrugDIN=row.ClinicalFields.DrugDIN,LifeStageCode=row.ClinicalFields.LifeStageCode,OnsetDate=row.ClinicalFields.OnsetDate};
        var edit=AllergyFieldSqlTests.Edit(row,omitted);
        response=await client.PutAsJsonAsync(AllergyFieldSqlTests.Route(patient,row.AllergyUid),edit);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var saved=(await response.Content.ReadFromJsonAsync<PatientAllergyDetailsResponse>())!;
        Assert.Equal(applicability,saved.ClinicalFields!.DrugApplicability);Assert.Equal(row.AllergenType,saved.AllergenType);
        if(applicability=="Drug")
        {
            var invalid=AllergyFieldSqlTests.Edit(saved,new(){DrugDIN="00123456",DrugApplicability="Non-drug"});
            Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync(AllergyFieldSqlTests.Route(patient,row.AllergyUid),invalid)).StatusCode);
            Assert.Equal(saved.RowVersion,(await Db.Repository().GetForPatientAsync(patient,row.AllergyUid))!.RowVersion);
        }
        var clear=AllergyFieldSqlTests.Edit(saved,new(){DrugApplicability=null});
        Assert.Equal(HttpStatusCode.OK,(await client.PutAsJsonAsync(AllergyFieldSqlTests.Route(patient,row.AllergyUid),clear)).StatusCode);
        Assert.Null((await Db.Repository().GetForPatientAsync(patient,row.AllergyUid))!.ClinicalFields!.DrugApplicability);
    }
}

public sealed class Step138PlanDatabase:IAsyncLifetime
{
    internal MedicationClassificationDatabase Db{get;}=new();
    public Task InitializeAsync()=>Db.InitializeAsync();
    public Task DisposeAsync()=>Db.DisposeAsync();
}
public sealed class Step138AllergyDatabase:IAsyncLifetime
{
    internal AllergyFieldDatabase Db{get;}=new();
    public Task InitializeAsync()=>Db.InitializeAsync();
    public Task DisposeAsync()=>Db.DisposeAsync();
}
