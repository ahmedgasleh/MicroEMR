using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.PatientChartAlerts;
using MicroEMR.Application.PatientCpp;
using MicroEMR.Infrastructure.PatientChartAlerts;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ChartAlertSqlTests(ChartAlertDatabase db) : IClassFixture<ChartAlertDatabase>
{
    internal static string Route(Guid p, Guid? uid=null) => $"/api/patients/{p}/chart-alerts"+(uid is null?"":$"/{uid}");
    internal static CreatePatientChartAlertRequest Draft() => new(){AlertTitle="TEST ONLY recorded assistance",AlertMessage="TEST ONLY note <script>",AlertType="Clinical",AlertPriority="High"};
    internal static UpdatePatientChartAlertRequest Edit(PatientChartAlertResponse x) => new(){AlertTitle=x.AlertTitle,AlertMessage=x.AlertMessage,AlertType=x.AlertType,AlertPriority=x.AlertPriority,RowVersion=x.RowVersion!};
    [Fact]
    public async Task RealHttpLifecycleRequiresCompleteAtomicAuditsAndPreservesResolutionHistory()
    {
        var p=await db.Clinical.SeedPatient(false);var a=await db.Clinical.SeedUser(false);var editor=await db.Clinical.SeedUser(false);
        await using var host=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,a,editor);using var client=host.Client(a);using var e=host.Client(editor);
        var create=await client.PostAsJsonAsync(Route(p),Draft());Assert.Equal(HttpStatusCode.Created,create.StatusCode);var row=(await create.Content.ReadFromJsonAsync<PatientChartAlertResponse>())!;
        Assert.Equal("Active",row.AlertStatus);Assert.Equal(a.Actor,row.CreatedBy);Assert.Equal(8,Convert.FromBase64String(row.RowVersion!).Length);
        var edit=Edit(row);edit.AlertMessage="TEST ONLY amended";var saved=await e.PutAsJsonAsync(Route(p,row.PatientChartAlertUid),edit);Assert.Equal(HttpStatusCode.OK,saved.StatusCode);var changed=(await saved.Content.ReadFromJsonAsync<PatientChartAlertResponse>())!;
        Assert.NotEqual(row.RowVersion,changed.RowVersion);Assert.Equal(row.CreatedAt,changed.CreatedAt);Assert.Equal(editor.Actor,changed.UpdatedBy);
        var audits=await db.Clinical.Audit(false,row.PatientChartAlertUid,"PatientChartAlert");Assert.Equal(2,audits.Count);
        using(var old=JsonDocument.Parse(audits[1].Old!))using(var next=JsonDocument.Parse(audits[1].New))
        { Assert.Equal(row.AlertMessage,old.RootElement.GetProperty("AlertMessage").GetString());Assert.Equal(changed.AlertMessage,next.RootElement.GetProperty("AlertMessage").GetString());Assert.Equal(changed.RowVersion,next.RootElement.GetProperty("RowVersion").GetString());Assert.Equal(db.Clinical.Tenant(false).TenantUid,next.RootElement.GetProperty("TenantUid").GetGuid());Assert.False(string.IsNullOrWhiteSpace(next.RootElement.GetProperty("RequestCorrelationId").GetString())); }
        Assert.Equal(editor.Actor,audits[1].Actor);Assert.Equal(editor.Subject,audits[1].Subject);
        var closed=await e.PostAsJsonAsync(Route(p,row.PatientChartAlertUid)+"/resolve",new ResolvePatientChartAlertRequest{RowVersion=changed.RowVersion!,ResolutionReason="TEST ONLY finished"});Assert.Equal(HttpStatusCode.OK,closed.StatusCode);
        var resolved=(await closed.Content.ReadFromJsonAsync<PatientChartAlertResponse>())!;Assert.Equal("Resolved",resolved.AlertStatus);Assert.Equal(editor.Actor,resolved.ResolvedBy);Assert.Equal("TEST ONLY finished",resolved.ResolutionReason);Assert.Equal(row.CreatedAt,resolved.CreatedAt);
        Assert.Equal(3,(await db.Clinical.Audit(false,row.PatientChartAlertUid,"PatientChartAlert")).Count);
        var before=await db.Snapshot(false);var repeat=await client.PostAsJsonAsync(Route(p,row.PatientChartAlertUid)+"/resolve",new{RowVersion=resolved.RowVersion,ResolutionReason="must not overwrite"});Assert.Equal(HttpStatusCode.OK,repeat.StatusCode);Assert.Equal(before,await db.Snapshot(false));
        Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync(Route(p,row.PatientChartAlertUid),Edit(resolved))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(Route(p,row.PatientChartAlertUid)+"/resolve",new{RowVersion=changed.RowVersion})).StatusCode);Assert.Equal(before,await db.Snapshot(false));
        Assert.Empty(await db.Service().ListAsync(p));Assert.Single(await db.Service().ListAsync(p,"Resolved"));Assert.Single(await db.Service().ListAsync(p,"All"));
    }
    [Theory]
    [InlineData(false,false)] [InlineData(false,true)] [InlineData(true,true)]
    public async Task ConcurrentEditsAndResolveAllowExactlyOneCurrentVersionMutation(bool firstResolve,bool secondResolve)
    {
        var p=await db.Clinical.SeedPatient(false);var a=await db.Clinical.SeedUser(false);var row=(await db.Service().CreateAsync(p,Draft(),a.Actor))!;
        await using var host=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,a);using var client=host.Client(a);
        Task<HttpResponseMessage> Save(bool resolve)=>resolve?client.PostAsJsonAsync(Route(p,row.PatientChartAlertUid)+"/resolve",new{RowVersion=row.RowVersion}):client.PutAsJsonAsync(Route(p,row.PatientChartAlertUid),Edit(row));
        var results=await Task.WhenAll(Save(firstResolve),Save(secondResolve));Assert.Single(results,x=>x.StatusCode==HttpStatusCode.OK);Assert.Single(results,x=>x.StatusCode==HttpStatusCode.Conflict);Assert.Equal(2,(await db.Clinical.Audit(false,row.PatientChartAlertUid,"PatientChartAlert")).Count);
    }
    [Theory]
    [InlineData("Create")] [InlineData("Update")] [InlineData("Resolve")]
    public async Task RequiredAuditFailureRollsBackClinicalValuesTokenAndSuccessAudit(string action)
    {
        var p=await db.Clinical.SeedPatient(false);var a=await db.Clinical.SeedUser(false);var row=(await db.Service().CreateAsync(p,Draft(),a.Actor))!;
        var before=await db.Snapshot(false);await db.Execute(false,"CREATE TRIGGER dbo.Step114AuditFault ON dbo.AuditLog AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE EntityName=N'PatientChartAlert') THROW 52998,'TEST ONLY audit fault',1; END;");
        try
        {
            await using var host=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,a);using var client=host.Client(a);
            var response=action=="Create"?await client.PostAsJsonAsync(Route(p),Draft()):action=="Update"?await client.PutAsJsonAsync(Route(p,row.PatientChartAlertUid),Edit(row)):await client.PostAsJsonAsync(Route(p,row.PatientChartAlertUid)+"/resolve",new{RowVersion=row.RowVersion});Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
        }
        finally{await db.Execute(false,"DROP TRIGGER dbo.Step114AuditFault;");}
        Assert.Equal(before,await db.Snapshot(false));
    }
    [Fact]
    public async Task ScopedReadsWritesPermissionsAndTwoTenantDirectionsAreFailClosedBeforeReadAudit()
    {
        var p=await db.Clinical.SeedPatient(false);var other=await db.Clinical.SeedPatient(false);var pb=await db.Clinical.SeedPatient(true);
        var a=await db.Clinical.SeedUser(false);var reader=await db.Clinical.SeedUser(false,false);var denied=await db.Clinical.SeedUser(false,false,false);var b=await db.Clinical.SeedUser(true);
        var row=(await db.Service().CreateAsync(p,Draft(),a.Actor))!;var rowb=(await db.Service(true).CreateAsync(pb,Draft(),b.Actor))!;
        await using var ha=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,a,reader,denied);await using var hb=await FamilyHistoryAcceptanceHost.Start(db.Clinical,true,b);
        using var ca=ha.Client(a);using var cb=hb.Client(b);using var cr=ha.Client(reader);using var cd=ha.Client(denied);
        var beforeA=await db.Snapshot(false);var beforeB=await db.Snapshot(true);var readsA=await db.ReadAuditCount(false);var readsB=await db.ReadAuditCount(true);
        foreach(var (c,patient,foreign) in new[]{(ca,other,row),(cb,pb,row),(cb,p,row),(ca,p,rowb),(ca,pb,rowb)})
        {
            Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync(Route(patient,foreign.PatientChartAlertUid))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await c.PutAsJsonAsync(Route(patient,foreign.PatientChartAlertUid),Edit(foreign))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await c.PostAsJsonAsync(Route(patient,foreign.PatientChartAlertUid)+"/resolve",new{RowVersion=foreign.RowVersion})).StatusCode);
        }
        foreach(var c in new[]{cr,cd})
        {
            Assert.Equal(HttpStatusCode.Forbidden,(await c.PostAsJsonAsync(Route(p),Draft())).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await c.PutAsJsonAsync(Route(p,row.PatientChartAlertUid),Edit(row))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await c.PostAsJsonAsync(Route(p,row.PatientChartAlertUid)+"/resolve",new{RowVersion=row.RowVersion})).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Forbidden,(await cd.GetAsync(Route(p))).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await cd.GetAsync(Route(p,row.PatientChartAlertUid))).StatusCode);
        Assert.Equal(beforeA,await db.Snapshot(false));Assert.Equal(beforeB,await db.Snapshot(true));Assert.Equal(readsA,await db.ReadAuditCount(false));Assert.Equal(readsB,await db.ReadAuditCount(true));
        Assert.Equal(HttpStatusCode.OK,(await cr.GetAsync(Route(p,row.PatientChartAlertUid))).StatusCode);Assert.Equal(readsA+1,await db.ReadAuditCount(false));
        await db.Execute(false,$"UPDATE dbo.Patient SET IsDeleted=1 WHERE PatientUid='{p}'");var deleted=await db.Snapshot(false);
        Assert.Equal(HttpStatusCode.NotFound,(await ca.GetAsync(Route(p))).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await ca.GetAsync(Route(p,row.PatientChartAlertUid))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await ca.PostAsJsonAsync(Route(p),Draft())).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await ca.PutAsJsonAsync(Route(p,row.PatientChartAlertUid),Edit(row))).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await ca.PostAsJsonAsync(Route(p,row.PatientChartAlertUid)+"/resolve",new{RowVersion=row.RowVersion})).StatusCode);
        Assert.Equal(deleted,await db.Snapshot(false));Assert.Equal(readsA+1,await db.ReadAuditCount(false));
    }
    [Fact]
    public async Task ReadAuditPersistenceFailurePreventsDisclosure()
    {
        var p=await db.Clinical.SeedPatient(false);var a=await db.Clinical.SeedUser(false);var row=(await db.Service().CreateAsync(p,Draft(),a.Actor))!;var reads=await db.ReadAuditCount(false);
        await db.Execute(false,"CREATE TRIGGER dbo.Step114ReadFault ON dbo.AuditLog AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE ActionName=N'PatientChartOpened') THROW 52998,'TEST ONLY read audit fault',1; END;");
        try{await using var host=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,a);using var c=host.Client(a);foreach(var path in new[]{Route(p),Route(p,row.PatientChartAlertUid)}){var response=await c.GetAsync(path);Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);Assert.DoesNotContain(row.AlertTitle,await response.Content.ReadAsStringAsync());}}
        finally{await db.Execute(false,"DROP TRIGGER dbo.Step114ReadFault;");}
        Assert.Equal(reads,await db.ReadAuditCount(false));
    }
    [Fact]
    public async Task InactiveUnresolvedAndSpoofedActorsCannotBypassTrustedAttribution()
    {
        var p=await db.Clinical.SeedPatient(false);var a=await db.Clinical.SeedUser(false);var inactive=await db.Clinical.SeedUser(false);var unresolved=await db.Clinical.SeedUser(false);await db.Execute(false,$"UPDATE dbo.ApplicationUser SET IsActive=0 WHERE UserId={inactive.Actor}; UPDATE dbo.ApplicationUser SET AuthSubjectId=NULL WHERE UserId={unresolved.Actor};");
        var row=(await db.Service().CreateAsync(p,Draft(),a.Actor))!;var before=await db.Snapshot(false);
        foreach(var actor in new long?[]{null,inactive.Actor,999999})
        {await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>db.Repository().CreateAsync(p,Draft(),actor));await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>db.Repository().UpdateAsync(p,row.PatientChartAlertUid,Edit(row),actor));await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>db.Repository().ResolveAsync(p,row.PatientChartAlertUid,new(){RowVersion=row.RowVersion!},actor));}
        await using var host=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,a,inactive,unresolved);using var ci=host.Client(inactive);using var cu=host.Client(unresolved);using var ca=host.Client(a);
        foreach(var c in new[]{ci,cu}){Assert.Equal(HttpStatusCode.Forbidden,(await c.PostAsJsonAsync(Route(p),Draft())).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await c.PutAsJsonAsync(Route(p,row.PatientChartAlertUid),Edit(row))).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await c.PostAsJsonAsync(Route(p,row.PatientChartAlertUid)+"/resolve",new{RowVersion=row.RowVersion})).StatusCode);}
        Assert.Equal(before,await db.Snapshot(false));
        var spoof=await ca.PostAsJsonAsync(Route(p),new{AlertTitle="TEST ONLY spoof",CreatedBy=inactive.Actor,RequestCorrelationId="client-spoof",TenantUid=Guid.NewGuid()});Assert.Equal(HttpStatusCode.Created,spoof.StatusCode);var saved=(await spoof.Content.ReadFromJsonAsync<PatientChartAlertResponse>())!;Assert.Equal(a.Actor,saved.CreatedBy);
        var audit=Assert.Single(await db.Clinical.Audit(false,saved.PatientChartAlertUid,"PatientChartAlert"));using var json=JsonDocument.Parse(audit.New);Assert.NotEqual("client-spoof",json.RootElement.GetProperty("RequestCorrelationId").GetString());Assert.Equal(db.Clinical.Tenant(false).TenantUid,json.RootElement.GetProperty("TenantUid").GetGuid());
    }
    [Fact]
    public async Task ValidationDirectSqlTokensMissingAuditAndOuterRollbackPreserveClinicalHistory()
    {
        var p=await db.Clinical.SeedPatient(false);var a=await db.Clinical.SeedUser(false);var row=(await db.Service().CreateAsync(p,Draft(),a.Actor))!;
        await using var host=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,a);using var c=host.Client(a);var before=await db.Snapshot(false);
        foreach(var token in new[]{"", "malformed", Convert.ToBase64String(new byte[7]),Convert.ToBase64String(new byte[9])})
        {var edit=Edit(row);edit.RowVersion=token;Assert.Equal(HttpStatusCode.BadRequest,(await c.PutAsJsonAsync(Route(p,row.PatientChartAlertUid),edit)).StatusCode);Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync(Route(p,row.PatientChartAlertUid)+"/resolve",new{RowVersion=token})).StatusCode);}
        foreach(var draft in new[]{new CreatePatientChartAlertRequest{AlertTitle=" "},new(){AlertTitle=new('x',201)},new(){AlertTitle="TEST ONLY",AlertMessage=new('x',1001)}})Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync(Route(p),draft)).StatusCode);
        foreach(var token in new[]{"NULL","0x01","0x010203040506070809"})foreach(var action in new[]{"Update","Resolve"})
        {var extra=action=="Update"?",@AlertTitle=N'TEST ONLY',@UpdatedBy=":" ,@ResolvedBy=";await Assert.ThrowsAsync<SqlException>(()=>db.Execute(false,$"EXEC dbo.PatientChartAlert_{action} @PatientUid='{p}',@PatientChartAlertUid='{row.PatientChartAlertUid}',@ExpectedRowVersion={token}{extra}{a.Actor}"));}
        Assert.Equal(before,await db.Snapshot(false));
        await db.Execute(false,"EXEC sp_rename N'dbo.AuditLog',N'Step114HeldAudit';");try{await Assert.ThrowsAsync<SqlException>(()=>db.Service().CreateAsync(p,Draft(),a.Actor));}finally{await db.Execute(false,"EXEC sp_rename N'dbo.Step114HeldAudit',N'AuditLog';");}Assert.Equal(before,await db.Snapshot(false));
        await db.Execute(false,$"BEGIN TRANSACTION; EXEC dbo.PatientChartAlert_Create @PatientUid='{p}',@AlertTitle=N'TEST ONLY outer rollback',@CreatedBy={a.Actor}; ROLLBACK;");Assert.Equal(before,await db.Snapshot(false));
    }
    [Fact]
    public async Task AdditiveUpgradePreservesLegacyIdentityValuesVersionAndAudit()
    {
        Assert.Equal(db.LegacySnapshot,await db.LegacyState());var row=(await db.Service().GetAsync(db.LegacyPatient,db.LegacyUid))!;
        Assert.Equal(db.LegacyToken,row.RowVersion);Assert.Equal("TEST ONLY legacy",row.AlertTitle);Assert.Equal("TEST ONLY legacy note",row.AlertMessage);
        var a=await db.Clinical.SeedUser(false);var draft=new CreatePatientChartAlertRequest{AlertTitle="TEST ONLY default",AlertType="unknown legacy input",AlertPriority="unknown"};var created=(await db.Service().CreateAsync(db.LegacyPatient,draft,a.Actor))!;Assert.Equal("General",created.AlertType);Assert.Equal("Normal",created.AlertPriority);
    }
    [Fact]
    public async Task CppUsesOwnedActiveSourceCompleteSelectedOutputEscapingAndRealMultipagePdf()
    {
        var p=await db.Clinical.SeedPatient(false);var a=await db.Clinical.SeedUser(false);
        for(var i=0;i<8;i++){var draft=Draft();draft.AlertTitle=$"TEST ONLY alert {i}";draft.AlertMessage=string.Join("\n",Enumerable.Range(0,25).Select(n=>$"Line {n}"))+$"\nTail{i} <script>";await db.Service().CreateAsync(p,draft,a.Actor);}
        var closed=(await db.Service().CreateAsync(p,new(){AlertTitle="TEST ONLY excluded resolved"},a.Actor))!;await db.Service().ResolveAsync(p,closed.PatientChartAlertUid,new(){RowVersion=closed.RowVersion!,ResolutionReason="TEST ONLY"},a.Actor);
        var reads=0;var alerts=CppSelectivePrintTests.Stub<IPatientChartAlertService>((_,args)=>{reads++;return db.Service().ListAsync((Guid)args[0]!, (string)args[1]!, (CancellationToken)args[2]!);});
        var source=CppSelectivePrintTests.Source(p,null!,CppSelectivePrintTests.Stub<MicroEMR.Application.ReadAudit.IPatientChartReadAuditService>((_,_)=>Task.FromResult(Guid.NewGuid())),alerts:alerts);
        var summary=(await source.GetAsync(p,"test"))!;Assert.Equal(5,summary.Alerts.Items.Count);Assert.Equal(8,summary.Alerts.TotalCount);
        var full=(await source.GetForPrintAsync(p,["Alerts"],"test"))!;Assert.Equal(8,full.Alerts.Items.Count);
        var fixture=new CppSelectivePrintTests.Fixture();fixture.Patient.PatientUid=p;fixture.ActualSource=source;var html=System.Text.Encoding.UTF8.GetString((await fixture.Print("Alerts"))!);
        Assert.Contains("Chart Alerts",html);Assert.Contains("Tail7",html);Assert.Contains("&lt;script&gt;",html);Assert.DoesNotContain("excluded resolved",html);Assert.DoesNotContain("Unselected allergen",html);
        await using(var renderer=new MicroEMR.Infrastructure.ClinicalOutput.PlaywrightPdfRenderer(NullLogger<MicroEMR.Infrastructure.ClinicalOutput.PlaywrightPdfRenderer>.Instance))
        {using var pdf=UglyToad.PdfPig.PdfDocument.Open(await renderer.RenderAsync(html));Assert.True(pdf.NumberOfPages>1);Assert.Contains(pdf.GetPages().SelectMany(x=>x.GetWords()),x=>x.Text=="Tail7");}
        var prior=reads;await source.GetForPrintAsync(p,["Problems"],"test");Assert.Equal(prior,reads);
        fixture.ActualSource=null;fixture.Data=fixture.Data with{PatientUid=p,Alerts=full.Alerts};Assert.DoesNotContain("Tail7",System.Text.Encoding.UTF8.GetString((await fixture.Print("Problems"))!));
        var empty=await db.Clinical.SeedPatient(false);Assert.Empty(await db.Service().ListAsync(empty));
        var emptySource=CppSelectivePrintTests.Source(empty,null!,CppSelectivePrintTests.Stub<MicroEMR.Application.ReadAudit.IPatientChartReadAuditService>((_,_)=>Task.FromResult(Guid.NewGuid())),alerts:db.Service());
        Assert.Equal(PatientCppSectionStates.NotDocumented,(await emptySource.GetAsync(empty,"test"))!.Alerts.State);
        var foreign=CppSelectivePrintTests.Stub<IPatientChartAlertService>((_,_)=>Task.FromResult<IReadOnlyList<PatientChartAlertResponse>>([new(){PatientUid=Guid.NewGuid(),AlertStatus="Resolved",AlertTitle="foreign"}]));
        fixture.Patient.PatientUid=empty;fixture.ActualSource=CppSelectivePrintTests.Source(empty,null!,CppSelectivePrintTests.Stub<MicroEMR.Application.ReadAudit.IPatientChartReadAuditService>((_,_)=>Task.FromResult(Guid.NewGuid())),alerts:foreign);await Assert.ThrowsAsync<CppPrintUnavailableException>(()=>fixture.Print("Alerts"));
    }
}

public sealed class ChartAlertDatabase : IAsyncLifetime
{
    internal FamilyHistoryAcceptanceDatabase Clinical {get;}=new();
    internal Guid LegacyPatient,LegacyUid;internal string LegacyToken="",LegacySnapshot="";
    public async Task InitializeAsync()
    {
        await Clinical.InitializeAsync();try
        {
            foreach(var other in new[]{false,true})
            {
                await Apply(other,"patient_chart_alert_stored_procedures.sql");await Apply(other,"tenant-clinical/migrations/0043-patient-chart-read-audit.sql");
                if(!other)
                {
                    LegacyPatient=await Clinical.SeedPatient(false);var a=await Clinical.SeedUser(false);await using var c=await Clinical.Open(false);
                    await using var q=new SqlCommand($"EXEC dbo.PatientChartAlert_Create @PatientUid='{LegacyPatient}',@AlertTitle=N'TEST ONLY legacy',@AlertMessage=N'TEST ONLY legacy note',@CreatedBy={a.Actor}",c);
                    await using(var r=await q.ExecuteReaderAsync()){await r.ReadAsync();LegacyUid=(Guid)r["PatientChartAlertUid"];LegacyToken=Convert.ToBase64String((byte[])r["RowVersion"]);}LegacySnapshot=await LegacyState();
                }
                await Apply(other,"tenant-clinical/migrations/0082-chart-alert-safeguards.sql");
            }
        }catch{await Clinical.DisposeAsync();throw;}
    }
    public Task DisposeAsync()=>Clinical.DisposeAsync();
    internal PatientChartAlertRepository Repository(bool other=false)=>new(Clinical.Factory(other));
    internal PatientChartAlertService Service(bool other=false)=>new(Repository(other));
    internal async Task Apply(bool other,string file){foreach(var batch in Regex.Split(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"database",file)),@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase))if(!string.IsNullOrWhiteSpace(batch))await Execute(other,batch);}
    internal async Task Execute(bool other,string sql){await using var c=await Clinical.Open(other);await using var q=new SqlCommand(sql,c);await q.ExecuteNonQueryAsync();}
    internal async Task<string> Scalar(bool other,string sql){await using var c=await Clinical.Open(other);await using var q=new SqlCommand(sql,c);await using var r=await q.ExecuteReaderAsync();var s=new System.Text.StringBuilder();while(await r.ReadAsync())s.Append(r.GetString(0));return s.ToString();}
    internal Task<string> LegacyState()=>Scalar(false,$"SELECT (SELECT * FROM dbo.PatientChartAlert WHERE PatientChartAlertUid='{LegacyUid}' FOR JSON PATH,INCLUDE_NULL_VALUES) Clinical,(SELECT * FROM dbo.AuditLog WHERE EntityName=N'PatientChartAlert' AND EntityId='{LegacyUid}' FOR JSON PATH,INCLUDE_NULL_VALUES) Audit FOR JSON PATH,WITHOUT_ARRAY_WRAPPER");
    internal async Task<int> ReadAuditCount(bool other){await using var c=await Clinical.Open(other);await using var q=new SqlCommand("SELECT COUNT(*) FROM dbo.AuditLog WHERE ActionName=N'PatientChartOpened'",c);return (int)(await q.ExecuteScalarAsync())!;}
    internal Task<string> Snapshot(bool other)=>Scalar(other,"SELECT (SELECT * FROM dbo.PatientChartAlert ORDER BY PatientChartAlertUid FOR JSON PATH,INCLUDE_NULL_VALUES) Clinical,(SELECT * FROM dbo.AuditLog WHERE EntityName=N'PatientChartAlert' ORDER BY AuditLogId FOR JSON PATH,INCLUDE_NULL_VALUES) Audit FOR JSON PATH,WITHOUT_ARRAY_WRAPPER");
}
