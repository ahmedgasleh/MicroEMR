using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Web.Authentication;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Services.PatientMedications;
using MicroEMR.Web.Services.PatientPrescriptions;
using Xunit;

namespace MicroEMR.Api.Tests;

// Real production Web clients -> loopback production API -> stored procedures -> generated LocalDB.
// Authentication supplies isolated fixture identities only; no client/repository/SQL response mocks.
public sealed class Step144IntegratedAcceptanceTests(Step144IntegratedFixture fixture) : IClassFixture<Step144IntegratedFixture>, IAsyncLifetime
{
    private WebApplication app=null!;
    private IPlaywright playwright=null!;
    private IBrowser browser=null!;
    private string address="";
    private readonly ConcurrentBag<(string Method,string Path,int Status)> outbound=[];
    private Guid planPatient,rxPatient,rxUid;
    private static string Root => FindRoot();

    public async Task InitializeAsync()
    {
        planPatient=await fixture.Plan.Db.Clinical.SeedPatient(false);
        rxPatient=await fixture.Orders.Db.Stores.SeedPatient();
        var draft=PrescriptionDatePrecisionTests.Draft();draft.ProductName="TEST ONLY integrated brand";draft.ProductDisplayText="TEST ONLY integrated A 1%; B 2 mg/mL";
        rxUid=(await fixture.Orders.Db.Repository().CreateAsync(rxPatient,draft,7)).PrescriptionUid;
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName=typeof(MicroEMR.Web.Controllers.PatientMedicationsController).Assembly.GetName().Name,ContentRootPath=Path.Combine(Root,"src/MicroEMR.Web"),EnvironmentName="Testing" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");builder.Logging.ClearProviders();builder.Services.AddHttpContextAccessor();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddSingleton(fixture);
        builder.Services.AddAuthentication("Step144").AddScheme<AuthenticationSchemeOptions,FixtureAuthentication>("Step144",_=>{});
        builder.Services.AddAuthorization();builder.Services.AddSingleton<IAuthorizationPolicyProvider,WebPermissionPolicyProvider>();builder.Services.AddScoped<IAuthorizationHandler,WebPermissionHandler>();
        builder.Services.AddScoped<IWebPermissionService,FixturePermissions>();builder.Services.AddSingleton<IWebPlatformEntitlementAccessor,NoPlatformEntitlements>();
        builder.Services.AddSingleton<IWebSessionTokenService,FixtureSession>();builder.Services.AddTransient<WebApiBearerTokenHandler>();
        builder.Services.AddHttpClient<IPatientMedicationApiClient,PatientMedicationApiClient>(client=>client.BaseAddress=fixture.PlanAddress)
            .AddHttpMessageHandler(()=>new ObserveHttp(outbound));
        builder.Services.AddHttpClient<IPatientPrescriptionApiClient,PatientPrescriptionApiClient>(client=>client.BaseAddress=fixture.OrderAddress)
            .AddHttpMessageHandler<WebApiBearerTokenHandler>().AddHttpMessageHandler(()=>new ObserveHttp(outbound));
        builder.Services.AddControllersWithViews();
        app=builder.Build();app.UseStaticFiles();app.UseRouting();app.UseAuthentication();app.UseAuthorization();
        // Chart destination is outside this acceptance; saved resources are reopened through real Details.
        app.MapGet("/Patients/Details",()=>Results.Content("<html><body>TEST ONLY save completed; open owned resource Details.</body></html>","text/html"));
        app.MapDefaultControllerRoute();await app.StartAsync();
        address=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        playwright=await Playwright.CreateAsync();browser=await playwright.Chromium.LaunchAsync(new(){Headless=true});
    }

    [Theory] [InlineData(1440)] [InlineData(390)]
    public async Task BrowserApiSqlSaveReloadHistoryValidationAndPermissions(int width)
    {
        await using var context=await browser.NewContextAsync(new(){ViewportSize=new(){Width=width,Height=width==390?844:900}});
        var page=await context.NewPageAsync();
        await page.RouteAsync("**/*",route=>new Uri(route.Request.Url).Host=="127.0.0.1"?route.ContinueAsync():route.AbortAsync());
        Assert.Equal(200,(await page.GotoAsync(address+$"/PatientMedications/Create?patientUid={planPatient}"))!.Status);
        await page.Locator("[name='MedicationName']").FillAsync("TEST ONLY real browser API SQL");
        var state=page.Locator("[name='ComplianceAssessment.State']");await state.FocusAsync();await page.Keyboard.PressAsync("Tab");
        Assert.True(await page.Locator("[name='ComplianceAssessment.AssessmentAt']").EvaluateAsync<bool>("x=>document.activeElement===x"));
        await state.SelectOptionAsync("Compliant");
        await page.Locator("[name='ComplianceAssessment.Explanation']").FillAsync("TEST ONLY <real persisted> & assessed");
        await page.Locator("[name='ComplianceAssessment.AssessmentAt']").FillAsync("2026-10-22T09:30:00-04:00");
        await page.Locator("main button[type=submit]").ClickAsync();await page.WaitForURLAsync("**/Patients/Details**");
        Assert.Contains(outbound,x=>x.Method=="POST" && x.Path.Contains("/medications") && x.Status==201);
        var plan=Assert.Single(await fixture.Plan.Db.Repository().GetByPatientUidAsync(planPatient));
        var stored=(await fixture.Plan.Db.Repository().GetByUidAsync(plan.MedicationUid))!;
        Assert.Equal("Compliant",stored.ComplianceAssessment!.State);Assert.Equal(fixture.Writer.Actor,stored.ComplianceAssessment.AssessorUserId);Assert.Single(stored.ComplianceHistory);
        Assert.Equal("TEST ONLY <real persisted> & assessed",stored.ComplianceAssessment.Explanation);
        await page.GotoAsync(address+$"/PatientMedications/Edit?medicationUid={plan.MedicationUid}");
        Assert.Equal("Compliant",await page.Locator("[name='ComplianceAssessment.State']").InputValueAsync());
        await page.Locator("[name='ComplianceAssessment.AssessmentAt']").FillAsync("2026-10-22T09:30:00");
        var puts=outbound.Count(x=>x.Method=="PUT");
        await page.Locator("main button[type=submit]").ClickAsync();await page.Locator(".validation-summary-errors").WaitForAsync();
        Assert.Contains("UTC offset",await page.Locator(".validation-summary-errors").InnerTextAsync());Assert.Equal(puts,outbound.Count(x=>x.Method=="PUT"));
        Assert.Single((await fixture.Plan.Db.Repository().GetByUidAsync(plan.MedicationUid))!.ComplianceHistory);
        await Capture(page,width,"assessment-validation");
        await page.Locator("[name='ComplianceAssessment.State']").SelectOptionAsync("Unknown");
        await page.Locator("[name='ComplianceAssessment.AssessmentAt']").FillAsync("");
        await page.Locator("main button[type=submit]").ClickAsync();await page.WaitForURLAsync("**/Patients/Details**");
        stored=(await fixture.Plan.Db.Repository().GetByUidAsync(plan.MedicationUid))!;
        Assert.Equal("Unknown",stored.ComplianceAssessment!.State);Assert.Equal(2,stored.ComplianceHistory.Count);Assert.Null(stored.ComplianceAssessment.AssessmentAt);
        await page.GotoAsync(address+$"/PatientMedications/Details?medicationUid={plan.MedicationUid}");
        await page.GetByText("Compliance assessment history",new(){Exact=true}).ClickAsync();
        Assert.Contains("Compliant",await page.Locator("section[aria-label='Medication compliance assessment']").InnerTextAsync());
        Assert.Contains("Unknown",await page.Locator("section[aria-label='Medication compliance assessment']").InnerTextAsync());
        await Capture(page,width,"assessment-history");

        var orderEdit=address+$"/PatientPrescriptions/Edit?patientUid={rxPatient}&prescriptionUid={rxUid}";
        await page.GotoAsync(orderEdit);
        Assert.Equal(new[]{"Not recorded","Yes"},await page.Locator("[name='OrderFields.PaperCopyRequiredBeforeDispense'] option").AllTextContentsAsync());
        await page.Locator("[name='OrderFields.PaperCopyRequiredBeforeDispense']").SelectOptionAsync("true");
        var interval=page.Locator("[name='OrderFields.MinimumDispenseIntervalDays']");
        foreach(var invalid in new[]{"0","-1","1.5"}){await interval.FillAsync(invalid);Assert.False(await interval.EvaluateAsync<bool>("x=>x.checkValidity()"));}
        await interval.FillAsync("7");await page.GetByRole(AriaRole.Button,new(){Name="Save Draft",Exact=true}).ClickAsync();await page.WaitForURLAsync("**/Patients/Details**");
        var order=(await fixture.Orders.Db.Repository().GetAsync(rxPatient,rxUid))!;
        Assert.True(order.PaperCopyRequiredBeforeDispense);Assert.Equal(7,order.MinimumDispenseIntervalDays);
        Assert.Contains(outbound,x=>x.Method=="PUT" && x.Path.Contains("/prescriptions/") && x.Status==200);
        await page.GotoAsync(orderEdit);Assert.Equal("7",await page.Locator("[name='OrderFields.MinimumDispenseIntervalDays']").InputValueAsync());
        Assert.Equal("true",await page.Locator("[name='OrderFields.PaperCopyRequiredBeforeDispense']").InputValueAsync());
        await page.GotoAsync(address+$"/PatientPrescriptions/Detail?patientUid={rxPatient}&prescriptionUid={rxUid}");
        Assert.Contains("7 whole days",await page.Locator("main").InnerTextAsync());Assert.Equal(0,await page.Locator("main input,main select,main form").CountAsync());
        await Capture(page,width,"order-details");

        // Reader may view this owned plan, but the real API must deny the attempted assessment save.
        await context.SetExtraHTTPHeadersAsync(new Dictionary<string,string>{{"X-Step144-Role","reader"}});
        await page.GotoAsync(address+$"/PatientMedications/Edit?medicationUid={plan.MedicationUid}");
        await page.Locator("[name='ComplianceAssessment.State']").SelectOptionAsync("Not assessed");
        await page.Locator("main button[type=submit]").ClickAsync();
        await page.Locator(".validation-summary-errors").WaitForAsync();
        Assert.Contains(outbound,x=>x.Method=="PUT" && x.Path.Contains("/medications/") && x.Status==403);
        var afterDenied=(await fixture.Plan.Db.Repository().GetByUidAsync(plan.MedicationUid))!;
        Assert.Equal(stored.RowVersion,afterDenied.RowVersion);Assert.Equal(2,afterDenied.ComplianceHistory.Count);
        Assert.Equal(403,(await page.GotoAsync(orderEdit))!.Status);
        Assert.Equal(order.RowVersion,(await fixture.Orders.Db.Repository().GetAsync(rxPatient,rxUid))!.RowVersion);
        Assert.Equal(2,(await fixture.Plan.Db.Clinical.Audit(false,plan.MedicationUid,"PatientMedication")).Count);
        Console.WriteLine($"Step144 width={width}: real API POST/PUT/GET and SQL persistence verified; denied API PUT=403; SQL schema fixtures disposable only.");
    }

    private static async Task Capture(IPage page,int width,string name)
    {
        await page.WaitForFunctionAsync("Array.from(document.styleSheets).some(s=>s.href?.includes('bootstrap.min.css'))");
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth<=innerWidth+1"));
        Assert.True(await page.EvaluateAsync<bool>("Array.from(document.querySelectorAll('main input:not([type=hidden]),main select,main textarea')).every(x=>x.labels.length>0)"));
        foreach(var field in await page.Locator("[name^='ComplianceAssessment.']").AllAsync()){
            await field.ScrollIntoViewIfNeededAsync();Assert.True(await field.EvaluateAsync<bool>("x=>{const r=x.getBoundingClientRect();return document.elementFromPoint(r.x+r.width/2,r.y+r.height/2)===x}"));
        }
        await page.EvaluateAsync("()=>{window.scrollTo(0,0);for(const x of document.querySelectorAll('*'))if(x.scrollTop)x.scrollTop=0}");
        var dir=Path.Combine(Root,".tmp/step144/screenshots");Directory.CreateDirectory(dir);await page.ScreenshotAsync(new(){Path=Path.Combine(dir,$"{name}-{width}.png"),FullPage=true});
    }

    public async Task DisposeAsync(){if(browser is not null)await browser.DisposeAsync();playwright?.Dispose();if(app is not null){await app.StopAsync();await app.DisposeAsync();}}
    private static string FindRoot(){for(var d=new DirectoryInfo(AppContext.BaseDirectory);d is not null;d=d.Parent)if(Directory.Exists(Path.Combine(d.FullName,"src/MicroEMR.Web/Views")))return d.FullName;throw new DirectoryNotFoundException();}
    private sealed class ObserveHttp(ConcurrentBag<(string Method,string Path,int Status)> log):DelegatingHandler
    { protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){var response=await base.SendAsync(request,token);log.Add((request.Method.Method,request.RequestUri!.AbsolutePath,(int)response.StatusCode));return response;} }
    private sealed class FixtureAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder,Step144IntegratedFixture fixture):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync(){var reader=Request.Headers["X-Step144-Role"]=="reader";var properties=new AuthenticationProperties();properties.StoreTokens([new AuthenticationToken{Name="access_token",Value=reader?fixture.Reader.Subject:fixture.Writer.Subject}]);return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier,"step144-fixture"),new(ClaimTypes.Name,"TEST ONLY integrated assessor"),new("rx-token",reader?"reader":"writer"),new("write",(!reader).ToString())],Scheme.Name)),properties,Scheme.Name)));}
    }
    private sealed class FixturePermissions(IHttpContextAccessor accessor):IWebPermissionService
    {
        public Task<IReadOnlySet<string>> GetAsync(CancellationToken token=default)=>Task.FromResult<IReadOnlySet<string>>(accessor.HttpContext!.User.FindFirst("write")?.Value=="True"?new HashSet<string>{PermissionKeys.PatientsView,PermissionKeys.ClinicalDataManage,PermissionKeys.PrescriptionsPrescribe}:new HashSet<string>{PermissionKeys.PatientsView});
        public async Task<bool> HasAsync(string key,CancellationToken token=default)=>(await GetAsync(token)).Contains(key);
    }
    private sealed class NoPlatformEntitlements:IWebPlatformEntitlementAccessor{public Task<bool> HasAsync(string key,CancellationToken token=default)=>Task.FromResult(false);}
    private sealed class FixtureSession:IWebSessionTokenService
    { public Task<string> GetValidAccessTokenAsync(HttpContext context,CancellationToken token)=>Task.FromResult(context.User.FindFirst("rx-token")!.Value);public Task RefreshCookieTicketAsync(CookieValidatePrincipalContext context)=>throw new InvalidOperationException("Real OIDC refresh is outside the isolated fixture"); }
}

public sealed class Step144IntegratedFixture:IAsyncLifetime
{
    internal Step143PlanDatabase Plan {get;}=new();
    internal Step143OrderDatabase Orders {get;}=new();
    internal FamilyHistoryAcceptanceDatabase.User Writer=null!,Reader=null!;
    private FamilyHistoryAcceptanceHost? planHost;
    private PrescriptionDateHost? orderHost;
    internal Uri PlanAddress=null!,OrderAddress=null!;
    private bool planReady,ordersReady;
    public async Task InitializeAsync()
    {
        // Existing fixtures allow test connection overrides; this acceptance forbids non-local overrides.
        foreach(var key in new[]{"MICROEMR_FAMILY_HISTORY_TEST_CONNECTION","MICROEMR_PRESCRIPTION_REFILL_TEST_CONNECTION"})
            if(Environment.GetEnvironmentVariable(key) is {} value){var connection=new SqlConnectionStringBuilder(value);if(!connection.DataSource.StartsWith("(localdb)\\",StringComparison.OrdinalIgnoreCase)||connection.InitialCatalog!="master"||!connection.IntegratedSecurity)throw new InvalidOperationException("Step144 requires local integrated-security disposable LocalDB fixtures only");}
        try{
            await Plan.InitializeAsync();planReady=true;await Orders.InitializeAsync();ordersReady=true;
            Writer=await Plan.Db.Clinical.SeedUser(false);Reader=await Plan.Db.Clinical.SeedUser(false,false);
            await using(var c=await Plan.Db.Clinical.Open(false))await CheckIdentity(c,"MicroEMR_Step97_");
            await using(var c=await Orders.Db.Factory().OpenConnectionAsync())await CheckIdentity(c,"MicroEMR_Step87_");
            planHost=await FamilyHistoryAcceptanceHost.Start(Plan.Db.Clinical,false,Writer,Reader);orderHost=await Orders.Db.Host();
            using var planClient=planHost.Client(Writer);PlanAddress=planClient.BaseAddress!;using var orderClient=orderHost.Client();OrderAddress=orderClient.BaseAddress!;
        }catch{await DisposeAsync();throw;}
    }
    private static async Task CheckIdentity(SqlConnection connection,string prefix)
    {
        await using var command=new SqlCommand("SELECT DB_NAME(),CONVERT(int,SERVERPROPERTY('IsLocalDB'))",connection);await using var r=await command.ExecuteReaderAsync();Assert.True(await r.ReadAsync());
        var database=r.GetString(0);Assert.Matches("^"+prefix+"[a-f0-9]{32}$",database);Assert.Equal(1,r.GetInt32(1));
        Console.WriteLine("Step144 actual isolated database: "+database+"; IsLocalDB=1");
    }
    public async Task DisposeAsync(){if(orderHost is not null){await orderHost.DisposeAsync();orderHost=null;}if(planHost is not null){await planHost.DisposeAsync();planHost=null;}if(ordersReady){await Orders.DisposeAsync();ordersReady=false;}if(planReady){await Plan.DisposeAsync();planReady=false;}}
}
