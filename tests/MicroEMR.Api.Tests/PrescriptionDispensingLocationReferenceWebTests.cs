using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Web.Models;
using MicroEMR.Web.Services.PatientPrescriptions;
using MicroEMR.Web.Models.Patients;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class DispensingLocationReferenceWebTests
{
    private static PrescriptionDispensingLocationReference Coding=>new("urn:TEST:薬局<system>"+new string('S',160),"École 🏥 <script>alert(1)</script>"+new string('D',140));
    [Theory] [InlineData("Create")] [InlineData("Edit")] [InlineData("Detail")]
    public async Task ProductionRazorEncodingEntryEditClearAndResponsiveLayout(string view)
    {
        var request=new CreatePrescriptionDraftRequest{DispensingLocationReference=Coding,Indication="Independent Indication",IsCorrectionDraft=true,DispensingLocationReferenceReviewRequired=true};
        request.ReasonCoding=new("urn:IndependentReason","R-01");
        request.ProtocolReference=new("urn:IndependentProtocol","P-01");
        request.TargetDispensingFacilityName="Independent facility";
        request.TargetDispensingFacilityAddress="Independent address";
        object model=view=="Detail"?new PrescriptionDetailViewModel(new PatientPrescriptionResponse{DispensingLocationReference=Coding,Indication=request.Indication,Status="Draft"},null,[]):request;
        var html=await TargetFacilityAddressAcceptanceViewTests.Render($"/Views/PatientPrescriptions/{view}.cshtml",model,"PatientPrescriptions",view);
        Assert.Contains(HtmlEncoder.Default.Encode(Coding.Identifier!),html);if(view!="Detail")Assert.Contains("Independent Indication",html);Assert.DoesNotContain("<script>alert(1)</script>",html);
        var root=new DirectoryInfo(AppContext.BaseDirectory);while(root is not null&&!Directory.Exists(Path.Combine(root.FullName,"src/MicroEMR.Web/Views")))root=root.Parent;Assert.NotNull(root);
        var web=Path.Combine(root!.FullName,"src/MicroEMR.Web/wwwroot");using var playwright=await Playwright.CreateAsync();
        await using var browser=await playwright.Chromium.LaunchAsync(new(){Headless=true,ExecutablePath=Path.Combine(root.FullName,".tmp/step116-browsers/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe")});
        foreach(var width in new[]{1280,360})
        {
            await using var context=await browser.NewContextAsync(new(){ViewportSize=new(){Width=width,Height=900}});await context.RouteAsync("**/*",route=>route.AbortAsync());var page=await context.NewPageAsync();await page.SetContentAsync(html);
            foreach(var css in new[]{"lib/bootstrap/dist/css/bootstrap.min.css","css/microemr-theme.css","css/site.css","css/app-layout.css"})await page.AddStyleTagAsync(new(){Path=Path.Combine(web,css)});
            Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth<=window.innerWidth+1"));
            if(view=="Detail")
            {
                var detail=page.Locator("[data-dispensing-location-reference-detail]");Assert.Contains(Coding.System!,await detail.InnerTextAsync());Assert.Contains(Coding.Identifier!,await detail.InnerTextAsync());Assert.Equal(0,await detail.Locator("script").CountAsync());
            }
            else
            {
                await page.AddScriptTagAsync(new(){Content=(await File.ReadAllTextAsync(Path.Combine(web,"dist/patients/prescription-dispensing-location-reference.js"))).Replace("export {};","")});
                var editor=page.Locator("[data-dispensing-location-reference-editor]");var names=new[]{"DispensingLocationSystem","DispensingLocationIdentifier"};var expected=new[]{Coding.System,Coding.Identifier};
                for(var i=0;i<names.Length;i++){var field=editor.Locator($"[name={names[i]}]");Assert.Equal(expected[i],await field.InputValueAsync());Assert.True(await field.EvaluateAsync<bool>("x=>x.validity.valid && x.labels.length===1"));}
                var indication=page.Locator("[name=Indication]");Assert.Equal(request.Indication,await indication.InputValueAsync());
                await editor.Locator("[name=DispensingLocationSystem]").FillAsync(new string('S',200));await editor.Locator("[name=DispensingLocationIdentifier]").FillAsync(new string('D',200));
                Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth<=window.innerWidth+1"));
                await editor.Locator("[name=DispensingLocationIdentifier]").FillAsync("");Assert.False(await editor.Locator("[name=DispensingLocationIdentifier]").EvaluateAsync<bool>("x=>x.validity.valid"));
                await editor.Locator("[data-dispensing-location-reference-clear]").ClickAsync();foreach(var name in names)Assert.Equal("",await editor.Locator($"[name={name}]").InputValueAsync());Assert.Equal("clear",await editor.Locator("[name=dispensingLocationReferenceOperation]").InputValueAsync());Assert.Equal(request.Indication,await indication.InputValueAsync());
                Assert.Equal(request.ReasonCoding.System,await page.Locator("[name=ReasonSystem]").InputValueAsync());Assert.Equal(request.ReasonCoding.Code,await page.Locator("[name=ReasonCode]").InputValueAsync());
                Assert.Equal(request.ProtocolReference.System,await page.Locator("[name=ProtocolSystem]").InputValueAsync());
                Assert.Equal(request.ProtocolReference.Identifier,await page.Locator("[name=ProtocolIdentifier]").InputValueAsync());
                Assert.Equal(request.TargetDispensingFacilityName,await page.Locator("[name=TargetDispensingFacilityName]").InputValueAsync());
                Assert.Equal(request.TargetDispensingFacilityAddress,await page.Locator("[name=TargetDispensingFacilityAddress]").InputValueAsync());
                Assert.Contains("Manual references are unresolved / unverified.",await editor.InnerTextAsync());
            }
        }
    }

    [Fact]
    public async Task DetailAbsentIsNotRecordedAndOptionalsAreOmitted()
    {
        var row=new PatientPrescriptionResponse{Status="Draft"};
        async Task<string> Render()=>await TargetFacilityAddressAcceptanceViewTests.Render("/Views/PatientPrescriptions/Detail.cshtml",new PrescriptionDetailViewModel(row,null,[]),"PatientPrescriptions","Detail");
        var html=await Render();Assert.Matches("(?s)data-dispensing-location-reference-detail.*?Not recorded",html);
        row.DispensingLocationReference=new("urn:TEST","identifier");html=await Render();var section=System.Text.RegularExpressions.Regex.Match(html,"(?s)<section[^>]*data-dispensing-location-reference-detail.*?</section>").Value;
        Assert.NotEmpty(section);Assert.DoesNotContain("Version",section);Assert.DoesNotContain("Display",section);
    }

    [Theory] [InlineData("preserve",false)] [InlineData("clear",true)] [InlineData("set",true)]
    public async Task WebPostBindsAtomicPresenceBeforeValidation(string operation,bool specified)
    {
        var api=CppSelectivePrintTests.Stub<IPatientPrescriptionApiClient>((_,_)=>throw new InvalidOperationException("No write in validation fixture"));
        var http=new DefaultHttpContext();http.Request.ContentType="application/x-www-form-urlencoded";http.Request.Headers["X-Requested-With"]="XMLHttpRequest";
        http.Request.Form=new FormCollection(new Dictionary<string,Microsoft.Extensions.Primitives.StringValues>{["dispensingLocationReferenceOperation"]=operation,["DispensingLocationSystem"]=Coding.System,["DispensingLocationIdentifier"]=Coding.Identifier});
        var controller=new MicroEMR.Web.Controllers.PatientPrescriptionsController(api){ControllerContext=new(){HttpContext=http}};controller.ModelState.AddModelError("fixture","Stop before persistence");var model=new PrescriptionDraftRequest{Indication="Independent"};
        Assert.IsType<BadRequestObjectResult>(await controller.Edit(Guid.NewGuid(),Guid.NewGuid(),model,default));Assert.Equal(specified,model.DispensingLocationReferenceSpecified);Assert.Equal(operation=="set"?Coding:null,model.DispensingLocationReference);Assert.Equal("Independent",model.Indication);Assert.Equal(operation,controller.ViewData["DispensingLocationReferenceOperation"]);
    }

    [Fact]
    public async Task ModalEditIncludesExactGroupAndIndependentReviewState()
    {
        var api=CppSelectivePrintTests.Stub<IPatientPrescriptionApiClient>((method,_)=>method=="GetAsync"?Task.FromResult<PatientPrescriptionResponse?>(new(){Status="Draft",DispensingLocationReference=Coding,SupersedesPrescriptionUid=Guid.NewGuid(),DispensingLocationReferenceReviewRequired=true,DispensingLocationReferenceReviewed=false}):throw new InvalidOperationException(method));
        var http=new DefaultHttpContext();http.Request.Headers["X-Requested-With"]="XMLHttpRequest";var controller=new MicroEMR.Web.Controllers.PatientPrescriptionsController(api){ControllerContext=new(){HttpContext=http}};
        var result=Assert.IsType<JsonResult>(await controller.Edit(Guid.NewGuid(),Guid.NewGuid(),default));var model=Assert.IsType<PrescriptionDraftRequest>(result.Value);Assert.Equal(Coding,model.DispensingLocationReference);Assert.True(model.IsCorrectionDraft);Assert.True(model.DispensingLocationReferenceReviewRequired);Assert.False(model.DispensingLocationReferenceReviewed);
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(model,new JsonSerializerOptions(JsonSerializerDefaults.Web)));Assert.Equal(Coding.System,json.RootElement.GetProperty("dispensingLocationReference").GetProperty("system").GetString());
    }
}
