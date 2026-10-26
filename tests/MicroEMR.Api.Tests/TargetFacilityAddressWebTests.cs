using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Models;
using MicroEMR.Web.Services.PatientPrescriptions;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class TargetFacilityAddressWebTests
{
    [Theory]
    [InlineData("Create")] [InlineData("Edit")] [InlineData("Detail")]
    public async Task ProductionViewsRenderNameAsEncodedTextWithoutChangingOutputScope(string viewName)
    {
        var name="薬局 <branch> & O'Brien\n10 Rue  École\n"+new string('X',900);
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{ApplicationName=typeof(PrescriptionDetailViewModel).Assembly.GetName().Name,ContentRootPath=AppContext.BaseDirectory});
        builder.Services.AddControllersWithViews();builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddSingleton(CppSelectivePrintTests.Stub<IWebPermissionService>((_,_)=>Task.FromResult(false)));
        builder.Services.AddSingleton(CppSelectivePrintTests.Stub<IWebPlatformEntitlementAccessor>((_,_)=>throw new InvalidOperationException("Anonymous renderer must not request entitlements")));
        await using var app=builder.Build();using var scope=app.Services.CreateScope();var services=scope.ServiceProvider;
        var http=new DefaultHttpContext{RequestServices=services};http.SetEndpoint(new Endpoint(_=>Task.CompletedTask,new EndpointMetadataCollection(),"Facility view fixture"));
        var route=new RouteData();route.Values["controller"]="PatientPrescriptions";route.Values["action"]=viewName;
        var action=new ActionContext(http,route,new ActionDescriptor{RouteValues=new Dictionary<string,string?>{{"controller","PatientPrescriptions"},{"action",viewName}}});
        var view=services.GetRequiredService<ICompositeViewEngine>().GetView(null,$"/Views/PatientPrescriptions/{viewName}.cshtml",false);Assert.True(view.Success);
        var draft=new CreatePrescriptionDraftRequest{TargetDispensingFacilityAddress=name,IsCorrectionDraft=true,FacilityAddressReviewRequired=true};
        object model=viewName=="Detail"?new PrescriptionDetailViewModel(new PatientPrescriptionResponse{TargetDispensingFacilityAddress=name,Status="Draft"},null,[]):draft;
        var data=new ViewDataDictionary(new EmptyModelMetadataProvider(),new ModelStateDictionary()){Model=model};
        using var writer=new StringWriter();await view.View.RenderAsync(new ViewContext(action,view.View,data,new TempDataDictionary(http,services.GetRequiredService<ITempDataProvider>()),writer,new HtmlHelperOptions()));
        var html=writer.ToString();Assert.Contains("Target Dispensing Facility Address",html);Assert.Contains(HtmlEncoder.Default.Encode(name),html);Assert.DoesNotContain("<branch>",html);if(viewName=="Detail")Assert.Contains("white-space: pre-wrap; overflow-wrap: anywhere;",html);
        if(viewName!="Detail")
        {
            Assert.Contains("name=\"facilityAddressOperation\"",html);Assert.Contains("name=\"TargetDispensingFacilityAddress\"",html);
            Assert.Contains("name=\"FacilityAddressReviewed\"",html);Assert.Contains("data-facility-address-review",html);
            var input=System.Text.RegularExpressions.Regex.Match(html,"<textarea\\b[^>]*name=\"TargetDispensingFacilityAddress\"[^>]*>").Value;
            Assert.NotEmpty(input);Assert.DoesNotContain("maxlength",input); // No silent browser paste truncation of this field.
        }
        await VerifyBrowser(viewName,html,name);
    }

    private static async Task VerifyBrowser(string viewName,string html,string value)
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(root is not null && !Directory.Exists(Path.Combine(root.FullName,"src/MicroEMR.Web/Views")))root=root.Parent;
        Assert.NotNull(root);
        var webRoot=Path.Combine(root!.FullName,"src/MicroEMR.Web/wwwroot");
        using var playwright=await Playwright.CreateAsync();
        await using var browser=await playwright.Chromium.LaunchAsync(new(){Headless=true,ExecutablePath=Path.Combine(root.FullName,".tmp/step116-browsers/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe")});
        foreach(var width in new[]{1280,360})
        {
            await using var context=await browser.NewContextAsync(new(){ViewportSize=new(){Width=width,Height=900}});
            await context.RouteAsync("**/*",route=>route.AbortAsync()); // Rendered synthetic HTML only; no application/API requests or saves.
            var page=await context.NewPageAsync();
            await page.SetContentAsync(html);
            foreach(var css in new[]{"lib/bootstrap/dist/css/bootstrap.min.css","css/microemr-theme.css","css/site.css","css/app-layout.css"})
                await page.AddStyleTagAsync(new(){Path=Path.Combine(webRoot,css)});
            Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth+1"));
            if(viewName=="Detail")
            {
                var address=page.Locator("span[style*='white-space: pre-wrap']");
                Assert.Equal(value,await address.TextContentAsync());
                Assert.Equal("pre-wrap",await address.EvaluateAsync<string>("x=>getComputedStyle(x).whiteSpace"));
                Assert.Equal(0,await address.Locator("branch").CountAsync());
            }
            else
            {
                var module=await File.ReadAllTextAsync(Path.Combine(webRoot,"dist/patients/prescription-facility-address.js"));
                await page.AddScriptTagAsync(new(){Content=module.Replace("export {};","")});
                var editor=page.Locator("[data-facility-address-editor]");var textarea=editor.Locator("textarea");
                Assert.Equal(value,await textarea.InputValueAsync());
                Assert.True(await textarea.EvaluateAsync<bool>("x=>x.validity.valid && x.labels.length===1"));
                await textarea.FillAsync("Pharmacie Étoile 薬局\n12 Rue  École");
                Assert.Equal("set",await editor.Locator("[name=facilityAddressOperation]").InputValueAsync());
                Assert.False(await editor.Locator("[name=FacilityAddressReviewed]").IsCheckedAsync());
                await editor.Locator("[data-facility-address-clear]").ClickAsync();
                Assert.Equal("",await textarea.InputValueAsync());
                Assert.Equal("clear",await editor.Locator("[name=facilityAddressOperation]").InputValueAsync());
            }
        }
    }

    [Theory]
    [InlineData("preserve",false,null)] [InlineData("clear",true,null)] [InlineData("set",true,"薬局 École")]
    public async Task ExistingWebPostMapsPresenceBeforeValidation(string operation,bool specified,string? expected)
    {
        var api=CppSelectivePrintTests.Stub<IPatientPrescriptionApiClient>((_,_)=>throw new InvalidOperationException("No API write expected in validation fixture"));
        var http=new DefaultHttpContext();http.Request.ContentType="application/x-www-form-urlencoded";http.Request.Headers["X-Requested-With"]="XMLHttpRequest";
        http.Request.Form=new FormCollection(new Dictionary<string,Microsoft.Extensions.Primitives.StringValues>{["facilityAddressOperation"]=operation,["TargetDispensingFacilityAddress"]="薬局 École"});
        var controller=new MicroEMR.Web.Controllers.PatientPrescriptionsController(api){ControllerContext=new(){HttpContext=http}};
        controller.ModelState.AddModelError("fixture","Stop before persistence.");var model=new PrescriptionDraftRequest();
        Assert.IsType<BadRequestObjectResult>(await controller.Edit(Guid.NewGuid(),Guid.NewGuid(),model,default));
        Assert.Equal(specified,model.TargetDispensingFacilityAddressSpecified);Assert.Equal(expected,model.TargetDispensingFacilityAddress);
        Assert.Equal(operation,controller.ViewData["FacilityAddressOperation"]);
    }

    [Fact]
    public async Task ExistingModalEditCarriesExactNameAndReviewState()
    {
        var api=CppSelectivePrintTests.Stub<IPatientPrescriptionApiClient>((method,_)=>method=="GetAsync"?Task.FromResult<PatientPrescriptionResponse?>(new(){Status="Draft",TargetDispensingFacilityAddress="Pharmacie 薬局",SupersedesPrescriptionUid=Guid.NewGuid(),FacilityAddressReviewRequired=true,FacilityAddressReviewed=false}):throw new InvalidOperationException(method));
        var http=new DefaultHttpContext();http.Request.Headers["X-Requested-With"]="XMLHttpRequest";
        var controller=new MicroEMR.Web.Controllers.PatientPrescriptionsController(api){ControllerContext=new(){HttpContext=http}};
        var result=Assert.IsType<JsonResult>(await controller.Edit(Guid.NewGuid(),Guid.NewGuid(),default));var model=Assert.IsType<PrescriptionDraftRequest>(result.Value);
        Assert.Equal("Pharmacie 薬局",model.TargetDispensingFacilityAddress);Assert.True(model.TargetDispensingFacilityAddressSpecified);Assert.True(model.IsCorrectionDraft);Assert.True(model.FacilityAddressReviewRequired);Assert.False(model.FacilityAddressReviewed);
    }
}
