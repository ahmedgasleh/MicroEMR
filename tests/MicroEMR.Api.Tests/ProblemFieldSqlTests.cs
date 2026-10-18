using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.PatientClinicalHistory;
using MicroEMR.Application.PatientProblems;
using MicroEMR.Application.PatientProblems.Contracts;
using MicroEMR.Application.PatientProblems.Services;
using MicroEMR.Infrastructure.PatientProblems;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ProblemFieldSqlTests(ProblemFieldDatabase db) : IClassFixture<ProblemFieldDatabase>
{
    [Theory]
    [InlineData("N",2000,null,null)] [InlineData("I",2000,2,null)] [InlineData("C",2000,2,29)] [InlineData("T",2001,3,4)] [InlineData("A",null,null,null)]
    public async Task ApiRoundTripPreservesClinicalFieldsPrecisionAndCompleteAudit(string stage,int? year,int? month,int? day)
    {
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);
        await using var host=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,actor);using var client=host.Client(actor);
        var start=await db.Clinical.Utc(false);
        var draft=Draft();draft.ClinicalFields=new(){OnsetDate=new(){Year=year,Month=month,Day=day},LifeStageCode=stage,Notes="TEST ONLY notes <script>"};
        var create=await client.PostAsJsonAsync(Route(patient),draft);Assert.Equal(HttpStatusCode.Created,create.StatusCode);
        var row=(await create.Content.ReadFromJsonAsync<PatientProblemResponse>())!;
        var read=Assert.Single(await db.Repository().GetByPatientUidAsync(patient,"All"));
        Assert.Equal(row.RowVersion,read.RowVersion);Assert.Equal(stage,read.ClinicalFields!.LifeStageCode);Assert.Equal(year,read.ClinicalFields.OnsetDate?.Year);Assert.Equal(month,read.ClinicalFields.OnsetDate?.Month);Assert.Equal(day,read.ClinicalFields.OnsetDate?.Day);
        Assert.Equal(day.HasValue,read.OnsetDate.HasValue);Assert.Equal("TEST ONLY notes <script>",read.ClinicalFields.Notes);Assert.Equal(draft.ProblemDescription,read.ProblemDescription);
        Assert.Equal(actor.Actor,read.CreatedBy);Assert.InRange(read.CreatedAt,start.AddSeconds(-1),(await db.Clinical.Utc(false)).AddSeconds(1));
        var edit=Edit(row,new(){OnsetDate=draft.ClinicalFields.OnsetDate,LifeStageCode=stage,Notes="TEST ONLY amended clinical notes"});
        var save=await client.PutAsJsonAsync(Route(patient,row.PatientProblemUid),edit);Assert.Equal(HttpStatusCode.OK,save.StatusCode);
        var updated=(await save.Content.ReadFromJsonAsync<PatientProblemResponse>())!;Assert.NotEqual(row.RowVersion,updated.RowVersion);Assert.Equal(row.CreatedAt,updated.CreatedAt);Assert.Equal(row.ProblemDescription,updated.ProblemDescription);
        var audit=await db.Clinical.Audit(false,row.PatientProblemUid,"PatientProblem");Assert.Equal(2,audit.Count);
        foreach(var a in audit){Assert.Equal(actor.Actor,a.Actor);Assert.Equal(actor.Subject,a.Subject);Assert.Equal(patient,a.Patient);}
        using var before=JsonDocument.Parse(audit[1].Old!);using var after=JsonDocument.Parse(audit[1].New);
        Assert.Equal("TEST ONLY notes <script>",before.RootElement.GetProperty("Notes").GetString());Assert.Equal("TEST ONLY amended clinical notes",after.RootElement.GetProperty("Notes").GetString());
        Assert.Equal(year,after.RootElement.GetProperty("OnsetYear").Deserialize<int?>());Assert.Equal(stage,after.RootElement.GetProperty("LifeStageCode").GetString());
        Assert.Equal(db.Clinical.Tenant(false).TenantUid,after.RootElement.GetProperty("TenantUid").GetGuid());Assert.Equal(patient,after.RootElement.GetProperty("PatientUid").GetGuid());
        var correlation=after.RootElement.GetProperty("RequestCorrelationId").GetString();Assert.False(string.IsNullOrWhiteSpace(correlation));
        Assert.Equal(correlation,await db.Read<string>(false,"SELECT TOP(1) RequestCorrelationId FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@Uid) ORDER BY AuditLogId DESC",row.PatientProblemUid));
    }

    [Fact]
    public async Task ClinicalResolutionIsSeparateFromServerTransitionAndTerminalHistoryIsRetained()
    {
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);
        await using var host=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,actor);using var client=host.Client(actor);
        var row=await db.Repository().CreateAsync(patient,Draft(),actor.Actor);var from=await db.Clinical.Utc(false);
        var response=await client.PostAsJsonAsync(Route(patient,row.PatientProblemUid)+"/resolve",new ResolvePatientProblemRequest{RowVersion=row.RowVersion,ClinicalResolutionDate=new(){Year=2010},ResolutionReason="TEST ONLY resolution reason"});
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);var resolved=(await response.Content.ReadFromJsonAsync<PatientProblemResponse>())!;
        Assert.Equal("2010",resolved.ClinicalResolutionDate!.Display);Assert.InRange(resolved.ResolvedAt!.Value,from.AddSeconds(-1),(await db.Clinical.Utc(false)).AddSeconds(1));
        Assert.Equal(row.ClinicalFields!.Notes,resolved.ClinicalFields!.Notes);Assert.Equal(row.ProblemDescription,resolved.ProblemDescription);Assert.Equal(actor.Actor,resolved.ResolvedBy);Assert.NotEqual(row.RowVersion,resolved.RowVersion);
        var snapshot=await db.Snapshot(false);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(Route(patient,row.PatientProblemUid)+"/resolve",new ResolvePatientProblemRequest{RowVersion=resolved.RowVersion,ClinicalResolutionDate=new(){Year=2011}})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync(Route(patient,row.PatientProblemUid),Edit(resolved,new()))).StatusCode);
        Assert.Equal(snapshot,await db.Snapshot(false));Assert.Empty(await db.Repository().GetByPatientUidAsync(patient,"Active"));Assert.Single(await db.Repository().GetByPatientUidAsync(patient,"Resolved"));
        var audit=(await db.Clinical.Audit(false,row.PatientProblemUid,"PatientProblem")).Last();Assert.Equal("Resolve",audit.Action);Assert.Contains("TEST ONLY resolution reason",audit.New);Assert.Contains("TEST ONLY notes",audit.New);Assert.Contains("ClinicalResolutionYear",audit.New);
        var unknown=await db.Repository().CreateAsync(patient,Draft(),actor.Actor);var old=(await db.Repository().ResolveAsync(patient,unknown.PatientProblemUid,new(){RowVersion=unknown.RowVersion},actor.Actor))!;Assert.Null(old.ClinicalResolutionDate);Assert.NotNull(old.ResolvedAt);
    }

    [Fact]
    public async Task LegacyUpgradeAndOmittedFieldsPreserveValuesAndExplicitClearIsSafe()
    {
        Assert.Equal(db.LegacyAudit,await db.AuditSnapshot(false));
        var legacy=(await db.Repository().GetByUidAsync(db.LegacyPatient,db.LegacyUid))!;Assert.Equal(db.LegacyToken,legacy.RowVersion);Assert.Equal(new DateTime(1998,3,4),legacy.OnsetDate);Assert.Equal("1998-03-04",legacy.EffectiveOnset!.Display);Assert.Null(legacy.ClinicalFields!.LifeStageCode);
        var historical=(await db.Repository().GetByUidAsync(db.LegacyPatient,db.ResolvedUid))!;Assert.Equal(db.ResolvedToken,historical.RowVersion);Assert.Null(historical.ClinicalResolutionDate);Assert.NotNull(historical.ResolvedAt);
        var actor=await db.Clinical.SeedUser(false);
        var partial=(await db.Repository().UpdateAsync(db.LegacyPatient,legacy.PatientProblemUid,Edit(legacy,new(){OnsetDate=new(){Year=1990,Month=2},LifeStageCode="C",Notes="TEST ONLY preserve"}),actor.Actor))!;
        var omitted=(await db.Repository().UpdateAsync(db.LegacyPatient,partial.PatientProblemUid,Edit(partial,null),actor.Actor))!;Assert.Equal("1990-02",omitted.EffectiveOnset!.Display);Assert.Null(omitted.OnsetDate);Assert.Equal("TEST ONLY preserve",omitted.ClinicalFields!.Notes);
        var oldNull=Edit(omitted,null);oldNull.OnsetDate=null;var before=await db.Snapshot(false);
        await Assert.ThrowsAsync<PatientProblemValidationException>(()=>db.Repository().UpdateAsync(db.LegacyPatient,omitted.PatientProblemUid,oldNull,actor.Actor));Assert.Equal(before,await db.Snapshot(false));
        var exact=Edit(omitted,null);exact.OnsetDate=new(2001,2,3);var changed=(await db.Repository().UpdateAsync(db.LegacyPatient,omitted.PatientProblemUid,exact,actor.Actor))!;Assert.Equal("2001-02-03",changed.EffectiveOnset!.Display);Assert.Equal("C",changed.ClinicalFields!.LifeStageCode);
        var cleared=(await db.Repository().UpdateAsync(db.LegacyPatient,changed.PatientProblemUid,Edit(changed,new()),actor.Actor))!;Assert.Null(cleared.EffectiveOnset);Assert.Null(cleared.ClinicalFields!.Notes);Assert.Null(cleared.ClinicalFields.LifeStageCode);Assert.Equal(legacy.ProblemDescription,cleared.ProblemDescription);
    }

    [Theory]
    [InlineData(false,false)] [InlineData(false,true)] [InlineData(true,false)] [InlineData(true,true)]
    public async Task ConcurrentEditAndResolveAllowOnlyOneCurrentVersionWrite(bool firstResolve,bool secondResolve)
    {
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);
        await using var host=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,actor);using var client=host.Client(actor);
        var row=await db.Repository().CreateAsync(patient,Draft(),actor.Actor);
        Task<HttpResponseMessage> Save(bool resolve)=>resolve?client.PostAsJsonAsync(Route(patient,row.PatientProblemUid)+"/resolve",new ResolvePatientProblemRequest{RowVersion=row.RowVersion,ClinicalResolutionDate=new(){Year=2010}}):client.PutAsJsonAsync(Route(patient,row.PatientProblemUid),Edit(row,new(){Notes="TEST ONLY competing edit"}));
        var results=await Task.WhenAll(Save(firstResolve),Save(secondResolve));Assert.Single(results,x=>x.StatusCode==HttpStatusCode.OK);Assert.Single(results,x=>x.StatusCode==HttpStatusCode.Conflict);
        Assert.Equal(2,(await db.Clinical.Audit(false,row.PatientProblemUid,"PatientProblem")).Count);
        var before=await db.Snapshot(false);Assert.Equal(HttpStatusCode.Conflict,(await Save(false)).StatusCode);Assert.Equal(HttpStatusCode.Conflict,(await Save(true)).StatusCode);Assert.Equal(before,await db.Snapshot(false));
    }

    [Fact]
    public async Task PermissionsActorsMissingTokensAndForeignScopesCannotMutateOrDisclose()
    {
        var patient=await db.Clinical.SeedPatient(false);var other=await db.Clinical.SeedPatient(false);var tenantPatient=await db.Clinical.SeedPatient(true);
        var writer=await db.Clinical.SeedUser(false);var reader=await db.Clinical.SeedUser(false,false);var hidden=await db.Clinical.SeedUser(false,true,false);var bWriter=await db.Clinical.SeedUser(true);var inactive=await db.Clinical.SeedUser(false);
        await db.Execute(false,$"UPDATE dbo.ApplicationUser SET IsActive=0 WHERE UserId={inactive.Actor}");
        var missing=new FamilyHistoryAcceptanceDatabase.User("problem-unresolved-"+Guid.NewGuid(),long.MaxValue,true);
        await using var aHost=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,writer,reader,hidden,inactive,missing);await using var bHost=await FamilyHistoryAcceptanceHost.Start(db.Clinical,true,bWriter);
        using var a=aHost.Client(writer);using var b=bHost.Client(bWriter);using var read=aHost.Client(reader);using var noRead=aHost.Client(hidden);using var disabled=aHost.Client(inactive);using var unresolved=aHost.Client(missing);
        var create=await a.PostAsJsonAsync(Route(patient),new{ProblemName="TEST ONLY trusted",CreatedBy=long.MaxValue,RequestCorrelationId="spoofed"});Assert.Equal(HttpStatusCode.Created,create.StatusCode);
        var row=(await create.Content.ReadFromJsonAsync<PatientProblemResponse>())!;Assert.Equal(writer.Actor,row.CreatedBy);Assert.DoesNotContain("spoofed",Assert.Single(await db.Clinical.Audit(false,row.PatientProblemUid,"PatientProblem")).New);
        var foreign=await db.Repository(true).CreateAsync(tenantPatient,Draft(),bWriter.Actor);
        var before=await db.Snapshot(false);var beforeB=await db.Snapshot(true);
        Assert.Equal(HttpStatusCode.OK,(await read.GetAsync(Route(patient))).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await noRead.GetAsync(Route(patient))).StatusCode);
        foreach(var client in new[]{read,disabled,unresolved})
        {
            Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync(Route(patient),Draft())).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.PutAsJsonAsync(Route(patient,row.PatientProblemUid),Edit(row,new()))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync(Route(patient,row.PatientProblemUid)+"/resolve",new ResolvePatientProblemRequest{RowVersion=row.RowVersion})).StatusCode);
        }
        foreach(var (client,p,target) in new[]{(a,other,row),(b,tenantPatient,row),(b,patient,row),(a,patient,foreign),(a,tenantPatient,foreign)})
        {
            var uri=Route(p,target.PatientProblemUid);Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(uri)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await client.PutAsJsonAsync(uri,Edit(target,new()))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync(uri+"/resolve",new ResolvePatientProblemRequest{RowVersion=target.RowVersion})).StatusCode);
            Assert.DoesNotContain((await client.GetFromJsonAsync<PatientProblemResponse[]>(Route(p)))!,x=>x.PatientProblemUid==target.PatientProblemUid);
        }
        foreach(var token in new[]{"","bad",Convert.ToBase64String(new byte[7])})
        {
            var edit=Edit(row,new());edit.RowVersion=token;Assert.Equal(HttpStatusCode.BadRequest,(await a.PutAsJsonAsync(Route(patient,row.PatientProblemUid),edit)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,(await a.PostAsJsonAsync(Route(patient,row.PatientProblemUid)+"/resolve",new ResolvePatientProblemRequest{RowVersion=token})).StatusCode);
        }
        Assert.Equal(HttpStatusCode.NotFound,(await b.PostAsJsonAsync(Route(patient),Draft())).StatusCode);
        Assert.Equal(before,await db.Snapshot(false));Assert.Equal(beforeB,await db.Snapshot(true));
        await Assert.ThrowsAsync<SqlException>(()=>db.Repository().CreateAsync(patient,Draft(),null));
        var deleted=await db.Clinical.SeedPatient(false);await db.Execute(false,$"UPDATE dbo.Patient SET IsDeleted=1 WHERE PatientUid='{deleted}'");
        Assert.Empty(await db.Repository().GetByPatientUidAsync(deleted,"All"));Assert.Equal(HttpStatusCode.NotFound,(await a.PostAsJsonAsync(Route(deleted),Draft())).StatusCode);
    }

    [Fact]
    public async Task InvalidClinicalInputsAndChronologyLeaveStateAndAuditUnchanged()
    {
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);
        await using var host=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,actor);using var client=host.Client(actor);
        var before=await db.Snapshot(false);
        ProblemClinicalFields[] invalid=[new(){OnsetDate=new(){Month=2}},new(){OnsetDate=new(){Year=2001,Month=2,Day=29}},new(){OnsetDate=new(){Year=2000,Day=1}},new(){OnsetDate=new(){Year=10000}},new(){LifeStageCode="a"},new(){LifeStageCode="Adult"},new(){Notes=new string('x',1001)}];
        foreach(var fields in invalid)
        {
            var input=Draft();input.ClinicalFields=fields;Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(Route(patient),input)).StatusCode);
            await Assert.ThrowsAsync<PatientProblemValidationException>(()=>db.Repository().CreateAsync(patient,input,actor.Actor));
        }
        Assert.Equal(before,await db.Snapshot(false));
        var row=await db.Repository().CreateAsync(patient,Draft(),actor.Actor);before=await db.Snapshot(false);
        var noUpdateToken=await Assert.ThrowsAsync<SqlException>(()=>db.Execute(false,$"EXEC dbo.PatientProblem_Update @PatientUid='{patient}',@PatientProblemUid='{row.PatientProblemUid}',@ProblemName=N'TEST ONLY blind write',@UpdatedBy={actor.Actor}"));Assert.Equal(51073,noUpdateToken.Number);
        var noResolveToken=await Assert.ThrowsAsync<SqlException>(()=>db.Execute(false,$"EXEC dbo.PatientProblem_Resolve @PatientUid='{patient}',@PatientProblemUid='{row.PatientProblemUid}',@ResolvedBy={actor.Actor}"));Assert.Equal(51073,noResolveToken.Number);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(Route(patient,row.PatientProblemUid)+"/resolve",new ResolvePatientProblemRequest{RowVersion=row.RowVersion,ClinicalResolutionDate=new(){Year=1999}})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(Route(patient),new{ProblemName="TEST ONLY ambiguous",OnsetDate="2000-01-01",ClinicalFields=new{OnsetDate=new{Year=2001}}})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync(Route(patient,row.PatientProblemUid),new{row.ProblemName,row.ProblemDescription,row.RowVersion,OnsetDate=(string?)null})).StatusCode);
        Assert.Equal(before,await db.Snapshot(false));
        var overlap=(await db.Repository().ResolveAsync(patient,row.PatientProblemUid,new(){RowVersion=row.RowVersion,ClinicalResolutionDate=new(){Year=2000}},actor.Actor))!;Assert.Equal("2000",overlap.ClinicalResolutionDate!.Display);
    }

    [Fact]
    public async Task AuditFailureRollsBackEveryMutationAndNoAuditTableCannotAllowSuccess()
    {
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);
        var row=await db.Repository().CreateAsync(patient,Draft(),actor.Actor);var before=await db.Snapshot(false);
        await db.Execute(false,"CREATE TRIGGER dbo.ProblemAuditFailure ON dbo.AuditLog AFTER INSERT AS BEGIN THROW 52995,'Synthetic problem audit failure.',1; END");
        try
        {
            await Assert.ThrowsAsync<SqlException>(()=>db.Repository().CreateAsync(patient,Draft(),actor.Actor));
            await Assert.ThrowsAsync<SqlException>(()=>db.Repository().UpdateAsync(patient,row.PatientProblemUid,Edit(row,new(){Notes="TEST ONLY rejected"}),actor.Actor));
            await Assert.ThrowsAsync<SqlException>(()=>db.Repository().ResolveAsync(patient,row.PatientProblemUid,new(){RowVersion=row.RowVersion},actor.Actor));
            Assert.Equal(before,await db.Snapshot(false));
        }
        finally{await db.Execute(false,"DROP TRIGGER dbo.ProblemAuditFailure");}
        // Fresh disposable store: recompile against an absent AuditLog name; retain the original table for restoration.
        var isolated=new ProblemFieldDatabase();await isolated.InitializeAsync();
        try
        {
            var p=await isolated.Clinical.SeedPatient(false);var a=await isolated.Clinical.SeedUser(false);
            await isolated.Execute(false,"EXEC sp_rename N'dbo.AuditLog',N'ProblemAuditRetainedFixture'; EXEC sp_recompile N'dbo.PatientProblem_Create';");
            try{await Assert.ThrowsAsync<SqlException>(()=>isolated.Repository().CreateAsync(p,Draft(),a.Actor));Assert.Equal(0,await isolated.Read<int>(false,"SELECT COUNT(*) FROM dbo.PatientProblem WHERE PatientUid=@Uid",p));}
            finally{await isolated.Execute(false,"EXEC sp_rename N'dbo.ProblemAuditRetainedFixture',N'AuditLog';");}
        }
        finally{await isolated.DisposeAsync();}
    }

    [Fact]
    public async Task CppUsesRealFullSourcePrintsAllFieldsAndKeepsCompactNotesPrivate()
    {
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);
        for(var i=0;i<8;i++){var draft=Draft();draft.ProblemName=$"TEST ONLY print {i}";draft.ClinicalFields!.Notes="TEST ONLY notes <script>\n"+string.Join("\n",Enumerable.Range(0,26).Select(n=>$"Line {n}"))+$"\nTail{i}";await db.Repository().CreateAsync(patient,draft,actor.Actor);}
        var terminal=await db.Repository().CreateAsync(patient,new(){ProblemName="TEST ONLY resolved excluded"},actor.Actor);await db.Repository().ResolveAsync(patient,terminal.PatientProblemUid,new(){RowVersion=terminal.RowVersion},actor.Actor);
        var calls=0;var repository=db.Repository();
        var service=CppSelectivePrintTests.Stub<MicroEMR.Application.PatientProblems.Services.IPatientProblemService>((method,args)=>
        {
            Assert.Equal("GetByPatientUidAsync",method);calls++;return repository.GetByPatientUidAsync((Guid)args[0]!,"Active");
        });
        var source=CppSelectivePrintTests.Source(patient,service,CppSelectivePrintTests.Stub<MicroEMR.Application.ReadAudit.IPatientChartReadAuditService>((_,_)=>Task.FromResult(Guid.NewGuid())));
        var summary=(await source.GetAsync(patient,"test"))!;Assert.Equal(5,summary.Problems.Items.Count);Assert.Equal(8,summary.Problems.TotalCount);Assert.Equal("2000-02",summary.Problems.Items[0].OnsetDisplay);Assert.Equal("Child",summary.Problems.Items[0].LifeStage);Assert.Null(summary.ProblemsForPrint);
        Assert.DoesNotContain("TEST ONLY notes",JsonSerializer.Serialize(summary));
        var fixture=new CppSelectivePrintTests.Fixture{ActualSource=source};fixture.Patient.PatientUid=patient;
        var prior=calls;var html=System.Text.Encoding.UTF8.GetString((await fixture.Print("Problems"))!);Assert.Equal(prior+1,calls);
        foreach(var text in new[]{"TEST ONLY print 7","TEST ONLY notes","TEST ONLY description","2000-02","Child"})Assert.Contains(text,html);
        Assert.Contains("&lt;script&gt;",html);Assert.DoesNotContain("<script>",html);
        Assert.DoesNotContain("resolved excluded",html);Assert.DoesNotContain("2000-02-01",html);Assert.DoesNotContain("Row Version",html);Assert.DoesNotContain(patient.ToString(),html);
        await using(var renderer=new MicroEMR.Infrastructure.ClinicalOutput.PlaywrightPdfRenderer(NullLogger<MicroEMR.Infrastructure.ClinicalOutput.PlaywrightPdfRenderer>.Instance))
        {
            using var pdf=UglyToad.PdfPig.PdfDocument.Open(await renderer.RenderAsync(html));Assert.True(pdf.NumberOfPages>1);
            var words=pdf.GetPages().SelectMany(page=>page.GetWords().Select(word=>(Page:page.Number,Word:word))).ToArray();
            var label=words.First(x=>x.Word.Text=="Stage");Assert.Contains(words,x=>x.Page==label.Page && x.Word.Text=="Child" && x.Word.BoundingBox.Left>label.Word.BoundingBox.Right && Math.Abs(x.Word.BoundingBox.Bottom-label.Word.BoundingBox.Bottom)<2);
            Assert.Contains(words,x=>x.Word.Text=="Tail7");
        }
        var selected=(await source.GetForPrintAsync(patient,["Problems"],"test"))!;Assert.Equal(8,selected.ProblemsForPrint!.Items.Count);Assert.DoesNotContain("TEST ONLY notes",JsonSerializer.Serialize(selected));
        var priorUnselected=calls;await source.GetForPrintAsync(patient,["Allergies"],"test");Assert.Equal(priorUnselected,calls);
    }

    internal static CreatePatientProblemRequest Draft()=>new(){ProblemName="TEST ONLY condition",ProblemDescription="TEST ONLY description",ClinicalFields=new(){OnsetDate=new(){Year=2000,Month=2},LifeStageCode="C",Notes="TEST ONLY notes"}};
    internal static UpdatePatientProblemRequest Edit(PatientProblemResponse row,ProblemClinicalFields? fields)=>new(){ProblemName=row.ProblemName,ProblemDescription=row.ProblemDescription,RowVersion=row.RowVersion,ClinicalFields=fields};
    internal static string Route(Guid patient,Guid? problem=null)=>$"api/patients/{patient}/problems"+(problem.HasValue?$"/{problem}":"");
}

public sealed class ProblemFieldDatabase : IAsyncLifetime
{
    internal FamilyHistoryAcceptanceDatabase Clinical {get;}=new();
    internal Guid LegacyPatient,LegacyUid,ResolvedUid;internal string LegacyToken="",ResolvedToken="",LegacyAudit="";
    public async Task InitializeAsync()
    {
        await Clinical.InitializeAsync();
        try
        {
            foreach(var other in new[]{false,true})
            {
                await Apply(other,"patient_problem_stored_procedures.sql");
                foreach(var file in new[]{"0043-patient-chart-read-audit.sql","0041-patient-clinical-history.sql","0078-clinical-history-fields.sql"})await Apply(other,"tenant-clinical/migrations/"+file);
                if(!other)
                {
                    LegacyPatient=await Clinical.SeedPatient(false);var actor=await Clinical.SeedUser(false);
                    var legacy=await LegacyCreate(actor.Actor,false);LegacyUid=legacy.Uid;LegacyToken=legacy.Token;
                    var resolved=await LegacyCreate(actor.Actor,true);ResolvedUid=resolved.Uid;ResolvedToken=resolved.Token;LegacyAudit=await AuditSnapshot(false);
                }
                await Apply(other,"tenant-clinical/migrations/0079-problem-fields-concurrency.sql");
            }
        }
        catch{await Clinical.DisposeAsync();throw;}
    }
    private async Task<(Guid Uid,string Token)> LegacyCreate(long actor,bool resolve)
    {
        await using var c=await Clinical.Open(false);await using var q=new SqlCommand("EXEC dbo.PatientProblem_Create @PatientUid=@Patient,@ProblemName=N'TEST ONLY legacy',@ProblemDescription=N'TEST ONLY legacy narrative',@OnsetDate='1998-03-04',@CreatedBy=@Actor",c);q.Parameters.AddWithValue("@Patient",LegacyPatient);q.Parameters.AddWithValue("@Actor",actor);
        Guid uid;byte[] version;
        await using(var r=await q.ExecuteReaderAsync()){await r.ReadAsync();uid=r.GetGuid(r.GetOrdinal("PatientProblemUid"));version=(byte[])r["RowVersion"];}
        if(resolve){await using var change=new SqlCommand("EXEC dbo.PatientProblem_Resolve @Patient,@Uid,N'TEST ONLY old reason',@Actor",c);change.Parameters.AddWithValue("@Patient",LegacyPatient);change.Parameters.AddWithValue("@Uid",uid);change.Parameters.AddWithValue("@Actor",actor);await using var r=await change.ExecuteReaderAsync();await r.ReadAsync();version=(byte[])r["RowVersion"];}
        return(uid,Convert.ToBase64String(version));
    }
    public Task DisposeAsync()=>Clinical.DisposeAsync();
    internal PatientProblemRepository Repository(bool other=false)=>new(Clinical.Factory(other),NullLogger<PatientProblemRepository>.Instance);
    internal async Task Apply(bool other,string file){var script=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"database",file));foreach(var batch in Regex.Split(script,@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase))if(!string.IsNullOrWhiteSpace(batch))await Execute(other,batch);}
    internal async Task Execute(bool other,string text){await using var c=await Clinical.Open(other);await using var q=new SqlCommand(text,c);await q.ExecuteNonQueryAsync();}
    internal async Task<T> Read<T>(bool other,string sql,Guid uid){await using var c=await Clinical.Open(other);await using var q=new SqlCommand(sql,c);q.Parameters.AddWithValue("@Uid",uid);return(T)(await q.ExecuteScalarAsync())!;}
    internal async Task<string> AuditSnapshot(bool other){await using var c=await Clinical.Open(other);await using var q=new SqlCommand("SELECT (SELECT * FROM dbo.AuditLog WHERE EntityName=N'PatientProblem' AND PatientId=(SELECT PatientId FROM dbo.Patient WHERE PatientUid=@Uid) ORDER BY AuditLogId FOR JSON PATH,INCLUDE_NULL_VALUES)",c);q.Parameters.AddWithValue("@Uid",LegacyPatient);return(string)(await q.ExecuteScalarAsync())!;}
    internal async Task<string> Snapshot(bool other)
    {
        await using var c=await Clinical.Open(other);await using var q=new SqlCommand("SELECT (SELECT * FROM dbo.PatientProblem ORDER BY PatientProblemUid FOR JSON PATH,INCLUDE_NULL_VALUES) Clinical,(SELECT * FROM dbo.AuditLog ORDER BY AuditLogId FOR JSON PATH,INCLUDE_NULL_VALUES) Audit FOR JSON PATH,WITHOUT_ARRAY_WRAPPER",c);
        await using var r=await q.ExecuteReaderAsync();var text=new System.Text.StringBuilder();while(await r.ReadAsync())text.Append(r.GetString(0));return text.ToString();
    }
}
