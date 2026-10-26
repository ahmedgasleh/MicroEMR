using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Application.Tenancy;
using MicroEMR.Core.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class EarliestPickupContractTests
{
    internal static PrescriptionPickupPolicy Policy(ITenantContext context, string zone="America/Toronto") =>
        new(context,new Catalog(context,zone),NullLogger<PrescriptionPickupPolicy>.Instance);
    private sealed class Catalog(ITenantContext context,string zone):ITenantCatalog
    {
        public Task<Tenant?> GetByUidAsync(Guid uid,CancellationToken token=default)=>Task.FromResult<Tenant?>(
            uid==context.TenantUid?new Tenant(uid,context.TenantKey,"TEST ONLY",TenantStatus.Active,zone,DateTimeOffset.UtcNow):null);
        public Task<Tenant?> GetByKeyAsync(string key,CancellationToken token=default)=>GetByUidAsync(context.TenantUid,token);
    }

    [Theory]
    [InlineData("2026-03-08T02:30",true)]
    [InlineData("2026-11-01T01:30",false)]
    public void DstGapAndAmbiguity(string local,bool gap)
    {
        var zone=TimeZoneInfo.FindSystemTimeZoneById("America/Toronto");
        if(gap)Assert.Throws<PickupValidationException>(()=>PrescriptionPickupPolicy.Offsets(zone,local));
        else Assert.Equal(new[]{"-04:00","-05:00"},PrescriptionPickupPolicy.Offsets(zone,local));
    }

    [Theory]
    [InlineData("UTC","2026-01-01T00:00","+00:00")]
    [InlineData("Asia/Kathmandu","2026-01-01T00:00","+05:45")]
    public void OffsetsUseClinicalZoneNotHost(string zone,string local,string expected)=>
        Assert.Equal(new[]{expected},PrescriptionPickupPolicy.Offsets(TimeZoneInfo.FindSystemTimeZoneById(zone),local));

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Precision\":\"Day\",\"Value\":\"2026-02-30\",\"TimeZoneId\":\"UTC\"}")]
    [InlineData("{\"Precision\":\"Minute\",\"Value\":\"2026-11-01T01:30\",\"TimeZoneId\":\"UTC\"}")]
    [InlineData("{\"Precision\":\"Minute\",\"Value\":\"2026-11-01T01:30:00-04:00\",\"TimeZoneId\":\"UTC\"}")]
    [InlineData("{\"Precision\":\"Day\",\"Value\":\"2026-11-01\",\"TimeZoneId\":\"UTC\",\"Value\":\"2026-11-02\"}")]
    public void InvalidOrDuplicatePickupCannotBecomeUnknown(string json)=>Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<EarliestPickup>(json));

    [Fact]
    public void PresenceSurvivesBothJsonConventions()
    {
        foreach(var options in new[]{new JsonSerializerOptions(),new JsonSerializerOptions(JsonSerializerDefaults.Web)})
        {
            var absent=JsonSerializer.Deserialize<PrescriptionDraftRequest>("{}",options)!;
            var clear=JsonSerializer.Deserialize<PrescriptionDraftRequest>("{\"earliestPickup\":null}",options)!;
            Assert.False(absent.EarliestPickupSpecified);Assert.True(clear.EarliestPickupSpecified);
            Assert.DoesNotContain("earliestPickup",JsonSerializer.Serialize(absent,options));
            Assert.Contains("\"earliestPickup\":null",JsonSerializer.Serialize(clear,options));
        }
    }

    [Theory]
    [InlineData("preserve",false)] [InlineData("clear",true)] [InlineData("set",true)]
    public async Task WebFormBindingRetainsIndependentPickupPresence(string operation,bool specified)
    {
        var api=CppSelectivePrintTests.Stub<MicroEMR.Web.Services.PatientPrescriptions.IPatientPrescriptionApiClient>((_,_)=>throw new InvalidOperationException("No write in validation-only controller check"));
        var http=new Microsoft.AspNetCore.Http.DefaultHttpContext();
        http.Request.ContentType="application/x-www-form-urlencoded";http.Request.Headers["X-Requested-With"]="XMLHttpRequest";
        http.Request.Form=new Microsoft.AspNetCore.Http.FormCollection(new Dictionary<string,Microsoft.Extensions.Primitives.StringValues>{
            ["pickupOperation"]=operation,["pickupPrecision"]="Day",["pickupDate"]="2026-11-03",["pickupZone"]="America/Toronto"});
        var controller=new MicroEMR.Web.Controllers.PatientPrescriptionsController(api){ControllerContext=new(){HttpContext=http}};
        controller.ModelState.AddModelError("fixture","Stop before persistence.");
        var model=new PrescriptionDraftRequest();
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(await controller.Edit(Guid.NewGuid(),Guid.NewGuid(),model,default));
        Assert.Equal(specified,model.EarliestPickupSpecified);
        if(operation=="set")Assert.Equal("2026-11-03",model.EarliestPickup!.Value);else Assert.Null(model.EarliestPickup);
    }
}

public sealed class EarliestPickupSqlTests(EarliestPickupDatabase fixture):IClassFixture<EarliestPickupDatabase>
{
    private PrescriptionDateDatabase Db=>fixture.Base.Db;
    private static EarliestPickup Known(bool timed)=>new(timed?"Minute":"Day",timed?"2026-11-01T01:30-04:00":"2026-11-03","America/Toronto");
    private Task<PrescriptionDateHost> Host(bool other=false)=>PrescriptionDateHost.Start(Db,other,services=>
        services.AddSingleton(EarliestPickupContractTests.Policy(Db.Tenant(other))));

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task SaveReadOmitClearAuditAndTenantIsolation(bool timed)
    {
        var patient=await Db.Stores.SeedPatient();
        await using var host=await Host();using var client=host.Client();
        var request=PrescriptionDatePrecisionTests.Draft();request.EarliestPickup=Known(timed);
        var response=await client.PostAsJsonAsync($"api/patients/{patient}/prescriptions",request);
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var row=(await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;
        Assert.Equal(request.EarliestPickup,row.EarliestPickup);
        Assert.Equal(request.EarliestPickup,(await Db.Repository().GetAsync(patient,row.PrescriptionUid))!.EarliestPickup);
        var edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=row.RowVersion;
        var saved=(await Db.Repository().UpdateAsync(patient,row.PrescriptionUid,edit,7))!;
        Assert.Equal(row.EarliestPickup,saved.EarliestPickup);Assert.NotEqual(row.RowVersion,saved.RowVersion);
        edit.RowVersion=row.RowVersion;edit.EarliestPickup=null;
        await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(()=>Db.Repository().UpdateAsync(patient,row.PrescriptionUid,edit,7));
        edit.RowVersion=saved.RowVersion;
        await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(()=>Db.Repository().UpdateAsync(patient,row.PrescriptionUid,edit,8));
        Assert.Null(await Db.Repository().UpdateAsync(await Db.Stores.SeedPatient(),row.PrescriptionUid,edit,7));
        Assert.Null(await Db.Repository(true).GetAsync(patient,row.PrescriptionUid));
        Assert.Null(await Db.Repository(true).UpdateAsync(patient,row.PrescriptionUid,edit,7));
        var cleared=(await Db.Repository().UpdateAsync(patient,row.PrescriptionUid,edit,7))!;
        Assert.Null(cleared.EarliestPickup);
        await using var c=await Db.Factory().OpenConnectionAsync();
        await using var audit=new SqlCommand("SELECT TOP(1) UserId,OldValue,NewValue FROM dbo.AuditLog WHERE EntityId=@id AND ActionName=N'PrescriptionDraftUpdated' ORDER BY AuditLogId DESC",c);
        audit.Parameters.AddWithValue("@id",row.PrescriptionUid.ToString());
        await using var reader=await audit.ExecuteReaderAsync();Assert.True(await reader.ReadAsync());Assert.Equal(7,Convert.ToInt32(reader[0]));
        Assert.Contains("EarliestPickup",reader.GetString(1));Assert.Contains("\"EarliestPickup\":null",reader.GetString(2));
    }

    [Fact]
    public async Task ApiRejectsGapWrongZoneMissingOffsetAndUnauthorizedWrite()
    {
        var patient=await Db.Stores.SeedPatient();await using var host=await Host();using var client=host.Client();
        foreach(var pickup in new[]{new EarliestPickup("Minute","2026-03-08T02:30-05:00","America/Toronto"),new("Minute","2026-11-01T01:30-06:00","America/Toronto"),new("Day","2026-11-03","UTC")})
        {
            var request=PrescriptionDatePrecisionTests.Draft();request.EarliestPickup=pickup;
            Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync($"api/patients/{patient}/prescriptions",request)).StatusCode);
        }
        var json="{\"earliestPickup\":{\"Precision\":\"Minute\",\"Value\":\"2026-11-01T01:30\",\"TimeZoneId\":\"America/Toronto\"}}";
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync($"api/patients/{patient}/prescriptions",new StringContent(json,System.Text.Encoding.UTF8,"application/json"))).StatusCode);
        using var denied=host.Client("reader");var valid=PrescriptionDatePrecisionTests.Draft();valid.EarliestPickup=Known(false);
        Assert.Equal(HttpStatusCode.Forbidden,(await denied.PostAsJsonAsync($"api/patients/{patient}/prescriptions",valid)).StatusCode);
        Assert.Empty(await Db.Repository().ListAsync(patient));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task CorrectionCarriesExactValueAndRequiresReviewAtBothFinalizationBoundaries(bool signed)
    {
        var patient=await Db.Stores.SeedPatient();var repo=Db.Repository();
        var request=PrescriptionDatePrecisionTests.Draft();request.EarliestPickup=Known(true);
        var row=await repo.CreateAsync(patient,request,7);
        var original=(await repo.FinalizeAsync(patient,row.PrescriptionUid,row.RowVersion,7))!;
        var artifact=(await repo.GetArtifactAsync(patient,row.PrescriptionUid))!;
        var edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=original.RowVersion;edit.EarliestPickup=null;
        await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(()=>repo.UpdateAsync(patient,row.PrescriptionUid,edit,7));
        await using var host=await Host();using var client=host.Client();
        var response=await client.PostAsJsonAsync($"api/patients/{patient}/prescriptions/{row.PrescriptionUid}/correction",new{original.RowVersion});
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var correction=(await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;
        Assert.Equal(row.EarliestPickup,correction.EarliestPickup);Assert.True(correction.PickupReviewRequired);
        if(signed)await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(()=>repo.SignAndFinalizeAsync(patient,correction.PrescriptionUid,correction.RowVersion,7));
        else await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(()=>repo.FinalizeAsync(patient,correction.PrescriptionUid,correction.RowVersion,7));
        var final=signed?await repo.SignAndFinalizeReviewedAsync(patient,correction.PrescriptionUid,correction.RowVersion,7,true):await repo.FinalizeReviewedAsync(patient,correction.PrescriptionUid,correction.RowVersion,7,true);
        Assert.Equal("Finalized",final!.Status);Assert.Equal(row.EarliestPickup,final.EarliestPickup);
        Assert.Equal(artifact,await repo.GetArtifactAsync(patient,row.PrescriptionUid));
        using var frozen=JsonDocument.Parse((await repo.GetArtifactAsync(patient,final.PrescriptionUid))!.Json);
        Assert.True(frozen.RootElement.GetProperty("EarliestPickupReviewed").GetBoolean());
        Assert.Equal(row.EarliestPickup,frozen.RootElement.GetProperty("EarliestPickup").Deserialize<EarliestPickup>());
        Assert.Equal(Db.Stores.PriorSignedJson,(await repo.GetArtifactAsync(Db.Stores.PriorSignedPatient,Db.Stores.PriorSignedPrescription))!.Json);
    }

    [Fact]
    public async Task AuditFailureRollsBackPickupAndRowVersionAndSqlRejectsMalformedFact()
    {
        var patient=await Db.Stores.SeedPatient();var repo=Db.Repository();var request=PrescriptionDatePrecisionTests.Draft();request.EarliestPickup=Known(false);
        var row=await repo.CreateAsync(patient,request,7);
        await using var c=await Db.Factory().OpenConnectionAsync();
        await PrescriptionPrivateNoteFixture.Batches(c,"CREATE TRIGGER dbo.PickupAuditFailure ON dbo.AuditLog AFTER INSERT AS BEGIN THROW 52997,'TEST ONLY audit failure',1; END");
        var edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=row.RowVersion;edit.EarliestPickup=null;
        try{await Assert.ThrowsAsync<SqlException>(()=>repo.UpdateAsync(patient,row.PrescriptionUid,edit,7));}
        finally{await PrescriptionPrivateNoteFixture.Batches(c,"DROP TRIGGER dbo.PickupAuditFailure");}
        var unchanged=(await repo.GetAsync(patient,row.PrescriptionUid))!;Assert.Equal(row.RowVersion,unchanged.RowVersion);Assert.Equal(row.EarliestPickup,unchanged.EarliestPickup);
        await using var invalid=new SqlCommand("UPDATE dbo.PatientPrescription SET EarliestPickup=N'{}' WHERE PrescriptionUid=@uid",c);invalid.Parameters.AddWithValue("@uid",row.PrescriptionUid);
        Assert.Equal(547,(await Assert.ThrowsAsync<SqlException>(()=>invalid.ExecuteNonQueryAsync())).Number);
    }

    [Fact]
    public async Task ChangedTenantZoneDoesNotReinterpretInheritedPickupAndClearStillRequiresReview()
    {
        var patient=await Db.Stores.SeedPatient();var repo=Db.Repository();var request=PrescriptionDatePrecisionTests.Draft();request.EarliestPickup=Known(true);
        var row=await repo.CreateAsync(patient,request,7);var original=(await repo.FinalizeAsync(patient,row.PrescriptionUid,row.RowVersion,7))!;
        var artifact=await repo.GetArtifactAsync(patient,row.PrescriptionUid);
        await using var host=await PrescriptionDateHost.Start(Db,false,services=>services.AddSingleton(EarliestPickupContractTests.Policy(Db.Tenant(false),"UTC")));
        using var client=host.Client();
        var response=await client.PostAsJsonAsync($"api/patients/{patient}/prescriptions/{row.PrescriptionUid}/correction",new{original.RowVersion});
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var correction=(await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;Assert.Equal(row.EarliestPickup,correction.EarliestPickup);
        var edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=correction.RowVersion;
        var unchanged=(await repo.UpdateAsync(patient,correction.PrescriptionUid,edit,7))!;Assert.Equal(row.EarliestPickup,unchanged.EarliestPickup);
        edit.RowVersion=unchanged.RowVersion;edit.EarliestPickup=null;
        var cleared=(await repo.UpdateAsync(patient,correction.PrescriptionUid,edit,7))!;Assert.True(cleared.PickupReviewRequired);
        var route=$"api/patients/{patient}/prescriptions/{cleared.PrescriptionUid}/sign-and-finalize";
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(route,new{cleared.RowVersion,ConfirmSignature=true})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync(route,new{cleared.RowVersion,ConfirmSignature=true,PickupReviewed=true})).StatusCode);
        Assert.Equal(artifact,await repo.GetArtifactAsync(patient,row.PrescriptionUid));
    }
}

public sealed class EarliestPickupDatabase:IAsyncLifetime
{
    internal Step143OrderDatabase Base {get;}=new();
    public async Task InitializeAsync()
    {
        await Base.InitializeAsync();
        try{foreach(var other in new[]{false,true}){await using var c=await Base.Db.Factory(other).OpenConnectionAsync();await PrescriptionPrivateNoteFixture.Batches(c,PrescriptionPrivateNoteFixture.Sql("tenant-clinical/migrations/0093-prescription-earliest-pickup.sql"));}}
        catch{await Base.DisposeAsync();throw;}
    }
    public Task DisposeAsync()=>Base.DisposeAsync();
}
