using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.PatientClinicalHistory;
using MicroEMR.Infrastructure.PatientClinicalHistory;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class HistoryFieldSqlTests(HistoryFieldDatabase db) : IClassFixture<HistoryFieldDatabase>
{
    [Theory]
    [InlineData("N", 2000, null, null)]
    [InlineData("I", 2000, 2, null)]
    [InlineData("C", 2000, 2, 29)]
    [InlineData("T", 2001, 3, 4)]
    [InlineData("A", null, null, null)]
    public async Task MedicalAndSurgicalRoundTripPrecisionStageStatusNotesAndAudit(string stage,int? year,int? month,int? day)
    {
        var patient=await db.Clinical.SeedPatient(false); var actor=await db.Clinical.SeedUser(false);
        await using var host=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,actor); using var client=host.Client(actor);
        var from=await db.Clinical.Utc(false);
        var draft=new CreatePatientClinicalHistoryRequest{HistoryType="Medical",Description="TEST ONLY - historical condition",ClinicalFields=new(){OnsetDate=new(){Year=year,Month=month,Day=day},LifeStageCode=stage,ClinicalStatus="Resolved",ResolutionDate=new(){Year=2010},Notes="TEST ONLY <script> clinical notes"}};
        var response=await client.PostAsJsonAsync(Route(patient),draft);
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var row=(await response.Content.ReadFromJsonAsync<PatientClinicalHistoryResponse>())!;
        var stored=Assert.Single(await db.Repository().ListAsync(patient,"All"));
        Assert.Equal(patient,stored.PatientUid);Assert.Equal(actor.Actor,stored.CreatedBy);
        Assert.InRange(stored.CreatedAt,from.AddSeconds(-1),(await db.Clinical.Utc(false)).AddSeconds(1));
        Assert.Equal(year,stored.ClinicalFields!.OnsetDate?.Year);Assert.Equal(month,stored.ClinicalFields.OnsetDate?.Month);Assert.Equal(day,stored.ClinicalFields.OnsetDate?.Day);
        Assert.Equal(stage,stored.ClinicalFields.LifeStageCode);Assert.Equal("Resolved",stored.ClinicalFields.ClinicalStatus);Assert.Null(stored.RelevantDate);
        var edit=Edit(row,new(){OnsetDate=draft.ClinicalFields.OnsetDate,LifeStageCode=stage,ClinicalStatus="Active",Notes="TEST ONLY updated"});
        var save=await client.PutAsJsonAsync(Route(patient,row.HistoryUid),edit);Assert.Equal(HttpStatusCode.OK,save.StatusCode);
        var updated=(await save.Content.ReadFromJsonAsync<PatientClinicalHistoryResponse>())!;
        Assert.NotEqual(row.RowVersion,updated.RowVersion);Assert.Equal(row.CreatedAt,updated.CreatedAt);Assert.Equal("Active",updated.ClinicalFields!.ClinicalStatus);Assert.Null(updated.ClinicalFields.ResolutionDate);
        var audits=await db.Clinical.Audit(false,row.HistoryUid,"PatientClinicalHistory");Assert.Equal(2,audits.Count);
        foreach(var audit in audits){Assert.Equal(patient,audit.Patient);Assert.Equal(actor.Actor,audit.Actor);Assert.Equal(actor.Subject,audit.Subject);}
        using var before=JsonDocument.Parse(audits[1].Old!);using var after=JsonDocument.Parse(audits[1].New);
        Assert.Equal(stage,after.RootElement.GetProperty("LifeStageCode").GetString());Assert.Equal("Resolved",before.RootElement.GetProperty("ClinicalStatus").GetString());
        Assert.Equal(JsonValueKind.Null,after.RootElement.GetProperty("ResolutionYear").ValueKind);Assert.Equal("TEST ONLY updated",after.RootElement.GetProperty("Notes").GetString());
        Assert.Equal(year,after.RootElement.GetProperty("OnsetYear").Deserialize<int?>());
        var surgery=await db.Repository().CreateAsync(patient,new(){HistoryType="Surgical",Description="TEST ONLY procedure",ClinicalFields=new(){ProcedureDate=new(){Year=2004,Month=5},Notes="TEST ONLY surgery"}},actor.Actor);
        var surgicalEdit=Edit(surgery,new(){ProcedureDate=new(){Year=2005,Month=6,Day=7},Notes="TEST ONLY procedure corrected"});
        Assert.Equal(HttpStatusCode.OK,(await client.PutAsJsonAsync(Route(patient,surgery.HistoryUid),surgicalEdit)).StatusCode);
        var surgical=Assert.Single(await db.Repository().ListAsync(patient,"All"),x=>x.HistoryType=="Surgical");
        Assert.Equal("2005-06-07",surgical.ClinicalFields!.ProcedureDate!.Display);Assert.Null(surgical.ClinicalFields.OnsetDate);Assert.Null(surgical.ClinicalFields.ClinicalStatus);
        Assert.Equal(2,(await db.Clinical.Audit(false,surgery.HistoryUid,"PatientClinicalHistory")).Count);
    }

    [Fact]
    public async Task InvalidDatesApplicabilityAndContradictionsLeaveRowsTokensAndAuditsUnchanged()
    {
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);
        await using var host=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,actor);using var client=host.Client(actor);
        var before=await db.Snapshot(false);
        HistoryClinicalFields[] invalid=[new(){OnsetDate=new(){Month=2}},new(){OnsetDate=new(){Year=2001,Month=2,Day=29}},new(){OnsetDate=new(){Year=2000,Day=1}},new(){OnsetDate=new(){Year=10000}},new(){OnsetDate=new(){Year=9999}},new(){LifeStageCode="a"},new(){LifeStageCode="Adult"},new(){ClinicalStatus="Past"},new(){ClinicalStatus="Active",ResolutionDate=new(){Year=2000}},new(){OnsetDate=new(){Year=2010},ResolutionDate=new(){Year=2009}},new(){ProcedureDate=new(){Year=2000}},new(){Notes=new string('x',1001)}];
        foreach(var fields in invalid)
        {
            Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(Route(patient),new CreatePatientClinicalHistoryRequest{HistoryType="Medical",Description="TEST ONLY invalid",ClinicalFields=fields})).StatusCode);
            await Assert.ThrowsAsync<PatientClinicalHistoryValidationException>(()=>db.Repository().CreateAsync(patient,new(){HistoryType="Medical",Description="TEST ONLY SQL invalid",ClinicalFields=fields},actor.Actor));
        }
        Assert.Equal(before,await db.Snapshot(false));
        var overlap=await db.Repository().CreateAsync(patient,new(){HistoryType="Medical",Description="TEST ONLY overlap",ClinicalFields=new(){OnsetDate=new(){Year=2000},ResolutionDate=new(){Year=2000,Month=1}}},actor.Actor);
        Assert.Null(overlap.ClinicalFields!.ClinicalStatus); // A resolution date does not infer status.
        Assert.Equal("2000",overlap.ClinicalFields.OnsetDate!.Display);
    }

    [Fact]
    public async Task LegacyUpgradeOmittedGroupExplicitClearAndKindChangePreserveHistory()
    {
        var legacy=Assert.Single(await db.Repository().ListAsync(db.LegacyPatient,"All"),x=>x.HistoryUid==db.LegacyUid);
        Assert.Equal(db.LegacyToken,legacy.RowVersion);Assert.Equal(new DateOnly(1998,3,4),legacy.RelevantDate);Assert.Null(legacy.ClinicalFields!.OnsetDate);Assert.Null(legacy.ClinicalFields.ClinicalStatus);
        var archived=Assert.Single((await db.Repository().ListAsync(db.LegacyPatient,"Archived")));Assert.Equal(db.ArchivedToken,archived.RowVersion);Assert.Equal("TEST ONLY archived legacy",archived.Description);
        var legacyAudit=Assert.Single(await db.Clinical.Audit(false,legacy.HistoryUid,"PatientClinicalHistory"));Assert.Equal(db.LegacyAudit,legacyAudit.New);
        var archivedAudit=await db.Clinical.Audit(false,archived.HistoryUid,"PatientClinicalHistory");Assert.Equal(2,archivedAudit.Count);Assert.Equal("Archive",archivedAudit[1].Action);Assert.Equal("Status=Archived",archivedAudit[1].New);
        var actor=await db.Clinical.SeedUser(false);
        var saved=(await db.Repository().UpdateAsync(db.LegacyPatient,legacy.HistoryUid,Edit(legacy,new(){OnsetDate=new(){Year=1990},LifeStageCode="C",Notes="TEST ONLY preserve"}),actor.Actor))!;
        var omitted=(await db.Repository().UpdateAsync(db.LegacyPatient,saved.HistoryUid,Edit(saved,null),actor.Actor))!;
        Assert.Equal(1990,omitted.ClinicalFields!.OnsetDate!.Year);Assert.Equal("TEST ONLY preserve",omitted.ClinicalFields.Notes);Assert.Equal(legacy.RelevantDate,omitted.RelevantDate);
        var changeKind=Edit(omitted,new());changeKind.HistoryType="Surgical";
        await Assert.ThrowsAsync<PatientClinicalHistoryValidationException>(()=>db.Repository().UpdateAsync(db.LegacyPatient,omitted.HistoryUid,changeKind,actor.Actor));
        var cleared=(await db.Repository().UpdateAsync(db.LegacyPatient,omitted.HistoryUid,Edit(omitted,new()),actor.Actor))!;
        Assert.Null(cleared.ClinicalFields!.OnsetDate);Assert.Null(cleared.ClinicalFields.Notes);
        changeKind=Edit(cleared,new(){ProcedureDate=new(){Year=1999}});changeKind.HistoryType="Surgical";
        var changed=(await db.Repository().UpdateAsync(db.LegacyPatient,cleared.HistoryUid,changeKind,actor.Actor))!;
        Assert.Equal("Surgical",changed.HistoryType);Assert.Equal(legacy.RelevantDate,changed.RelevantDate);Assert.Equal(legacy.Description,changed.Description);
    }

    [Fact]
    public async Task DenialsStaleWritesForeignPatientsAndBothTenantDirectionsDoNotChangeClinicalState()
    {
        var patient=await db.Clinical.SeedPatient(false);var other=await db.Clinical.SeedPatient(false);var tenantPatient=await db.Clinical.SeedPatient(true);
        var writer=await db.Clinical.SeedUser(false);var reader=await db.Clinical.SeedUser(false,false);var hidden=await db.Clinical.SeedUser(false,true,false);var writerB=await db.Clinical.SeedUser(true);
        await using var aHost=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,writer,reader,hidden);await using var bHost=await FamilyHistoryAcceptanceHost.Start(db.Clinical,true,writerB);
        using var a=aHost.Client(writer);using var b=bHost.Client(writerB);using var read=aHost.Client(reader);using var noRead=aHost.Client(hidden);
        var row=await db.Repository().CreateAsync(patient,Draft(),writer.Actor);var foreign=await db.Repository(true).CreateAsync(tenantPatient,Draft(),writerB.Actor);
        var updated=(await db.Repository().UpdateAsync(patient,row.HistoryUid,Edit(row,new(){Notes="TEST ONLY newer"}),writer.Actor))!;
        var before=await db.Snapshot(false);var beforeB=await db.Snapshot(true);
        Assert.Equal(HttpStatusCode.OK,(await read.GetAsync(Route(patient))).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await noRead.GetAsync(Route(patient))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await read.PostAsJsonAsync(Route(patient),Draft())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await read.PutAsJsonAsync(Route(patient,row.HistoryUid),Edit(updated,new()))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await read.PostAsJsonAsync(Route(patient,row.HistoryUid)+"/archive",new{updated.RowVersion})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await a.PutAsJsonAsync(Route(patient,row.HistoryUid),Edit(row,new()))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await a.PostAsJsonAsync(Route(patient,row.HistoryUid)+"/archive",new{row.RowVersion})).StatusCode);
        foreach(var token in new[]{"bad",Convert.ToBase64String(new byte[7])}){var x=Edit(updated,new());x.RowVersion=token;Assert.Equal(HttpStatusCode.Conflict,(await a.PutAsJsonAsync(Route(patient,row.HistoryUid),x)).StatusCode);}
        foreach(var (client,p,target) in new[]{(a,other,updated),(b,tenantPatient,updated),(b,patient,updated),(a,patient,foreign),(a,tenantPatient,foreign)})
        {
            Assert.Equal(HttpStatusCode.NotFound,(await client.PutAsJsonAsync(Route(p,target.HistoryUid),Edit(target,new()))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync(Route(p,target.HistoryUid)+"/archive",new{target.RowVersion})).StatusCode);
            Assert.DoesNotContain((await client.GetFromJsonAsync<PatientClinicalHistoryResponse[]>(Route(p)))!,x=>x.HistoryUid==target.HistoryUid);
        }
        Assert.Equal(HttpStatusCode.NotFound,(await b.PostAsJsonAsync(Route(patient),Draft())).StatusCode);
        Assert.Equal(before,await db.Snapshot(false));Assert.Equal(beforeB,await db.Snapshot(true));
    }

    [Fact]
    public async Task AuditFailureRollsBackCreateEditAndArchiveAndArchiveRetainsAllFields()
    {
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);
        var row=await db.Repository().CreateAsync(patient,Draft(),actor.Actor);var before=await db.Snapshot(false);
        await db.Execute(false,"CREATE TRIGGER dbo.HistoryAuditFailure ON dbo.AuditLog AFTER INSERT AS BEGIN THROW 52996,'Synthetic history audit failure.',1; END");
        try
        {
            await Assert.ThrowsAsync<SqlException>(()=>db.Repository().CreateAsync(patient,Draft(),actor.Actor));
            await Assert.ThrowsAsync<SqlException>(()=>db.Repository().UpdateAsync(patient,row.HistoryUid,Edit(row,new(){Notes="TEST ONLY rejected"}),actor.Actor));
            await Assert.ThrowsAsync<SqlException>(()=>db.Repository().ArchiveAsync(patient,row.HistoryUid,row.RowVersion,actor.Actor));
            Assert.Equal(before,await db.Snapshot(false));
        }
        finally{await db.Execute(false,"DROP TRIGGER dbo.HistoryAuditFailure");}
        var archived=(await db.Repository().ArchiveAsync(patient,row.HistoryUid,row.RowVersion,actor.Actor))!;
        Assert.Equal("Archived",archived.Status);Assert.NotEqual(row.RowVersion,archived.RowVersion);Assert.Equal(row.ClinicalFields!.Notes,archived.ClinicalFields!.Notes);Assert.Empty(await db.Repository().ListAsync(patient,"Active"));
        await Assert.ThrowsAsync<PatientClinicalHistoryArchivedException>(()=>db.Repository().UpdateAsync(patient,row.HistoryUid,Edit(archived,new()),actor.Actor));
        var audit=(await db.Clinical.Audit(false,row.HistoryUid,"PatientClinicalHistory")).Last();Assert.Equal("Archive",audit.Action);Assert.Contains("TEST ONLY notes",audit.Old);Assert.Contains("TEST ONLY notes",audit.New);
        await Assert.ThrowsAsync<SqlException>(()=>db.Repository().CreateAsync(patient,Draft(),long.MaxValue));
    }

    [Fact]
    public async Task SelectedHistoryPrintUsesAllRealActiveRowsExactPrecisionAndEscapedFields()
    {
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);
        for(var i=0;i<8;i++) await db.Repository().CreateAsync(patient,new(){HistoryType="Medical",Description=$"TEST ONLY print {i}",ClinicalFields=new(){OnsetDate=new(){Year=2000,Month=2},ResolutionDate=new(){Year=2010},ClinicalStatus="Resolved",LifeStageCode="C",Notes="TEST ONLY <script> notes"}},actor.Actor);
        var surgery=await db.Repository().CreateAsync(patient,new(){HistoryType="Surgical",Description="TEST ONLY printed procedure",ClinicalFields=new(){ProcedureDate=new(){Year=2004,Month=5,Day=6}}},actor.Actor);
        var archive=await db.Repository().CreateAsync(patient,new(){HistoryType="Medical",Description="TEST ONLY archived never print"},actor.Actor);
        await db.Repository().ArchiveAsync(patient,archive.HistoryUid,archive.RowVersion,actor.Actor);
        var fixture=new CppSelectivePrintTests.Fixture{ActualHistory=new PatientClinicalHistoryService(db.Repository())};fixture.Patient.PatientUid=patient;fixture.Data=fixture.Data with {PatientUid=patient};
        var html=System.Text.Encoding.UTF8.GetString((await fixture.Print("History"))!);
        foreach(var text in new[]{"TEST ONLY print 7","2000-02","2010","2004-05-06","Child","Resolved","TEST ONLY &lt;script&gt; notes"})Assert.Contains(text,html);
        var procedurePosition=html.IndexOf("TEST ONLY printed procedure",StringComparison.Ordinal);
        var procedureBlock=html[procedurePosition..html.IndexOf("</dl>",procedurePosition,StringComparison.Ordinal)];
        Assert.DoesNotContain("Onset Date",procedureBlock);Assert.DoesNotContain("Life Stage",procedureBlock);Assert.DoesNotContain("Clinical Status",procedureBlock);
        Assert.DoesNotContain("2000-02-01",html);Assert.DoesNotContain("<script>",html);Assert.DoesNotContain("archived never print",html);Assert.DoesNotContain("Unselected allergen",html);
        Assert.DoesNotContain("TEST ONLY print",System.Text.Encoding.UTF8.GetString((await fixture.Print("Problems"))!));
        var foreign=new CppSelectivePrintTests.Fixture{ActualHistory=new PatientClinicalHistoryService(db.Repository())};
        foreign.ActualHistory=CppSelectivePrintTests.Stub<IPatientClinicalHistoryService>((_,_)=>Task.FromResult<IReadOnlyList<PatientClinicalHistoryResponse>>([surgery]));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>foreign.Print("History"));
    }

    [Fact]
    public async Task TrustedActorCannotBeSpoofedAndUnresolvedOrInactiveActorsCannotWrite()
    {
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);var inactive=await db.Clinical.SeedUser(false);
        await db.Execute(false,$"UPDATE dbo.ApplicationUser SET IsActive=0 WHERE UserId={inactive.Actor}");
        var missing=new FamilyHistoryAcceptanceDatabase.User("history-unresolved-"+Guid.NewGuid(),long.MaxValue,true);
        await using var host=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,actor,inactive,missing);
        using var good=host.Client(actor);using var denied=host.Client(inactive);using var unresolved=host.Client(missing);
        var result=await good.PostAsJsonAsync(Route(patient),new{HistoryType="Medical",Description="TEST ONLY trusted actor",CreatedBy=long.MaxValue,Actor=long.MaxValue,ClinicalFields=new{Notes="TEST ONLY actor"}});
        Assert.Equal(HttpStatusCode.Created,result.StatusCode);var row=(await result.Content.ReadFromJsonAsync<PatientClinicalHistoryResponse>())!;Assert.Equal(actor.Actor,row.CreatedBy);
        Assert.Equal(actor.Actor,Assert.Single(await db.Clinical.Audit(false,row.HistoryUid,"PatientClinicalHistory")).Actor);
        var before=await db.Snapshot(false);
        foreach(var client in new[]{denied,unresolved})
        {
            Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync(Route(patient),Draft())).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.PutAsJsonAsync(Route(patient,row.HistoryUid),Edit(row,new()))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync(Route(patient,row.HistoryUid)+"/archive",new{row.RowVersion})).StatusCode);
        }
        Assert.Equal(before,await db.Snapshot(false));
        Assert.Equal(HttpStatusCode.BadRequest,(await good.PutAsJsonAsync(Route(patient,row.HistoryUid),new{row.HistoryType,row.Description})).StatusCode);
        Assert.Equal(before,await db.Snapshot(false));
    }

    [Theory]
    [InlineData("[]", false)]
    [InlineData("[{}]", false)]
    [InlineData("null", false)]
    [InlineData("42", false)]
    [InlineData("\"text\"", false)]
    [InlineData("{broken", false)]
    [InlineData(" \t\r\n{\"Notes\":\"TEST ONLY JSON whitespace\"}", true)]
    public async Task ClinicalFieldJsonRequiresAnObjectForCreateAndUpdate(string json,bool valid)
    {
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);
        async Task Save(bool update,PatientClinicalHistoryResponse? row=null)
        {
            await using var c=await db.Clinical.Open(false);
            await using var q=new SqlCommand(update?"dbo.PatientClinicalHistory_Update":"dbo.PatientClinicalHistory_Create",c){CommandType=System.Data.CommandType.StoredProcedure};
            q.Parameters.AddWithValue("@PatientUid",patient);q.Parameters.AddWithValue("@HistoryType","Medical");q.Parameters.AddWithValue("@Description","TEST ONLY JSON root");
            q.Parameters.AddWithValue("@Actor",actor.Actor);q.Parameters.AddWithValue("@ClinicalFields",json);
            if(update){q.Parameters.AddWithValue("@HistoryUid",row!.HistoryUid);q.Parameters.AddWithValue("@ExpectedRowVersion",Convert.FromBase64String(row.RowVersion));}
            await q.ExecuteNonQueryAsync();
        }
        var before=await db.Snapshot(false);
        if(valid) await Save(false);
        else
        {
            Assert.Equal(52107,(await Assert.ThrowsAsync<SqlException>(()=>Save(false))).Number);
            Assert.Equal(before,await db.Snapshot(false));
            await db.Repository().CreateAsync(patient,Draft(),actor.Actor);
        }
        var row=Assert.Single(await db.Repository().ListAsync(patient,"All"));before=await db.Snapshot(false);
        if(valid)
        {
            Assert.Equal("TEST ONLY JSON whitespace",row.ClinicalFields!.Notes);
            await Save(true,row);
            Assert.Equal("TEST ONLY JSON whitespace",Assert.Single(await db.Repository().ListAsync(patient,"All")).ClinicalFields!.Notes);
        }
        else
        {
            Assert.Equal(52107,(await Assert.ThrowsAsync<SqlException>(()=>Save(true,row))).Number);
            Assert.Equal(before,await db.Snapshot(false));
        }
    }

    internal static CreatePatientClinicalHistoryRequest Draft()=>new(){HistoryType="Medical",Description="TEST ONLY condition",ClinicalFields=new(){OnsetDate=new(){Year=2000,Month=2},LifeStageCode="C",Notes="TEST ONLY notes"}};
    internal static UpdatePatientClinicalHistoryRequest Edit(PatientClinicalHistoryResponse row,HistoryClinicalFields? fields)=>new(){HistoryType=row.HistoryType,Description=row.Description,RelevantDate=row.RelevantDate,RowVersion=row.RowVersion,ClinicalFields=fields};
    internal static string Route(Guid patient,Guid? history=null)=>$"api/patients/{patient}/clinical-history"+(history.HasValue?$"/{history}":"");
}

public sealed class HistoryFieldDatabase : IAsyncLifetime
{
    internal FamilyHistoryAcceptanceDatabase Clinical {get;}=new();
    internal Guid LegacyPatient,LegacyUid; internal string LegacyToken="",ArchivedToken="",LegacyAudit="";
    public async Task InitializeAsync()
    {
        await Clinical.InitializeAsync();
        try
        {
            foreach(var other in new[]{false,true})
            {
                await Apply(other,"0041-patient-clinical-history.sql");
                if(!other)
                {
                    LegacyPatient=await Clinical.SeedPatient(false);var actor=await Clinical.SeedUser(false);
                    var row=await LegacyCreate(actor.Actor,"TEST ONLY legacy");LegacyUid=row.Uid;LegacyToken=row.Token;LegacyAudit=Assert.Single(await Clinical.Audit(false,LegacyUid,"PatientClinicalHistory")).New;
                    var archived=await LegacyCreate(actor.Actor,"TEST ONLY archived legacy",true);ArchivedToken=archived.Token;
                }
                await Apply(other,"0078-clinical-history-fields.sql");
            }
        }
        catch{await Clinical.DisposeAsync();throw;}
    }
    private async Task<(Guid Uid,string Token)> LegacyCreate(long actor,string text,bool archived=false)
    {
        await using var c=await Clinical.Open(false);
        await using var cmd=new SqlCommand("EXEC dbo.PatientClinicalHistory_Create @PatientUid=@Patient,@HistoryType=N'Medical',@Description=@Text,@RelevantDate='1998-03-04',@Actor=@Actor",c);
        cmd.Parameters.AddWithValue("@Patient",LegacyPatient);cmd.Parameters.AddWithValue("@Text",text);cmd.Parameters.AddWithValue("@Actor",actor);
        Guid uid;byte[] token;
        await using(var r=await cmd.ExecuteReaderAsync()){await r.ReadAsync();uid=r.GetGuid(r.GetOrdinal("HistoryUid"));token=(byte[])r["RowVersion"];}
        if(archived)
        {
            await using var archive=new SqlCommand("EXEC dbo.PatientClinicalHistory_Archive @Patient,@History,@Version,@Actor",c);
            archive.Parameters.AddWithValue("@Patient",LegacyPatient);archive.Parameters.AddWithValue("@History",uid);archive.Parameters.AddWithValue("@Version",token);archive.Parameters.AddWithValue("@Actor",actor);
            await using var r=await archive.ExecuteReaderAsync();await r.ReadAsync();token=(byte[])r["RowVersion"];
        }
        return(uid,Convert.ToBase64String(token));
    }
    public Task DisposeAsync()=>Clinical.DisposeAsync();
    internal PatientClinicalHistoryRepository Repository(bool other=false)=>new(Clinical.Factory(other));
    internal async Task Apply(bool other,string file)
    {
        var sql=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"database","tenant-clinical","migrations",file));
        foreach(var batch in Regex.Split(sql,@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase))if(!string.IsNullOrWhiteSpace(batch))await Execute(other,batch);
    }
    internal async Task Execute(bool other,string sql){await using var c=await Clinical.Open(other);await using var q=new SqlCommand(sql,c);await q.ExecuteNonQueryAsync();}
    internal async Task<string> Snapshot(bool other)
    {
        await using var c=await Clinical.Open(other);await using var q=new SqlCommand("SELECT (SELECT * FROM dbo.PatientClinicalHistory ORDER BY HistoryUid FOR JSON PATH,INCLUDE_NULL_VALUES) Clinical,(SELECT * FROM dbo.AuditLog ORDER BY AuditLogId FOR JSON PATH,INCLUDE_NULL_VALUES) Audit FOR JSON PATH,WITHOUT_ARRAY_WRAPPER",c);
        await using var r=await q.ExecuteReaderAsync();var text=new System.Text.StringBuilder();while(await r.ReadAsync())text.Append(r.GetString(0));return text.ToString();
    }
}
