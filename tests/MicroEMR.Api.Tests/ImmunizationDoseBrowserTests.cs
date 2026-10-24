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
using MicroEMR.Application.PatientImmunizations;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Services.PatientImmunizations;
using Xunit;

namespace MicroEMR.Api.Tests;

// Actual Razor/TS/Web proxy -> loopback API -> disposable tenant SQL. No development login or mocked persistence.
public sealed class ImmunizationDoseBrowserTests(ImmunizationFieldDatabase db) : IClassFixture<ImmunizationFieldDatabase>
{
    internal static string Root
    {
        get { for(var d=new DirectoryInfo(AppContext.BaseDirectory);d is not null;d=d.Parent)
            if(Directory.Exists(Path.Combine(d.FullName,"src/MicroEMR.Web/Views")))return d.FullName;
            throw new DirectoryNotFoundException(); }
    }

    [Theory] [InlineData(1440)] [InlineData(375)]
    public async Task RealFormCreateEditValidationReloadHistoryAndScreenshot(int width)
    {
        var patient=await db.Clinical.SeedPatient(false);var actor=await db.Clinical.SeedUser(false);
        await using var api=await FamilyHistoryAcceptanceHost.Start(db.Clinical,false,actor);using var apiHttp=api.Client(actor);
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{ApplicationName=typeof(MicroEMR.Web.Controllers.PatientImmunizationsController).Assembly.GetName().Name,ContentRootPath=Path.Combine(Root,"src/MicroEMR.Web"),EnvironmentName="Testing"});
        builder.WebHost.UseUrls("http://127.0.0.1:0");builder.Logging.ClearProviders();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton(actor);
        builder.Services.AddAuthentication("DoseFixture").AddScheme<AuthenticationSchemeOptions,FixtureAuthentication>("DoseFixture",_=>{});
        builder.Services.AddAuthorization();builder.Services.AddSingleton<IAuthorizationPolicyProvider,WebPermissionPolicyProvider>();
        builder.Services.AddScoped<IAuthorizationHandler,WebPermissionHandler>();
        builder.Services.AddSingleton(CppSelectivePrintTests.Stub<IWebPermissionService>((method,_)=>method=="HasAsync"?(object)Task.FromResult(true):Task.FromResult<IReadOnlySet<string>>(new HashSet<string>())));
        builder.Services.AddSingleton(CppSelectivePrintTests.Stub<IWebPlatformEntitlementAccessor>((_,_)=>Task.FromResult(false)));
        builder.Services.AddScoped<IPatientImmunizationApiClient>(sp=>new PatientImmunizationApiClient(apiHttp,sp.GetRequiredService<IHttpContextAccessor>()));
        builder.Services.AddControllersWithViews();await using var web=builder.Build();
        web.UseStaticFiles();web.UseRouting();web.UseAuthentication();web.UseAuthorization();
        web.MapGet("/dose-fixture",(Func<HttpContext,Task<IResult>>)(http=>Render(http,patient)));
        web.MapControllers();await web.StartAsync();
        var address=web.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var playwright=await Playwright.CreateAsync();
        await using var browser=await playwright.Chromium.LaunchAsync(new(){Headless=true,ExecutablePath=Path.Combine(Root,".tmp/step116-browsers/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe")});
        await using var context=await browser.NewContextAsync(new(){ViewportSize=new(){Width=width,Height=900}});
        await context.RouteAsync("**/*",route=>new Uri(route.Request.Url).Authority==new Uri(address).Authority?route.ContinueAsync():route.AbortAsync());
        var page=await context.NewPageAsync();page.SetDefaultTimeout(10000);
        Assert.Equal(200,(await page.GotoAsync(address+"/dose-fixture"))!.Status);
        await page.GetByText("No immunizations recorded",new(){Exact=true}).WaitForAsync();
        await page.Locator("#addImmunizationButton").ClickAsync();await page.Locator("#immunizationModal.show").WaitForAsync();
        await page.Locator("[name=VaccineName]").FillAsync("TEST ONLY dose browser");
        await page.Locator("[name=ImmunizationOutcome]").SelectOptionAsync("false");
        await page.Locator("[name=SourceType]").SelectOptionAsync("HistoricalExternal");
        var amount=page.Locator("#immunizationDoseAmount");var unit=page.Locator("#immunizationDoseUnit");
        Assert.Equal(new[]{"","mL","mg","mcg","units"},await unit.Locator("option").EvaluateAllAsync<string[]>("xs=>xs.map(x=>x.value)"));
        Assert.True(await amount.EvaluateAsync<bool>("x=>x.labels.length===1"));Assert.True(await unit.EvaluateAsync<bool>("x=>x.labels.length===1"));
        await amount.FocusAsync();await page.Keyboard.PressAsync("Tab");Assert.True(await unit.EvaluateAsync<bool>("x=>document.activeElement===x"));
        foreach(var invalid in new[]{"0","-1","10000","0.0001"})
        {
            await amount.FillAsync(invalid);await unit.SelectOptionAsync("mL");await page.Locator("#saveImmunizationButton").ClickAsync();
            Assert.False(await amount.EvaluateAsync<bool>("x=>x.validity.valid"));Assert.Empty(await db.Repository().ListAsync(patient,"All"));
        }
        await amount.FillAsync("0.001");await unit.SelectOptionAsync("");await page.Locator("#saveImmunizationButton").ClickAsync();
        await page.GetByText("Enter dose amount greater than 0 and at most 9999.999 with up to 3 decimal places, and select its unit; or leave both blank.",new(){Exact=true}).WaitForAsync();
        Assert.Empty(await db.Repository().ListAsync(patient,"All"));
        await unit.SelectOptionAsync("mL");await Save(page);
        await page.Locator("#immunizationList").GetByText("0.001 mL",new(){Exact=false}).WaitForAsync();
        await page.ReloadAsync();await page.Locator("#immunizationList .edit-immunization").ClickAsync();
        Assert.Equal("0.001",await amount.InputValueAsync());Assert.Equal("mL",await unit.InputValueAsync());
        await amount.FillAsync("9999.999");await unit.SelectOptionAsync("mg");await Save(page);
        await page.ReloadAsync();await page.Locator("#immunizationList .edit-immunization").ClickAsync();
        Assert.Equal("9999.999",await amount.InputValueAsync());Assert.Equal("mg",await unit.InputValueAsync());
        await Capture(page,width,"editor");
        await amount.FillAsync("");await unit.SelectOptionAsync("");await Save(page);
        var saved=Assert.Single(await db.Repository().ListAsync(patient,"All"));Assert.Null(saved.Dose!.Amount);Assert.Null(saved.Dose.UnitCode);
        // Refusal UI suppresses administered-dose controls; NULL dose does not invent a refusal.
        await page.Locator("#addImmunizationButton").ClickAsync();await page.Locator("[name=VaccineName]").FillAsync("TEST ONLY refusal browser");
        await page.Locator("[name=ImmunizationOutcome]").SelectOptionAsync("true");Assert.True(await amount.IsDisabledAsync());Assert.True(await unit.IsDisabledAsync());
        await Save(page);await page.ReloadAsync();await page.Locator("#immunizationList").GetByText("TEST ONLY refusal browser",new(){Exact=true}).WaitForAsync();
        var rows=await db.Repository().ListAsync(patient,"All");Assert.Equal(2,rows.Count);Assert.Contains(rows,x=>x.Status=="Completed"&&x.Dose!.Amount is null);Assert.Contains(rows,x=>x.Status=="Refused"&&x.Dose!.Amount is null);
        Assert.Equal(3,(await db.Clinical.Audit(false,saved.ImmunizationUid,"PatientImmunization")).Count);
        if(width<600)
        {
            await page.Locator("#immunizationList .edit-immunization").First.ScrollIntoViewIfNeededAsync();
            Assert.True(await page.Locator("#immunizationList .table-responsive").EvaluateAsync<bool>("x=>x.scrollLeft>0"));
            await page.Locator("#immunizationList .table-responsive").EvaluateAsync("x=>x.scrollLeft=0");
        }
        await Capture(page,width,"history");
        await context.CloseAsync();await browser.CloseAsync();Assert.False(browser.IsConnected);await web.StopAsync();
    }

    private static async Task Save(IPage page)
    {
        var response=await page.RunAndWaitForResponseAsync(()=>page.Locator("#saveImmunizationButton").ClickAsync(),r=>r.Request.Method=="POST"&&r.Url.Contains("/immunizations"));
        Assert.True(response.Ok,await response.TextAsync());
        await page.Locator("#immunizationModal").WaitForAsync(new(){State=WaitForSelectorState.Hidden});
    }
    private static async Task Capture(IPage page,int width,string name)
    {
        if(name=="editor")
        {
            await page.WaitForFunctionAsync("()=>{const m=document.querySelector('#immunizationModal');return m.classList.contains('show')&&getComputedStyle(m).opacity==='1'&&getComputedStyle(m.querySelector('.modal-dialog')).transform==='none'}");
            await page.Locator("#immunizationDoseAmount").ScrollIntoViewIfNeededAsync();
            foreach(var selector in new[]{"#immunizationDoseAmount","#immunizationDoseUnit"})
            {
                await page.Locator(selector).ScrollIntoViewIfNeededAsync();
                Assert.True(await page.Locator(selector).EvaluateAsync<bool>("x=>{const r=x.getBoundingClientRect();return r.left>=0&&r.right<=innerWidth&&document.elementFromPoint(r.x+r.width/2,r.y+r.height/2)===x}"));
            }
        }
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth<=innerWidth+1"));
        var dir=Path.Combine(Root,".tmp/step162/screenshots");Directory.CreateDirectory(dir);
        await page.ScreenshotAsync(new(){Path=Path.Combine(dir,$"{name}-{width}.png"),FullPage=true});
    }
    private static async Task<IResult> Render(HttpContext http,Guid patient)
    {
        var action=new ActionContext(http,new RouteData(),new ActionDescriptor());
        var view=http.RequestServices.GetRequiredService<ICompositeViewEngine>().GetView(null,"/Views/Patients/_ImmunizationModal.cshtml",false);Assert.True(view.Success);
        using var writer=new StringWriter();
        await view.View!.RenderAsync(new ViewContext(action,view.View,new ViewDataDictionary(new EmptyModelMetadataProvider(),new ModelStateDictionary()),new TempDataDictionary(http,http.RequestServices.GetRequiredService<ITempDataProvider>()),writer,new HtmlHelperOptions()));
        return Results.Content($"<!doctype html><html><head><meta name='viewport' content='width=device-width,initial-scale=1'><link rel='stylesheet' href='/lib/bootstrap/dist/css/bootstrap.min.css'></head><body><main><div id='patientImmunizationRoot' data-can-manage='true' data-mutation-url='/patients/{patient}/immunizations' data-list-url='/patients/{patient}/immunizations'><button id='addImmunizationButton'>Add Immunization</button><select id='immunizationStatusFilter'><option>All</option></select><div id='immunizationMessage' class='d-none'></div><div id='immunizationList'></div></div></main>"+writer+"<script src='/lib/bootstrap/dist/js/bootstrap.bundle.min.js'></script><script type='module' src='/dist/patients/patient-immunizations.js'></script></body></html>","text/html");
    }
    private sealed class FixtureAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder,FamilyHistoryAcceptanceDatabase.User actor):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var properties=new AuthenticationProperties();properties.StoreTokens([new(){Name="access_token",Value=actor.Subject}]);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,actor.Subject)],Scheme.Name)),properties,Scheme.Name)));
        }
    }
}
