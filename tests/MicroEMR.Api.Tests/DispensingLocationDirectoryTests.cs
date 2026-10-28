using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Infrastructure.PatientPrescriptions;
using Xunit;

namespace MicroEMR.Api.Tests;

// Reuse Stage 1 fixtures; apply the additive migration only to their disposable tenant databases.
public sealed class DispensingDirectoryDatabase:IAsyncLifetime
{
    internal DispensingLocationReferenceDatabase Reference {get;}=new();
    internal PrescriptionDateDatabase Db=>Reference.Db;
    internal string BeforeCollation {get;private set;}="";
    internal DispensingLocationDirectory Directory(bool other=false)=>new(new DispensingLocationRepository(Db.Factory(other)),NullLogger<DispensingLocationDirectory>.Instance);
    public async Task InitializeAsync()
    {
        await Reference.InitializeAsync();
        try{foreach(var other in new[]{false,true}){await using var c=await Db.Factory(other).OpenConnectionAsync();await using var q=new SqlCommand("SELECT CONVERT(nvarchar(128),DATABASEPROPERTYEX(DB_NAME(),'Collation'))",c);BeforeCollation=(string)(await q.ExecuteScalarAsync())!;await PrescriptionPrivateNoteFixture.Batches(c,PrescriptionPrivateNoteFixture.Sql("tenant-clinical/migrations/0099-dispensing-location-directory.sql"));}}
        catch{await Reference.DisposeAsync();throw;}
    }
    public Task DisposeAsync()=>Reference.DisposeAsync();
    internal Task<PrescriptionDateHost> Host(bool other=false,bool admin=true)=>PrescriptionDateHost.Start(Db,other,services=>{
        services.AddScoped<IDispensingLocationRepository>(_=>new DispensingLocationRepository(Db.Factory(other)));services.AddScoped<DispensingLocationDirectory>();
        services.AddScoped<ICurrentUserPermissionService>(sp=>new Permissions(sp.GetRequiredService<IHttpContextAccessor>(),admin));
    });
    private sealed class Permissions(IHttpContextAccessor context,bool admin):ICurrentUserPermissionService
    {
        public Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(CancellationToken token=default)
        {
            var keys=new HashSet<string>{PermissionKeys.PatientsView};
            if(context.HttpContext!.User.FindFirst("write")?.Value=="True"){keys.Add(PermissionKeys.PrescriptionsPrescribe);if(admin)keys.Add(PermissionKeys.ClinicSettingsManage);}
            return Task.FromResult<IReadOnlySet<string>>(keys);
        }
        public async Task<bool> HasPermissionAsync(string key,CancellationToken token=default)=>(await GetEffectivePermissionsAsync(token)).Contains(key);
    }
}

public sealed class DispensingDirectoryTests(DispensingDirectoryDatabase fixture):IClassFixture<DispensingDirectoryDatabase>
{
    private PrescriptionDateDatabase Db=>fixture.Db;
    internal static SaveDispensingLocation Request(string? system=null,string identifier="Ab-01")=>new(){System=system??"urn:TEST:"+Guid.NewGuid(),Identifier=identifier,FacilityName="École <location> 薬局",FacilityAddress="123 TEST ONLY Street\nToronto",Phone="+1 555 0100"};
    private static PrescriptionDispensingLocationReference Pair(DispensingLocation x)=>new(x.System,x.Identifier);
    private static void Select(PrescriptionDraftRequest r,DispensingLocation x){r.DispensingLocationReference=Pair(x);r.DispensingLocationSelection=new(x.LocationUid,x.RowVersion);}
    private static string Route(Guid p,Guid? u=null)=>$"api/patients/{p}/prescriptions"+(u.HasValue?"/"+u:"");

    [Theory]
    [InlineData("urn:TEST","urn:test","Ab-01","Ab-01")]
    [InlineData("urn:TEST","urn:TEST","Ab-01","ab-01")]
    [InlineData("urn:TEST","urn:TEST","é","e\u0301")]
    [InlineData("urn:TEST","urn:TEST","薬局🏥","薬局🏦")]
    public async Task ExactIdentityAndBoundaryLookupAreUnambiguous(string firstSystem,string secondSystem,string firstId,string secondId)
    {
        var prefix=Guid.NewGuid()+":";var service=fixture.Directory();
        var a=await service.SaveAsync(null,"Create",Request(prefix+firstSystem,firstId),7);
        var b=await service.SaveAsync(null,"Create",Request(prefix+secondSystem,secondId),7);
        Assert.NotEqual(a.LocationUid,b.LocationUid);
        Assert.Equal(a.LocationUid,(await service.ResolveAsync("\u3000 "+a.System+" \u00a0"," "+a.Identifier+"\u3000"))!.LocationUid);
        Assert.Equal(b.LocationUid,(await service.ResolveAsync(b.System,b.Identifier))!.LocationUid);
        await Assert.ThrowsAsync<DispensingLocationConflictException>(()=>service.SaveAsync(null,"Create",Request(a.System,a.Identifier),7));
        await using var c=await Db.Factory().OpenConnectionAsync();await using var q=new SqlCommand("SELECT CONVERT(nvarchar(128),DATABASEPROPERTYEX(DB_NAME(),'Collation')); SELECT collation_name FROM sys.columns WHERE object_id=OBJECT_ID('dbo.DispensingLocation') AND name IN('System','Identifier'); SELECT collation_name FROM sys.columns WHERE object_id=OBJECT_ID('dbo.PatientPrescription') AND name IN('DispensingLocationSystem','DispensingLocationIdentifier');",c);
        await using var r=await q.ExecuteReaderAsync();Assert.True(await r.ReadAsync());Assert.Equal(fixture.BeforeCollation,r.GetString(0));Assert.True(await r.NextResultAsync());var columns=0;while(await r.ReadAsync()){Assert.Equal("Latin1_General_100_BIN2",r.GetString(0));columns++;}Assert.Equal(2,columns);Assert.True(await r.NextResultAsync());while(await r.ReadAsync())Assert.Equal(fixture.BeforeCollation,r.GetString(0));
    }

    [Fact]
    public async Task LifecycleImmutableIdentityConcurrencyAndCompleteAudit()
    {
        var service=fixture.Directory();var request=Request();var row=await service.SaveAsync(null,"Create",request,7);
        request.RowVersion=row.RowVersion;request.FacilityName="Changed details";var edit=await service.SaveAsync(row.LocationUid,"Edit",request,7);
        await Assert.ThrowsAsync<DispensingLocationConflictException>(()=>service.SaveAsync(row.LocationUid,"Edit",request,7));
        request.RowVersion=edit.RowVersion;request.Identifier="Wrong";await Assert.ThrowsAsync<DispensingLocationConflictException>(()=>service.SaveAsync(row.LocationUid,"Edit",request,7));request.Identifier=row.Identifier;
        var inactive=await service.SaveAsync(row.LocationUid,"Deactivate",new(){RowVersion=edit.RowVersion},7);
        Assert.False(inactive.IsActive);Assert.Equal(row.Identifier,inactive.Identifier);
        await Assert.ThrowsAsync<DispensingLocationConflictException>(()=>service.SaveAsync(null,"Create",Request(row.System,row.Identifier),7));
        var active=await service.SaveAsync(row.LocationUid,"Reactivate",new(){RowVersion=inactive.RowVersion},7);Assert.True(active.IsActive);
        await using var c=await Db.Factory().OpenConnectionAsync();await using var q=new SqlCommand("SELECT UserId,OldValue,NewValue FROM dbo.AuditLog WHERE EntityName=N'DispensingLocation' AND EntityId=@id ORDER BY AuditLogId",c);q.Parameters.AddWithValue("@id",row.LocationUid.ToString());
        await using(var reader=await q.ExecuteReaderAsync()){var count=0;while(await reader.ReadAsync()){Assert.Equal(7L,reader.GetInt64(0));using var after=JsonDocument.Parse(reader.GetString(2));Assert.Equal(Db.Tenant(false).TenantUid,after.RootElement.GetProperty("TenantUid").GetGuid());Assert.Equal(row.LocationUid,after.RootElement.GetProperty("LocationUid").GetGuid());Assert.Equal(row.Identifier,after.RootElement.GetProperty("Identifier").GetString());Assert.Equal(count!=2,after.RootElement.GetProperty("IsActive").GetBoolean());if(count==0)Assert.True(reader.IsDBNull(1));else{using var before=JsonDocument.Parse(reader.GetString(1));Assert.NotEqual(before.RootElement.GetProperty("RowVersion").GetString(),after.RootElement.GetProperty("RowVersion").GetString());}count++;}Assert.Equal(4,count);}
        await using var immutable=new SqlCommand("UPDATE dbo.DispensingLocation SET Identifier=N'changed' WHERE LocationUid=@id",c);immutable.Parameters.AddWithValue("@id",row.LocationUid);Assert.Equal(53101,(await Assert.ThrowsAsync<SqlException>(()=>immutable.ExecuteNonQueryAsync())).Number);
        Assert.Equal(active.RowVersion,(await service.GetAsync(row.LocationUid))!.RowVersion);
    }

    [Theory] [InlineData("System")] [InlineData("Identifier")] [InlineData("Name")] [InlineData("Address")] [InlineData("Phone")]
    public async Task DirectoryValidationRejectsWithoutMutation(string field)
    {
        var request=Request();if(field=="System")request.System="bad\n";if(field=="Identifier")request.Identifier=new string('i',201);if(field=="Name")request.FacilityName=" ";if(field=="Address")request.FacilityAddress=new string('a',1001);if(field=="Phone")request.Phone=new string('p',31);
        await Assert.ThrowsAnyAsync<ArgumentException>(()=>fixture.Directory().SaveAsync(null,"Create",request,7));
        await using var host=await fixture.Host();using var client=host.Client();Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("api/dispensing-locations",request)).StatusCode);
    }

    [Fact]
    public async Task ActiveSearchBoundedPagingLiteralQueriesTenantAndPermissionGuards()
    {
        var service=fixture.Directory();var marker="Find"+Guid.NewGuid();var a=Request();a.FacilityName=marker;var first=await service.SaveAsync(null,"Create",a,7);var b=Request();b.FacilityName=marker;var second=await service.SaveAsync(null,"Create",b,7);
        var page=await service.SearchAsync(marker,false,1,1);Assert.Single(page.Items);Assert.True(page.HasMore);Assert.Single((await service.SearchAsync(marker,false,2,1)).Items);
        await service.SaveAsync(second.LocationUid,"Deactivate",new(){RowVersion=second.RowVersion},7);
        Assert.Single((await service.SearchAsync(marker,false)).Items);Assert.Equal(2,(await service.SearchAsync(marker,true)).Items.Count);
        Assert.Empty((await service.SearchAsync("%",true)).Items);Assert.Single((await service.SearchAsync(first.Identifier,false)).Items,x=>x.LocationUid==first.LocationUid);
        Assert.Null(await fixture.Directory(true).GetAsync(first.LocationUid));Assert.Null(await fixture.Directory(true).ResolveAsync(first.System,first.Identifier));
        Assert.Empty((await fixture.Directory(true).SearchAsync(marker,true)).Items);
        await Assert.ThrowsAsync<DispensingLocationConflictException>(()=>fixture.Directory(true).SaveAsync(first.LocationUid,"Deactivate",new(){RowVersion=first.RowVersion},7));
        await using var host=await fixture.Host();using var writer=host.Client();using var reader=host.Client("reader");
        Assert.Equal(HttpStatusCode.Forbidden,(await reader.GetAsync("api/dispensing-locations")).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await reader.PostAsJsonAsync("api/dispensing-locations",Request())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await writer.GetAsync("api/dispensing-locations?pageSize=51")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await writer.GetAsync("api/dispensing-locations/maintenance?includeInactive=true")).StatusCode);
        await using var prescribeHost=await fixture.Host(admin:false);using var prescriber=prescribeHost.Client();
        Assert.Equal(HttpStatusCode.Forbidden,(await prescriber.GetAsync("api/dispensing-locations/maintenance")).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await prescriber.PostAsJsonAsync("api/dispensing-locations",Request())).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await prescriber.GetAsync("api/dispensing-locations/"+first.LocationUid)).StatusCode);
    }

    [Theory] [InlineData("stale")] [InlineData("inactive")] [InlineData("identity")] [InlineData("tenant")]
    public async Task InvalidSelectionNeverMutatesPrescriptionOrAudit(string kind)
    {
        var location=await fixture.Directory(kind=="tenant").SaveAsync(null,"Create",Request(),7);
        var patient=await Db.Stores.SeedPatient();var request=PrescriptionDatePrecisionTests.Draft();request.DispensingLocationReference=new("Manual","Original");var original=await Db.Repository().CreateAsync(patient,request,7);
        var edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=original.RowVersion;Select(edit,location);
        if(kind=="stale"||kind=="inactive")await fixture.Directory().SaveAsync(location.LocationUid,kind=="inactive"?"Deactivate":"Edit",new(){System=location.System,Identifier=location.Identifier,FacilityName="New",FacilityAddress=location.FacilityAddress,RowVersion=location.RowVersion},7);
        if(kind=="identity")edit.DispensingLocationReference=Pair(location) with{Identifier="ab-01"};
        await using var host=await fixture.Host();using var client=host.Client();
        Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync(Route(patient,original.PrescriptionUid),edit)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(Route(patient),edit)).StatusCode);
        Assert.Single(await Db.Repository().ListAsync(patient));
        Assert.Equal(original.RowVersion,(await Db.Repository().GetAsync(patient,original.PrescriptionUid))!.RowVersion);
        Assert.Equal(original.DispensingLocationReference,(await Db.Repository().GetAsync(patient,original.PrescriptionUid))!.DispensingLocationReference);
        await using var c=await Db.Factory().OpenConnectionAsync();await using var q=new SqlCommand("SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityId=@id AND ActionName=N'PrescriptionDraftUpdated'",c);q.Parameters.AddWithValue("@id",original.PrescriptionUid.ToString());Assert.Equal(0,Convert.ToInt32(await q.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task SelectedSaveManualFallbackAndHistoryRemainIndependent()
    {
        var service=fixture.Directory();var location=await service.SaveAsync(null,"Create",Request(),7);var patient=await Db.Stores.SeedPatient();
        await using var host=await fixture.Host();using var client=host.Client();
        var request=PrescriptionDatePrecisionTests.Draft();Select(request,location);request.TargetDispensingFacilityName="Independent name";request.TargetDispensingFacilityAddress="Independent address";
        var response=await client.PostAsJsonAsync(Route(patient),request);Assert.Equal(HttpStatusCode.Created,response.StatusCode);var row=(await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;Assert.Equal(Pair(location),row.DispensingLocationReference);Assert.Equal(request.TargetDispensingFacilityName,row.TargetDispensingFacilityName);Assert.Equal(request.TargetDispensingFacilityAddress,row.TargetDispensingFacilityAddress);
        Assert.Equal(location.LocationUid,row.DispensingLocationDirectoryUid);
        var original=(await Db.Repository().FinalizeAsync(patient,row.PrescriptionUid,row.RowVersion,7))!;var artifact=await Db.Repository().GetArtifactAsync(patient,row.PrescriptionUid);
        var changed=Request(location.System,location.Identifier);changed.RowVersion=location.RowVersion;changed.FacilityName="Current directory details";var current=await service.SaveAsync(location.LocationUid,"Edit",changed,7);await service.SaveAsync(current.LocationUid,"Deactivate",new(){RowVersion=current.RowVersion},7);
        Assert.False((await service.ResolveAsync(location.System,location.Identifier))!.IsActive);
        Assert.Equal(artifact,await Db.Repository().GetArtifactAsync(patient,row.PrescriptionUid));var preserved=(await Db.Repository().GetAsync(patient,row.PrescriptionUid))!;Assert.Equal("Independent name",preserved.TargetDispensingFacilityName);Assert.Equal(Pair(location),preserved.DispensingLocationReference);
        response=await client.PostAsJsonAsync(Route(patient,row.PrescriptionUid)+"/correction",new{original.RowVersion});Assert.Equal(HttpStatusCode.Created,response.StatusCode);var correction=(await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;Assert.Equal(Pair(location),correction.DispensingLocationReference);Assert.True(correction.DispensingLocationReferenceReviewRequired);Assert.Null(correction.DispensingLocationDirectoryUid);
        var manual=PrescriptionDatePrecisionTests.Draft();manual.DispensingLocationReference=Pair(location);var manualResponse=await client.PostAsJsonAsync(Route(patient),manual);Assert.Equal(HttpStatusCode.Created,manualResponse.StatusCode);var manualRow=(await manualResponse.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;Assert.Null(manualRow.TargetDispensingFacilityName);Assert.Null(manualRow.TargetDispensingFacilityAddress);
        await using var c=await Db.Factory().OpenConnectionAsync();await using var audit=new SqlCommand("SELECT NewValue FROM dbo.AuditLog WHERE EntityId=@id AND ActionName=N'PrescriptionDraftCreated'",c);audit.Parameters.AddWithValue("@id",row.PrescriptionUid.ToString());using var snapshot=JsonDocument.Parse((string)(await audit.ExecuteScalarAsync())!);Assert.Equal(location.LocationUid,snapshot.RootElement.GetProperty("DispensingLocationDirectoryUid").GetGuid());
    }

    [Fact]
    public async Task DraftSelectionEditPreserveManualClearAndClinicalConcurrency()
    {
        var location=await fixture.Directory().SaveAsync(null,"Create",Request(),7);var patient=await Db.Stores.SeedPatient();var repo=Db.Repository();
        var row=await repo.CreateAsync(patient,PrescriptionDatePrecisionTests.Draft(),7);var edit=PrescriptionDatePrecisionTests.Draft();edit.RowVersion=row.RowVersion;Select(edit,location);
        await using var host=await fixture.Host();using var client=host.Client();using var denied=host.Client("reader");
        Assert.Equal(HttpStatusCode.Forbidden,(await denied.PutAsJsonAsync(Route(patient,row.PrescriptionUid),edit)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.PutAsJsonAsync(Route(await Db.Stores.SeedPatient(),row.PrescriptionUid),edit)).StatusCode);
        var response=await client.PutAsJsonAsync(Route(patient,row.PrescriptionUid),edit);Assert.Equal(HttpStatusCode.OK,response.StatusCode);var saved=(await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;Assert.Equal(Pair(location),saved.DispensingLocationReference);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync(Route(patient,row.PrescriptionUid),edit)).StatusCode);
        var omitted=PrescriptionDatePrecisionTests.Draft();omitted.RowVersion=saved.RowVersion;saved=(await repo.UpdateAsync(patient,row.PrescriptionUid,omitted,7))!;Assert.Equal(Pair(location),saved.DispensingLocationReference);
        await using var c=await Db.Factory().OpenConnectionAsync();await using var provenance=new SqlCommand("SELECT DispensingLocationDirectoryUid FROM dbo.PatientPrescription WHERE PrescriptionUid=@id",c);provenance.Parameters.AddWithValue("@id",row.PrescriptionUid);Assert.Equal(location.LocationUid,(Guid)(await provenance.ExecuteScalarAsync())!);
        omitted.RowVersion=saved.RowVersion;omitted.DispensingLocationReference=null;var clear=(await repo.UpdateAsync(patient,row.PrescriptionUid,omitted,7))!;Assert.Null(clear.DispensingLocationReference);Assert.IsType<DBNull>(await provenance.ExecuteScalarAsync());
    }

    [Fact]
    public async Task IncompleteSelectionRejectsBeforeAnyClinicalWrite()
    {
        var patient=await Db.Stores.SeedPatient();await using var host=await fixture.Host();using var client=host.Client();
        foreach(var version in new[]{"invalid",Convert.ToBase64String(new byte[8])})
        {
            var request=PrescriptionDatePrecisionTests.Draft();request.DispensingLocationSelection=new(Guid.NewGuid(),version);
            if(version=="invalid")request.DispensingLocationReference=new("S","I");
            Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(Route(patient),request)).StatusCode);
        }
        Assert.Empty(await Db.Repository().ListAsync(patient));
    }

    [Fact]
    public async Task ApiMaintenanceUsesTrustedActorAndExactResolution()
    {
        await using var host=await fixture.Host();using var client=host.Client();var request=Request();request.Phone=null;request.FacilityName+=" "+Guid.NewGuid();
        var body=JsonSerializer.SerializeToNode(request)!;body["actorUserId"]=8;body["tenantUid"]=Db.Tenant(true).TenantUid;
        var response=await client.PostAsJsonAsync("api/dispensing-locations",body);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var row=(await response.Content.ReadFromJsonAsync<DispensingLocation>())!;Assert.Equal(7,row.CreatedBy);Assert.Null(row.Phone);
        var resolved=await client.GetFromJsonAsync<DispensingLocation>("api/dispensing-locations/resolve?system="+Uri.EscapeDataString(" "+row.System+" ")+"&identifier="+Uri.EscapeDataString(row.Identifier));Assert.Equal(row,resolved);
        await using var other=await fixture.Host(other:true);using var foreign=other.Client();Assert.Equal(HttpStatusCode.NotFound,(await foreign.GetAsync("api/dispensing-locations/"+row.LocationUid)).StatusCode);
        Assert.Empty((await foreign.GetFromJsonAsync<DispensingLocationPage>("api/dispensing-locations?query="+Uri.EscapeDataString(row.FacilityName)))!.Items);
        request.RowVersion=row.RowVersion;request.FacilityAddress="Updated address\r\nSecond line";request.Phone=" 555-0100 ";
        response=await client.PutAsJsonAsync("api/dispensing-locations/"+row.LocationUid,request);Assert.Equal(HttpStatusCode.OK,response.StatusCode);var updated=(await response.Content.ReadFromJsonAsync<DispensingLocation>())!;Assert.Equal("Updated address\nSecond line",updated.FacilityAddress);Assert.Equal("555-0100",updated.Phone);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync("api/dispensing-locations/"+row.LocationUid,request)).StatusCode);
    }

    [Fact]
    public async Task DirectoryAuditFailureRollsBackDetailsAndVersion()
    {
        var service=fixture.Directory();var request=Request();var row=await service.SaveAsync(null,"Create",request,7);request.RowVersion=row.RowVersion;request.FacilityName="Rejected update";
        await using var c=await Db.Factory().OpenConnectionAsync();await PrescriptionPrivateNoteFixture.Batches(c,"CREATE TRIGGER dbo.DirectoryAuditFailure ON dbo.AuditLog AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE EntityName=N'DispensingLocation') THROW 52998,'TEST ONLY audit failure',1; END");
        try{await Assert.ThrowsAsync<SqlException>(()=>service.SaveAsync(row.LocationUid,"Edit",request,7));}finally{await PrescriptionPrivateNoteFixture.Batches(c,"DROP TRIGGER dbo.DirectoryAuditFailure");}
        Assert.Equal(row,await service.GetAsync(row.LocationUid));
    }
}
