using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.PatientCpp;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Application.ReadAudit;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PrescriptionGenericNameTests(PrescriptionDateDatabase db):IClassFixture<PrescriptionDateDatabase>
{
    [Fact]
    public void SerializedOmissionAndExplicitClearRemainDistinctAcrossWebApiTransport()
    {
        var omitted=JsonSerializer.Serialize(new PrescriptionDraftRequest());
        Assert.DoesNotContain("genericName",omitted);
        var clear=JsonSerializer.Serialize(new PrescriptionDraftRequest{GenericName=null});
        Assert.Contains("\"genericName\":null",clear);
        var roundTrip=JsonSerializer.Deserialize<PrescriptionDraftRequest>(clear)!;
        Assert.True(roundTrip.GenericNameSpecified);Assert.Null(roundTrip.GenericName);
        Assert.False(JsonSerializer.Deserialize<PrescriptionDraftRequest>(omitted)!.GenericNameSpecified);
    }
    private static string Route(Guid patient,Guid? uid=null)=>$"api/patients/{patient}/prescriptions"+(uid is {} value?$"/{value}":"");
    private static CreatePrescriptionDraftRequest Draft(string? generic="TEST ONLY generic α")
    { var r=PrescriptionDatePrecisionTests.Draft();r.GenericName=generic;return r; }
    private static JsonObject Body(PatientPrescriptionResponse row,bool include=true,string? name="TEST ONLY amended generic")
    {
        var body=JsonSerializer.SerializeToNode(Draft(),new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
        body["rowVersion"]=row.RowVersion;
        if(include)body["genericName"]=name;else body.Remove("genericName");
        return body;
    }
    private static async Task<PatientPrescriptionResponse> Read(HttpResponseMessage response)
    {Assert.True(response.IsSuccessStatusCode,$"HTTP {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");return (await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;}
    private async Task<int> Reads()=>await db.Stores.Read<int>("SELECT COUNT(*) FROM dbo.AuditLog WHERE ActionName=N'PatientChartOpened'",Guid.Empty);

    [Fact]
    public async Task PreMigrationSignedVersionTwoArtifactAndRenderedClinicalContentRemainUnchanged()
    {
        var old=await db.Repository().GetArtifactAsync(db.Stores.PriorSignedPatient,db.Stores.PriorSignedPrescription);
        Assert.Equal(db.Stores.PriorSignedJson,old!.Json);
        Assert.Equal(db.Stores.PriorSignedRowVersion,(await db.Repository().GetAsync(db.Stores.PriorSignedPatient,db.Stores.PriorSignedPrescription))!.RowVersion);
        using var before=JsonDocument.Parse(db.Stores.PriorSignedJson);using var after=JsonDocument.Parse(old.Json);
        var occurrence=new PrescriptionOutputOccurrence(Guid.NewGuid(),DateTimeOffset.Parse("2026-10-19T12:00:00Z"),new("test-subject-7","TEST ONLY operator","step118"));
        var expected=PrescriptionOutputService.Compose(before.RootElement,occurrence);
        var actual=PrescriptionOutputService.Compose(after.RootElement,occurrence);
        Assert.Equal(expected,actual);Assert.DoesNotContain("Generic name",actual.Body);Assert.DoesNotContain("GenericNameVersion",old.Json);
        Assert.Equal(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(db.Stores.PriorSignedJson)),System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(old.Json)));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task ForeignSourceRowsAreRejectedBeforeDedicatedReadAudit(bool detail)
    {
        var patient=Guid.NewGuid();var uid=Guid.NewGuid();var reads=0;
        var foreign=new PatientPrescriptionResponse{PatientUid=Guid.NewGuid(),PrescriptionUid=uid,GenericName="FOREIGN"};
        var service=CppSelectivePrintTests.Stub<IPatientPrescriptionService>((method,_)=>method=="GetAsync"?Task.FromResult<PatientPrescriptionResponse?>(foreign):Task.FromResult<IReadOnlyList<PatientPrescriptionResponse>>([foreign]));
        var audit=CppSelectivePrintTests.Stub<IPatientChartReadAuditService>((_,_)=>{reads++;return Task.FromResult(Guid.NewGuid());});
        var controller=new MicroEMR.Api.Controllers.PatientPrescriptionsController(service);
        var result=detail?(await controller.Get(patient,uid,audit,default)).Result:(await controller.List(patient,audit,default)).Result;
        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(result);Assert.Equal(0,reads);
    }

    [Theory]
    [InlineData("TEST ONLY generic α")][InlineData(null)][InlineData("   ")]
    public async Task HttpRoundTripKeepsGenericSeparateAndUnknownHonest(string? value)
    {
        var p=await db.Stores.SeedPatient();await using var host=await db.Host();using var c=host.Client();
        var request=Draft(value);var row=await Read(await c.PostAsJsonAsync(Route(p),request));
        Assert.Equal(string.IsNullOrWhiteSpace(value)?null:value,row.GenericName);
        Assert.Equal(request.ProductName,row.ProductName);Assert.Equal(request.ProductDisplayText,row.ProductDisplayText);
        Assert.Equal(row.GenericName,(await Read(await c.GetAsync(Route(p,row.PrescriptionUid)))).GenericName);
        Assert.Equal(row.GenericName,(await c.GetFromJsonAsync<PatientPrescriptionResponse[]>(Route(p)))!.Single().GenericName);
    }

    [Fact]
    public async Task OmissionPreservesExplicitNullClearsAndAuditCapturesBeforeAfter()
    {
        var p=await db.Stores.SeedPatient();await using var host=await db.Host();using var c=host.Client();
        var row=await Read(await c.PostAsJsonAsync(Route(p),Draft()));
        var preserved=await Read(await c.PutAsJsonAsync(Route(p,row.PrescriptionUid),Body(row,false)));
        Assert.Equal(row.GenericName,preserved.GenericName);
        var updated=await Read(await c.PutAsJsonAsync(Route(p,row.PrescriptionUid),Body(preserved)));
        Assert.Equal("TEST ONLY amended generic",updated.GenericName);
        Assert.Equal(HttpStatusCode.Conflict,(await c.PutAsJsonAsync(Route(p,row.PrescriptionUid),Body(preserved))).StatusCode);
        var cleared=await Read(await c.PutAsJsonAsync(Route(p,row.PrescriptionUid),Body(updated,true,null)));
        Assert.Null(cleared.GenericName);Assert.Equal(row.ProductName,cleared.ProductName);
        var snapshots=await db.Stores.Read<string>("SELECT TOP(1) CONCAT(OldValue,N'|',NewValue) FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@Uid) AND ActionName=N'PrescriptionDraftUpdated' ORDER BY AuditLogId DESC",row.PrescriptionUid);
        var parts=snapshots.Split('|');using var old=JsonDocument.Parse(parts[0]);using var next=JsonDocument.Parse(parts[1]);
        Assert.Equal(updated.GenericName,old.RootElement.GetProperty("GenericName").GetString());
        Assert.Equal(JsonValueKind.Null,next.RootElement.GetProperty("GenericName").ValueKind);
        Assert.Equal(7,next.RootElement.GetProperty("UpdatedBy").GetInt32());
        Assert.Equal(3,await db.Stores.AuditCount(row.PrescriptionUid,"PrescriptionDraftUpdated"));
        Assert.Equal(db.Stores.LegacyJson,(await db.Repository().GetArtifactAsync(db.Stores.LegacyPatient,db.Stores.LegacyPrescription))!.Json);
        Assert.Equal(db.Stores.LegacyRowVersion,(await db.Repository().GetAsync(db.Stores.LegacyPatient,db.Stores.LegacyPrescription))!.RowVersion);
    }

    [Fact]
    public async Task InvalidLengthAndMalformedVersionFailWithoutWritesAtHttpApplicationAndSql()
    {
        var p=await db.Stores.SeedPatient();await using var host=await db.Host();using var c=host.Client();
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync(Route(p),Draft(new string('x',301)))).StatusCode);
        var service=new PatientPrescriptionService(db.Repository(),new PrescriptionDatePrecisionTests.Capture());
        await Assert.ThrowsAsync<ArgumentException>(()=>service.CreateAsync(p,Draft(new string('x',301)),7));
        var row=await Read(await c.PostAsJsonAsync(Route(p),Draft()));
        var invalid=Body(row);invalid["rowVersion"]=Convert.ToBase64String(new byte[9]);
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PutAsJsonAsync(Route(p,row.PrescriptionUid),invalid)).StatusCode);
        // Direct procedure input is MAX and checked before assignment; no parameter truncation.
        await using var connection=await db.Factory().OpenConnectionAsync();
        await using var command=new SqlCommand("EXEC dbo.PatientPrescription_UpdateDraft @PatientUid=@Patient,@PrescriptionUid=@Uid,@ProductName=N'Test',@ProductDisplayText=N'Test',@Route=N'Oral',@FrequencyCode=N'ONCE',@FrequencyDisplay=N'Once',@Prn=0,@Directions=N'Test',@Quantity=1,@QuantityUnit=N'tablet',@AuthorizedRepeats=0,@ActorUserId=7,@RowVersion=@Version,@GenericName=@Name,@GenericNameSpecified=1",connection);
        command.Parameters.AddWithValue("@Patient",p);command.Parameters.AddWithValue("@Uid",row.PrescriptionUid);command.Parameters.AddWithValue("@Version",Convert.FromBase64String(row.RowVersion));command.Parameters.AddWithValue("@Name",new string('x',301));
        Assert.Equal(53000,(await Assert.ThrowsAsync<SqlException>(()=>command.ExecuteNonQueryAsync())).Number);
        Assert.Equal(row.RowVersion,(await db.Repository().GetAsync(p,row.PrescriptionUid))!.RowVersion);
        Assert.Equal(0,await db.Stores.AuditCount(row.PrescriptionUid,"PrescriptionDraftUpdated"));
    }

    [Theory]
    [InlineData("finalize")][InlineData("sign-and-finalize")]
    public async Task FutureSnapshotCorrectionAndLifecycleAuditPreserveOriginalArtifact(string action)
    {
        var p=await db.Stores.SeedPatient();await using var host=await db.Host();using var c=host.Client();
        var row=await Read(await c.PostAsJsonAsync(Route(p),Draft("TEST ONLY <generic>")));
        var signed=await Read(await c.PostAsJsonAsync(Route(p,row.PrescriptionUid)+"/"+action,new{row.RowVersion,ConfirmSignature=true}));
        var original=(await db.Repository().GetArtifactAsync(p,row.PrescriptionUid))!;
        using(var doc=JsonDocument.Parse(original.Json)){Assert.Equal(1,doc.RootElement.GetProperty("GenericNameVersion").GetInt32());Assert.Equal(row.GenericName,doc.RootElement.GetProperty("GenericName").GetString());}
        var renderer=new PrescriptionDatePrecisionTests.Capture();
        var service=new PatientPrescriptionService(db.Repository(),renderer);
        var output=new PrescriptionOutputService(db.Repository(),db.Repository(),service,renderer,TimeProvider.System);
        var html=Encoding.UTF8.GetString((await output.GenerateAsync(p,row.PrescriptionUid,new("test-subject-7","TEST ONLY operator","step118")))!);
        Assert.Contains("Generic name",html);Assert.Contains("TEST ONLY &lt;generic&gt;",html);Assert.DoesNotContain("TEST ONLY <generic>",html);
        Assert.Equal(HttpStatusCode.Conflict,(await c.PutAsJsonAsync(Route(p,row.PrescriptionUid),Body(signed))).StatusCode);
        var correction=await Read(await c.PostAsJsonAsync(Route(p,row.PrescriptionUid)+"/correction",new{signed.RowVersion}));
        Assert.Equal(row.GenericName,correction.GenericName);
        await Read(await c.PostAsJsonAsync(Route(p,correction.PrescriptionUid)+"/sign-and-finalize",new{correction.RowVersion,ConfirmSignature=true}));
        Assert.Equal(original,await db.Repository().GetArtifactAsync(p,row.PrescriptionUid));
        foreach(var name in new[]{"PrescriptionFinalized","PrescriptionSuperseded"})
        {
            var audit=await db.Stores.Read<string>("SELECT CONCAT(OldValue,N'|',NewValue) FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@Uid) AND ActionName=N'"+name+"'",row.PrescriptionUid);
            Assert.All(audit.Split('|'),snapshot=>{using var doc=JsonDocument.Parse(snapshot);Assert.Equal(row.GenericName,doc.RootElement.GetProperty("GenericName").GetString());});
        }
    }

    [Theory]
    [InlineData("create")][InlineData("update")][InlineData("finalize")][InlineData("sign-and-finalize")][InlineData("cancel")]
    public async Task RequiredAuditFailureRollsBackAffectedWritesAndArtifacts(string action)
    {
        var p=await db.Stores.SeedPatient();var row=await db.Repository().CreateAsync(p,Draft(),7);
        if(action=="cancel")row=(await db.Repository().SignAndFinalizeAsync(p,row.PrescriptionUid,row.RowVersion,7))!;
        var artifact=await db.Repository().GetArtifactAsync(p,row.PrescriptionUid);
        var before=await db.Stores.AuditCount(row.PrescriptionUid,"PrescriptionFinalized");
        await db.Stores.Execute("CREATE TRIGGER dbo.Step118AuditFault ON dbo.AuditLog AFTER INSERT AS BEGIN THROW 53999,'TEST ONLY audit failure',1; END");
        try
        {
            await using var host=await db.Host();using var c=host.Client();HttpResponseMessage response;
            if(action=="create")response=await c.PostAsJsonAsync(Route(p),Draft("different"));
            else if(action=="update")response=await c.PutAsJsonAsync(Route(p,row.PrescriptionUid),Body(row));
            else response=await c.PostAsJsonAsync(Route(p,row.PrescriptionUid)+"/"+action,new{row.RowVersion,ConfirmSignature=true,Reason="TEST ONLY"});
            Assert.Equal(HttpStatusCode.InternalServerError,response.StatusCode);
        }
        finally{await db.Stores.Execute("DROP TRIGGER dbo.Step118AuditFault");}
        var retained=(await db.Repository().GetAsync(p,row.PrescriptionUid))!;
        Assert.Equal(row.GenericName,retained.GenericName);Assert.Equal(row.RowVersion,retained.RowVersion);Assert.Equal(row.Status,retained.Status);
        Assert.Equal(artifact,await db.Repository().GetArtifactAsync(p,row.PrescriptionUid));Assert.Single(await db.Repository().ListAsync(p));
        Assert.Equal(before,await db.Stores.AuditCount(row.PrescriptionUid,"PrescriptionFinalized"));
        Assert.Equal(0,await db.Stores.AuditCount(row.PrescriptionUid,"PrescriptionDraftUpdated"));Assert.Equal(0,await db.Stores.AuditCount(row.PrescriptionUid,"PrescriptionCancelled"));
    }

    [Fact]
    public async Task SupersessionAuditFailureRollsBackBothOrdersAndNewArtifact()
    {
        var p=await db.Stores.SeedPatient();var row=await db.Repository().CreateAsync(p,Draft(),7);
        var signed=(await db.Repository().SignAndFinalizeAsync(p,row.PrescriptionUid,row.RowVersion,7))!;
        var original=await db.Repository().GetArtifactAsync(p,row.PrescriptionUid);
        var next=Draft("TEST ONLY correction generic");next.SupersedesPrescriptionUid=row.PrescriptionUid;
        var correction=await db.Repository().CreateAsync(p,next,7);
        await db.Stores.Execute("CREATE TRIGGER dbo.Step118SupersedeFault ON dbo.AuditLog AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE ActionName=N'PrescriptionSuperseded') THROW 53999,'TEST ONLY supersession audit failure',1; END");
        try{await Assert.ThrowsAsync<SqlException>(()=>db.Repository().SignAndFinalizeAsync(p,correction.PrescriptionUid,correction.RowVersion,7));}
        finally{await db.Stores.Execute("DROP TRIGGER dbo.Step118SupersedeFault");}
        Assert.Equal(signed.RowVersion,(await db.Repository().GetAsync(p,row.PrescriptionUid))!.RowVersion);
        Assert.Equal(correction.RowVersion,(await db.Repository().GetAsync(p,correction.PrescriptionUid))!.RowVersion);
        Assert.Equal(original,await db.Repository().GetArtifactAsync(p,row.PrescriptionUid));Assert.Null(await db.Repository().GetArtifactAsync(p,correction.PrescriptionUid));
        Assert.Equal(0,await db.Stores.AuditCount(correction.PrescriptionUid,"PrescriptionFinalized"));
        Assert.Equal(0,await db.Stores.AuditCount(row.PrescriptionUid,"PrescriptionSuperseded"));
    }

    [Fact]
    public async Task RealHttpOwnedReadsAuditAfterValidationAndFailClosedOnAuditFailure()
    {
        var p=await db.Stores.SeedPatient();var wrong=await db.Stores.SeedPatient();var row=await db.Repository().CreateAsync(p,Draft(),7);
        await using var host=await db.Host();await using var other=await db.Host(true);using var c=host.Client();using var b=other.Client();using var reader=host.Client("reader");using var inactive=host.Client("inactive");
        var reads=await Reads();
        Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync(Route(wrong,row.PrescriptionUid))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await b.GetAsync(Route(p,row.PrescriptionUid))).StatusCode);
        var p2=await db.SeedOtherPatient();var second=await db.Repository(true).CreateAsync(p2,Draft(),7);
        Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync(Route(p2,second.PrescriptionUid))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await inactive.GetAsync(Route(p,row.PrescriptionUid))).StatusCode);
        using var unresolved=host.Client("unresolved");using var denied=host.Client("no-view");
        Assert.Equal(HttpStatusCode.Forbidden,(await unresolved.GetAsync(Route(p,row.PrescriptionUid))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await denied.GetAsync(Route(p,row.PrescriptionUid))).StatusCode);
        using var anon=new HttpClient{BaseAddress=c.BaseAddress};Assert.Equal(HttpStatusCode.Unauthorized,(await anon.GetAsync(Route(p))).StatusCode);
        Assert.Equal(reads,await Reads());
        Assert.Equal(HttpStatusCode.Forbidden,(await reader.PutAsJsonAsync(Route(p,row.PrescriptionUid),Body(row))).StatusCode);
        await Read(await reader.GetAsync(Route(p,row.PrescriptionUid)));Assert.Equal(reads+1,await Reads());
        await c.GetAsync(Route(p));Assert.Equal(reads+2,await Reads());
        await db.Stores.Execute("CREATE TRIGGER dbo.Step118ReadFault ON dbo.AuditLog AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE ActionName=N'PatientChartOpened') THROW 53999,'TEST ONLY read audit failure',1; END");
        try {foreach(var route in new[]{Route(p),Route(p,row.PrescriptionUid)}){var response=await c.GetAsync(route);Assert.Equal(HttpStatusCode.InternalServerError,response.StatusCode);Assert.DoesNotContain(row.GenericName!,await response.Content.ReadAsStringAsync());}}
        finally{await db.Stores.Execute("DROP TRIGGER dbo.Step118ReadFault");}
        Assert.Equal(reads+2,await Reads());
    }

    [Fact]
    public async Task CppProjectionSelectedPrintAndOwnershipBeforeReadAudit()
    {
        var p=await db.Stores.SeedPatient();var service=new PatientPrescriptionService(db.Repository(),new PrescriptionDatePrecisionTests.Capture());
        for(var i=0;i<7;i++){var r=await service.CreateAsync(p,Draft($"TEST ONLY generic {i} <script>"),7);await service.FinalizeAsync(p,r.PrescriptionUid,r.RowVersion,7);}
        var reads=0;var calls=0;IReadOnlyList<PatientPrescriptionResponse>? foreign=null;
        var audit=CppSelectivePrintTests.Stub<IPatientChartReadAuditService>((_,_)=>{reads++;return Task.FromResult(Guid.NewGuid());});
        var rx=CppSelectivePrintTests.Stub<IPatientPrescriptionService>((_,_)=>{calls++;return foreign is null?service.ListAsync(p):Task.FromResult(foreign);});
        var source=CppSelectivePrintTests.Source(p,null!,audit,prescriptions:rx);
        var summary=(await source.GetAsync(p,"step118"))!;Assert.Equal(5,summary.Prescriptions.Items.Count);Assert.All(summary.Prescriptions.Items,x=>Assert.Contains("generic",x.GenericName));
        var fixture=new CppSelectivePrintTests.Fixture();fixture.Patient.PatientUid=p;fixture.ActualSource=source;
        var html=Encoding.UTF8.GetString((await fixture.Print("Prescriptions"))!);Assert.Contains("TEST ONLY generic 6 &lt;script&gt;",html);Assert.DoesNotContain("<script>",html);
        var previous=calls;Assert.DoesNotContain("TEST ONLY generic",Encoding.UTF8.GetString((await fixture.Print("RiskFactors"))!));Assert.Equal(previous,calls);
        // A discarded Draft must still be rejected before a successful read event.
        foreign=[new(){PatientUid=Guid.NewGuid(),Status="Draft",GenericName="FOREIGN"}];var before=reads;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>source.GetAsync(p,"step118"));
        await Assert.ThrowsAsync<CppPrintUnavailableException>(()=>source.GetForPrintAsync(p,["Prescriptions"],"step118"));
        Assert.Equal(before,reads);
    }
}
