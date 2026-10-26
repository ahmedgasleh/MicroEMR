using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.PatientPrescriptions;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class TargetFacilityNameContractTests
{
    [Theory]
    [InlineData(null,null)] [InlineData("",null)] [InlineData(" \u00a0\u3000",null)]
    [InlineData(" \u00a0École 薬局  O'Brien & Co.\u2003 ","École 薬局  O'Brien & Co.")]
    [InlineData("e\u0301 Pharmacy","e\u0301 Pharmacy")]
    public void NormalizationPreservesUnicodeAndInteriorSpacing(string? input,string? expected)=>Assert.Equal(expected,TargetDispensingFacilityName.Normalize(input));

    [Theory] [InlineData("\r")] [InlineData("\n")] [InlineData("\t")] [InlineData("\0")] [InlineData("\u0085")] [InlineData("\u2028")] [InlineData("\u2029")]
    public void ControlsAndBrokenUnicodeRejectBeforeTrimming(string value)=>Assert.Throws<FacilityNameValidationException>(()=>TargetDispensingFacilityName.Normalize(" "+value+" "));

    [Fact]
    public void UnpairedSurrogatesRejectWithoutAttributeMetadataReplacement()
    {
        foreach(var value in new[]{(char)0xd800,(char)0xdc00})
            Assert.Throws<FacilityNameValidationException>(()=>TargetDispensingFacilityName.Normalize(new string(value,1)));
    }

    [Fact]
    public void LengthCountsNormalizedUtf16WithoutTruncation()
    {
        var max=string.Concat(Enumerable.Repeat("\U0001f3e5",100));Assert.Equal(200,max.Length);
        Assert.Equal(max,TargetDispensingFacilityName.Normalize("\u00a0 "+max+" \u3000"));
        Assert.Throws<FacilityNameValidationException>(()=>TargetDispensingFacilityName.Normalize(max+"x"));
    }

    [Fact]
    public void JsonPresenceSurvivesInternalAndWebSerializationAndDuplicateOrNontextRejects()
    {
        foreach(var options in new[]{new JsonSerializerOptions(),new JsonSerializerOptions(JsonSerializerDefaults.Web)})
        {
            var absent=JsonSerializer.Deserialize<PrescriptionDraftRequest>("{}",options)!;
            Assert.False(absent.TargetDispensingFacilityNameSpecified);Assert.DoesNotContain("targetDispensingFacilityName",JsonSerializer.Serialize(absent,options));
            foreach(var value in new[]{"null","\"\"","\"薬局\""})
            {
                var request=JsonSerializer.Deserialize<PrescriptionDraftRequest>("{\"targetDispensingFacilityName\":"+value+"}",options)!;
                Assert.True(request.TargetDispensingFacilityNameSpecified);
                var again=JsonSerializer.Deserialize<PrescriptionDraftRequest>(JsonSerializer.Serialize(request,options),options)!;
                Assert.True(again.TargetDispensingFacilityNameSpecified);Assert.Equal(request.TargetDispensingFacilityName,again.TargetDispensingFacilityName);
            }
            Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<PrescriptionDraftRequest>("{\"targetDispensingFacilityName\":42}",options));
            Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<PrescriptionDraftRequest>("{\"targetDispensingFacilityName\":\"\\ud800\"}",options));
            Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<PrescriptionDraftRequest>("{\"targetDispensingFacilityName\":\"A\",\"targetDispensingFacilityName\":\"B\"}",options));
        }
    }
}

public sealed class TargetFacilityNameSqlTests(TargetFacilityNameDatabase fixture):IClassFixture<TargetFacilityNameDatabase>
{
    private PrescriptionDateDatabase Db=>fixture.Pickup.Base.Db;
    private Task<PrescriptionDateHost> Host(bool other=false)=>PrescriptionDateHost.Start(Db,other);
    private static string Route(Guid patient,Guid? prescription=null)=>$"api/patients/{patient}/prescriptions"+(prescription is {} uid?$"/{uid}":"");

    [Theory]
    [InlineData("Pharmacie du Centre")] [InlineData(" \u00a0薬局  École O'Brien & Co.\u3000")] [InlineData("🏥 Pharmacie")]
    public async Task ApiUnicodeRoundTripDuplicatesOmissionClearAndAudit(string input)
    {
        var patient=await Db.Stores.SeedPatient();await using var host=await Host();using var client=host.Client();
        var request=PrescriptionDatePrecisionTests.Draft();request.TargetDispensingFacilityName=input;
        var response=await client.PostAsJsonAsync(Route(patient),request);Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var row=(await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;Assert.Equal(TargetDispensingFacilityName.Normalize(input),row.TargetDispensingFacilityName);
        Assert.Equal(HttpStatusCode.Created,(await client.PostAsJsonAsync(Route(patient),request)).StatusCode); // Names are not unique identities.
        var edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=row.RowVersion;
        var saved=(await Db.Repository().UpdateAsync(patient,row.PrescriptionUid,edit,7))!;Assert.Equal(row.TargetDispensingFacilityName,saved.TargetDispensingFacilityName);
        edit.RowVersion=saved.RowVersion;edit.TargetDispensingFacilityName=null;
        response=await client.PutAsJsonAsync(Route(patient,row.PrescriptionUid),edit);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var cleared=(await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;Assert.Null(cleared.TargetDispensingFacilityName);
        await using var c=await Db.Factory().OpenConnectionAsync();
        await using var audit=new SqlCommand("SELECT TOP(1) UserId,OldValue,NewValue FROM dbo.AuditLog WHERE EntityId=@id AND ActionName=N'PrescriptionDraftUpdated' ORDER BY AuditLogId DESC",c);audit.Parameters.AddWithValue("@id",row.PrescriptionUid.ToString());
        await using var reader=await audit.ExecuteReaderAsync();Assert.True(await reader.ReadAsync());Assert.Equal(7,Convert.ToInt32(reader[0]));
        using var before=JsonDocument.Parse(reader.GetString(1));using var after=JsonDocument.Parse(reader.GetString(2));
        Assert.Equal(row.TargetDispensingFacilityName,before.RootElement.GetProperty("TargetDispensingFacilityName").GetString());Assert.Equal(JsonValueKind.Null,after.RootElement.GetProperty("TargetDispensingFacilityName").ValueKind);
    }

    [Theory] [InlineData(200,true)] [InlineData(201,false)]
    public async Task ApiLengthAndRawSqlNormalizationAgree(int length,bool valid)
    {
        var patient=await Db.Stores.SeedPatient();var name=valid?string.Concat(Enumerable.Repeat("🏥",100)):new string('é',length);await using var host=await Host();using var client=host.Client();
        var request=PrescriptionDatePrecisionTests.Draft();request.TargetDispensingFacilityName=name;
        var response=await client.PostAsJsonAsync(Route(patient),request);Assert.Equal(valid?HttpStatusCode.Created:HttpStatusCode.BadRequest,response.StatusCode);
        Assert.Equal(valid?1:0,(await Db.Repository().ListAsync(patient)).Count);
        await using var c=await Db.Factory().OpenConnectionAsync();
        await using var command=new SqlCommand("SELECT dbo.PrescriptionFacilityNameValid(@value),dbo.PrescriptionFacilityNameNormalize(@value)",c);
        command.Parameters.Add("@value",System.Data.SqlDbType.NVarChar,-1).Value="\u00a0 "+name+" \u3000";
        await using var reader=await command.ExecuteReaderAsync();Assert.True(await reader.ReadAsync());Assert.Equal(valid,reader.GetBoolean(0));Assert.Equal(name,reader.GetString(1));
    }

    [Theory] [InlineData("\r\n")] [InlineData("\t")] [InlineData("\u0085")] [InlineData("\u2029")]
    public async Task InvalidApiAndProcedureInputLeavesNoClinicalMutation(string bad)
    {
        var patient=await Db.Stores.SeedPatient();await using var host=await Host();using var client=host.Client();
        var request=PrescriptionDatePrecisionTests.Draft();request.TargetDispensingFacilityName=bad;
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(Route(patient),request)).StatusCode);
        await using var c=await Db.Factory().OpenConnectionAsync();
        await using var raw=new SqlCommand("EXEC dbo.PatientPrescription_CreateDraft @PatientUid=@p,@ProductName=N'TEST ONLY',@ProductDisplayText=N'TEST ONLY',@Route=N'Oral',@FrequencyCode=N'ONCE',@FrequencyDisplay=N'Once',@Prn=0,@Directions=N'TEST ONLY',@Quantity=1,@QuantityUnit=N'tablet',@AuthorizedRepeats=0,@PrescribedDate='2026-10-26',@ActorUserId=7,@TargetDispensingFacilityNameSpecified=1,@TargetDispensingFacilityName=@name",c);
        raw.Parameters.AddWithValue("@p",patient);raw.Parameters.Add("@name",System.Data.SqlDbType.NVarChar,-1).Value=bad;
        Assert.Equal(53020,(await Assert.ThrowsAsync<SqlException>(()=>raw.ExecuteNonQueryAsync())).Number);Assert.Empty(await Db.Repository().ListAsync(patient));
    }

    [Theory] [InlineData("")] [InlineData(" \u00a0\u3000")]
    public async Task EmptyAndBoundaryOnlyClearWithPreservedOtherFacts(string clear)
    {
        var patient=await Db.Stores.SeedPatient();var request=PrescriptionDatePrecisionTests.Draft();request.TargetDispensingFacilityName="Original";
        var row=await Db.Repository().CreateAsync(patient,request,7);var edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=row.RowVersion;edit.TargetDispensingFacilityName=clear;
        var saved=(await Db.Repository().UpdateAsync(patient,row.PrescriptionUid,edit,7))!;Assert.Null(saved.TargetDispensingFacilityName);Assert.Equal(row.ProductDisplayText,saved.ProductDisplayText);
        await using var c=await Db.Factory().OpenConnectionAsync();await using var raw=new SqlCommand("UPDATE dbo.PatientPrescription SET TargetDispensingFacilityName=N' ' WHERE PrescriptionUid=@id",c);raw.Parameters.AddWithValue("@id",row.PrescriptionUid);
        Assert.Equal(547,(await Assert.ThrowsAsync<SqlException>(()=>raw.ExecuteNonQueryAsync())).Number);
        Assert.Equal(saved.RowVersion,(await Db.Repository().GetAsync(patient,row.PrescriptionUid))!.RowVersion);
    }

    [Fact]
    public async Task WideProcedureParameterNormalizesBeforeLengthCheckAndNeverTruncates()
    {
        var patient=await Db.Stores.SeedPatient();var exact=string.Concat(Enumerable.Repeat("🏥",100));
        await using var c=await Db.Factory().OpenConnectionAsync();
        await using var raw=new SqlCommand("EXEC dbo.PatientPrescription_CreateDraft @PatientUid=@p,@ProductName=N'TEST ONLY',@ProductDisplayText=N'TEST ONLY',@Route=N'Oral',@FrequencyCode=N'ONCE',@FrequencyDisplay=N'Once',@Prn=0,@Directions=N'TEST ONLY',@Quantity=1,@QuantityUnit=N'tablet',@AuthorizedRepeats=0,@PrescribedDate='2026-10-26',@ActorUserId=7,@TargetDispensingFacilityNameSpecified=1,@TargetDispensingFacilityName=@name",c);
        raw.Parameters.AddWithValue("@p",patient);var parameter=raw.Parameters.Add("@name",System.Data.SqlDbType.NVarChar,-1);parameter.Value="\u3000 "+exact+" \u00a0";
        await raw.ExecuteNonQueryAsync();Assert.Equal(exact,Assert.Single(await Db.Repository().ListAsync(patient)).TargetDispensingFacilityName);
        parameter.Value=exact+"x";Assert.Equal(53020,(await Assert.ThrowsAsync<SqlException>(()=>raw.ExecuteNonQueryAsync())).Number);
        Assert.Equal(exact,Assert.Single(await Db.Repository().ListAsync(patient)).TargetDispensingFacilityName);
    }

    [Fact]
    public async Task PermissionOwnershipTenantConcurrencyAndAuditFailureProtectName()
    {
        var patient=await Db.Stores.SeedPatient();var repo=Db.Repository();var request=PrescriptionDatePrecisionTests.Draft();request.TargetDispensingFacilityName="Original";var row=await repo.CreateAsync(patient,request,7);
        var edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=row.RowVersion;edit.TargetDispensingFacilityName="Replacement";
        await using var host=await Host();using var denied=host.Client("reader");Assert.Equal(HttpStatusCode.Forbidden,(await denied.PutAsJsonAsync(Route(patient,row.PrescriptionUid),edit)).StatusCode);
        await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(()=>repo.UpdateAsync(patient,row.PrescriptionUid,edit,8));
        Assert.Null(await repo.UpdateAsync(await Db.Stores.SeedPatient(),row.PrescriptionUid,edit,7));Assert.Null(await Db.Repository(true).UpdateAsync(patient,row.PrescriptionUid,edit,7));
        await using var c=await Db.Factory().OpenConnectionAsync();await PrescriptionPrivateNoteFixture.Batches(c,"CREATE TRIGGER dbo.FacilityAuditFailure ON dbo.AuditLog AFTER INSERT AS BEGIN THROW 52997,'TEST ONLY audit failure',1; END");
        try{await Assert.ThrowsAsync<SqlException>(()=>repo.UpdateAsync(patient,row.PrescriptionUid,edit,7));}finally{await PrescriptionPrivateNoteFixture.Batches(c,"DROP TRIGGER dbo.FacilityAuditFailure");}
        var unchanged=(await repo.GetAsync(patient,row.PrescriptionUid))!;Assert.Equal(row.RowVersion,unchanged.RowVersion);Assert.Equal("Original",unchanged.TargetDispensingFacilityName);
        var saved=(await repo.UpdateAsync(patient,row.PrescriptionUid,edit,7))!;await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(()=>repo.UpdateAsync(patient,row.PrescriptionUid,edit,7));
        Assert.NotEqual(row.RowVersion,saved.RowVersion);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task LinkedCorrectionInheritsNameRequiresReviewAndPreservesArtifacts(bool signed)
    {
        var patient=await Db.Stores.SeedPatient();var repo=Db.Repository();var request=PrescriptionDatePrecisionTests.Draft();request.TargetDispensingFacilityName="薬局 Original";var row=await repo.CreateAsync(patient,request,7);
        var final=(await repo.FinalizeAsync(patient,row.PrescriptionUid,row.RowVersion,7))!;var originalArtifact=await repo.GetArtifactAsync(patient,row.PrescriptionUid);
        var edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=final.RowVersion;edit.TargetDispensingFacilityName="Forbidden rename";await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(()=>repo.UpdateAsync(patient,row.PrescriptionUid,edit,7));
        await using var host=await Host();using var client=host.Client();var response=await client.PostAsJsonAsync(Route(patient,row.PrescriptionUid)+"/correction",new{final.RowVersion});Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var correction=(await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;Assert.Equal(row.TargetDispensingFacilityName,correction.TargetDispensingFacilityName);Assert.True(correction.FacilityNameReviewRequired);Assert.False(correction.FacilityNameReviewed);
        if(signed)await Assert.ThrowsAsync<FacilityNameReviewException>(()=>repo.SignAndFinalizeAsync(patient,correction.PrescriptionUid,correction.RowVersion,7));
        else await Assert.ThrowsAsync<FacilityNameReviewException>(()=>repo.FinalizeAsync(patient,correction.PrescriptionUid,correction.RowVersion,7));
        edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=correction.RowVersion;edit.FacilityNameReviewed=true;
        var reviewed=(await repo.UpdateAsync(patient,correction.PrescriptionUid,edit,7))!;Assert.True(reviewed.FacilityNameReviewed);
        edit.RowVersion=reviewed.RowVersion;edit.FacilityNameReviewed=false;edit.TargetDispensingFacilityName=null;
        var cleared=(await repo.UpdateAsync(patient,correction.PrescriptionUid,edit,7))!;Assert.False(cleared.FacilityNameReviewed);Assert.True(cleared.FacilityNameReviewRequired);
        await Assert.ThrowsAsync<FacilityNameReviewException>(()=>repo.FinalizeAsync(patient,cleared.PrescriptionUid,cleared.RowVersion,7));
        edit.RowVersion=cleared.RowVersion;edit.FacilityNameReviewed=true;edit.TargetDispensingFacilityName="Clinique Nouvelle";
        reviewed=(await repo.UpdateAsync(patient,cleared.PrescriptionUid,edit,7))!;
        var corrected=signed?await repo.SignAndFinalizeAsync(patient,reviewed.PrescriptionUid,reviewed.RowVersion,7):await repo.FinalizeAsync(patient,reviewed.PrescriptionUid,reviewed.RowVersion,7);
        Assert.Equal("Finalized",corrected!.Status);Assert.Equal("Clinique Nouvelle",corrected.TargetDispensingFacilityName);
        Assert.Equal(originalArtifact,await repo.GetArtifactAsync(patient,row.PrescriptionUid));Assert.Equal(row.TargetDispensingFacilityName,(await repo.GetAsync(patient,row.PrescriptionUid))!.TargetDispensingFacilityName);
        using var frozen=JsonDocument.Parse((await repo.GetArtifactAsync(patient,corrected.PrescriptionUid))!.Json);Assert.Equal("Clinique Nouvelle",frozen.RootElement.GetProperty("TargetDispensingFacilityName").GetString());Assert.True(frozen.RootElement.GetProperty("FacilityNameReviewed").GetBoolean());
        Assert.Null((await repo.GetAsync(Db.Stores.PriorSignedPatient,Db.Stores.PriorSignedPrescription))!.TargetDispensingFacilityName);Assert.Equal(Db.Stores.PriorSignedJson,(await repo.GetArtifactAsync(Db.Stores.PriorSignedPatient,Db.Stores.PriorSignedPrescription))!.Json);
        var renderer=new PrescriptionDatePrecisionTests.Capture();var service=new PatientPrescriptionService(repo,renderer);
        var output=new PrescriptionOutputService(repo,repo,service,renderer,TimeProvider.System);
        var bytes=(await output.GenerateAsync(patient,corrected.PrescriptionUid,new("test-subject-7","TEST ONLY","facility-test")))!;
        Assert.DoesNotContain("Clinique Nouvelle",System.Text.Encoding.UTF8.GetString(bytes));
    }
}

public sealed class TargetFacilityNameDatabase:IAsyncLifetime
{
    internal EarliestPickupDatabase Pickup {get;}=new();
    public async Task InitializeAsync()
    {
        await Pickup.InitializeAsync();
        try{foreach(var other in new[]{false,true}){await using var c=await Pickup.Base.Db.Factory(other).OpenConnectionAsync();await PrescriptionPrivateNoteFixture.Batches(c,PrescriptionPrivateNoteFixture.Sql("tenant-clinical/migrations/0094-prescription-target-facility-name.sql"));}}
        catch{await Pickup.DisposeAsync();throw;}
    }
    public Task DisposeAsync()=>Pickup.DisposeAsync();
}
