using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.PatientPrescriptions;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class DispensingLocationReferenceContractTests
{
    [Fact]
    public void UnicodeBoundaryTrimPreservesCaseAndInternalCharacters()
    {
        Assert.Null(PrescriptionDispensingLocationReference.Normalize(null));
        Assert.Equal(new("urn:École  薬局", "aB  01"), PrescriptionDispensingLocationReference.Normalize(new(" \u00a0urn:École  薬局\u3000", " aB  01 ")));
    }

    [Theory] [InlineData(0,200)] [InlineData(1,200)]
    public void EveryComponentCountsUtf16WithoutTruncation(int index,int maximum)
    {
        var fields=new[]{"system","identifier"};fields[index]=string.Concat(Enumerable.Repeat("🏥",maximum/2));
        PrescriptionDispensingLocationReference Value()=>new(fields[0],fields[1]);
        Assert.Equal(Value(),PrescriptionDispensingLocationReference.Normalize(Value()));fields[index]+="x";
        Assert.Throws<DispensingLocationReferenceValidationException>(()=>PrescriptionDispensingLocationReference.Normalize(Value()));
    }

    [Theory] [InlineData(null,"identifier")] [InlineData("system",null)] [InlineData(" ","identifier")] [InlineData(null,null)]
    public void PartialGroupsAndOrphanOptionalsReject(string? system,string? code)=>Assert.Throws<DispensingLocationReferenceValidationException>(()=>PrescriptionDispensingLocationReference.Normalize(new(system,code)));

    [Theory] [InlineData("\t")] [InlineData("\r")] [InlineData("\n")] [InlineData("\0")] [InlineData("\u0085")] [InlineData("\u2028")] [InlineData("\u2029")]
    public void AllComponentsRejectControlsBeforeTrimming(string bad)
    {
        foreach(var index in Enumerable.Range(0,2))
        {
            var values=new[]{"system","identifier"};values[index]=" "+bad+" ";
            Assert.Throws<DispensingLocationReferenceValidationException>(()=>PrescriptionDispensingLocationReference.Normalize(new(values[0],values[1])));
        }
    }

    [Fact]
    public void InvalidSurrogatesRejectInEveryComponent()
    {
        foreach(var bad in new[]{"\ud800","\udc00"})foreach(var index in Enumerable.Range(0,2))
        {
            var values=new[]{"system","identifier"};values[index]=bad;
            Assert.Throws<DispensingLocationReferenceValidationException>(()=>PrescriptionDispensingLocationReference.Normalize(new(values[0],values[1])));
        }
    }

    [Fact]
    public void JsonPresenceNullCompleteReplacementAndDuplicateValidation()
    {
        foreach(var options in new[]{new JsonSerializerOptions(),new JsonSerializerOptions(JsonSerializerDefaults.Web)})
        {
            var omitted=JsonSerializer.Deserialize<PrescriptionDraftRequest>("{}",options)!;
            Assert.False(omitted.DispensingLocationReferenceSpecified);using(var serialized=JsonDocument.Parse(JsonSerializer.Serialize(omitted,options)))Assert.False(serialized.RootElement.TryGetProperty("dispensingLocationReference",out _));
            foreach(var json in new[]{"null","{\"system\":\"S\",\"identifier\":\"C\"}"})
            {
                var model=JsonSerializer.Deserialize<PrescriptionDraftRequest>("{\"dispensingLocationReference\":"+json+"}",options)!;
                var again=JsonSerializer.Deserialize<PrescriptionDraftRequest>(JsonSerializer.Serialize(model,options),options)!;
                Assert.True(again.DispensingLocationReferenceSpecified);Assert.Equal(model.DispensingLocationReference,again.DispensingLocationReference);
            }
            foreach(var json in new[]{"42","[]","{\"system\":1}","{\"system\":\"S\",\"System\":\"T\"}","{\"unknown\":\"x\"}","{\"system\":\"\\ud800\"}"})
                Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<PrescriptionDraftRequest>("{\"dispensingLocationReference\":"+json+"}",options));
            Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<PrescriptionDraftRequest>("{\"dispensingLocationReference\":null,\"dispensingLocationReference\":null}",options));
        }
    }
}

public sealed class DispensingLocationReferenceDatabase:IAsyncLifetime
{
    internal ProtocolReferenceDatabase Protocol {get;}=new();
    internal PrescriptionDateDatabase Db=>Protocol.Db;
    public async Task InitializeAsync()
    {
        await Protocol.InitializeAsync();
        try{foreach(var other in new[]{false,true}){await using var c=await Db.Factory(other).OpenConnectionAsync();await PrescriptionPrivateNoteFixture.Batches(c,PrescriptionPrivateNoteFixture.Sql("tenant-clinical/migrations/0098-prescription-dispensing-location-reference.sql"));}}
        catch{await Protocol.DisposeAsync();throw;}
    }
    public Task DisposeAsync()=>Protocol.DisposeAsync();
}

public sealed class DispensingLocationReferenceSqlTests(DispensingLocationReferenceDatabase fixture):IClassFixture<DispensingLocationReferenceDatabase>
{
    private PrescriptionDateDatabase Db=>fixture.Db;
    private static string Route(Guid p,Guid? u=null)=>$"api/patients/{p}/prescriptions"+(u is {} uid?$"/{uid}":"");
    private static PrescriptionDispensingLocationReference Coding=>new("urn:TEST:École 薬局","Ab-01");

    [Fact]
    public async Task OptionalCreateAndReusableNamespacedIdentity()
    {
        var patient=await Db.Stores.SeedPatient();await using var host=await PrescriptionDateHost.Start(Db,false);using var client=host.Client();
        var request=PrescriptionDatePrecisionTests.Draft();var response=await client.PostAsJsonAsync(Route(patient),request);Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var empty=(await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;
        Assert.Null((await client.GetFromJsonAsync<PatientPrescriptionResponse>(Route(patient,empty.PrescriptionUid)))!.DispensingLocationReference);
        request.DispensingLocationReference=Coding;var first=await Db.Repository().CreateAsync(patient,request,7);var second=await Db.Repository().CreateAsync(patient,request,7);
        Assert.NotEqual(first.PrescriptionUid,second.PrescriptionUid);Assert.Equal(Coding,first.DispensingLocationReference);Assert.Equal(Coding,second.DispensingLocationReference);
        request.DispensingLocationReference=Coding with{System="urn:OtherJurisdiction"};var other=await Db.Repository().CreateAsync(patient,request,7);
        Assert.Equal(Coding.Identifier,other.DispensingLocationReference!.Identifier);Assert.NotEqual(first.DispensingLocationReference,other.DispensingLocationReference);
    }

    [Fact]
    public async Task ApiAtomicReplacementOmissionClearAuditAndIndicationIndependence()
    {
        var patient=await Db.Stores.SeedPatient();await using var host=await PrescriptionDateHost.Start(Db,false);using var client=host.Client();
        var request=PrescriptionDatePrecisionTests.Draft();request.Indication="TEST ONLY free text";request.DispensingLocationReference=Coding;
        request.ReasonCoding=new("urn:TEST:reason","R-01","2026","Independent reason");
        request.ProtocolReference=new("urn:TEST:protocol","P-01");
        request.TargetDispensingFacilityName="Independent facility";
        request.TargetDispensingFacilityAddress="Independent address";
        var submitted=JsonSerializer.SerializeToNode(request)!;
        submitted["actorUserId"]=8;submitted["userId"]=8;
        submitted["patientUid"]=Guid.NewGuid();submitted["tenantUid"]=Db.Tenant(true).TenantUid;
        var response=await client.PostAsJsonAsync(Route(patient),submitted);Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var row=(await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;
        Assert.Equal(Coding,(await client.GetFromJsonAsync<PatientPrescriptionResponse>(Route(patient,row.PrescriptionUid)))!.DispensingLocationReference);
        var edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=row.RowVersion;edit.Indication=request.Indication;
        row=(await Db.Repository().UpdateAsync(patient,row.PrescriptionUid,edit,7))!;Assert.Equal(Coding,row.DispensingLocationReference);
        var populatedReplacement=new PrescriptionDispensingLocationReference("urn:PopulatedReplacement","Cd-02");
        edit.RowVersion=row.RowVersion;edit.DispensingLocationReference=populatedReplacement;
        response=await client.PutAsJsonAsync(Route(patient,row.PrescriptionUid),edit);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        row=(await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;
        edit.RowVersion=row.RowVersion;edit.DispensingLocationReference=new(" urn:Replacement "," 薬局-02 ");
        response=await client.PutAsJsonAsync(Route(patient,row.PrescriptionUid),edit);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        row=(await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;Assert.Equal(new("urn:Replacement","薬局-02"),row.DispensingLocationReference);Assert.Equal(request.Indication,row.Indication);
        edit.RowVersion=row.RowVersion;edit.DispensingLocationReference=null;
        response=await client.PutAsJsonAsync(Route(patient,row.PrescriptionUid),edit);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var clear=(await client.GetFromJsonAsync<PatientPrescriptionResponse>(Route(patient,row.PrescriptionUid)))!;
        Assert.Null(clear.DispensingLocationReference);Assert.Equal(request.Indication,clear.Indication);Assert.NotEqual(row.RowVersion,clear.RowVersion);
        Assert.Equal(request.ReasonCoding,clear.ReasonCoding);
        Assert.Equal(request.ProtocolReference,clear.ProtocolReference);
        Assert.Equal(request.TargetDispensingFacilityName,clear.TargetDispensingFacilityName);
        Assert.Equal(request.TargetDispensingFacilityAddress,clear.TargetDispensingFacilityAddress);
        await using var c=await Db.Factory().OpenConnectionAsync();
        await using var audit=new SqlCommand("SELECT a.ActionName,a.OldValue,a.NewValue,a.UserId,p.PatientUid,a.EntityName FROM dbo.AuditLog a LEFT JOIN dbo.Patient p ON p.PatientId=a.PatientId WHERE a.EntityId=@id ORDER BY a.AuditLogId",c);audit.Parameters.AddWithValue("@id",row.PrescriptionUid.ToString());
        await using var reader=await audit.ExecuteReaderAsync();var seenCreate=false;var seenReplace=false;var seenClear=false;var seenPopulatedReplacement=false;
        static void AssertCoding(JsonElement snapshot,PrescriptionDispensingLocationReference? expected)
        {
            Assert.Equal(expected?.System,snapshot.GetProperty("DispensingLocationSystem").GetString());
            Assert.Equal(expected?.Identifier,snapshot.GetProperty("DispensingLocationIdentifier").GetString());
        }
        while(await reader.ReadAsync())
        {
            if(reader.GetString(0) is not ("PrescriptionDraftCreated" or "PrescriptionDraftUpdated"))continue;
            Assert.Equal(7L,reader.GetInt64(3));Assert.Equal(patient,reader.GetGuid(4));Assert.Equal("PatientPrescription",reader.GetString(5));
            using var after=JsonDocument.Parse(reader.GetString(2));var x=after.RootElement;
            Assert.Equal(patient,x.GetProperty("PatientUid").GetGuid());Assert.Equal(row.PrescriptionUid,x.GetProperty("PrescriptionUid").GetGuid());
            if(reader.GetString(0)=="PrescriptionDraftCreated"){Assert.True(reader.IsDBNull(1));AssertCoding(x,Coding);seenCreate=true;}
            if(x.GetProperty("DispensingLocationSystem").GetString()==populatedReplacement.System)
            {
                using var before=JsonDocument.Parse(reader.GetString(1));AssertCoding(before.RootElement,Coding);AssertCoding(x,populatedReplacement);seenPopulatedReplacement=true;
            }
            if(reader.GetString(0)=="PrescriptionDraftUpdated" && x.GetProperty("DispensingLocationSystem").ValueKind==JsonValueKind.Null)
            {
                using var before=JsonDocument.Parse(reader.GetString(1));AssertCoding(before.RootElement,new("urn:Replacement","薬局-02"));
                foreach(var key in new[]{"DispensingLocationSystem","DispensingLocationIdentifier"})Assert.Equal(JsonValueKind.Null,x.GetProperty(key).ValueKind);seenClear=true;
            }
            if(x.GetProperty("DispensingLocationSystem").GetString()=="urn:Replacement"){using var before=JsonDocument.Parse(reader.GetString(1));AssertCoding(before.RootElement,populatedReplacement);AssertCoding(x,new("urn:Replacement","薬局-02"));seenReplace=true;}
        }
        Assert.True(seenCreate && seenReplace && seenClear && seenPopulatedReplacement);
        await using var other=await Db.Factory(true).OpenConnectionAsync();Assert.NotEqual(c.Database,other.Database);
        await using var otherAudit=new SqlCommand("SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityId=@id",other);otherAudit.Parameters.AddWithValue("@id",row.PrescriptionUid.ToString());
        Assert.Equal(0,Convert.ToInt32(await otherAudit.ExecuteScalarAsync()));
    }

    [Theory] [InlineData("{}")] [InlineData("{\"system\":\"S\"}")] [InlineData("{\"identifier\":\"C\"}")] [InlineData("{\"version\":\"V\"}")] [InlineData("{\"display\":\"D\"}")] [InlineData("{\"system\":\"S\",\"identifier\":\"C\\n\"}")]
    [InlineData("{\"system\":\"S\",\"identifier\":\"C\",\"version\":42}")] [InlineData("{\"system\":\"S\",\"identifier\":\"C\",\"extra\":\"X\"}")]
    [InlineData("{\"system\":\"S\",\"system\":\"T\",\"identifier\":\"C\"}")] [InlineData("{\"system\":\"S\",\"identifier\":null}")]
    public async Task InvalidApiAndSqlGroupsNeverMutate(string coding)
    {
        var patient=await Db.Stores.SeedPatient();var repo=Db.Repository();var request=PrescriptionDatePrecisionTests.Draft();request.DispensingLocationReference=Coding;var row=await repo.CreateAsync(patient,request,7);
        await using var host=await PrescriptionDateHost.Start(Db,false);using var client=host.Client();
        var payload=JsonSerializer.SerializeToNode(PrescriptionDatePrecisionTests.Draft(),new JsonSerializerOptions(JsonSerializerDefaults.Web))!;payload["rowVersion"]=row.RowVersion;payload["dispensingLocationReference"]=System.Text.Json.Nodes.JsonNode.Parse(coding);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync(Route(patient,row.PrescriptionUid),payload)).StatusCode);
        await using var c=await Db.Factory().OpenConnectionAsync();await using var q=new SqlCommand("EXEC dbo.PatientPrescription_CreateDraft @PatientUid=@p,@ProductName=N'TEST ONLY',@ProductDisplayText=N'TEST ONLY',@Route=N'Oral',@FrequencyCode=N'ONCE',@FrequencyDisplay=N'Once',@Prn=0,@Directions=N'TEST ONLY',@Quantity=1,@QuantityUnit=N'tablet',@AuthorizedRepeats=0,@PrescribedDate='2026-10-26',@ActorUserId=7,@DispensingLocationReferenceSpecified=1,@DispensingLocationReference=@coding",c);
        q.Parameters.AddWithValue("@p",patient);q.Parameters.Add("@coding",System.Data.SqlDbType.NVarChar,-1).Value=coding;
        Assert.Equal(53028,(await Assert.ThrowsAsync<SqlException>(()=>q.ExecuteNonQueryAsync())).Number);
        Assert.Single(await repo.ListAsync(patient));var unchanged=(await repo.GetAsync(patient,row.PrescriptionUid))!;Assert.Equal(Coding,unchanged.DispensingLocationReference);Assert.Equal(row.RowVersion,unchanged.RowVersion);
        await using var audit=new SqlCommand("SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityId=@id AND ActionName IN(N'PrescriptionDraftCreated',N'PrescriptionDraftUpdated')",c);audit.Parameters.AddWithValue("@id",row.PrescriptionUid.ToString());Assert.Equal(1,Convert.ToInt32(await audit.ExecuteScalarAsync()));
    }

    [Theory] [InlineData(0,200)] [InlineData(1,200)]
    public async Task SqlAndApiUtf16BoundaryAgreementWithoutTruncation(int index,int maximum)
    {
        var fields=new[]{"system","identifier"};var patient=await Db.Stores.SeedPatient();await using var host=await PrescriptionDateHost.Start(Db,false);using var client=host.Client();
        foreach(var valid in new[]{true,false})
        {
            fields[index]=string.Concat(Enumerable.Repeat("🏥",maximum/2))+(valid?"":"x");var request=PrescriptionDatePrecisionTests.Draft();request.DispensingLocationReference=new(fields[0],fields[1]);
            var response=await client.PostAsJsonAsync(Route(patient),request);Assert.Equal(valid?HttpStatusCode.Created:HttpStatusCode.BadRequest,response.StatusCode);
            await using var c=await Db.Factory().OpenConnectionAsync();await using var q=new SqlCommand("SELECT dbo.PrescriptionDispensingLocationReferenceValid(@coding)",c);q.Parameters.Add("@coding",System.Data.SqlDbType.NVarChar,-1).Value=JsonSerializer.Serialize(request.DispensingLocationReference);Assert.Equal(valid,Convert.ToBoolean(await q.ExecuteScalarAsync()));
        }
        Assert.Single(await Db.Repository().ListAsync(patient));
    }

    [Fact]
    public async Task SecurityConcurrencyAuditRollbackAndSqlConstraints()
    {
        var patient=await Db.Stores.SeedPatient();var repo=Db.Repository();var request=PrescriptionDatePrecisionTests.Draft();request.DispensingLocationReference=Coding;var row=await repo.CreateAsync(patient,request,7);
        var edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=row.RowVersion;edit.DispensingLocationReference=new("S","C");
        await using var host=await PrescriptionDateHost.Start(Db,false);using var denied=host.Client("reader");Assert.Equal(HttpStatusCode.Forbidden,(await denied.PutAsJsonAsync(Route(patient,row.PrescriptionUid),edit)).StatusCode);
        await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(()=>repo.UpdateAsync(patient,row.PrescriptionUid,edit,8));Assert.Null(await repo.UpdateAsync(await Db.Stores.SeedPatient(),row.PrescriptionUid,edit,7));Assert.Null(await Db.Repository(true).UpdateAsync(patient,row.PrescriptionUid,edit,7));
        await using var c=await Db.Factory().OpenConnectionAsync();
        foreach(var assignment in new[]{"DispensingLocationSystem=NULL","DispensingLocationIdentifier=NULL","DispensingLocationIdentifier=N' '+DispensingLocationIdentifier","DispensingLocationIdentifier=NCHAR(55296)","DispensingLocationIdentifier=NCHAR(10)"})
        {await using var q=new SqlCommand("UPDATE dbo.PatientPrescription SET "+assignment+" WHERE PrescriptionUid=@id",c);q.Parameters.AddWithValue("@id",row.PrescriptionUid);Assert.Equal(547,(await Assert.ThrowsAsync<SqlException>(()=>q.ExecuteNonQueryAsync())).Number);}
        await PrescriptionPrivateNoteFixture.Batches(c,"CREATE TRIGGER dbo.DispensingLocationAuditFailure ON dbo.AuditLog AFTER INSERT AS BEGIN THROW 52997,'TEST ONLY audit failure',1; END");
        try{await Assert.ThrowsAsync<SqlException>(()=>repo.UpdateAsync(patient,row.PrescriptionUid,edit,7));}finally{await PrescriptionPrivateNoteFixture.Batches(c,"DROP TRIGGER dbo.DispensingLocationAuditFailure");}
        Assert.Equal(row.RowVersion,(await repo.GetAsync(patient,row.PrescriptionUid))!.RowVersion);Assert.Equal(Coding,(await repo.GetAsync(patient,row.PrescriptionUid))!.DispensingLocationReference);
        var saved=(await repo.UpdateAsync(patient,row.PrescriptionUid,edit,7))!;Assert.NotEqual(row.RowVersion,saved.RowVersion);await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(()=>repo.UpdateAsync(patient,row.PrescriptionUid,edit,7));
    }

    [Fact]
    public async Task WideRawProcedureTrimsAtBoundaryAndRejectsLongGroupsBeforeAnyWrite()
    {
        var patient=await Db.Stores.SeedPatient();await using var c=await Db.Factory().OpenConnectionAsync();
        await using var q=new SqlCommand("EXEC dbo.PatientPrescription_CreateDraft @PatientUid=@p,@ProductName=N'TEST ONLY',@ProductDisplayText=N'TEST ONLY',@Route=N'Oral',@FrequencyCode=N'ONCE',@FrequencyDisplay=N'Once',@Prn=0,@Directions=N'TEST ONLY',@Quantity=1,@QuantityUnit=N'tablet',@AuthorizedRepeats=0,@PrescribedDate='2026-10-26',@ActorUserId=7,@DispensingLocationReferenceSpecified=1,@DispensingLocationReference=@coding",c);
        q.Parameters.AddWithValue("@p",patient);var parameter=q.Parameters.Add("@coding",System.Data.SqlDbType.NVarChar,-1);
        var exact=new PrescriptionDispensingLocationReference(new string('S',200),new string('C',200));
        parameter.Value=JsonSerializer.Serialize(exact with{System="\u3000 "+exact.System+" \u00a0"});await q.ExecuteNonQueryAsync();Assert.Equal(exact,Assert.Single(await Db.Repository().ListAsync(patient)).DispensingLocationReference);
        foreach(var value in new[]{exact with{System=exact.System+"x"},exact with{Identifier=exact.Identifier+"x"}})
        {
            parameter.Value=JsonSerializer.Serialize(value);Assert.Equal(53028,(await Assert.ThrowsAsync<SqlException>(()=>q.ExecuteNonQueryAsync())).Number);Assert.Equal(exact,Assert.Single(await Db.Repository().ListAsync(patient)).DispensingLocationReference);
        }
    }

    [Theory] [InlineData(false,false)] [InlineData(false,true)] [InlineData(true,false)] [InlineData(true,true)]
    public async Task ReviewedCorrectionCanFinalizePreservedOrClearedReference(bool signed,bool clear)
    {
        var patient=await Db.Stores.SeedPatient();var repo=Db.Repository();
        var request=PrescriptionDatePrecisionTests.Draft();request.DispensingLocationReference=Coding;
        var row=await repo.CreateAsync(patient,request,7);
        var original=(await repo.FinalizeAsync(patient,row.PrescriptionUid,row.RowVersion,7))!;
        var artifact=await repo.GetArtifactAsync(patient,original.PrescriptionUid);
        var correction=PrescriptionDatePrecisionTests.Draft();correction.SupersedesPrescriptionUid=original.PrescriptionUid;
        correction.DispensingLocationReference=new("urn:IgnoreSubmittedCorrection","Ignored");
        var draft=await repo.CreateAsync(patient,correction,7);Assert.Equal(Coding,draft.DispensingLocationReference);
        var edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=draft.RowVersion;
        if(clear)edit.DispensingLocationReference=null;
        edit.DispensingLocationReferenceReviewed=true;
        draft=(await repo.UpdateAsync(patient,draft.PrescriptionUid,edit,7))!;
        var final=signed?await repo.SignAndFinalizeAsync(patient,draft.PrescriptionUid,draft.RowVersion,7):await repo.FinalizeAsync(patient,draft.PrescriptionUid,draft.RowVersion,7);
        Assert.Equal(clear?null:Coding,final!.DispensingLocationReference);Assert.True(final.DispensingLocationReferenceReviewed);
        Assert.Equal(artifact,await repo.GetArtifactAsync(patient,original.PrescriptionUid));
        Assert.Equal(Coding,(await repo.GetAsync(patient,original.PrescriptionUid))!.DispensingLocationReference);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task CorrectionCarryReviewResetFinalizationSnapshotsAndArtifacts(bool signed)
    {
        var patient=await Db.Stores.SeedPatient();var repo=Db.Repository();var request=PrescriptionDatePrecisionTests.Draft();request.DispensingLocationReference=Coding;request.Indication="Free text independent";var row=await repo.CreateAsync(patient,request,7);
        var original=(await repo.FinalizeAsync(patient,row.PrescriptionUid,row.RowVersion,7))!;var artifact=await repo.GetArtifactAsync(patient,row.PrescriptionUid);
        var edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=original.RowVersion;edit.DispensingLocationReference=null;await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(()=>repo.UpdateAsync(patient,row.PrescriptionUid,edit,7));
        await using var host=await PrescriptionDateHost.Start(Db,false);using var client=host.Client();var response=await client.PostAsJsonAsync(Route(patient,row.PrescriptionUid)+"/correction",new{original.RowVersion});Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var draft=(await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;Assert.Equal(Coding,draft.DispensingLocationReference);Assert.True(draft.DispensingLocationReferenceReviewRequired);Assert.False(draft.DispensingLocationReferenceReviewed);
        Task<PatientPrescriptionResponse?> Finalize(PatientPrescriptionResponse x)=>signed?repo.SignAndFinalizeAsync(patient,x.PrescriptionUid,x.RowVersion,7):repo.FinalizeAsync(patient,x.PrescriptionUid,x.RowVersion,7);
        await Assert.ThrowsAsync<DispensingLocationReferenceReviewException>(()=>Finalize(draft));
        edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=draft.RowVersion;edit.Indication=original.Indication;edit.FacilityAddressReviewed=true;edit.FacilityNameReviewed=true;edit.ReasonCodingReviewed=true;
        draft=(await repo.UpdateAsync(patient,draft.PrescriptionUid,edit,7))!;await Assert.ThrowsAsync<DispensingLocationReferenceReviewException>(()=>Finalize(draft));
        edit.RowVersion=draft.RowVersion;edit.DispensingLocationReferenceReviewed=true;draft=(await repo.UpdateAsync(patient,draft.PrescriptionUid,edit,7))!;Assert.True(draft.DispensingLocationReferenceReviewed);
        edit.RowVersion=draft.RowVersion;edit.DispensingLocationReferenceReviewed=false;draft=(await repo.UpdateAsync(patient,draft.PrescriptionUid,edit,7))!;Assert.True(draft.DispensingLocationReferenceReviewed);
        // Changing a reference component invalidates persisted review; clearing retains the source review obligation.
        edit.RowVersion=draft.RowVersion;edit.DispensingLocationReference=Coding with{Identifier="Changed"};draft=(await repo.UpdateAsync(patient,draft.PrescriptionUid,edit,7))!;Assert.False(draft.DispensingLocationReferenceReviewed);await Assert.ThrowsAsync<DispensingLocationReferenceReviewException>(()=>Finalize(draft));
        edit.RowVersion=draft.RowVersion;edit.DispensingLocationReference=null;draft=(await repo.UpdateAsync(patient,draft.PrescriptionUid,edit,7))!;Assert.False(draft.DispensingLocationReferenceReviewed);Assert.True(draft.DispensingLocationReferenceReviewRequired);await Assert.ThrowsAsync<DispensingLocationReferenceReviewException>(()=>Finalize(draft));
        edit.RowVersion=draft.RowVersion;edit.DispensingLocationReference=Coding with{System="Replacement 🏥"};edit.DispensingLocationReferenceReviewed=true;draft=(await repo.UpdateAsync(patient,draft.PrescriptionUid,edit,7))!;
        var corrected=(await Finalize(draft))!;Assert.Equal("Finalized",corrected.Status);Assert.Equal(edit.DispensingLocationReference,corrected.DispensingLocationReference);Assert.Equal(original.Indication,corrected.Indication);
        Assert.Equal(Coding,(await repo.GetAsync(patient,original.PrescriptionUid))!.DispensingLocationReference);Assert.Equal(artifact,await repo.GetArtifactAsync(patient,original.PrescriptionUid));
        using var frozen=JsonDocument.Parse((await repo.GetArtifactAsync(patient,corrected.PrescriptionUid))!.Json);Assert.Equal(edit.DispensingLocationReference,frozen.RootElement.GetProperty("DispensingLocationReference").Deserialize<PrescriptionDispensingLocationReference>());Assert.True(frozen.RootElement.GetProperty("DispensingLocationReferenceReviewed").GetBoolean());
        Assert.Null((await repo.GetAsync(Db.Stores.PriorSignedPatient,Db.Stores.PriorSignedPrescription))!.DispensingLocationReference);Assert.Equal(Db.Stores.PriorSignedJson,(await repo.GetArtifactAsync(Db.Stores.PriorSignedPatient,Db.Stores.PriorSignedPrescription))!.Json);
        var cancelled=(await repo.CancelAsync(patient,corrected.PrescriptionUid,corrected.RowVersion,"TEST ONLY cancellation",7))!;Assert.Equal(edit.DispensingLocationReference,cancelled.DispensingLocationReference);Assert.Equal("TEST ONLY cancellation",cancelled.CancellationReason);Assert.Equal(original.Indication,cancelled.Indication);
    }
}
