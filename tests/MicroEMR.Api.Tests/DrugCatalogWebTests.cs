using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using MicroEMR.Application.DrugCatalog;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Web.Services.PatientPrescriptions;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class DrugCatalogWebTests
{
    [Fact]
    public async Task ExplicitSelectionRequiresConfirmationAndClearsOnManualEditOrModalReset()
    {
        var request = PrescriptionDatePrecisionTests.Draft(); request.ProductName = request.ProductDisplayText = "Manual product";
        var html = await TargetFacilityAddressAcceptanceViewTests.Render("/Views/PatientPrescriptions/Create.cshtml", request, "PatientPrescriptions", "Create");
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "src/MicroEMR.Web/Views"))) root = root.Parent;
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, ExecutablePath = Path.Combine(root!.FullName, ".tmp/step116-browsers/chromium_headless_shell-1228/chrome-headless-shell-win64/chrome-headless-shell.exe") });
        var page = await browser.NewPageAsync(); await page.RouteAsync("**/*", r => r.AbortAsync()); await page.SetContentAsync(html);
        using var archive = DpdTestArchive.Stream(name: "TEST <script>product</script>");
        var facts = Assert.Single((await new MicroEMR.Infrastructure.DrugCatalog.DpdExtractReader().ReadAsync(archive)).Products);
        var product = new CatalogDrug(Guid.NewGuid(), facts, DateTime.UtcNow, new string('A', 64), Convert.ToBase64String(new byte[8]));
        var json = JsonSerializer.Serialize(product, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await page.EvaluateAsync("row=>{window.drugRow=JSON.parse(row);window.fetch=async url=>({ok:true,json:async()=>url.includes('uid=')?window.drugRow:{items:[window.drugRow],hasMore:false}});}", json);
        var script = await File.ReadAllTextAsync(Path.Combine(root.FullName, "src/MicroEMR.Web/wwwroot/dist/patients/prescription-drug-catalog.js"));
        await page.AddScriptTagAsync(new() { Content = script.Replace("export {};", "") });
        var panel = page.Locator("[data-drug-catalog]");
        await panel.Locator("[data-drug-search]").ClickAsync(); await panel.Locator("[data-drug-results] button").ClickAsync();
        await Assertions.Expect(panel.Locator("[data-drug-preview]")).ToBeVisibleAsync();
        await Assertions.Expect(panel.Locator("[data-drug-mismatch]")).ToBeVisibleAsync();
        await Assertions.Expect(panel.Locator("[data-drug-apply]")).ToBeDisabledAsync();
        Assert.Equal("Manual product", await page.Locator("[name=ProductName]").InputValueAsync());
        Assert.Equal(0, await panel.Locator("[data-drug-details] script").CountAsync());
        await panel.Locator("[data-drug-cancel]").ClickAsync(); Assert.Equal("Manual product", await page.Locator("[name=ProductName]").InputValueAsync());
        await panel.Locator("[data-drug-results] button").ClickAsync(); await panel.Locator("[data-drug-confirm]").CheckAsync();
        var route = await page.Locator("[name=Route]").InputValueAsync();
        await panel.Locator("[data-drug-apply]").ClickAsync();
        Assert.Equal(facts.Name, await page.Locator("[name=ProductName]").InputValueAsync());
        Assert.Equal("00000001", await page.Locator("[name=ProductIdentifierValue]").InputValueAsync());
        Assert.Equal(product.ProductUid.ToString(), await panel.Locator("[name=DrugCatalogSelectionUid]").InputValueAsync());
        Assert.Equal(route, await page.Locator("[name=Route]").InputValueAsync());
        await page.Locator("[name=ProductName]").FillAsync("Manual replacement"); Assert.Equal("", await panel.Locator("[name=DrugCatalogSelectionUid]").InputValueAsync());
        await page.EvaluateAsync("()=>{window.fetch=()=>new Promise(resolve=>window.completeDrugSearch=resolve);}");
        await panel.Locator("[data-drug-search]").ClickAsync(); await panel.EvaluateAsync("p=>p.closest('form').dispatchEvent(new Event('prescription-dates-loaded'))");
        await page.EvaluateAsync("window.completeDrugSearch({ok:true,json:async()=>({items:[window.drugRow],hasMore:false})})");
        await Assertions.Expect(panel.Locator("[data-drug-results] button")).ToHaveCountAsync(0);
        await page.EvaluateAsync("window.fetch=async()=>({ok:false})"); await panel.Locator("[data-drug-search]").ClickAsync();
        await Assertions.Expect(panel.Locator("[data-drug-status]")).ToContainTextAsync("unavailable");
    }

    [Fact]
    public async Task RecordedDetailIsEncodedAndManualDetailRemainsCompatible()
    {
        using var archive = DpdTestArchive.Stream(name: "TEST <script>source</script>");
        var facts = Assert.Single((await new MicroEMR.Infrastructure.DrugCatalog.DpdExtractReader().ReadAsync(archive)).Products);
        var row = new PatientPrescriptionResponse { Status = "Finalized", ProductDisplayText = "Historical product", CatalogDrug = new(Guid.NewGuid(), facts, DateTime.UtcNow, new string('A', 64), Convert.ToBase64String(new byte[8])) };
        var html = await TargetFacilityAddressAcceptanceViewTests.Render("/Views/PatientPrescriptions/Detail.cshtml", new MicroEMR.Web.Models.PrescriptionDetailViewModel(row, null, []), "PatientPrescriptions", "Detail");
        Assert.Contains("00000001", html); Assert.Contains("TEST &lt;script&gt;source&lt;/script&gt;", html); Assert.DoesNotContain("TEST <script>source</script>", html);
        Assert.Contains("TEST INGREDIENT", html); Assert.Contains("Recorded drug catalogue selection", html);
        row.CatalogDrug = null;
        var manual = await TargetFacilityAddressAcceptanceViewTests.Render("/Views/PatientPrescriptions/Detail.cshtml", new MicroEMR.Web.Models.PrescriptionDetailViewModel(row, null, []), "PatientPrescriptions", "Detail");
        Assert.DoesNotContain("data-drug-catalog-detail", manual); Assert.Contains("Historical product", manual);
        var import = await TargetFacilityAddressAcceptanceViewTests.Render("/Views/DrugCatalog/Import.cshtml", new object(), "DrugCatalog", "Import");
        Assert.Contains("__RequestVerificationToken", import);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WebBindsOnlyCompleteSelectionOrShowsValidationError(bool valid)
    {
        var api = CppSelectivePrintTests.Stub<IPatientPrescriptionApiClient>((_, _) => throw new InvalidOperationException("No persistence expected"));
        var http = new DefaultHttpContext(); http.Request.ContentType = "application/x-www-form-urlencoded"; http.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        var uid = Guid.NewGuid();
        http.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues> { ["DrugCatalogSelectionUid"] = valid ? uid.ToString() : "bad", ["DrugCatalogSelectionVersion"] = Convert.ToBase64String(new byte[8]) });
        var controller = new MicroEMR.Web.Controllers.PatientPrescriptionsController(api) { ControllerContext = new() { HttpContext = http } };
        controller.ModelState.AddModelError("stop", "No persistence"); var model = new PrescriptionDraftRequest();
        Assert.IsType<BadRequestObjectResult>(await controller.Edit(Guid.NewGuid(), Guid.NewGuid(), model, default));
        Assert.Equal(valid, model.DrugCatalogSelection is not null); if (valid) Assert.Equal(uid, model.DrugCatalogSelection!.ProductUid);
    }
}
