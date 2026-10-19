using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Api.Middleware;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PatientCpp;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Application.SecurityAudit;
using MicroEMR.Application.Tenancy;
using MicroEMR.Infrastructure.ClinicalUsers;
using MicroEMR.Infrastructure.PatientPrescriptions;
using MicroEMR.Infrastructure.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PrescriptionDateValidationTests
{
    [Theory]
    [InlineData("Year", 2020, null, null, true)]
    [InlineData("Month", 2020, 2, null, true)]
    [InlineData("Day", 2020, 2, 29, true)]
    [InlineData("Day", 2021, 2, 29, false)]
    [InlineData("Month", 2020, 13, null, false)]
    [InlineData("Year", 2020, 1, null, false)]
    [InlineData("Day", 2020, null, 1, false)]
    [InlineData("Month", null, 2, null, false)]
    [InlineData("Year", 0, null, null, false)]
    [InlineData("Year", 10000, null, null, false)]
    [InlineData("", 2020, null, null, false)]
    [InlineData("Unknown", null, null, null, false)]
    public void WrittenDateRequiresExplicitValidKnownPrecision(string precision,int? year,int? month,int? day,bool valid)
    {
        var fields=new PrescriptionDateFields {WrittenDate=new(){Precision=precision,Year=year,Month=month,Day=day}};
        Assert.Equal(valid,!fields.Validate().Any());
    }

    [Fact]
    public void OptionalStartUnknownIsDistinctFromRequiredWrittenAndLegacyPresence()
    {
        var create=PrescriptionDatePrecisionTests.Draft("Year");
        Assert.Empty(create.ValidateDates());
        create.DateFields!.StartDate=new(){Precision="Unknown"};
        Assert.Empty(create.ValidateDates());
        create.PrescribedDate=new(2020,1,1);
        Assert.NotEmpty(create.ValidateDates());
        Assert.Empty(new PrescriptionDraftRequest().ValidateDates()); // omission is a preserving edit
        Assert.NotEmpty(new CreatePrescriptionDraftRequest().ValidateDates());
    }
}

public sealed class PrescriptionDatePrecisionTests(PrescriptionDateDatabase db):IClassFixture<PrescriptionDateDatabase>
{
    internal static CreatePrescriptionDraftRequest Draft(string precision="Month") => new()
    {
        ProductName="TEST ONLY date fixture",ProductDisplayText="TEST ONLY date fixture tablet",Route="Oral",FrequencyCode="ONCE_DAILY",
        Directions="TEST ONLY instructions",Quantity=1,QuantityUnit="tablet",AuthorizedRepeats=0,
        DateFields=new(){WrittenDate=new(){Precision=precision,Year=2020,Month=precision=="Year"?null:2,Day=precision=="Day"?29:null},
            StartDate=new(){Precision=precision,Year=2021,Month=precision=="Year"?null:3,Day=precision=="Day"?7:null}}
    };
    private static PrescriptionDraftRequest Edit(PatientPrescriptionResponse row,bool structured=true)
    {
        var request=JsonSerializer.Deserialize<PrescriptionDraftRequest>(JsonSerializer.Serialize(Draft()))!;
        request.DateFields=structured?row.DateFields:null;request.RowVersion=row.RowVersion;return request;
    }
    private static string Route(Guid patient,Guid? prescription=null)=>$"api/patients/{patient}/prescriptions"+(prescription.HasValue?$"/{prescription}":"");
    private static async Task<PatientPrescriptionResponse> Read(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode,$"HTTP {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;
    }

    [Theory]
    [InlineData("Year","2020","2021")]
    [InlineData("Month","2020-02","2021-03")]
    [InlineData("Day","2020-02-29","2021-03-07")]
    public async Task RealHttpRoundTripEditSignCorrectionAndCancelKeepRecordedPrecision(string precision,string written,string start)
    {
        var patient=await db.Stores.SeedPatient();await using var host=await db.Host();using var client=host.Client();
        var row=await Read(await client.PostAsJsonAsync(Route(patient),Draft(precision)));
        Assert.Equal(written,row.PrescribedDateDisplay);Assert.Equal(start,row.StartDateDisplay);
        Assert.Equal(precision=="Day",row.PrescribedDate.HasValue);
        var reopened=await Read(await client.GetAsync(Route(patient,row.PrescriptionUid)));
        var edit=Edit(reopened);edit.Directions="TEST ONLY amended";
        var saved=await Read(await client.PutAsJsonAsync(Route(patient,row.PrescriptionUid),edit));
        Assert.Equal(written,saved.PrescribedDateDisplay);Assert.Equal(start,saved.StartDateDisplay);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync(Route(patient,row.PrescriptionUid),edit)).StatusCode);
        var audit=await db.Stores.Read<string>("SELECT NewValue FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@Uid) AND ActionName=N'PrescriptionDraftUpdated'",row.PrescriptionUid);
        using(var json=JsonDocument.Parse(audit)) {Assert.Equal(7,json.RootElement.GetProperty("UpdatedBy").GetInt32());Assert.Contains(precision,json.RootElement.GetProperty("DateFields").GetString());}
        var old=await db.Stores.Read<string>("SELECT OldValue FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@Uid) AND ActionName=N'PrescriptionDraftUpdated'",row.PrescriptionUid);
        Assert.Contains("DateFields",old);Assert.DoesNotContain("TEST ONLY amended",old);
        var finalized=await Read(await client.PostAsJsonAsync(Route(patient,row.PrescriptionUid)+"/sign-and-finalize",new{saved.RowVersion,ConfirmSignature=true}));
        var artifact=(await db.Repository().GetArtifactAsync(patient,row.PrescriptionUid))!;
        using(var json=JsonDocument.Parse(artifact.Json))
        {Assert.Equal(written,json.RootElement.GetProperty("PrescribedDate").GetString());Assert.Equal(start,json.RootElement.GetProperty("StartDate").GetString());Assert.Equal(1,json.RootElement.GetProperty("DatePrecisionVersion").GetInt32());}
        var renderer=new Capture();var output=new PrescriptionOutputService(db.Repository(),db.Repository(),new PatientPrescriptionService(db.Repository(),renderer),renderer,TimeProvider.System);
        var html=Encoding.UTF8.GetString((await output.GenerateAsync(patient,row.PrescriptionUid,new("test-subject-7","TEST ONLY operator","step116")))!);
        Assert.Contains(written,html);Assert.Contains(start,html);
        var correction=await Read(await client.PostAsJsonAsync(Route(patient,row.PrescriptionUid)+"/correction",new{finalized.RowVersion}));
        Assert.Equal(start,correction.StartDateDisplay);
        // A correction is a new prescription: retain the established explicit today's written-date initialization.
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow),correction.PrescribedDate);
        await Read(await client.PostAsJsonAsync(Route(patient,correction.PrescriptionUid)+"/sign-and-finalize",new{correction.RowVersion,ConfirmSignature=true}));
        Assert.Equal("Superseded",(await db.Repository().GetAsync(patient,row.PrescriptionUid))!.Status);
        Assert.Equal(artifact,await db.Repository().GetArtifactAsync(patient,row.PrescriptionUid));
        Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync(Route(patient,row.PrescriptionUid),Edit(finalized))).StatusCode);
        var cancelRow=(await db.Repository().GetAsync(patient,correction.PrescriptionUid))!;
        var cancelArtifact=await db.Repository().GetArtifactAsync(patient,correction.PrescriptionUid);
        var cancelled=await Read(await client.PostAsJsonAsync(Route(patient,correction.PrescriptionUid)+"/cancel",new{cancelRow.RowVersion,Reason="TEST ONLY cancelled"}));
        Assert.Equal(start,cancelled.StartDateDisplay);Assert.Equal(cancelArtifact,await db.Repository().GetArtifactAsync(patient,correction.PrescriptionUid));
    }

    [Fact]
    public async Task LegacyOmissionPreservesStructuredDatesAndExplicitUnknownClearsOnlyStart()
    {
        var patient=await db.Stores.SeedPatient();var row=await db.Repository().CreateAsync(patient,Draft(),7);
        var legacy=Edit(row,false);legacy.PrescribedDate=new(1999,1,1);legacy.StartDate=null;
        var saved=(await db.Repository().UpdateAsync(patient,row.PrescriptionUid,legacy,7))!;
        Assert.Equal("2020-02",saved.PrescribedDateDisplay);Assert.Equal("2021-03",saved.StartDateDisplay);
        var edit=Edit(saved);edit.DateFields!.StartDate=new(){Precision="Unknown"};
        var unknown=(await db.Repository().UpdateAsync(patient,row.PrescriptionUid,edit,7))!;
        Assert.Null(unknown.StartDate);Assert.Equal("Not recorded",unknown.StartDateDisplay);
        var omitted=Edit(unknown,false);Assert.False(omitted.PrescribedDateSpecified);
        var preserved=(await db.Repository().UpdateAsync(patient,row.PrescriptionUid,omitted,7))!;
        Assert.Equal("2020-02",preserved.PrescribedDateDisplay);Assert.Equal("Not recorded",preserved.StartDateDisplay);
        var legacyRow=(await db.Repository().GetAsync(db.Stores.LegacyPatient,db.Stores.LegacyPrescription))!;
        Assert.Equal(db.Stores.LegacyRowVersion,legacyRow.RowVersion);Assert.Equal(db.Stores.LegacyJson,(await db.Repository().GetArtifactAsync(db.Stores.LegacyPatient,legacyRow.PrescriptionUid))!.Json);
        var exact=await db.Repository().CreateAsync(patient,PrescriptionRefillInstructionsTests.Draft(),7);
        Assert.Null(exact.DateFields);Assert.Equal(PrescriptionRefillInstructionsTests.Draft().PrescribedDate,exact.PrescribedDate);
    }

    [Fact]
    public async Task RealHttpPermissionsBothTenantDirectionsOwnershipActorAndInvalidInputAreRejected()
    {
        var patient=await db.Stores.SeedPatient();var otherPatient=await db.Stores.SeedPatient();
        await using var a=await db.Host();await using var b=await db.Host(true);using var ca=a.Client();using var cb=b.Client();using var reader=a.Client("reader");using var inactive=a.Client("inactive");
        var row=await Read(await ca.PostAsJsonAsync(Route(patient),Draft()));
        var body=JsonSerializer.Serialize(Draft(),new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var spoof=new StringContent(body.TrimEnd('}')+",\"createdBy\":8,\"prescriberUserId\":8}",Encoding.UTF8,"application/json");
        var trusted=await Read(await ca.PostAsync(Route(patient),spoof));Assert.Equal(7,trusted.CreatedBy);Assert.Equal(7,trusted.PrescriberUserId);
        Assert.Equal(HttpStatusCode.Forbidden,(await reader.PostAsJsonAsync(Route(patient),Draft())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await inactive.PostAsJsonAsync(Route(patient),Draft())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await ca.PutAsJsonAsync(Route(otherPatient,row.PrescriptionUid),Edit(row))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await cb.PutAsJsonAsync(Route(patient,row.PrescriptionUid),Edit(row))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await cb.GetAsync(Route(patient,row.PrescriptionUid))).StatusCode);
        var second=await db.SeedOtherPatient();var rb=await Read(await cb.PostAsJsonAsync(Route(second),Draft("Year")));
        Assert.Equal(HttpStatusCode.NotFound,(await ca.GetAsync(Route(second,rb.PrescriptionUid))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await ca.PutAsJsonAsync(Route(second,rb.PrescriptionUid),Edit(rb))).StatusCode);
        var invalid=Draft("Day");invalid.DateFields!.WrittenDate!.Year=2021;
        Assert.Equal(HttpStatusCode.BadRequest,(await ca.PostAsJsonAsync(Route(patient),invalid)).StatusCode);
        invalid.DateFields.WrittenDate.Precision="";
        Assert.Equal(HttpStatusCode.BadRequest,(await ca.PostAsJsonAsync(Route(patient),invalid)).StatusCode);
        Assert.Equal(row.RowVersion,(await db.Repository().GetAsync(patient,row.PrescriptionUid))!.RowVersion);
    }

    [Theory]
    [InlineData("Create")][InlineData("Update")][InlineData("Finalize")][InlineData("Sign")]
    public async Task AuditFaultRollsBackValuesVersionsAndArtifacts(string action)
    {
        var patient=await db.Stores.SeedPatient();var row=await db.Repository().CreateAsync(patient,Draft(),7);
        await db.Stores.Execute("CREATE TRIGGER dbo.Step116AuditFault ON dbo.AuditLog AFTER INSERT AS BEGIN THROW 53999,'TEST ONLY audit failure',1; END");
        try
        {
            await using var host=await db.Host();using var client=host.Client();
            HttpResponseMessage response;
            if(action=="Create")response=await client.PostAsJsonAsync(Route(patient),Draft("Year"));
            else if(action=="Update") {var edit=Edit(row);edit.DateFields=Draft("Year").DateFields;response=await client.PutAsJsonAsync(Route(patient,row.PrescriptionUid),edit);}
            else if(action=="Finalize")response=await client.PostAsJsonAsync(Route(patient,row.PrescriptionUid)+"/finalize",new{row.RowVersion});
            else response=await client.PostAsJsonAsync(Route(patient,row.PrescriptionUid)+"/sign-and-finalize",new{row.RowVersion,ConfirmSignature=true});
            using(response)Assert.Equal(HttpStatusCode.InternalServerError,response.StatusCode);
        }
        finally {await db.Stores.Execute("DROP TRIGGER dbo.Step116AuditFault");}
        var saved=(await db.Repository().GetAsync(patient,row.PrescriptionUid))!;
        Assert.Equal(row.RowVersion,saved.RowVersion);Assert.Equal("Draft",saved.Status);Assert.Equal(row.PrescribedDateDisplay,saved.PrescribedDateDisplay);Assert.Equal(row.StartDateDisplay,saved.StartDateDisplay);
        Assert.Null(await db.Repository().GetArtifactAsync(patient,row.PrescriptionUid));Assert.Single(await db.Repository().ListAsync(patient));
        Assert.Equal(1,await db.Stores.AuditCount(row.PrescriptionUid,"PrescriptionDraftCreated"));Assert.Equal(0,await db.Stores.AuditCount(row.PrescriptionUid,"PrescriptionDraftUpdated"));Assert.Equal(0,await db.Stores.AuditCount(row.PrescriptionUid,"PrescriptionFinalized"));
    }

    [Fact]
    public async Task DirectSqlCannotPersistImpossibleOrConflictingDatePrecision()
    {
        var patient=await db.Stores.SeedPatient();var row=await db.Repository().CreateAsync(patient,Draft(),7);
        foreach(var value in new[]{"{}","{\"WrittenDate\":{\"Year\":2020,\"Month\":2,\"Day\":29}}","{\"WrittenDate\":{\"Precision\":\"Day\",\"Year\":2021,\"Month\":2,\"Day\":29}}","{\"WrittenDate\":{\"Precision\":\"Unknown\"}}"})
        {
            await using var c=await db.Factory().OpenConnectionAsync();await using var command=new SqlCommand("UPDATE dbo.PatientPrescription SET DateFields=@Fields WHERE PrescriptionUid=@Uid",c);
            command.Parameters.AddWithValue("@Fields",value);command.Parameters.AddWithValue("@Uid",row.PrescriptionUid);await Assert.ThrowsAsync<SqlException>(()=>command.ExecuteNonQueryAsync());
        }
        Assert.Equal(row.RowVersion,(await db.Repository().GetAsync(patient,row.PrescriptionUid))!.RowVersion);
    }

    [Fact]
    public async Task CppAndSelectedPrintUseFullOwnedPrescriptionSourceWithoutSyntheticDates()
    {
        var f=new CppSelectivePrintTests.Fixture();var patient=await db.Stores.SeedPatient();f.Patient.PatientUid=patient;
        var service=new PatientPrescriptionService(db.Repository(),new Capture());
        for(var i=0;i<7;i++)
        {
            var request=Draft(i%2==0?"Year":"Month");request.ProductDisplayText=$"TEST ONLY precision row {i} <script>";
            var row=await service.CreateAsync(patient,request,7);await service.FinalizeAsync(patient,row.PrescriptionUid,row.RowVersion,7);
        }
        var audit=CppSelectivePrintTests.Stub<MicroEMR.Application.ReadAudit.IPatientChartReadAuditService>((_,_)=>Task.FromResult(Guid.NewGuid()));
        var reads=0;
        var prescriptionSource=CppSelectivePrintTests.Stub<IPatientPrescriptionService>((method,args)=>{Assert.Equal("ListAsync",method);reads++;return service.ListAsync((Guid)args[0]!, (CancellationToken)args[1]!);});
        var source=CppSelectivePrintTests.Source(patient,null!,audit,prescriptions:prescriptionSource);f.ActualSource=source;
        var card=(await source.GetAsync(patient,"step116"))!;Assert.Equal(5,card.Prescriptions.Items.Count);Assert.Equal(7,card.Prescriptions.TotalCount);
        Assert.All(card.Prescriptions.Items,x=>{Assert.Null(x.PrescribedDate);Assert.True(x.PrescribedDateDisplay is "2020" or "2020-02");});
        var all=(await source.GetForPrintAsync(patient,["Prescriptions"],"step116"))!;Assert.Equal(7,all.Prescriptions.Items.Count);
        var html=Encoding.UTF8.GetString((await f.Print("Prescriptions"))!);
        Assert.Contains("precision row 6 &lt;script&gt;",html);Assert.Contains("<dd>2020-02</dd>",html);Assert.Contains("<dd>2020</dd>",html);
        Assert.DoesNotContain("2020-02-01",html);Assert.DoesNotContain("0001-01-01",html);Assert.DoesNotContain("Prescribed Date Display",html);Assert.DoesNotContain("<script>",html);
        var readsBefore=reads;var unselected=Encoding.UTF8.GetString((await f.Print("RiskFactors"))!);Assert.DoesNotContain("precision row",unselected);Assert.Equal(readsBefore,reads);
        var printedAfter=(await db.Repository().ListAsync(patient)).ToArray();Assert.Equal(7,printedAfter.Length);
    }

    internal sealed class Capture:IPdfRenderer,IPrescriptionPdfRenderer
    {
        public Task<byte[]> RenderAsync(string html,CancellationToken token=default)=>Task.FromResult(Encoding.UTF8.GetBytes(html));
        public Task<byte[]> RenderPrescriptionAsync(PrescriptionPdfLayout layout,CancellationToken token=default)=>RenderAsync(layout.Body+layout.Header+layout.Footer,token);
    }
}

public sealed class PrescriptionDateDatabase:IAsyncLifetime
{
    internal PrescriptionRefillSqlDatabase Stores { get; }=new();
    private readonly Dictionary<bool,string> connections=[];
    private readonly Dictionary<bool,Guid> tenants=new(){[false]=Guid.NewGuid(),[true]=Guid.NewGuid()};
    internal TenantContext Tenant(bool other)=>new(tenants[other],other?"date-b":"date-a","TEST ONLY dates");
    internal ITenantSqlConnectionFactory Factory(bool other=false)=>new TenantSqlConnectionFactory(Tenant(other),new Assignment(this,other),new Secret(connections[other]),NullLogger<TenantSqlConnectionFactory>.Instance);
    internal PatientPrescriptionRepository Repository(bool other=false)=>new(Factory(other));
    public async Task InitializeAsync()
    {
        await Stores.InitializeAsync();
        foreach(var other in new[]{false,true})
        {
            await using var c=await Stores.PrescriptionFactory(other).OpenConnectionAsync();connections[other]=c.ConnectionString;
            await using var setup=new SqlCommand("ALTER TABLE dbo.ApplicationUser ADD UserUid uniqueidentifier NOT NULL DEFAULT NEWID(),Username nvarchar(100) NOT NULL DEFAULT N'fixture'; CREATE TABLE dbo.TenantDatabaseIdentity(TenantUid uniqueidentifier NOT NULL); INSERT dbo.TenantDatabaseIdentity VALUES(@Tenant); INSERT dbo.ApplicationUser(UserId,IsActive,AuthSubjectId) VALUES(9,0,N'test-subject-9');",c);
            setup.Parameters.AddWithValue("@Tenant",tenants[other]);await setup.ExecuteNonQueryAsync();
            var sql=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"database","tenant-clinical","migrations","0018-clinical-user-auth-subject.sql"));
            foreach(var batch in Regex.Split(sql,@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase))if(!string.IsNullOrWhiteSpace(batch)){await using var cmd=new SqlCommand(batch,c);await cmd.ExecuteNonQueryAsync();}
        }
    }
    public Task DisposeAsync()=>Stores.DisposeAsync();
    internal async Task<Guid> SeedOtherPatient(){var uid=Guid.NewGuid();await using var c=await Factory(true).OpenConnectionAsync();await using var cmd=new SqlCommand("INSERT dbo.Patient(PatientUid) VALUES(@Uid)",c);cmd.Parameters.AddWithValue("@Uid",uid);await cmd.ExecuteNonQueryAsync();return uid;}
    internal Task<PrescriptionDateHost> Host(bool other=false)=>PrescriptionDateHost.Start(this,other);
    private sealed class Assignment(PrescriptionDateDatabase db,bool other):ITenantDatabaseResolver
    {public Task<TenantDatabaseInfo?> ResolveAsync(Guid uid,CancellationToken token=default)=>Task.FromResult<TenantDatabaseInfo?>(new(uid,"fixture",new SqlConnectionStringBuilder(db.connections[other]).InitialCatalog,"fixture","Active"));}
    private sealed class Secret(string connection):ITenantDatabaseSecretProvider
    {public Task<TenantDatabaseSecret> ResolveAsync(string reference,CancellationToken token=default)=>Task.FromResult(new TenantDatabaseSecret(connection));}
}

internal sealed class PrescriptionDateHost(WebApplication app):IAsyncDisposable
{
    internal static async Task<PrescriptionDateHost> Start(PrescriptionDateDatabase db,bool other)
    {
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Testing"});builder.WebHost.UseUrls("http://127.0.0.1:0");builder.Logging.ClearProviders();builder.Services.AddHttpContextAccessor();
        builder.Services.AddAuthentication("Dates").AddScheme<AuthenticationSchemeOptions,Authentication>("Dates",_=>{});builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider,PermissionPolicyProvider>();builder.Services.AddScoped<IAuthorizationHandler,PermissionAuthorizationHandler>();builder.Services.AddScoped<ICurrentUserPermissionService,Permissions>();
        builder.Services.AddScoped<ITenantSqlConnectionFactory>(_=>db.Factory(other));builder.Services.AddScoped<IPatientPrescriptionRepository,PatientPrescriptionRepository>();builder.Services.AddScoped<IPrescriptionOutputRepository,PatientPrescriptionRepository>();
        builder.Services.AddScoped<IPdfRenderer,PrescriptionDatePrecisionTests.Capture>();builder.Services.AddScoped<IPrescriptionPdfRenderer,PrescriptionDatePrecisionTests.Capture>();builder.Services.AddSingleton(TimeProvider.System);builder.Services.AddScoped<IPatientPrescriptionService,PatientPrescriptionService>();builder.Services.AddScoped<PrescriptionOutputService>();
        builder.Services.AddScoped<IClinicalUserRepository,ClinicalUserRepository>();builder.Services.AddScoped<IAuthenticatedClinicalUserAccessor,AuthenticatedClinicalUserAccessor>();
        builder.Services.AddScoped<ITenantContextAccessor>(_=>{var accessor=new TenantContextAccessor();accessor.SetTenant(db.Tenant(other));return accessor;});
        var audit=DispatchProxy.Create<IPlatformSecurityAuditRepository,CppSelectivePrintTests.Proxy>();((CppSelectivePrintTests.Proxy)(object)audit).Call=(_,_)=>throw new InvalidOperationException("Unexpected platform audit");builder.Services.AddSingleton(audit);
        builder.Services.AddControllers().AddApplicationPart(typeof(MicroEMR.Api.Controllers.PatientPrescriptionsController).Assembly);
        var app=builder.Build();app.UseRouting();app.UseAuthentication();app.UseAuthorization();app.UseMiddleware<ClinicalUserActorResolutionMiddleware>();app.MapControllers();await app.StartAsync();return new(app);
    }
    internal HttpClient Client(string identity="writer"){var address=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();var client=new HttpClient{BaseAddress=new(address)};client.DefaultRequestHeaders.Authorization=new("Bearer",identity);return client;}
    public async ValueTask DisposeAsync(){await app.StopAsync();await app.DisposeAsync();}
    private sealed class Authentication(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync(){var name=Request.Headers.Authorization.ToString().Replace("Bearer ","");if(name is not("writer" or "reader" or "inactive"))return Task.FromResult(AuthenticateResult.NoResult());var subject=name=="inactive"?"test-subject-9":"test-subject-7";return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(new ClaimsIdentity([new("sub",subject),new("write",(name!="reader").ToString())],Scheme.Name)),Scheme.Name)));}
    }
    private sealed class Permissions(IHttpContextAccessor context):ICurrentUserPermissionService
    {
        public Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(CancellationToken token=default){var keys=new HashSet<string>{PermissionKeys.PatientsView};if(context.HttpContext!.User.FindFirst("write")?.Value=="True")keys.Add(PermissionKeys.PrescriptionsPrescribe);return Task.FromResult<IReadOnlySet<string>>(keys);}
        public async Task<bool> HasPermissionAsync(string permission,CancellationToken token=default)=>(await GetEffectivePermissionsAsync(token)).Contains(permission);
    }
}
