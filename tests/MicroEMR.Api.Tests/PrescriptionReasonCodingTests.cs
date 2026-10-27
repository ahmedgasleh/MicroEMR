using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.PatientPrescriptions;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ReasonCodingContractTests
{
    [Fact]
    public void UnicodeBoundaryTrimPreservesCaseAndInternalCharacters()
    {
        Assert.Null(PrescriptionReasonCoding.Normalize(null));
        Assert.Equal(new("urn:École 薬局", "aB-01", null, "École  🏥"), PrescriptionReasonCoding.Normalize(new(" \u00a0urn:École 薬局\u3000", " aB-01 ", " \u2003", " École  🏥 ")));
    }

    [Theory] [InlineData(0,200)] [InlineData(1,100)] [InlineData(2,100)] [InlineData(3,500)]
    public void EveryComponentCountsUtf16WithoutTruncation(int index,int maximum)
    {
        var fields=new[]{"system","code","version","display"};fields[index]=string.Concat(Enumerable.Repeat("🏥",maximum/2));
        PrescriptionReasonCoding Value()=>new(fields[0],fields[1],fields[2],fields[3]);
        Assert.Equal(Value(),PrescriptionReasonCoding.Normalize(Value()));fields[index]+="x";
        Assert.Throws<ReasonCodingValidationException>(()=>PrescriptionReasonCoding.Normalize(Value()));
    }

    [Theory] [InlineData(null,"code")] [InlineData("system",null)] [InlineData(" ","code")] [InlineData(null,null)]
    public void PartialGroupsAndOrphanOptionalsReject(string? system,string? code)=>Assert.Throws<ReasonCodingValidationException>(()=>PrescriptionReasonCoding.Normalize(new(system,code,"version","display")));

    [Theory] [InlineData("\t")] [InlineData("\r")] [InlineData("\n")] [InlineData("\0")] [InlineData("\u0085")] [InlineData("\u2028")] [InlineData("\u2029")]
    public void AllComponentsRejectControlsBeforeTrimming(string bad)
    {
        foreach(var index in Enumerable.Range(0,4))
        {
            var values=new[]{"system","code","version","display"};values[index]=" "+bad+" ";
            Assert.Throws<ReasonCodingValidationException>(()=>PrescriptionReasonCoding.Normalize(new(values[0],values[1],values[2],values[3])));
        }
    }

    [Fact]
    public void InvalidSurrogatesRejectInEveryComponent()
    {
        foreach(var bad in new[]{"\ud800","\udc00"})foreach(var index in Enumerable.Range(0,4))
        {
            var values=new[]{"system","code","version","display"};values[index]=bad;
            Assert.Throws<ReasonCodingValidationException>(()=>PrescriptionReasonCoding.Normalize(new(values[0],values[1],values[2],values[3])));
        }
    }

    [Fact]
    public void JsonPresenceNullCompleteReplacementAndDuplicateValidation()
    {
        foreach(var options in new[]{new JsonSerializerOptions(),new JsonSerializerOptions(JsonSerializerDefaults.Web)})
        {
            var omitted=JsonSerializer.Deserialize<PrescriptionDraftRequest>("{}",options)!;
            Assert.False(omitted.ReasonCodingSpecified);using(var serialized=JsonDocument.Parse(JsonSerializer.Serialize(omitted,options)))Assert.False(serialized.RootElement.TryGetProperty("reasonCoding",out _));
            foreach(var json in new[]{"null","{\"system\":\"S\",\"code\":\"C\"}","{\"system\":\"S\",\"code\":\"C\",\"display\":\"薬局\"}"})
            {
                var model=JsonSerializer.Deserialize<PrescriptionDraftRequest>("{\"reasonCoding\":"+json+"}",options)!;
                var again=JsonSerializer.Deserialize<PrescriptionDraftRequest>(JsonSerializer.Serialize(model,options),options)!;
                Assert.True(again.ReasonCodingSpecified);Assert.Equal(model.ReasonCoding,again.ReasonCoding);
                if(model.ReasonCoding is {} value)Assert.Null(value.Version);
            }
            foreach(var json in new[]{"42","[]","{\"system\":1}","{\"system\":\"S\",\"System\":\"T\"}","{\"unknown\":\"x\"}","{\"system\":\"\\ud800\"}"})
                Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<PrescriptionDraftRequest>("{\"reasonCoding\":"+json+"}",options));
            Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<PrescriptionDraftRequest>("{\"reasonCoding\":null,\"reasonCoding\":null}",options));
        }
    }
}

public sealed class ReasonCodingDatabase:IAsyncLifetime
{
    internal TargetFacilityAddressDatabase Address {get;}=new();
    internal PrescriptionDateDatabase Db=>Address.Name.Pickup.Base.Db;
    public async Task InitializeAsync()
    {
        await Address.InitializeAsync();
        try{foreach(var other in new[]{false,true}){await using var c=await Db.Factory(other).OpenConnectionAsync();await PrescriptionPrivateNoteFixture.Batches(c,PrescriptionPrivateNoteFixture.Sql("tenant-clinical/migrations/0096-prescription-reason-coding.sql"));}}
        catch{await Address.DisposeAsync();throw;}
    }
    public Task DisposeAsync()=>Address.DisposeAsync();
}

public sealed class ReasonCodingSqlTests(ReasonCodingDatabase fixture):IClassFixture<ReasonCodingDatabase>
{
    private PrescriptionDateDatabase Db=>fixture.Db;
    private static string Route(Guid p,Guid? u=null)=>$"api/patients/{p}/prescriptions"+(u is {} uid?$"/{uid}":"");
    private static PrescriptionReasonCoding Coding=>new("urn:TEST:École 薬局","Ab-01","2026","TEST ONLY 🏥");

    [Fact]
    public async Task ApiAtomicReplacementOmissionClearAuditAndIndicationIndependence()
    {
        var patient=await Db.Stores.SeedPatient();await using var host=await PrescriptionDateHost.Start(Db,false);using var client=host.Client();
        var request=PrescriptionDatePrecisionTests.Draft();request.Indication="TEST ONLY free text";request.ReasonCoding=Coding;
        var submitted=JsonSerializer.SerializeToNode(request)!;
        submitted["actorUserId"]=8;submitted["userId"]=8;
        submitted["patientUid"]=Guid.NewGuid();submitted["tenantUid"]=Db.Tenant(true).TenantUid;
        var response=await client.PostAsJsonAsync(Route(patient),submitted);Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var row=(await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;
        Assert.Equal(Coding,(await client.GetFromJsonAsync<PatientPrescriptionResponse>(Route(patient,row.PrescriptionUid)))!.ReasonCoding);
        var edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=row.RowVersion;edit.Indication=request.Indication;
        row=(await Db.Repository().UpdateAsync(patient,row.PrescriptionUid,edit,7))!;Assert.Equal(Coding,row.ReasonCoding);
        var populatedReplacement=new PrescriptionReasonCoding("urn:PopulatedReplacement","Cd-02","2027","TEST ONLY replacement display");
        edit.RowVersion=row.RowVersion;edit.ReasonCoding=populatedReplacement;
        response=await client.PutAsJsonAsync(Route(patient,row.PrescriptionUid),edit);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        row=(await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;
        edit.RowVersion=row.RowVersion;edit.ReasonCoding=new(" urn:Replacement "," 薬局-02 ");
        response=await client.PutAsJsonAsync(Route(patient,row.PrescriptionUid),edit);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        row=(await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;Assert.Equal(new("urn:Replacement","薬局-02"),row.ReasonCoding);Assert.Equal(request.Indication,row.Indication);
        edit.RowVersion=row.RowVersion;edit.ReasonCoding=null;
        response=await client.PutAsJsonAsync(Route(patient,row.PrescriptionUid),edit);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var clear=(await client.GetFromJsonAsync<PatientPrescriptionResponse>(Route(patient,row.PrescriptionUid)))!;
        Assert.Null(clear.ReasonCoding);Assert.Equal(request.Indication,clear.Indication);Assert.NotEqual(row.RowVersion,clear.RowVersion);
        await using var c=await Db.Factory().OpenConnectionAsync();
        await using var audit=new SqlCommand("SELECT a.ActionName,a.OldValue,a.NewValue,a.UserId,p.PatientUid,a.EntityName FROM dbo.AuditLog a LEFT JOIN dbo.Patient p ON p.PatientId=a.PatientId WHERE a.EntityId=@id ORDER BY a.AuditLogId",c);audit.Parameters.AddWithValue("@id",row.PrescriptionUid.ToString());
        await using var reader=await audit.ExecuteReaderAsync();var seenCreate=false;var seenReplace=false;var seenClear=false;var seenPopulatedReplacement=false;
        static void AssertCoding(JsonElement snapshot,PrescriptionReasonCoding? expected)
        {
            Assert.Equal(expected?.System,snapshot.GetProperty("ReasonCodeSystem").GetString());
            Assert.Equal(expected?.Code,snapshot.GetProperty("ReasonCode").GetString());
            Assert.Equal(expected?.Version,snapshot.GetProperty("ReasonCodeVersion").GetString());
            Assert.Equal(expected?.Display,snapshot.GetProperty("ReasonCodeDisplay").GetString());
        }
        while(await reader.ReadAsync())
        {
            if(reader.GetString(0) is not ("PrescriptionDraftCreated" or "PrescriptionDraftUpdated"))continue;
            Assert.Equal(7L,reader.GetInt64(3));Assert.Equal(patient,reader.GetGuid(4));Assert.Equal("PatientPrescription",reader.GetString(5));
            using var after=JsonDocument.Parse(reader.GetString(2));var x=after.RootElement;
            Assert.Equal(patient,x.GetProperty("PatientUid").GetGuid());Assert.Equal(row.PrescriptionUid,x.GetProperty("PrescriptionUid").GetGuid());
            if(reader.GetString(0)=="PrescriptionDraftCreated"){Assert.True(reader.IsDBNull(1));AssertCoding(x,Coding);seenCreate=true;}
            if(x.GetProperty("ReasonCodeSystem").GetString()==populatedReplacement.System)
            {
                using var before=JsonDocument.Parse(reader.GetString(1));AssertCoding(before.RootElement,Coding);AssertCoding(x,populatedReplacement);seenPopulatedReplacement=true;
            }
            if(reader.GetString(0)=="PrescriptionDraftUpdated" && x.GetProperty("ReasonCodeSystem").ValueKind==JsonValueKind.Null)
            {
                using var before=JsonDocument.Parse(reader.GetString(1));AssertCoding(before.RootElement,new("urn:Replacement","薬局-02"));
                foreach(var key in new[]{"ReasonCodeSystem","ReasonCode","ReasonCodeVersion","ReasonCodeDisplay"})Assert.Equal(JsonValueKind.Null,x.GetProperty(key).ValueKind);seenClear=true;
            }
            if(x.GetProperty("ReasonCodeSystem").GetString()=="urn:Replacement"){using var before=JsonDocument.Parse(reader.GetString(1));AssertCoding(before.RootElement,populatedReplacement);Assert.Equal(JsonValueKind.Null,x.GetProperty("ReasonCodeVersion").ValueKind);Assert.Equal(JsonValueKind.Null,x.GetProperty("ReasonCodeDisplay").ValueKind);seenReplace=true;}
        }
        Assert.True(seenCreate && seenReplace && seenClear && seenPopulatedReplacement);
        await using var other=await Db.Factory(true).OpenConnectionAsync();Assert.NotEqual(c.Database,other.Database);
        await using var otherAudit=new SqlCommand("SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityId=@id",other);otherAudit.Parameters.AddWithValue("@id",row.PrescriptionUid.ToString());
        Assert.Equal(0,Convert.ToInt32(await otherAudit.ExecuteScalarAsync()));
    }

    [Theory] [InlineData("{}")] [InlineData("{\"system\":\"S\"}")] [InlineData("{\"code\":\"C\"}")] [InlineData("{\"version\":\"V\"}")] [InlineData("{\"display\":\"D\"}")] [InlineData("{\"system\":\"S\",\"code\":\"C\\n\"}")]
    [InlineData("{\"system\":\"S\",\"code\":\"C\",\"version\":42}")] [InlineData("{\"system\":\"S\",\"code\":\"C\",\"extra\":\"X\"}")]
    [InlineData("{\"system\":\"S\",\"system\":\"T\",\"code\":\"C\"}")] [InlineData("{\"system\":\"S\",\"code\":null}")]
    public async Task InvalidApiAndSqlGroupsNeverMutate(string coding)
    {
        var patient=await Db.Stores.SeedPatient();var repo=Db.Repository();var request=PrescriptionDatePrecisionTests.Draft();request.ReasonCoding=Coding;var row=await repo.CreateAsync(patient,request,7);
        await using var host=await PrescriptionDateHost.Start(Db,false);using var client=host.Client();
        var payload=JsonSerializer.SerializeToNode(PrescriptionDatePrecisionTests.Draft(),new JsonSerializerOptions(JsonSerializerDefaults.Web))!;payload["rowVersion"]=row.RowVersion;payload["reasonCoding"]=System.Text.Json.Nodes.JsonNode.Parse(coding);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync(Route(patient,row.PrescriptionUid),payload)).StatusCode);
        await using var c=await Db.Factory().OpenConnectionAsync();await using var q=new SqlCommand("EXEC dbo.PatientPrescription_CreateDraft @PatientUid=@p,@ProductName=N'TEST ONLY',@ProductDisplayText=N'TEST ONLY',@Route=N'Oral',@FrequencyCode=N'ONCE',@FrequencyDisplay=N'Once',@Prn=0,@Directions=N'TEST ONLY',@Quantity=1,@QuantityUnit=N'tablet',@AuthorizedRepeats=0,@PrescribedDate='2026-10-26',@ActorUserId=7,@ReasonCodingSpecified=1,@ReasonCoding=@coding",c);
        q.Parameters.AddWithValue("@p",patient);q.Parameters.Add("@coding",System.Data.SqlDbType.NVarChar,-1).Value=coding;
        Assert.Equal(53024,(await Assert.ThrowsAsync<SqlException>(()=>q.ExecuteNonQueryAsync())).Number);
        Assert.Single(await repo.ListAsync(patient));var unchanged=(await repo.GetAsync(patient,row.PrescriptionUid))!;Assert.Equal(Coding,unchanged.ReasonCoding);Assert.Equal(row.RowVersion,unchanged.RowVersion);
    }

    [Theory] [InlineData(0,200)] [InlineData(1,100)] [InlineData(2,100)] [InlineData(3,500)]
    public async Task SqlAndApiUtf16BoundaryAgreementWithoutTruncation(int index,int maximum)
    {
        var fields=new[]{"system","code","version","display"};var patient=await Db.Stores.SeedPatient();await using var host=await PrescriptionDateHost.Start(Db,false);using var client=host.Client();
        foreach(var valid in new[]{true,false})
        {
            fields[index]=string.Concat(Enumerable.Repeat("🏥",maximum/2))+(valid?"":"x");var request=PrescriptionDatePrecisionTests.Draft();request.ReasonCoding=new(fields[0],fields[1],fields[2],fields[3]);
            var response=await client.PostAsJsonAsync(Route(patient),request);Assert.Equal(valid?HttpStatusCode.Created:HttpStatusCode.BadRequest,response.StatusCode);
            await using var c=await Db.Factory().OpenConnectionAsync();await using var q=new SqlCommand("SELECT dbo.PrescriptionReasonCodingValid(@coding)",c);q.Parameters.Add("@coding",System.Data.SqlDbType.NVarChar,-1).Value=JsonSerializer.Serialize(request.ReasonCoding);Assert.Equal(valid,Convert.ToBoolean(await q.ExecuteScalarAsync()));
        }
        Assert.Single(await Db.Repository().ListAsync(patient));
    }

    [Fact]
    public async Task SecurityConcurrencyAuditRollbackAndSqlConstraints()
    {
        var patient=await Db.Stores.SeedPatient();var repo=Db.Repository();var request=PrescriptionDatePrecisionTests.Draft();request.ReasonCoding=Coding;var row=await repo.CreateAsync(patient,request,7);
        var edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=row.RowVersion;edit.ReasonCoding=new("S","C");
        await using var host=await PrescriptionDateHost.Start(Db,false);using var denied=host.Client("reader");Assert.Equal(HttpStatusCode.Forbidden,(await denied.PutAsJsonAsync(Route(patient,row.PrescriptionUid),edit)).StatusCode);
        await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(()=>repo.UpdateAsync(patient,row.PrescriptionUid,edit,8));Assert.Null(await repo.UpdateAsync(await Db.Stores.SeedPatient(),row.PrescriptionUid,edit,7));Assert.Null(await Db.Repository(true).UpdateAsync(patient,row.PrescriptionUid,edit,7));
        await using var c=await Db.Factory().OpenConnectionAsync();
        foreach(var assignment in new[]{"ReasonCodeSystem=NULL","ReasonCode=NULL","ReasonCodeDisplay=N' '+ReasonCodeDisplay","ReasonCode=NCHAR(55296)","ReasonCode=NCHAR(10)"})
        {await using var q=new SqlCommand("UPDATE dbo.PatientPrescription SET "+assignment+" WHERE PrescriptionUid=@id",c);q.Parameters.AddWithValue("@id",row.PrescriptionUid);Assert.Equal(547,(await Assert.ThrowsAsync<SqlException>(()=>q.ExecuteNonQueryAsync())).Number);}
        await PrescriptionPrivateNoteFixture.Batches(c,"CREATE TRIGGER dbo.ReasonAuditFailure ON dbo.AuditLog AFTER INSERT AS BEGIN THROW 52997,'TEST ONLY audit failure',1; END");
        try{await Assert.ThrowsAsync<SqlException>(()=>repo.UpdateAsync(patient,row.PrescriptionUid,edit,7));}finally{await PrescriptionPrivateNoteFixture.Batches(c,"DROP TRIGGER dbo.ReasonAuditFailure");}
        Assert.Equal(row.RowVersion,(await repo.GetAsync(patient,row.PrescriptionUid))!.RowVersion);Assert.Equal(Coding,(await repo.GetAsync(patient,row.PrescriptionUid))!.ReasonCoding);
        var saved=(await repo.UpdateAsync(patient,row.PrescriptionUid,edit,7))!;Assert.NotEqual(row.RowVersion,saved.RowVersion);await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(()=>repo.UpdateAsync(patient,row.PrescriptionUid,edit,7));
    }

    [Fact]
    public async Task WideRawProcedureTrimsAtBoundaryAndRejectsLongGroupsBeforeAnyWrite()
    {
        var patient=await Db.Stores.SeedPatient();await using var c=await Db.Factory().OpenConnectionAsync();
        await using var q=new SqlCommand("EXEC dbo.PatientPrescription_CreateDraft @PatientUid=@p,@ProductName=N'TEST ONLY',@ProductDisplayText=N'TEST ONLY',@Route=N'Oral',@FrequencyCode=N'ONCE',@FrequencyDisplay=N'Once',@Prn=0,@Directions=N'TEST ONLY',@Quantity=1,@QuantityUnit=N'tablet',@AuthorizedRepeats=0,@PrescribedDate='2026-10-26',@ActorUserId=7,@ReasonCodingSpecified=1,@ReasonCoding=@coding",c);
        q.Parameters.AddWithValue("@p",patient);var parameter=q.Parameters.Add("@coding",System.Data.SqlDbType.NVarChar,-1);
        var exact=new PrescriptionReasonCoding(new string('S',200),new string('C',100),new string('V',100),new string('D',500));
        parameter.Value=JsonSerializer.Serialize(exact with{System="\u3000 "+exact.System+" \u00a0"});await q.ExecuteNonQueryAsync();Assert.Equal(exact,Assert.Single(await Db.Repository().ListAsync(patient)).ReasonCoding);
        foreach(var value in new[]{exact with{System=exact.System+"x"},exact with{Code=exact.Code+"x"},exact with{Version=exact.Version+"x"},exact with{Display=exact.Display+"x"}})
        {
            parameter.Value=JsonSerializer.Serialize(value);Assert.Equal(53024,(await Assert.ThrowsAsync<SqlException>(()=>q.ExecuteNonQueryAsync())).Number);Assert.Equal(exact,Assert.Single(await Db.Repository().ListAsync(patient)).ReasonCoding);
        }
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task CorrectionCarryReviewResetFinalizationSnapshotsAndArtifacts(bool signed)
    {
        var patient=await Db.Stores.SeedPatient();var repo=Db.Repository();var request=PrescriptionDatePrecisionTests.Draft();request.ReasonCoding=Coding;request.Indication="Free text independent";var row=await repo.CreateAsync(patient,request,7);
        var original=(await repo.FinalizeAsync(patient,row.PrescriptionUid,row.RowVersion,7))!;var artifact=await repo.GetArtifactAsync(patient,row.PrescriptionUid);
        var edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=original.RowVersion;edit.ReasonCoding=null;await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(()=>repo.UpdateAsync(patient,row.PrescriptionUid,edit,7));
        await using var host=await PrescriptionDateHost.Start(Db,false);using var client=host.Client();var response=await client.PostAsJsonAsync(Route(patient,row.PrescriptionUid)+"/correction",new{original.RowVersion});Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var draft=(await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;Assert.Equal(Coding,draft.ReasonCoding);Assert.True(draft.ReasonCodingReviewRequired);Assert.False(draft.ReasonCodingReviewed);
        Task<PatientPrescriptionResponse?> Finalize(PatientPrescriptionResponse x)=>signed?repo.SignAndFinalizeAsync(patient,x.PrescriptionUid,x.RowVersion,7):repo.FinalizeAsync(patient,x.PrescriptionUid,x.RowVersion,7);
        await Assert.ThrowsAsync<ReasonCodingReviewException>(()=>Finalize(draft));
        edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=draft.RowVersion;edit.Indication=original.Indication;edit.FacilityAddressReviewed=true;edit.FacilityNameReviewed=true;
        draft=(await repo.UpdateAsync(patient,draft.PrescriptionUid,edit,7))!;await Assert.ThrowsAsync<ReasonCodingReviewException>(()=>Finalize(draft));
        edit.RowVersion=draft.RowVersion;edit.ReasonCodingReviewed=true;draft=(await repo.UpdateAsync(patient,draft.PrescriptionUid,edit,7))!;Assert.True(draft.ReasonCodingReviewed);
        edit.RowVersion=draft.RowVersion;edit.ReasonCodingReviewed=false;draft=(await repo.UpdateAsync(patient,draft.PrescriptionUid,edit,7))!;Assert.True(draft.ReasonCodingReviewed);
        // Changing even an optional member invalidates persisted review; clearing retains the source review obligation.
        edit.RowVersion=draft.RowVersion;edit.ReasonCoding=Coding with{Version="Changed"};draft=(await repo.UpdateAsync(patient,draft.PrescriptionUid,edit,7))!;Assert.False(draft.ReasonCodingReviewed);await Assert.ThrowsAsync<ReasonCodingReviewException>(()=>Finalize(draft));
        edit.RowVersion=draft.RowVersion;edit.ReasonCoding=null;draft=(await repo.UpdateAsync(patient,draft.PrescriptionUid,edit,7))!;Assert.False(draft.ReasonCodingReviewed);Assert.True(draft.ReasonCodingReviewRequired);await Assert.ThrowsAsync<ReasonCodingReviewException>(()=>Finalize(draft));
        edit.RowVersion=draft.RowVersion;edit.ReasonCoding=Coding with{Display="Replacement 🏥"};edit.ReasonCodingReviewed=true;draft=(await repo.UpdateAsync(patient,draft.PrescriptionUid,edit,7))!;
        var corrected=(await Finalize(draft))!;Assert.Equal("Finalized",corrected.Status);Assert.Equal(edit.ReasonCoding,corrected.ReasonCoding);Assert.Equal(original.Indication,corrected.Indication);
        Assert.Equal(Coding,(await repo.GetAsync(patient,original.PrescriptionUid))!.ReasonCoding);Assert.Equal(artifact,await repo.GetArtifactAsync(patient,original.PrescriptionUid));
        using var frozen=JsonDocument.Parse((await repo.GetArtifactAsync(patient,corrected.PrescriptionUid))!.Json);Assert.Equal(edit.ReasonCoding,frozen.RootElement.GetProperty("ReasonCoding").Deserialize<PrescriptionReasonCoding>());Assert.True(frozen.RootElement.GetProperty("ReasonCodingReviewed").GetBoolean());
        Assert.Null((await repo.GetAsync(Db.Stores.PriorSignedPatient,Db.Stores.PriorSignedPrescription))!.ReasonCoding);Assert.Equal(Db.Stores.PriorSignedJson,(await repo.GetArtifactAsync(Db.Stores.PriorSignedPatient,Db.Stores.PriorSignedPrescription))!.Json);
        var cancelled=(await repo.CancelAsync(patient,corrected.PrescriptionUid,corrected.RowVersion,"TEST ONLY cancellation",7))!;Assert.Equal(edit.ReasonCoding,cancelled.ReasonCoding);Assert.Equal("TEST ONLY cancellation",cancelled.CancellationReason);Assert.Equal(original.Indication,cancelled.Indication);
    }
}
