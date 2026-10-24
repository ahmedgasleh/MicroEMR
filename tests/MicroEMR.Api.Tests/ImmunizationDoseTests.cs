using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.PatientImmunizations;
using MicroEMR.Core.Domain;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ImmunizationDoseTests(ImmunizationFieldDatabase db) : IClassFixture<ImmunizationFieldDatabase>
{
    [Theory]
    [InlineData("0.001", "mL")] [InlineData("9999.999", "mg")]
    [InlineData("25.123", "mcg")] [InlineData("1", "units")]
    public async Task ApiSqlRoundTripAuditOmissionClearAndStale(string text, string unit)
    {
        var amount=decimal.Parse(text,System.Globalization.CultureInfo.InvariantCulture);
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);
        await using var host=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,actor);using var client=host.Client(actor);
        var request=ImmunizationFieldSqlTests.Draft();request.Dose=new(){Amount=amount,UnitCode=unit};
        var response=await client.PostAsJsonAsync(ImmunizationFieldSqlTests.Route(patient),request);
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var row=(await response.Content.ReadFromJsonAsync<PatientImmunizationResponse>())!;
        var stored=(await db.Repository().GetAsync(patient,row.ImmunizationUid))!;
        Assert.Equal(amount,stored.Dose!.Amount);Assert.Equal(unit,stored.Dose.UnitCode);Assert.Equal(actor.Actor,stored.CreatedBy);
        // Old clients may replace ClinicalFields without knowing the independent dose subgroup.
        var omitted=ImmunizationFieldSqlTests.Edit(row,row.ClinicalFields);omitted.Notes="TEST ONLY old client";
        var save=await client.PutAsJsonAsync(ImmunizationFieldSqlTests.Route(patient,row.ImmunizationUid),omitted);
        Assert.Equal(HttpStatusCode.OK,save.StatusCode);var preserved=(await save.Content.ReadFromJsonAsync<PatientImmunizationResponse>())!;
        Assert.Equal(amount,preserved.Dose!.Amount);Assert.NotEqual(row.RowVersion,preserved.RowVersion);
        var unchanged=await db.Snapshot(false);
        omitted.Dose=new(){Amount=2,UnitCode="mg"};
        Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync(ImmunizationFieldSqlTests.Route(patient,row.ImmunizationUid),omitted)).StatusCode);
        Assert.Equal(unchanged,await db.Snapshot(false));
        var clear=ImmunizationFieldSqlTests.Edit(preserved,preserved.ClinicalFields);clear.Dose=new();
        var cleared=await client.PutAsJsonAsync(ImmunizationFieldSqlTests.Route(patient,row.ImmunizationUid),clear);
        Assert.Equal(HttpStatusCode.OK,cleared.StatusCode);
        var final=(await client.GetFromJsonAsync<PatientImmunizationResponse>(ImmunizationFieldSqlTests.Route(patient,row.ImmunizationUid)))!;
        Assert.Null(final.Dose!.Amount);Assert.Null(final.Dose.UnitCode);
        var audits=await db.Clinical.Audit(false,row.ImmunizationUid,"PatientImmunization");Assert.Equal(3,audits.Count);
        Assert.All(audits,x=>{Assert.Equal(actor.Actor,x.Actor);Assert.Equal(actor.Subject,x.Subject);Assert.Equal(patient,x.Patient);});
        using var old=JsonDocument.Parse(audits[2].Old!);using var current=JsonDocument.Parse(audits[2].New);
        Assert.Equal(amount,old.RootElement.GetProperty("DoseAmount").GetDecimal());Assert.Equal(unit,old.RootElement.GetProperty("DoseUnitCode").GetString());
        Assert.Equal(JsonValueKind.Null,current.RootElement.GetProperty("DoseAmount").ValueKind);
        Assert.Equal(final.RowVersion,current.RootElement.GetProperty("RowVersion").GetString());
    }

    [Theory]
    [InlineData("{\"amount\":0,\"unitCode\":\"mL\"}")]
    [InlineData("{\"amount\":-1,\"unitCode\":\"mL\"}")]
    [InlineData("{\"amount\":10000,\"unitCode\":\"mL\"}")]
    [InlineData("{\"amount\":0.0001,\"unitCode\":\"mL\"}")]
    [InlineData("{\"amount\":1.0000,\"unitCode\":\"mL\"}")]
    [InlineData("{\"amount\":1}")]
    [InlineData("{\"unitCode\":\"mL\"}")]
    [InlineData("{\"amount\":1,\"unitCode\":\"ML\"}")]
    [InlineData("{\"amount\":1,\"unitCode\":\"mL \"}")]
    [InlineData("{\"amount\":1,\"unitCode\":\"invented\"}")]
    [InlineData("{\"amount\":\"malformed\",\"unitCode\":\"mL\"}")]
    [InlineData("{\"amount\":9999999999999999999999999999999999,\"unitCode\":\"mL\"}")]
    public async Task InvalidApiDoseDoesNotChangeClinicalRowsOrAudit(string dose)
    {
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);
        await using var host=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,actor);using var client=host.Client(actor);
        var payload=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(ImmunizationFieldSqlTests.Draft()))!;
        payload["Dose"]=System.Text.Json.Nodes.JsonNode.Parse(dose);
        var request=payload.ToJsonString();
        var before=await db.Snapshot(false);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync(ImmunizationFieldSqlTests.Route(patient),new StringContent(request,Encoding.UTF8,"application/json"))).StatusCode);
        Assert.Equal(before,await db.Snapshot(false));
    }

    [Theory]
    [InlineData("{\"Amount\":0.0001,\"UnitCode\":\"mL\"}")]
    [InlineData("{\"Amount\":9999.9999,\"UnitCode\":\"mL\"}")]
    [InlineData("{\"Amount\":0,\"UnitCode\":\"mL\"}")]
    [InlineData("{\"Amount\":-1,\"UnitCode\":\"mL\"}")]
    [InlineData("{\"Amount\":10000,\"UnitCode\":\"mL\"}")]
    [InlineData("{\"Amount\":1}")]
    [InlineData("{\"UnitCode\":\"mL\"}")]
    [InlineData("{\"Amount\":\"bad\",\"UnitCode\":\"mL\"}")]
    [InlineData("{\"Amount\":1,\"UnitCode\":\"mL \"}")]
    [InlineData("{\"Amount\":1,\"UnitCode\":\"ML\"}")]
    [InlineData("[]")] [InlineData("invalid")]
    [InlineData("{\"Amount\":{},\"UnitCode\":null}")]
    public async Task StoredProcedureRejectsRawDoseBeforeRounding(string dose)
    {
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);
        var before=await db.Snapshot(false);await using var c=await db.Clinical.Open(false);
        await using var q=new SqlCommand("EXEC dbo.PatientImmunization_Create @PatientUid=@Patient,@VaccineName=N'TEST ONLY',@AdministrationDate='2000-01-01',@SourceType=N'HistoricalExternal',@Actor=@Actor,@Dose=@Dose",c);
        q.Parameters.AddWithValue("@Patient",patient);q.Parameters.AddWithValue("@Actor",actor.Actor);q.Parameters.AddWithValue("@Dose",dose);
        Assert.Equal(52300,(await Assert.ThrowsAsync<SqlException>(()=>q.ExecuteNonQueryAsync())).Number);
        Assert.Equal(before,await db.Snapshot(false));
    }

    [Fact]
    public async Task DoseWritesEnforcePermissionsTenantPatientActorAndAuditRollback()
    {
        var patient=await db.Clinical.SeedPatient(false);var otherPatient=await db.Clinical.SeedPatient(false);
        var actor=await db.Clinical.SeedUser(false);var reader=await db.Clinical.SeedUser(false,false);var foreignActor=await db.Clinical.SeedUser(true);
        await using var host=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,actor,reader);
        await using var foreignHost=await FamilyHistoryAcceptanceHost.Start(db.Clinical,true,foreignActor);
        using var client=host.Client(actor);using var denied=host.Client(reader);using var foreign=foreignHost.Client(foreignActor);
        var request=ImmunizationFieldSqlTests.Draft();request.Dose=new(){Amount=1,UnitCode="mL"};
        var row=await db.Repository().CreateAsync(patient,request,actor.Actor);
        var edit=ImmunizationFieldSqlTests.Edit(row,row.ClinicalFields);edit.Dose=new(){Amount=2,UnitCode="mg"};
        var before=await db.Snapshot(false);var beforeForeign=await db.Snapshot(true);
        Assert.Equal(HttpStatusCode.Forbidden,(await denied.PostAsJsonAsync(ImmunizationFieldSqlTests.Route(patient),request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await denied.PutAsJsonAsync(ImmunizationFieldSqlTests.Route(patient,row.ImmunizationUid),edit)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.PutAsJsonAsync(ImmunizationFieldSqlTests.Route(otherPatient,row.ImmunizationUid),edit)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await foreign.PutAsJsonAsync(ImmunizationFieldSqlTests.Route(patient,row.ImmunizationUid),edit)).StatusCode);
        Assert.Equal(before,await db.Snapshot(false));Assert.Equal(beforeForeign,await db.Snapshot(true));
        await db.Execute(false,"CREATE TRIGGER dbo.DoseAuditFailure ON dbo.AuditLog AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE EntityName=N'PatientImmunization') THROW 52997,'TEST ONLY dose audit failure.',1; END;");
        try
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.PostAsJsonAsync(ImmunizationFieldSqlTests.Route(patient),request)).StatusCode);Assert.Equal(before,await db.Snapshot(false));
            Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.PutAsJsonAsync(ImmunizationFieldSqlTests.Route(patient,row.ImmunizationUid),edit)).StatusCode);Assert.Equal(before,await db.Snapshot(false));
            edit.Dose=new();
            Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.PutAsJsonAsync(ImmunizationFieldSqlTests.Route(patient,row.ImmunizationUid),edit)).StatusCode);Assert.Equal(before,await db.Snapshot(false));
        }
        finally { await db.Execute(false,"DROP TRIGGER dbo.DoseAuditFailure"); }
    }

    [Fact]
    public async Task LegacyRefusalAndNullDoseRemainCompatibleAndTerminalDoseCannotChange()
    {
        var legacy=(await db.Repository().GetAsync(db.LegacyPatient,db.LegacyUid))!;
        Assert.Null(legacy.Dose!.Amount);Assert.Null(legacy.Dose.UnitCode);Assert.Equal(db.LegacyToken,legacy.RowVersion);Assert.Equal(db.LegacyAudit,await db.LegacyAuditSnapshot());
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);
        var request=ImmunizationFieldSqlTests.Draft(true);request.Dose=new();
        var refused=await db.Repository().CreateAsync(patient,request,actor.Actor);Assert.Equal("Refused",refused.Status);Assert.Null(refused.Dose!.Amount);
        request.Dose=new(){Amount=1,UnitCode="mL"};var snapshot=await db.Snapshot(false);
        await Assert.ThrowsAsync<PatientImmunizationValidationException>(()=>db.Repository().CreateAsync(patient,request,actor.Actor));Assert.Equal(snapshot,await db.Snapshot(false));
        var normal=ImmunizationFieldSqlTests.Draft();normal.Dose=new(){Amount=1,UnitCode="mL"};
        var row=await db.Repository().CreateAsync(patient,normal,actor.Actor);
        var terminal=(await db.Repository().MarkEnteredInErrorAsync(patient,row.ImmunizationUid,new(){RowVersion=row.RowVersion,Reason="TEST ONLY correction"},actor.Actor))!;
        Assert.Equal(1,terminal.Dose!.Amount);var edit=ImmunizationFieldSqlTests.Edit(terminal,terminal.ClinicalFields);edit.Dose=new();snapshot=await db.Snapshot(false);
        await Assert.ThrowsAsync<PatientImmunizationTerminalException>(()=>db.Repository().UpdateAsync(patient,row.ImmunizationUid,edit,actor.Actor));Assert.Equal(snapshot,await db.Snapshot(false));
        Assert.Equal(new[]{"mL","mg","mcg","units"},ImmunizationDoseUnits.All);
    }
}
