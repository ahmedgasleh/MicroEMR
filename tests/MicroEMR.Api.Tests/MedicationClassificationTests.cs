using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.PatientCpp;
using MicroEMR.Application.PatientMedications.Contracts;
using MicroEMR.Application.PatientMedications.Services;
using MicroEMR.Application.ReadAudit;
using MicroEMR.Infrastructure.PatientMedications;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class MedicationClassificationTests(MedicationClassificationDatabase db) : IClassFixture<MedicationClassificationDatabase>
{
    [Theory]
    [InlineData("Ongoing")]
    [InlineData("Acute")]
    [InlineData(null)]
    [InlineData("Unclassified")]
    public async Task ApiRoundTripAndAuditPreserveExplicitIntent(string? classification)
    {
        var patient = await db.Clinical.SeedPatient(false);
        var writer = await db.Clinical.SeedUser(false);
        await using var host = await FamilyHistoryAcceptanceHost.Start(db.Clinical, false, writer);
        using var client = host.Client(writer);
        var before = await db.Clinical.Utc(false);
        var result = await client.PostAsJsonAsync(Route(patient), new CreatePatientMedicationRequest
        { MedicationName="TEST ONLY - classification fixture", Classification=classification, Strength="TEST ONLY", Directions="TEST ONLY - no supply", Notes="Fictional" });
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        var row = (await result.Content.ReadFromJsonAsync<PatientMedicationDetailsResponse>())!;
        var stored = (await db.Repository().GetByUidAsync(row.MedicationUid))!;
        var expected = classification == "Unclassified" ? null : classification;
        Assert.Equal(expected,stored.Classification);
        Assert.Equal(patient,stored.PatientUid); Assert.Equal(writer.Actor, stored.CreatedBy);
        Assert.InRange(stored.CreatedAt, before.AddSeconds(-1), (await db.Clinical.Utc(false)).AddSeconds(1));
        Assert.Single(await db.Repository().GetByPatientUidAsync(patient));
        var audit = Assert.Single(await db.Clinical.Audit(false,row.MedicationUid,"PatientMedication"));
        Assert.Equal(writer.Actor,audit.Actor); Assert.Equal(patient,audit.Patient); Assert.Equal("Create",audit.Action);
        using var json = JsonDocument.Parse(audit.New);
        Assert.Equal(expected,json.RootElement.GetProperty("Classification").GetString());
        var change = Update(stored,"Acute");
        var save = await client.PutAsJsonAsync(Route(patient)+"/"+row.MedicationUid,change);
        Assert.Equal(HttpStatusCode.OK,save.StatusCode);
        var updated=(await save.Content.ReadFromJsonAsync<PatientMedicationDetailsResponse>())!;
        Assert.Equal("Acute",updated.Classification); Assert.NotEqual(stored.RowVersion,updated.RowVersion);
        Assert.Equal(stored.CreatedAt,updated.CreatedAt); Assert.Equal(stored.CreatedBy,updated.CreatedBy);
        Assert.Equal(stored.Notes,updated.Notes); Assert.Equal(stored.Directions,updated.Directions);
        var updateAudit=(await db.Clinical.Audit(false,row.MedicationUid,"PatientMedication")).Last();
        Assert.Equal(writer.Actor,updateAudit.Actor); Assert.Equal("Update",updateAudit.Action);
        Assert.Contains("\"Classification\":\"Acute\"",updateAudit.New);
        Assert.NotNull(updateAudit.Old);
    }

    [Fact]
    public async Task DenialsStaleWritesAndForeignScopesLeaveClinicalStateAndAuditUnchanged()
    {
        var patient=await db.Clinical.SeedPatient(false); var otherPatient=await db.Clinical.SeedPatient(false);
        var writer=await db.Clinical.SeedUser(false); var reader=await db.Clinical.SeedUser(false,false);
        var noRead=await db.Clinical.SeedUser(false,true,false); var foreign=await db.Clinical.SeedUser(true);
        await using var host=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,writer,reader,noRead);
        await using var otherHost=await FamilyHistoryAcceptanceHost.Start(db.Clinical,true,foreign);
        using var client=host.Client(writer); using var denied=host.Client(reader); using var hidden=host.Client(noRead); using var tenantB=otherHost.Client(foreign);
        var row=await db.Repository().CreateAsync(patient,new() {MedicationName="TEST ONLY - security",Classification="Ongoing"},writer.Actor,null);
        var saved=(await db.Repository().UpdateAsync(patient,row.MedicationUid,Update(row,"Acute"),writer.Actor))!;
        var before=await db.Snapshot(false); var foreignBefore=await db.Snapshot(true);
        var route=Route(patient)+"/"+row.MedicationUid;
        Assert.Equal(HttpStatusCode.Forbidden,(await denied.PostAsJsonAsync(Route(patient),new {MedicationName="TEST ONLY denied",Classification="Ongoing"})).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await denied.PutAsJsonAsync(route,Update(saved,"Ongoing"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await denied.PostAsJsonAsync(route+"/discontinue",new {saved.RowVersion})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await denied.GetAsync(Route(patient))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await hidden.GetAsync(Route(patient))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync(route,Update(row,"Ongoing"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(route+"/discontinue",new {row.RowVersion})).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.PutAsJsonAsync(Route(otherPatient)+"/"+row.MedicationUid,Update(saved,"Ongoing"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await tenantB.PutAsJsonAsync(route,Update(saved,"Ongoing"))).StatusCode);
        Assert.Null(await db.Repository(true).GetByUidAsync(row.MedicationUid));
        Assert.Empty(await db.Repository(true).GetByPatientUidAsync(patient));
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(Route(patient),new {MedicationName="TEST ONLY invalid",Classification="Chronic"})).StatusCode);
        Assert.Equal(before,await db.Snapshot(false)); Assert.Equal(foreignBefore,await db.Snapshot(true));
    }

    [Fact]
    public async Task LegacyUpgradeDoesNotChangeClinicalValuesOrTokenAndAuditFailureRollsBack()
    {
        var legacy=(await db.Repository().GetByUidAsync(db.LegacyUid))!;
        Assert.Null(legacy.Classification); Assert.Equal(db.LegacyToken,legacy.RowVersion);
        Assert.Equal("TEST ONLY - legacy",legacy.MedicationName);
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);
        var row=await db.Repository().CreateAsync(patient,new(){MedicationName="TEST ONLY rollback",Classification="Ongoing"},actor.Actor,null);
        var before=await db.Snapshot(false);
        await db.Execute(false,"CREATE TRIGGER dbo.MedicationAuditFailure ON dbo.AuditLog AFTER INSERT AS BEGIN THROW 52997,'Synthetic audit failure.',1; END");
        try
        {
            await Assert.ThrowsAsync<SqlException>(()=>db.Repository().UpdateAsync(patient,row.MedicationUid,Update(row,"Acute"),actor.Actor));
            await Assert.ThrowsAsync<SqlException>(()=>db.Repository().CreateAsync(patient,new(){MedicationName="TEST ONLY not persisted",Classification="Acute"},actor.Actor,null));
            Assert.Equal(before,await db.Snapshot(false));
        }
        finally {await db.Execute(false,"DROP TRIGGER dbo.MedicationAuditFailure");}
        var discontinued=(await new PatientMedicationService(db.Repository()).DiscontinueAsync(patient,row.MedicationUid,new(){RowVersion=row.RowVersion},actor.Actor))!;
        Assert.Equal("Ongoing",discontinued.Classification);Assert.Equal("Discontinued",discontinued.Status);
        Assert.NotEqual(row.RowVersion,discontinued.RowVersion);
    }

    internal static string Route(Guid patient)=>$"api/patients/{patient}/medications";
    internal static UpdatePatientMedicationRequest Update(PatientMedicationDetailsResponse row,string? classification)=>new()
    {MedicationName=row.MedicationName,Classification=classification,Strength=row.Strength,DosageForm=row.DosageForm,Route=row.Route,
        Directions=row.Directions,Frequency=row.Frequency,StartDate=row.StartDate,EndDate=row.EndDate,Indication=row.Indication,PrescriberName=row.PrescriberName,
        Notes=row.Notes,Status=row.Status,RowVersion=row.RowVersion};
}

public sealed class MedicationClassificationDatabase : IAsyncLifetime
{
    internal FamilyHistoryAcceptanceDatabase Clinical {get;}=new();
    internal Guid LegacyUid {get;}=Guid.NewGuid();
    internal string LegacyToken="";
    public async Task InitializeAsync()
    {
        await Clinical.InitializeAsync();
        try
        {
            foreach(var other in new[]{false,true})
            {
                await Apply(other,"patient_medication_stored_procedures.sql");
                await Apply(other,"tenant-clinical/migrations/0059-medication-discontinuation-concurrency.sql");
                if(!other)
                {
                    var patient=await Clinical.SeedPatient(false);
                    await using var connection=await Clinical.Open(false);
                    await using var command=new SqlCommand("INSERT dbo.PatientMedication(MedicationUid,PatientUid,MedicationName) OUTPUT inserted.RowVersion VALUES(@Uid,@Patient,N'TEST ONLY - legacy')",connection);
                    command.Parameters.AddWithValue("@Uid",LegacyUid);command.Parameters.AddWithValue("@Patient",patient);
                    LegacyToken=Convert.ToBase64String((byte[])(await command.ExecuteScalarAsync())!);
                }
                await Apply(other,"tenant-clinical/migrations/0077-medication-classification.sql");
                await Apply(other,"tenant-clinical/migrations/0087-medication-plan-intent-safeguards.sql");
            }
        }
        catch {await Clinical.DisposeAsync();throw;}
    }
    public Task DisposeAsync()=>Clinical.DisposeAsync();
    internal PatientMedicationRepository Repository(bool other=false)=>new(Clinical.Factory(other),NullLogger<PatientMedicationRepository>.Instance);
    internal async Task Apply(bool other,string file)
    {
        var sql=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"database",file));
        foreach(var batch in Regex.Split(sql,@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase))
            if(!string.IsNullOrWhiteSpace(batch)) await Execute(other,batch);
    }
    internal async Task Execute(bool other,string sql)
    {await using var c=await Clinical.Open(other);await using var cmd=new SqlCommand(sql,c);await cmd.ExecuteNonQueryAsync();}
    internal async Task<string> Snapshot(bool other)
    {
        await using var c=await Clinical.Open(other);
        await using var cmd=new SqlCommand("SELECT (SELECT * FROM dbo.PatientMedication ORDER BY MedicationUid FOR JSON PATH,INCLUDE_NULL_VALUES) Clinical,(SELECT * FROM dbo.AuditLog ORDER BY AuditLogId FOR JSON PATH,INCLUDE_NULL_VALUES) Audit FOR JSON PATH,WITHOUT_ARRAY_WRAPPER",c);
        await using var reader=await cmd.ExecuteReaderAsync();
        var chunks=new System.Text.StringBuilder();
        while(await reader.ReadAsync()) chunks.Append(reader.GetString(0));
        return chunks.ToString();
    }
}
