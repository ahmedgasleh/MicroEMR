using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.PatientPrescriptions;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class TargetFacilityAddressContractTests
{
    [Theory]
    [InlineData(null,null)] [InlineData("",null)] [InlineData(" \u00a0\u3000",null)]
    [InlineData(" \u00a0École 薬局  O'Brien & Co.\u2003 ","École 薬局  O'Brien & Co.")]
    [InlineData("e\u0301 Pharmacy","e\u0301 Pharmacy")]
    [InlineData("\r\n  10 Rue École\r\nUnit  2\rMontréal\n ","10 Rue École\nUnit  2\nMontréal")]
    public void NormalizationPreservesUnicodeAndInteriorSpacing(string? input,string? expected)=>Assert.Equal(expected,TargetDispensingFacilityAddress.Normalize(input));

    [Theory] [InlineData("\t")] [InlineData("\0")] [InlineData("\u0085")] [InlineData("\u2028")] [InlineData("\u2029")]
    public void ControlsAndBrokenUnicodeRejectBeforeTrimming(string value)=>Assert.Throws<FacilityAddressValidationException>(()=>TargetDispensingFacilityAddress.Normalize(" "+value+" "));

    [Fact]
    public void UnpairedSurrogatesRejectWithoutAttributeMetadataReplacement()
    {
        foreach(var value in new[]{(char)0xd800,(char)0xdc00})
            Assert.Throws<FacilityAddressValidationException>(()=>TargetDispensingFacilityAddress.Normalize(new string(value,1)));
    }

    [Fact]
    public void LengthCountsNormalizedUtf16WithoutTruncation()
    {
        var max=string.Concat(Enumerable.Repeat("\U0001f3e5",500));Assert.Equal(1000,max.Length);
        Assert.Equal(max,TargetDispensingFacilityAddress.Normalize("\u00a0 "+max+" \u3000"));
        Assert.Throws<FacilityAddressValidationException>(()=>TargetDispensingFacilityAddress.Normalize(max+"x"));
    }

    [Fact]
    public void JsonPresenceSurvivesInternalAndWebSerializationAndDuplicateOrNontextRejects()
    {
        foreach(var options in new[]{new JsonSerializerOptions(),new JsonSerializerOptions(JsonSerializerDefaults.Web)})
        {
            var absent=JsonSerializer.Deserialize<PrescriptionDraftRequest>("{}",options)!;
            Assert.False(absent.TargetDispensingFacilityAddressSpecified);Assert.DoesNotContain("targetDispensingFacilityAddress",JsonSerializer.Serialize(absent,options));
            foreach(var value in new[]{"null","\"\"","\"薬局\""})
            {
                var request=JsonSerializer.Deserialize<PrescriptionDraftRequest>("{\"targetDispensingFacilityAddress\":"+value+"}",options)!;
                Assert.True(request.TargetDispensingFacilityAddressSpecified);
                var again=JsonSerializer.Deserialize<PrescriptionDraftRequest>(JsonSerializer.Serialize(request,options),options)!;
                Assert.True(again.TargetDispensingFacilityAddressSpecified);Assert.Equal(request.TargetDispensingFacilityAddress,again.TargetDispensingFacilityAddress);
            }
            Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<PrescriptionDraftRequest>("{\"targetDispensingFacilityAddress\":42}",options));
            Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<PrescriptionDraftRequest>("{\"targetDispensingFacilityAddress\":\"\\ud800\"}",options));
            Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<PrescriptionDraftRequest>("{\"targetDispensingFacilityAddress\":\"A\",\"targetDispensingFacilityAddress\":\"B\"}",options));
        }
    }
}

public sealed class TargetFacilityAddressSqlTests(TargetFacilityAddressDatabase fixture):IClassFixture<TargetFacilityAddressDatabase>
{
    private PrescriptionDateDatabase Db=>fixture.Name.Pickup.Base.Db;
    private Task<PrescriptionDateHost> Host(bool other=false)=>PrescriptionDateHost.Start(Db,other);
    private static string Route(Guid patient,Guid? prescription=null)=>$"api/patients/{patient}/prescriptions"+(prescription is {} uid?$"/{uid}":"");

    [Theory]
    [InlineData("\r\n 10 Rue  École\r\n\n薬局\rMontréal\n ")]
    [InlineData("\r\n\u00a0\u3000")]
    [InlineData("10 Rue École\nUnit 2\n🏥")]
    public async Task ApplicationAndSqlAgreeOnMultilineCanonicalRepresentation(string input)
    {
        var expected=TargetDispensingFacilityAddress.Normalize(input);
        await using var c=await Db.Factory().OpenConnectionAsync();
        await using var q=new SqlCommand("SELECT dbo.PrescriptionFacilityAddressValid(@value),dbo.PrescriptionFacilityAddressNormalize(@value)",c);
        q.Parameters.Add("@value",System.Data.SqlDbType.NVarChar,-1).Value=input;
        await using var reader=await q.ExecuteReaderAsync();Assert.True(await reader.ReadAsync());Assert.True(reader.GetBoolean(0));
        Assert.Equal(expected,reader.IsDBNull(1)?null:reader.GetString(1));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task AddressReviewCannotBeReplacedByNamePickupOrTransitionFlags(bool signed)
    {
        var patient=await Db.Stores.SeedPatient();var repo=Db.Repository();var request=PrescriptionDatePrecisionTests.Draft();
        request.TargetDispensingFacilityAddress="10 Rue École\nMontréal";request.TargetDispensingFacilityName="Pharmacie";
        request.EarliestPickup=new("Day","2026-11-03","America/Toronto");
        var row=await repo.CreateAsync(patient,request,7);var original=(await repo.FinalizeAsync(patient,row.PrescriptionUid,row.RowVersion,7))!;
        await using var host=await Host();using var client=host.Client();
        var response=await client.PostAsJsonAsync(Route(patient,row.PrescriptionUid)+"/correction",new{original.RowVersion});
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);var draft=(await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;
        var edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=draft.RowVersion;edit.FacilityNameReviewed=true;
        draft=(await repo.UpdateAsync(patient,draft.PrescriptionUid,edit,7))!;
        var action=signed?"sign-and-finalize":"finalize";
        response=await client.PostAsJsonAsync(Route(patient,draft.PrescriptionUid)+"/"+action,new{draft.RowVersion,ConfirmSignature=true,PickupReviewed=true,FacilityAddressReviewed=true});
        Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode); // Only the persisted Draft review counts.
        var unchanged=(await repo.GetAsync(patient,draft.PrescriptionUid))!;Assert.Equal(draft.RowVersion,unchanged.RowVersion);Assert.False(unchanged.FacilityAddressReviewed);
        edit.RowVersion=draft.RowVersion;edit.FacilityAddressReviewed=true;
        draft=(await repo.UpdateAsync(patient,draft.PrescriptionUid,edit,7))!;Assert.True(draft.FacilityAddressReviewed);
        edit.RowVersion=draft.RowVersion;edit.FacilityAddressReviewed=false;edit.TargetDispensingFacilityAddress="11 Rue École\nMontréal";
        draft=(await repo.UpdateAsync(patient,draft.PrescriptionUid,edit,7))!;Assert.False(draft.FacilityAddressReviewed);
        edit.RowVersion=draft.RowVersion;edit.FacilityAddressReviewed=true;
        draft=(await repo.UpdateAsync(patient,draft.PrescriptionUid,edit,7))!;
        edit.RowVersion=draft.RowVersion;edit.FacilityAddressReviewed=false;edit.OmitTargetDispensingFacilityAddress();
        var unrelated=(await repo.UpdateAsync(patient,draft.PrescriptionUid,edit,7))!;Assert.True(unrelated.FacilityAddressReviewed); // Unrelated save preserves review.
        response=await client.PostAsJsonAsync(Route(patient,draft.PrescriptionUid)+"/"+action,new{unrelated.RowVersion,ConfirmSignature=true,PickupReviewed=true});
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
    }

    [Fact]
    public async Task SqlConstraintRejectsNoncanonicalCrAndMalformedUtf16WithoutMutation()
    {
        var patient=await Db.Stores.SeedPatient();var repo=Db.Repository();var row=await repo.CreateAsync(patient,PrescriptionDatePrecisionTests.Draft(),7);
        await using var c=await Db.Factory().OpenConnectionAsync();
        foreach(var expression in new[]{"N'Line 1'+NCHAR(13)+N'Line 2'","NCHAR(55296)","NCHAR(56320)"})
        {
            await using var q=new SqlCommand("UPDATE dbo.PatientPrescription SET TargetDispensingFacilityAddress="+expression+" WHERE PrescriptionUid=@id",c);
            q.Parameters.AddWithValue("@id",row.PrescriptionUid);
            Assert.Equal(547,(await Assert.ThrowsAsync<SqlException>(()=>q.ExecuteNonQueryAsync())).Number);
        }
        Assert.Equal(row.RowVersion,(await repo.GetAsync(patient,row.PrescriptionUid))!.RowVersion);
    }

    [Theory]
    [InlineData("Pharmacie du Centre\r\n10 Rue École\rMontréal")] [InlineData(" \u00a0薬局  École O'Brien & Co.\u3000")] [InlineData("🏥 Pharmacie")]
    public async Task ApiUnicodeRoundTripDuplicatesOmissionClearAndAudit(string input)
    {
        var patient=await Db.Stores.SeedPatient();await using var host=await Host();using var client=host.Client();
        var request=PrescriptionDatePrecisionTests.Draft();request.TargetDispensingFacilityAddress=input;
        var response=await client.PostAsJsonAsync(Route(patient),request);Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var row=(await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;Assert.Equal(TargetDispensingFacilityAddress.Normalize(input),row.TargetDispensingFacilityAddress);
        Assert.Equal(HttpStatusCode.Created,(await client.PostAsJsonAsync(Route(patient),request)).StatusCode); // Names are not unique identities.
        var edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=row.RowVersion;
        var saved=(await Db.Repository().UpdateAsync(patient,row.PrescriptionUid,edit,7))!;Assert.Equal(row.TargetDispensingFacilityAddress,saved.TargetDispensingFacilityAddress);
        edit.RowVersion=saved.RowVersion;edit.TargetDispensingFacilityAddress=null;
        response=await client.PutAsJsonAsync(Route(patient,row.PrescriptionUid),edit);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var cleared=(await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;Assert.Null(cleared.TargetDispensingFacilityAddress);
        await using var c=await Db.Factory().OpenConnectionAsync();
        await using var audit=new SqlCommand("SELECT TOP(1) UserId,OldValue,NewValue FROM dbo.AuditLog WHERE EntityId=@id AND ActionName=N'PrescriptionDraftUpdated' ORDER BY AuditLogId DESC",c);audit.Parameters.AddWithValue("@id",row.PrescriptionUid.ToString());
        await using var reader=await audit.ExecuteReaderAsync();Assert.True(await reader.ReadAsync());Assert.Equal(7,Convert.ToInt32(reader[0]));
        using var before=JsonDocument.Parse(reader.GetString(1));using var after=JsonDocument.Parse(reader.GetString(2));
        Assert.Equal(row.TargetDispensingFacilityAddress,before.RootElement.GetProperty("TargetDispensingFacilityAddress").GetString());Assert.Equal(JsonValueKind.Null,after.RootElement.GetProperty("TargetDispensingFacilityAddress").ValueKind);
    }

    [Theory] [InlineData(1000,true)] [InlineData(1001,false)]
    public async Task ApiLengthAndRawSqlNormalizationAgree(int length,bool valid)
    {
        var patient=await Db.Stores.SeedPatient();var name=valid?string.Concat(Enumerable.Repeat("🏥",500)):new string('é',length);await using var host=await Host();using var client=host.Client();
        var request=PrescriptionDatePrecisionTests.Draft();request.TargetDispensingFacilityAddress=name;
        var response=await client.PostAsJsonAsync(Route(patient),request);Assert.Equal(valid?HttpStatusCode.Created:HttpStatusCode.BadRequest,response.StatusCode);
        Assert.Equal(valid?1:0,(await Db.Repository().ListAsync(patient)).Count);
        await using var c=await Db.Factory().OpenConnectionAsync();
        await using var command=new SqlCommand("SELECT dbo.PrescriptionFacilityAddressValid(@value),dbo.PrescriptionFacilityAddressNormalize(@value)",c);
        command.Parameters.Add("@value",System.Data.SqlDbType.NVarChar,-1).Value="\u00a0 "+name+" \u3000";
        await using var reader=await command.ExecuteReaderAsync();Assert.True(await reader.ReadAsync());Assert.Equal(valid,reader.GetBoolean(0));Assert.Equal(name,reader.GetString(1));
    }

    [Theory] [InlineData("\0")] [InlineData("\t")] [InlineData("\u0085")] [InlineData("\u2029")]
    public async Task InvalidApiAndProcedureInputLeavesNoClinicalMutation(string bad)
    {
        var patient=await Db.Stores.SeedPatient();await using var host=await Host();using var client=host.Client();
        var request=PrescriptionDatePrecisionTests.Draft();request.TargetDispensingFacilityAddress=bad;
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(Route(patient),request)).StatusCode);
        await using var c=await Db.Factory().OpenConnectionAsync();
        await using var raw=new SqlCommand("EXEC dbo.PatientPrescription_CreateDraft @PatientUid=@p,@ProductName=N'TEST ONLY',@ProductDisplayText=N'TEST ONLY',@Route=N'Oral',@FrequencyCode=N'ONCE',@FrequencyDisplay=N'Once',@Prn=0,@Directions=N'TEST ONLY',@Quantity=1,@QuantityUnit=N'tablet',@AuthorizedRepeats=0,@PrescribedDate='2026-10-26',@ActorUserId=7,@TargetDispensingFacilityAddressSpecified=1,@TargetDispensingFacilityAddress=@name",c);
        raw.Parameters.AddWithValue("@p",patient);raw.Parameters.Add("@name",System.Data.SqlDbType.NVarChar,-1).Value=bad;
        Assert.Equal(53022,(await Assert.ThrowsAsync<SqlException>(()=>raw.ExecuteNonQueryAsync())).Number);Assert.Empty(await Db.Repository().ListAsync(patient));
    }

    [Theory] [InlineData("")] [InlineData(" \u00a0\u3000\r\n")]
    public async Task EmptyAndBoundaryOnlyClearWithPreservedOtherFacts(string clear)
    {
        var patient=await Db.Stores.SeedPatient();var request=PrescriptionDatePrecisionTests.Draft();request.TargetDispensingFacilityAddress="Original";
        var row=await Db.Repository().CreateAsync(patient,request,7);var edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=row.RowVersion;edit.TargetDispensingFacilityAddress=clear;
        var saved=(await Db.Repository().UpdateAsync(patient,row.PrescriptionUid,edit,7))!;Assert.Null(saved.TargetDispensingFacilityAddress);Assert.Equal(row.ProductDisplayText,saved.ProductDisplayText);
        await using var c=await Db.Factory().OpenConnectionAsync();await using var raw=new SqlCommand("UPDATE dbo.PatientPrescription SET TargetDispensingFacilityAddress=N' ' WHERE PrescriptionUid=@id",c);raw.Parameters.AddWithValue("@id",row.PrescriptionUid);
        Assert.Equal(547,(await Assert.ThrowsAsync<SqlException>(()=>raw.ExecuteNonQueryAsync())).Number);
        Assert.Equal(saved.RowVersion,(await Db.Repository().GetAsync(patient,row.PrescriptionUid))!.RowVersion);
    }

    [Fact]
    public async Task WideProcedureParameterNormalizesBeforeLengthCheckAndNeverTruncates()
    {
        var patient=await Db.Stores.SeedPatient();var exact=string.Concat(Enumerable.Repeat("🏥",500));
        await using var c=await Db.Factory().OpenConnectionAsync();
        await using var raw=new SqlCommand("EXEC dbo.PatientPrescription_CreateDraft @PatientUid=@p,@ProductName=N'TEST ONLY',@ProductDisplayText=N'TEST ONLY',@Route=N'Oral',@FrequencyCode=N'ONCE',@FrequencyDisplay=N'Once',@Prn=0,@Directions=N'TEST ONLY',@Quantity=1,@QuantityUnit=N'tablet',@AuthorizedRepeats=0,@PrescribedDate='2026-10-26',@ActorUserId=7,@TargetDispensingFacilityAddressSpecified=1,@TargetDispensingFacilityAddress=@name",c);
        raw.Parameters.AddWithValue("@p",patient);var parameter=raw.Parameters.Add("@name",System.Data.SqlDbType.NVarChar,-1);parameter.Value="\u3000 "+exact+" \u00a0";
        await raw.ExecuteNonQueryAsync();Assert.Equal(exact,Assert.Single(await Db.Repository().ListAsync(patient)).TargetDispensingFacilityAddress);
        parameter.Value=exact+"x";Assert.Equal(53022,(await Assert.ThrowsAsync<SqlException>(()=>raw.ExecuteNonQueryAsync())).Number);
        Assert.Equal(exact,Assert.Single(await Db.Repository().ListAsync(patient)).TargetDispensingFacilityAddress);
    }

    [Fact]
    public async Task PermissionOwnershipTenantConcurrencyAndAuditFailureProtectName()
    {
        var patient=await Db.Stores.SeedPatient();var repo=Db.Repository();var request=PrescriptionDatePrecisionTests.Draft();request.TargetDispensingFacilityAddress="Original";var row=await repo.CreateAsync(patient,request,7);
        var edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=row.RowVersion;edit.TargetDispensingFacilityAddress="Replacement";
        await using var host=await Host();using var denied=host.Client("reader");Assert.Equal(HttpStatusCode.Forbidden,(await denied.PutAsJsonAsync(Route(patient,row.PrescriptionUid),edit)).StatusCode);
        await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(()=>repo.UpdateAsync(patient,row.PrescriptionUid,edit,8));
        Assert.Null(await repo.UpdateAsync(await Db.Stores.SeedPatient(),row.PrescriptionUid,edit,7));Assert.Null(await Db.Repository(true).UpdateAsync(patient,row.PrescriptionUid,edit,7));
        await using var c=await Db.Factory().OpenConnectionAsync();await PrescriptionPrivateNoteFixture.Batches(c,"CREATE TRIGGER dbo.FacilityAuditFailure ON dbo.AuditLog AFTER INSERT AS BEGIN THROW 52997,'TEST ONLY audit failure',1; END");
        try{await Assert.ThrowsAsync<SqlException>(()=>repo.UpdateAsync(patient,row.PrescriptionUid,edit,7));}finally{await PrescriptionPrivateNoteFixture.Batches(c,"DROP TRIGGER dbo.FacilityAuditFailure");}
        var unchanged=(await repo.GetAsync(patient,row.PrescriptionUid))!;Assert.Equal(row.RowVersion,unchanged.RowVersion);Assert.Equal("Original",unchanged.TargetDispensingFacilityAddress);
        var saved=(await repo.UpdateAsync(patient,row.PrescriptionUid,edit,7))!;await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(()=>repo.UpdateAsync(patient,row.PrescriptionUid,edit,7));
        Assert.NotEqual(row.RowVersion,saved.RowVersion);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task LinkedCorrectionInheritsNameRequiresReviewAndPreservesArtifacts(bool signed)
    {
        var patient=await Db.Stores.SeedPatient();var repo=Db.Repository();var request=PrescriptionDatePrecisionTests.Draft();request.TargetDispensingFacilityAddress="薬局 Original";var row=await repo.CreateAsync(patient,request,7);
        var final=(await repo.FinalizeAsync(patient,row.PrescriptionUid,row.RowVersion,7))!;var originalArtifact=await repo.GetArtifactAsync(patient,row.PrescriptionUid);
        var edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=final.RowVersion;edit.TargetDispensingFacilityAddress="Forbidden rename";await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(()=>repo.UpdateAsync(patient,row.PrescriptionUid,edit,7));
        await using var host=await Host();using var client=host.Client();var response=await client.PostAsJsonAsync(Route(patient,row.PrescriptionUid)+"/correction",new{final.RowVersion});Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var correction=(await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;Assert.Equal(row.TargetDispensingFacilityAddress,correction.TargetDispensingFacilityAddress);Assert.True(correction.FacilityAddressReviewRequired);Assert.False(correction.FacilityAddressReviewed);
        if(signed)await Assert.ThrowsAsync<FacilityAddressReviewException>(()=>repo.SignAndFinalizeAsync(patient,correction.PrescriptionUid,correction.RowVersion,7));
        else await Assert.ThrowsAsync<FacilityAddressReviewException>(()=>repo.FinalizeAsync(patient,correction.PrescriptionUid,correction.RowVersion,7));
        edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=correction.RowVersion;edit.FacilityAddressReviewed=true;
        var reviewed=(await repo.UpdateAsync(patient,correction.PrescriptionUid,edit,7))!;Assert.True(reviewed.FacilityAddressReviewed);
        edit.RowVersion=reviewed.RowVersion;edit.FacilityAddressReviewed=false;edit.TargetDispensingFacilityAddress=null;
        var cleared=(await repo.UpdateAsync(patient,correction.PrescriptionUid,edit,7))!;Assert.False(cleared.FacilityAddressReviewed);Assert.True(cleared.FacilityAddressReviewRequired);
        await Assert.ThrowsAsync<FacilityAddressReviewException>(()=>repo.FinalizeAsync(patient,cleared.PrescriptionUid,cleared.RowVersion,7));
        edit.RowVersion=cleared.RowVersion;edit.FacilityAddressReviewed=true;edit.TargetDispensingFacilityAddress="Clinique Nouvelle";
        reviewed=(await repo.UpdateAsync(patient,cleared.PrescriptionUid,edit,7))!;
        var corrected=signed?await repo.SignAndFinalizeAsync(patient,reviewed.PrescriptionUid,reviewed.RowVersion,7):await repo.FinalizeAsync(patient,reviewed.PrescriptionUid,reviewed.RowVersion,7);
        Assert.Equal("Finalized",corrected!.Status);Assert.Equal("Clinique Nouvelle",corrected.TargetDispensingFacilityAddress);
        Assert.Equal(originalArtifact,await repo.GetArtifactAsync(patient,row.PrescriptionUid));Assert.Equal(row.TargetDispensingFacilityAddress,(await repo.GetAsync(patient,row.PrescriptionUid))!.TargetDispensingFacilityAddress);
        using var frozen=JsonDocument.Parse((await repo.GetArtifactAsync(patient,corrected.PrescriptionUid))!.Json);Assert.Equal("Clinique Nouvelle",frozen.RootElement.GetProperty("TargetDispensingFacilityAddress").GetString());Assert.True(frozen.RootElement.GetProperty("FacilityAddressReviewed").GetBoolean());
        Assert.Null((await repo.GetAsync(Db.Stores.PriorSignedPatient,Db.Stores.PriorSignedPrescription))!.TargetDispensingFacilityAddress);Assert.Equal(Db.Stores.PriorSignedJson,(await repo.GetArtifactAsync(Db.Stores.PriorSignedPatient,Db.Stores.PriorSignedPrescription))!.Json);
        var renderer=new PrescriptionDatePrecisionTests.Capture();var service=new PatientPrescriptionService(repo,renderer);
        var output=new PrescriptionOutputService(repo,repo,service,renderer,TimeProvider.System);
        var bytes=(await output.GenerateAsync(patient,corrected.PrescriptionUid,new("test-subject-7","TEST ONLY","facility-test")))!;
        Assert.DoesNotContain("Clinique Nouvelle",System.Text.Encoding.UTF8.GetString(bytes));
    }
}

public sealed class TargetFacilityAddressDatabase:IAsyncLifetime
{
    internal TargetFacilityNameDatabase Name {get;}=new();
    public async Task InitializeAsync()
    {
        await Name.InitializeAsync();
        try{foreach(var other in new[]{false,true}){await using var c=await Name.Pickup.Base.Db.Factory(other).OpenConnectionAsync();await PrescriptionPrivateNoteFixture.Batches(c,PrescriptionPrivateNoteFixture.Sql("tenant-clinical/migrations/0095-prescription-target-facility-address.sql"));}}
        catch{await Name.DisposeAsync();throw;}
    }
    public Task DisposeAsync()=>Name.DisposeAsync();
}
