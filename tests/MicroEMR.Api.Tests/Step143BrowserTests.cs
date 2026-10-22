using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientMedications.Contracts;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Services.PatientMedications;
using MicroEMR.Web.Services.PatientPrescriptions;
using Xunit;
using MedDetails = MicroEMR.Web.Models.PatientMedications.PatientMedicationDetailsResponse;

namespace MicroEMR.Api.Tests;

// Production Web controllers, Razor, local CSS/JS and antiforgery on a loopback host.
// API clients are isolated in-memory transports. Real API/SQL behavior is tested separately.
public sealed class Step143BrowserTests : IAsyncLifetime
{
    private WebApplication app=null!;
    private IPlaywright playwright=null!;
    private IBrowser browser=null!;
    private string address="";
    private readonly Guid patient=Guid.NewGuid(),medication=Guid.NewGuid(),prescription=Guid.NewGuid();
    private MedDetails med=null!;
    private PatientPrescriptionResponse rx=null!;
    private bool conflict;
    private int medicationWrites,orderWrites;
    private static string Root => FindRoot();

    public async Task InitializeAsync()
    {
        med=new(){PatientUid=patient,MedicationUid=medication,MedicationName="TEST ONLY synthetic plan",Classification="Acute",Status="Active",RowVersion=Convert.ToBase64String(new byte[8])};
        rx=new(){PatientUid=patient,PrescriptionUid=prescription,Status="Draft",ProductName="TEST ONLY independent brand",GenericName="Separate generic",ProductDisplayText="TEST ONLY A 1%; B 2 mg/mL; <compound> & base",Directions="TEST ONLY retained directions",Route="Oral",FrequencyCode="ONCE",FrequencyDisplay="Once",Quantity=1,QuantityUnit="tablet",AuthorizedRepeats=0,PrescribedDate=new(2026,10,22),RowVersion=Convert.ToBase64String(new byte[8])};
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{ApplicationName=typeof(MicroEMR.Web.Controllers.PatientMedicationsController).Assembly.GetName().Name,ContentRootPath=Path.Combine(Root,"src/MicroEMR.Web"),EnvironmentName="Testing"});
        builder.WebHost.UseUrls("http://127.0.0.1:0");builder.Logging.ClearProviders();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAuthentication("Step143Browser").AddScheme<AuthenticationSchemeOptions,FixtureAuthentication>("Step143Browser",_=>{});
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider,WebPermissionPolicyProvider>();
        builder.Services.AddScoped<IAuthorizationHandler,WebPermissionHandler>();
        builder.Services.AddSingleton(CppSelectivePrintTests.Stub<IWebPermissionService>((method,args)=>method=="HasAsync"?(object)Task.FromResult((string)args[0]! != PermissionKeys.PrescriptionsViewPrivateNotes):Task.FromResult<IReadOnlySet<string>>(new HashSet<string>{PermissionKeys.PatientsView,PermissionKeys.ClinicalDataManage,PermissionKeys.PrescriptionsPrescribe})));
        builder.Services.AddSingleton(CppSelectivePrintTests.Stub<IWebPlatformEntitlementAccessor>((_,_)=>Task.FromResult(false)));
        builder.Services.AddSingleton(CppSelectivePrintTests.Stub<IPatientMedicationApiClient>((method,args)=>method switch
        {
            "GetByUidAsync"=>(object)Task.FromResult<MedDetails?>(med),
            "CreateAsync"=>Create((MicroEMR.Web.Models.PatientMedications.CreatePatientMedicationRequest)args[1]!),
            "UpdateAsync"=>Update((MicroEMR.Web.Models.PatientMedications.UpdatePatientMedicationRequest)args[2]!),
            _=>throw new InvalidOperationException(method)
        }));
        builder.Services.AddSingleton(CppSelectivePrintTests.Stub<IPatientPrescriptionApiClient>((method,args)=>method switch
        {
            "GetAsync"=>Task.FromResult<PatientPrescriptionResponse?>(rx),
            "UpdateAsync"=>SaveOrder((PrescriptionDraftRequest)args[2]!),
            "PrivateNoteAsync"=>Task.FromResult<PrescriptionPrivateNoteDetail?>(null),
            "PrivateNoteHistoryAsync"=>Task.FromResult<IReadOnlyList<PrescriptionPrivateNoteRevision>>([]),
            _=>throw new InvalidOperationException(method)
        }));
        builder.Services.AddControllersWithViews();
        app=builder.Build();app.UseStaticFiles();app.UseRouting();app.UseAuthentication();app.UseAuthorization();
        app.MapGet("/Patients/Details",()=>Results.Content("<html><body>TEST ONLY post-save landing</body></html>","text/html"));
        app.MapGet("/step143/modal",(Func<HttpContext,Task<IResult>>)RenderModal);
        app.MapDefaultControllerRoute();await app.StartAsync();
        address=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        playwright=await Playwright.CreateAsync();browser=await playwright.Chromium.LaunchAsync(new(){Headless=true});
    }

    private Task<MedDetails> Create(MicroEMR.Web.Models.PatientMedications.CreatePatientMedicationRequest value)
    { Capture(value.ComplianceAssessment);med.MedicationName=value.MedicationName;medicationWrites++;return Task.FromResult(med); }
    private Task<MedDetails?> Update(MicroEMR.Web.Models.PatientMedications.UpdatePatientMedicationRequest value)
    { if(conflict){conflict=false;throw new HttpRequestException("TEST ONLY stale save",null,HttpStatusCode.Conflict);}Capture(value.ComplianceAssessment);medicationWrites++;return Task.FromResult<MedDetails?>(med); }
    private void Capture(MedicationComplianceAssessment? value)
    {
        if(value is null)return;
        var revision=new MedicationComplianceRevision{State=value.State,Explanation=value.Explanation,AssessmentAt=value.AssessmentAt,AssessorUserId=7,RecordedAtUtc=DateTime.UtcNow,RevisionNumber=med.ComplianceHistory.Count+1};
        med.ComplianceHistory=[revision,..med.ComplianceHistory];med.ComplianceAssessment=value.State is null?null:revision;
    }
    private Task<PatientPrescriptionResponse?> SaveOrder(PrescriptionDraftRequest value)
    {
        orderWrites++;rx.PaperCopyRequiredBeforeDispense=value.OrderFields?.PaperCopyRequiredBeforeDispense;
        rx.MinimumDispenseIntervalDays=value.OrderFields?.MinimumDispenseIntervalDays;rx.ProductName=value.ProductName;rx.ProductDisplayText=value.ProductDisplayText;rx.Directions=value.Directions;
        return Task.FromResult<PatientPrescriptionResponse?>(rx);
    }

    private async Task<IResult> RenderModal(HttpContext http)
    {
        var action=new ActionContext(http,new RouteData(),new ActionDescriptor());
        var view=http.RequestServices.GetRequiredService<ICompositeViewEngine>().GetView(null,"/Views/Patients/_EditMedicationModal.cshtml",false);
        Assert.True(view.Success);
        var model=new MicroEMR.Web.Models.Patients.PatientChartViewModel{Patient=new(){PatientUid=patient}};
        var data=new ViewDataDictionary<MicroEMR.Web.Models.Patients.PatientChartViewModel>(new EmptyModelMetadataProvider(),new ModelStateDictionary()){Model=model};
        using var writer=new StringWriter();
        await view.View!.RenderAsync(new ViewContext(action,view.View,data,new TempDataDictionary(http,http.RequestServices.GetRequiredService<ITempDataProvider>()),writer,new HtmlHelperOptions()));
        return Results.Content("<!doctype html><html><head><meta name='viewport' content='width=device-width,initial-scale=1'><link rel='stylesheet' href='/lib/bootstrap/dist/css/bootstrap.min.css'></head><body>"+writer+"<script src='/lib/bootstrap/dist/js/bootstrap.bundle.min.js'></script><script type='module' src='/dist/patients/medication-classification.js'></script></body></html>","text/html");
    }

    [Theory] [InlineData(1440)] [InlineData(390)]
    public async Task ProductionModalUsesActualEditJsonAndEmittedLoader(int width)
    {
        Capture(new(){State="Unknown",Explanation="TEST ONLY <modal> & retained",AssessmentAt=DateTimeOffset.Parse("2026-10-22T09:30:00-04:00")});
        await using var context=await browser.NewContextAsync(new(){ViewportSize=new(){Width=width,Height=width==390?844:900}});
        var page=await context.NewPageAsync();
        await page.RouteAsync("**/*",route=>new Uri(route.Request.Url).Host=="127.0.0.1"?route.ContinueAsync():route.AbortAsync());
        Assert.Equal(200,(await page.GotoAsync(address+"/step143/modal"))!.Status);
        var editUrl=address+$"/PatientMedications/Edit?medicationUid={medication}";
        await page.EvaluateAsync("async url=>{const data=await(await fetch(url,{headers:{'X-Requested-With':'XMLHttpRequest'}})).json();document.querySelector('#editMedicationForm').dispatchEvent(new CustomEvent('medication-loaded',{detail:data}));bootstrap.Modal.getOrCreateInstance(document.querySelector('#editMedicationModal')).show()}",editUrl);
        await page.Locator("#editMedicationModal.show").WaitForAsync();
        Assert.Equal("Unknown",await page.Locator("[name='ComplianceAssessment.State']").InputValueAsync());
        Assert.Equal("TEST ONLY <modal> & retained",await page.Locator("[name='ComplianceAssessment.Explanation']").InputValueAsync());
        Assert.Contains("-04:00",await page.Locator("[name='ComplianceAssessment.AssessmentAt']").InputValueAsync());
        Assert.True(await page.EvaluateAsync<bool>("Array.from(document.querySelectorAll('[data-compliance-assessment] input,[data-compliance-assessment] select,[data-compliance-assessment] textarea')).every(x=>x.labels.length>0)"));
        foreach(var control in await page.Locator("[name^='ComplianceAssessment.']").AllAsync()){
            await control.ScrollIntoViewIfNeededAsync();
            Assert.True(await control.EvaluateAsync<bool>("x=>{const r=x.getBoundingClientRect();return document.elementFromPoint(r.x+r.width/2,r.y+r.height/2)===x}"));
        }
        await page.EvaluateAsync("document.querySelector('.modal-body').scrollTop=0");
        await page.ScreenshotAsync(new(){Path=Path.Combine(Root,$".tmp/step143/screenshots/plan-modal-{width}.png"),FullPage=true});
        med.ComplianceAssessment=null;
        await page.EvaluateAsync("async url=>{const data=await(await fetch(url,{headers:{'X-Requested-With':'XMLHttpRequest'}})).json();document.querySelector('#editMedicationForm').dispatchEvent(new CustomEvent('medication-loaded',{detail:data}))}",editUrl);
        foreach(var field in await page.Locator("[name^='ComplianceAssessment.']").AllAsync())Assert.Equal("",await field.InputValueAsync());
    }

    [Theory] [InlineData(1440)] [InlineData(390)]
    public async Task ActualPagesExerciseAssessmentOrderValidationAndReadOnlyLayouts(int width)
    {
        await using var context=await browser.NewContextAsync(new(){ViewportSize=new(){Width=width,Height=width==390?844:900}});
        var page=await context.NewPageAsync();
        await page.RouteAsync("**/*",route=>new Uri(route.Request.Url).Host=="127.0.0.1"?route.ContinueAsync():route.AbortAsync());
        Assert.Equal(200,(await page.GotoAsync(address+$"/PatientMedications/Create?patientUid={patient}"))!.Status);
        await page.Locator("[name='MedicationName']").FillAsync("TEST ONLY browser assessment");
        var state=page.Locator("[name='ComplianceAssessment.State']");
        Assert.Equal(new[]{"Not recorded","Compliant","Not compliant","Unknown","Not assessed"},await state.Locator("option").AllTextContentsAsync());
        await state.FocusAsync();await page.Keyboard.PressAsync("Tab");
        Assert.True(await page.Locator("[name='ComplianceAssessment.AssessmentAt']").EvaluateAsync<bool>("x=>document.activeElement===x"));
        await state.SelectOptionAsync("Compliant");
        await page.Locator("[name='ComplianceAssessment.Explanation']").FillAsync("TEST ONLY <observed> & retained");
        await page.Locator("[name='ComplianceAssessment.AssessmentAt']").FillAsync("2026-10-22T09:30:00-04:00");
        await CheckLayoutAndCapture(page,width,"plan-create");
        await page.Locator("main button[type='submit']").ClickAsync();await page.WaitForURLAsync("**/Patients/Details**");
        Assert.Equal(1,medicationWrites);Assert.Equal("Compliant",med.ComplianceAssessment!.State);
        await page.GotoAsync(address+$"/PatientMedications/Edit?medicationUid={medication}");
        Assert.Equal("Compliant",await page.Locator("[name='ComplianceAssessment.State']").InputValueAsync());
        await page.Locator("[name='ComplianceAssessment.AssessmentAt']").FillAsync("2026-10-22T09:30:00");
        await page.Locator("main button[type='submit']").ClickAsync();
        await page.Locator(".validation-summary-errors").WaitForAsync();
        Assert.Contains("UTC offset",await page.Locator(".validation-summary-errors").InnerTextAsync());
        Assert.Equal("2026-10-22T09:30:00",await page.Locator("[name='ComplianceAssessment.AssessmentAt']").InputValueAsync());
        Assert.Equal(1,medicationWrites);
        await CheckLayoutAndCapture(page,width,"plan-validation");
        await page.Locator("[name='ComplianceAssessment.AssessmentAt']").FillAsync("2026-10-22T09:30:00-04:00");
        conflict=true;
        await page.Locator("[name='ComplianceAssessment.Explanation']").FillAsync("TEST ONLY unsaved conflict text");
        await page.Locator("main button[type='submit']").ClickAsync();
        await page.Locator(".validation-summary-errors").WaitForAsync();
        Assert.Equal("alert",await page.Locator(".validation-summary-errors").GetAttributeAsync("role"));
        Assert.Contains("Reload",await page.Locator(".validation-summary-errors").InnerTextAsync());
        Assert.Equal("TEST ONLY unsaved conflict text",await page.Locator("[name='ComplianceAssessment.Explanation']").InputValueAsync());
        Assert.Equal(1,medicationWrites);await CheckLayoutAndCapture(page,width,"plan-conflict");
        await page.Locator("[name='ComplianceAssessment.State']").SelectOptionAsync("Unknown");
        await page.Locator("[name='ComplianceAssessment.AssessmentAt']").FillAsync("");
        await page.Locator("main button[type='submit']").ClickAsync();await page.WaitForURLAsync("**/Patients/Details**");
        await page.GotoAsync(address+$"/PatientMedications/Details?medicationUid={medication}");
        Assert.Contains("Unknown",await page.Locator("section[aria-label='Medication compliance assessment']").InnerTextAsync());
        Assert.Equal(0,await page.Locator("section[aria-label='Medication compliance assessment'] input").CountAsync());
        await CheckLayoutAndCapture(page,width,"plan-details");
        await page.GotoAsync(address+$"/PatientPrescriptions/Edit?patientUid={patient}&prescriptionUid={prescription}");
        var paper=page.Locator("[name='OrderFields.PaperCopyRequiredBeforeDispense']");
        Assert.Equal(new[]{"Not recorded","Yes"},await paper.Locator("option").AllTextContentsAsync());
        await paper.SelectOptionAsync("true");
        var interval=page.Locator("[name='OrderFields.MinimumDispenseIntervalDays']");
        await interval.FillAsync("0");Assert.False(await interval.EvaluateAsync<bool>("x=>x.checkValidity()"));
        await interval.FillAsync("1.5");Assert.False(await interval.EvaluateAsync<bool>("x=>x.checkValidity()"));
        await interval.FillAsync("7");Assert.True(await interval.EvaluateAsync<bool>("x=>x.checkValidity()"));
        await CheckLayoutAndCapture(page,width,"prescription-edit");
        await page.GetByRole(AriaRole.Button,new(){Name="Save Draft",Exact=true}).ClickAsync();await page.WaitForURLAsync("**/Patients/Details**");
        Assert.Equal(1,orderWrites);Assert.True(rx.PaperCopyRequiredBeforeDispense);Assert.Equal(7,rx.MinimumDispenseIntervalDays);
        await page.GotoAsync(address+$"/PatientPrescriptions/Edit?patientUid={patient}&prescriptionUid={prescription}");
        Assert.Equal("7",await page.Locator("[name='OrderFields.MinimumDispenseIntervalDays']").InputValueAsync());
        rx.Status="Finalized";
        await page.GotoAsync(address+$"/PatientPrescriptions/Detail?patientUid={patient}&prescriptionUid={prescription}");
        Assert.Contains("7 whole days",await page.Locator("main").InnerTextAsync());
        Assert.Contains(rx.ProductName,await page.Locator("main").InnerTextAsync());
        Assert.Contains(rx.Directions,await page.Locator("main").InnerTextAsync());
        Assert.Equal(0,await page.Locator("main input,main select,main form").CountAsync());
        await CheckLayoutAndCapture(page,width,"prescription-details");
    }

    private static async Task CheckLayoutAndCapture(IPage page,int width,string name)
    {
        // Full local Bootstrap/theme assets must actually load; screenshots alone are not an assertion.
        await page.WaitForFunctionAsync("Array.from(document.styleSheets).some(s=>s.href?.includes('bootstrap.min.css'))");
        Assert.True(await page.EvaluateAsync<bool>("!document.querySelector('.row') || getComputedStyle(document.querySelector('.row')).display==='flex'"));
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth + 1"));
        Assert.True(await page.EvaluateAsync<bool>("Array.from(document.querySelectorAll('main input:not([type=hidden]),main select,main textarea')).every(x=>x.labels?.length>0)"));
        foreach(var field in await page.Locator("[name^='ComplianceAssessment.'],[name='OrderFields.PaperCopyRequiredBeforeDispense'],[name='OrderFields.MinimumDispenseIntervalDays']").AllAsync())
        {
            await field.ScrollIntoViewIfNeededAsync();
            Assert.True(await field.EvaluateAsync<bool>("x=>{const r=x.getBoundingClientRect(), hit=document.elementFromPoint(r.x+r.width/2,r.y+r.height/2);return hit===x||x.contains(hit)}"));
        }
        await page.EvaluateAsync("()=>{window.scrollTo(0,0);for(const x of document.querySelectorAll('*'))if(x.scrollTop)x.scrollTop=0}");
        var directory=Path.Combine(Root,".tmp/step143/screenshots");Directory.CreateDirectory(directory);
        await page.ScreenshotAsync(new(){Path=Path.Combine(directory,$"{name}-{width}.png"),FullPage=true});
    }

    public async Task DisposeAsync(){if(browser is not null)await browser.DisposeAsync();playwright?.Dispose();if(app is not null){await app.StopAsync();await app.DisposeAsync();}}
    private static string FindRoot(){for(var d=new DirectoryInfo(AppContext.BaseDirectory);d is not null;d=d.Parent)if(Directory.Exists(Path.Combine(d.FullName,"src/MicroEMR.Web/Views")))return d.FullName;throw new DirectoryNotFoundException();}
    private sealed class FixtureAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()=>Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"step143-test"),new Claim(ClaimTypes.Name,"TEST ONLY assessor")],Scheme.Name)),Scheme.Name)));
    }
}
