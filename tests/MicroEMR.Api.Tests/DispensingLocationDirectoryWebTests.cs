using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Web.Services.PatientPrescriptions;
using MicroEMR.Web.Controllers;
using MicroEMR.Web.Authorization;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class DispensingDirectoryWebTests
{
    [Theory] [InlineData("Create",360)] [InlineData("Edit",1280)]
    public async Task LookupPreviewCancelConfirmationManualAndStaleStates(string view,int width)
    {
        var request=new CreatePrescriptionDraftRequest{TargetDispensingFacilityName="Manual facility",TargetDispensingFacilityAddress="Manual address",DispensingLocationReference=new("manual","reference")};
        var html=await TargetFacilityAddressAcceptanceViewTests.Render($"/Views/PatientPrescriptions/{view}.cshtml",request,"PatientPrescriptions",view);
        var root=Root();var web=Path.Combine(root,"src/MicroEMR.Web/wwwroot");using var playwright=await Playwright.CreateAsync();
        await using var browser=await playwright.Chromium.LaunchAsync(new(){Headless=true,ExecutablePath=Path.Combine(root,".tmp/step116-browsers/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe")});
        var page=await browser.NewPageAsync(new(){ViewportSize=new(){Width=width,Height=900}});await page.RouteAsync("**/*",route=>route.AbortAsync());await page.SetContentAsync(html);
        foreach(var css in new[]{"lib/bootstrap/dist/css/bootstrap.min.css","css/site.css","css/app-layout.css"})await page.AddStyleTagAsync(new(){Path=Path.Combine(web,css)});
        await page.AddScriptTagAsync(new(){Content=await File.ReadAllTextAsync(Path.Combine(web,"dist/patients/prescription-dispensing-location-reference.js")) is {} script?script.Replace("export {};",""):""});
        var location=new{locationUid=Guid.NewGuid(),system="urn:TEST:<system>",identifier="Ab-01",facilityName="Directory <script>facility</script>",facilityAddress="TEST address\nToronto",phone="555",isActive=true,rowVersion=Convert.ToBase64String(new byte[8])};
        await page.EvaluateAsync("row=>{window.testRow=row;window.testInactive=false;window.fetch=async url=>({ok:true,json:async()=>url.includes('uid=')?{...window.testRow,isActive:!window.testInactive}:{items:[window.testRow],hasMore:false}});}",location);
        await page.AddScriptTagAsync(new(){Content=(await File.ReadAllTextAsync(Path.Combine(web,"dist/patients/prescription-location-lookup.js"))).Replace("export {};","")});
        var panel=page.Locator("[data-location-lookup]");await panel.Locator("[data-location-query]").FillAsync("TEST");await panel.Locator("[data-location-search]").ClickAsync();await panel.Locator("[data-location-results] button").First.ClickAsync();
        await Assertions.Expect(panel.Locator("[data-location-preview]")).ToBeVisibleAsync();Assert.Contains(location.facilityAddress,await panel.Locator("[data-location-details]").InnerTextAsync());Assert.Equal(0,await panel.Locator("[data-location-details] script").CountAsync());await Assertions.Expect(panel.Locator("[data-location-mismatch]")).ToBeVisibleAsync();
        Assert.Equal("manual",await page.Locator("[name=DispensingLocationSystem]").InputValueAsync());await panel.Locator("[data-location-cancel]").ClickAsync();Assert.Equal("Manual facility",await page.Locator("[name=TargetDispensingFacilityName]").InputValueAsync());Assert.Equal("manual",await page.Locator("[name=DispensingLocationSystem]").InputValueAsync());
        await panel.Locator("[data-location-results] button").First.ClickAsync();await panel.Locator("[data-location-apply]").ClickAsync();Assert.Equal(location.system,await page.Locator("[name=DispensingLocationSystem]").InputValueAsync());Assert.Equal("Manual facility",await page.Locator("[name=TargetDispensingFacilityName]").InputValueAsync());Assert.Equal(location.locationUid.ToString(),await page.Locator("[name=DispensingLocationSelectionUid]").InputValueAsync());
        await panel.Locator("[data-location-results] button").First.ClickAsync();await panel.Locator("[data-location-populate]").CheckAsync();await panel.Locator("[data-location-apply]").ClickAsync();Assert.Equal(location.facilityName,await page.Locator("[name=TargetDispensingFacilityName]").InputValueAsync());Assert.Equal(location.facilityAddress,await page.Locator("[name=TargetDispensingFacilityAddress]").InputValueAsync());
        Assert.True(await panel.Locator("[data-location-query]").EvaluateAsync<bool>("x=>x.labels.length===1"));Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth<=window.innerWidth+1"));
        await page.Locator("[name=DispensingLocationIdentifier]").FillAsync("Manual change");Assert.Equal("",await page.Locator("[name=DispensingLocationSelectionUid]").InputValueAsync());Assert.Contains("unresolved",await panel.Locator("[data-location-status]").InnerTextAsync());
        await page.EvaluateAsync("window.testInactive=true");await panel.Locator("[data-location-results] button").First.ClickAsync();await Assertions.Expect(panel.Locator("[data-location-status]")).ToContainTextAsync("inactive");await Assertions.Expect(panel.Locator("[data-location-preview]")).ToBeHiddenAsync();
        await page.EvaluateAsync("window.fetch=async()=>({ok:false})");await panel.Locator("[data-location-search]").ClickAsync();await Assertions.Expect(panel.Locator("[data-location-status]")).ToContainTextAsync("unavailable");
        await page.EvaluateAsync("()=>{window.fetch=()=>new Promise(resolve=>window.completeSearch=resolve);}");await panel.Locator("[data-location-search]").ClickAsync();
        await panel.EvaluateAsync("x=>x.closest('form').dispatchEvent(new CustomEvent('prescription-dates-loaded',{bubbles:true,detail:{}}))");
        await page.EvaluateAsync("window.completeSearch({ok:true,json:async()=>({items:[window.testRow],hasMore:false})})");await Assertions.Expect(panel.Locator("[data-location-results] button")).ToHaveCountAsync(0);Assert.Equal("",await page.Locator("[name=DispensingLocationSelectionUid]").InputValueAsync());
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task WebPostBindsSelectionAlongsideAtomicReference(bool valid)
    {
        var api=CppSelectivePrintTests.Stub<IPatientPrescriptionApiClient>((_,_)=>throw new InvalidOperationException("No persistence"));var http=new DefaultHttpContext();http.Request.ContentType="application/x-www-form-urlencoded";http.Request.Headers["X-Requested-With"]="XMLHttpRequest";var uid=Guid.NewGuid();
        http.Request.Form=new FormCollection(new Dictionary<string,Microsoft.Extensions.Primitives.StringValues>{["dispensingLocationReferenceOperation"]="set",["DispensingLocationSystem"]="S",["DispensingLocationIdentifier"]="I",["DispensingLocationSelectionUid"]=valid?uid.ToString():"invalid",["DispensingLocationSelectionVersion"]=Convert.ToBase64String(new byte[8])});
        var controller=new MicroEMR.Web.Controllers.PatientPrescriptionsController(api){ControllerContext=new(){HttpContext=http}};controller.ModelState.AddModelError("fixture","Stop");var model=new PrescriptionDraftRequest();Assert.IsType<BadRequestObjectResult>(await controller.Edit(Guid.NewGuid(),Guid.NewGuid(),model,default));Assert.Equal(valid,model.DispensingLocationSelection is not null);if(valid)Assert.Equal(uid,model.DispensingLocationSelection!.LocationUid);Assert.True(model.DispensingLocationReferenceSpecified);
    }

    [Fact]
    public async Task StaleSelectionSaveShowsActionableDirectoryMessage()
    {
        const string message="Dispensing location directory selection changed, is inactive or invalid. Search and select the location again.";
        var api=CppSelectivePrintTests.Stub<IPatientPrescriptionApiClient>((_,_)=>throw new MicroEMR.Web.Services.SafeApiResponseException(System.Net.HttpStatusCode.Conflict,JsonSerializer.Serialize(new{message})));
        var http=new DefaultHttpContext();http.Request.Headers["X-Requested-With"]="XMLHttpRequest";
        var controller=new MicroEMR.Web.Controllers.PatientPrescriptionsController(api){ControllerContext=new(){HttpContext=http}};
        var result=Assert.IsType<BadRequestObjectResult>(await controller.Edit(Guid.NewGuid(),Guid.NewGuid(),new(),default));Assert.Contains(message,JsonSerializer.Serialize(result.Value));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task FullDetailDistinguishesRecordedSelectionFromUnresolvedReference(bool selected)
    {
        var row=new PatientPrescriptionResponse{Status="Finalized",DispensingLocationDirectoryUid=selected?Guid.NewGuid():null,DispensingLocationReference=new("urn:TEST","Ab-01"),TargetDispensingFacilityName="Historical name",TargetDispensingFacilityAddress="Historical address"};
        var html=await TargetFacilityAddressAcceptanceViewTests.Render("/Views/PatientPrescriptions/Detail.cshtml",new MicroEMR.Web.Models.PrescriptionDetailViewModel(row,null,[]),"PatientPrescriptions","Detail");Assert.Contains(selected?"directory selection was checked when saved":"unresolved / unverified",html);Assert.Contains("Historical name",html);Assert.Contains("Historical address",html);Assert.DoesNotContain("data-location-lookup",html);
    }

    [Fact]
    public async Task MaintenanceViewsEncodeDetailsAndArePermissionProtected()
    {
        var row=new DispensingLocation(Guid.NewGuid(),"urn:<TEST>","Ab-01","<script>facility</script>","123 TEST\nToronto",null,true,DateTime.UtcNow,7,null,null,Convert.ToBase64String(new byte[8]));
        var html=await TargetFacilityAddressAcceptanceViewTests.Render("/Views/DispensingLocations/Index.cshtml",new DispensingLocationIndex(new([row],1,20,false),null,false),"DispensingLocations","Index");Assert.DoesNotContain("<script>facility</script>",html);Assert.Contains("&lt;script&gt;facility&lt;/script&gt;",html);
        foreach(var uid in new Guid?[]{null,row.LocationUid})
        {
            var form=await TargetFacilityAddressAcceptanceViewTests.Render("/Views/DispensingLocations/Edit.cshtml",new DispensingLocationEdit(uid,new(){System=row.System,Identifier=row.Identifier,FacilityName=row.FacilityName,FacilityAddress=row.FacilityAddress,RowVersion=row.RowVersion}),"DispensingLocations",uid.HasValue?"Edit":"Add");Assert.Contains("name=\"RowVersion\"",form);Assert.Contains("&lt;script&gt;facility&lt;/script&gt;",form);Assert.Contains("__RequestVerificationToken",form);
        }
        var type=typeof(MicroEMR.Web.Controllers.DispensingLocationsController);
        foreach(var method in type.GetMethods().Where(x=>x.DeclaringType==type)){var permission=Assert.Single(method.GetCustomAttributes(typeof(RequireWebPermissionAttribute),true).Cast<RequireWebPermissionAttribute>());Assert.Contains(method.Name is "Lookup" or "LookupDetail"?PermissionKeys.PrescriptionsPrescribe:PermissionKeys.ClinicSettingsManage,permission.Policy!);}
    }
    private static string Root(){var root=new DirectoryInfo(AppContext.BaseDirectory);while(root is not null&&!Directory.Exists(Path.Combine(root.FullName,"src/MicroEMR.Web/Views")))root=root.Parent;return root!.FullName;}
}
