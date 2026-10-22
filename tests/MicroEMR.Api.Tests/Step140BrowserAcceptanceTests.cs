using System.Text.RegularExpressions;
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
using MicroEMR.Application.PatientAllergies.Contracts;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Models;
using Xunit;

namespace MicroEMR.Api.Tests;

// In-memory production Razor + installed Chromium. No live host, HTTP API or database.
public sealed class Step140BrowserAcceptanceTests : IAsyncLifetime
{
    private IPlaywright playwright = null!;
    private IBrowser browser = null!;
    private static string Root => FindRoot();

    public async Task InitializeAsync()
    {
        playwright = await Playwright.CreateAsync();
        browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
    }

    public async Task DisposeAsync()
    {
        if (browser is not null) await browser.DisposeAsync();
        playwright?.Dispose();
    }

    [Fact]
    public async Task DescriptionEditorPreservesCompoundAndIndependentNamesAndEnforcesBrowserBoundary()
    {
        const string compound = "TEST ONLY A 1%; B 2 mg/mL; <preparation> & base";
        var model = new PrescriptionDraftRequest { ProductName = "Separate brand", GenericName = "Separate generic", ProductDisplayText = compound };
        var page = await Page(await Render("/Views/PatientPrescriptions/_Form.cshtml", model));
        var description = page.Locator("[name='ProductDisplayText']");
        Assert.Equal(compound, await description.InputValueAsync());
        Assert.Equal("Separate brand", await page.Locator("[name='ProductName']").InputValueAsync());
        Assert.Equal("Separate generic", await page.Locator("[name='GenericName']").InputValueAsync());
        Assert.Contains("Drug Description / Product display", await page.Locator("label[for='ProductDisplayText']").InnerTextAsync());
        Assert.Contains("Single line", await page.Locator("#drug-description-help").InnerTextAsync());
        Assert.Equal(0, await page.Locator("preparation").CountAsync());
        await description.FillAsync("");
        await description.PressSequentiallyAsync(new string('A', 300));
        Assert.Equal(300, (await description.InputValueAsync()).Length);
        await description.PressSequentiallyAsync("A");
        Assert.Equal(300, (await description.InputValueAsync()).Length); // Client prevention, not server rejection.
        await description.FillAsync("TEST ONLY first\r\nsecond");
        Assert.Equal("TEST ONLY first second", await description.InputValueAsync()); // Chromium fill normalizes CR/LF to a space; not server rejection.
        Assert.Equal(new[] { "", "true", "false" }, await page.Locator("[name='OrderFields.SubstitutionProhibited'] option").EvaluateAllAsync<string[]>("xs => xs.map(x => x.value)"));
    }

    [Fact]
    public async Task InvalidEditorRenderRetainsAttemptedTextAndExposesValidationFeedback()
    {
        const string attempted = "TEST ONLY <compound> & unsaved content";
        var page = await Page(await Render("/Views/PatientPrescriptions/_Form.cshtml", new PrescriptionDraftRequest(), state =>
        {
            state.SetModelValue("ProductDisplayText", attempted, attempted);
            state.AddModelError("ProductDisplayText", "TEST ONLY validation error; review description.");
        }));
        Assert.Equal(attempted, await page.Locator("[name='ProductDisplayText']").InputValueAsync());
        Assert.Contains("review description", await page.Locator("[data-valmsg-for='ProductDisplayText']").InnerTextAsync());
        Assert.True(await page.Locator("[role='alert']").IsVisibleAsync());
    }

    [Theory]
    [InlineData("Draft", true)] [InlineData("Draft", false)] [InlineData("Draft", null)]
    [InlineData("Finalized", true)] [InlineData("Finalized", false)] [InlineData("Finalized", null)]
    public async Task ReadOnlyDetailsUseRetainedIdentityAndDistinctOrderStates(string status, bool? prohibited)
    {
        var rx = new PatientPrescriptionResponse
        {
            Status = status, ProductName = "Separate brand", ProductDisplayText = "TEST ONLY A 1%; B 2%; <text> & base",
            TreatmentType = prohibited is null ? null : "TEST ONLY supplied treatment",
            SubstitutionProhibited = prohibited, PrescriberFirstNameSnapshot = prohibited is null ? null : "Original",
            PrescriberLastNameSnapshot = prohibited is null ? null : "Prescriber", PrescriberOhipSnapshot = prohibited is null ? null : "001234"
        };
        var page = await Page(await Render("/Views/PatientPrescriptions/Detail.cshtml", new PrescriptionDetailViewModel(rx, null, [])));
        Assert.Equal(1, await page.Locator("body.app-body #appSidebar").CountAsync());
        var main = page.Locator("main.app-main");
        Assert.Equal(1, await main.Locator(".content-panel-header").CountAsync());
        var text = await main.InnerTextAsync();
        Assert.Contains(rx.ProductDisplayText, text);
        Assert.Contains("Substitution prohibited: " + (prohibited is null ? "Not recorded" : prohibited.Value ? "Yes" : "No"), text);
        Assert.Contains("OHIP Billing Number: " + (rx.PrescriberOhipSnapshot ?? "Not recorded"), text);
        Assert.Contains("Prescriber first name: " + (rx.PrescriberFirstNameSnapshot ?? "Not recorded"), text);
        Assert.Contains("Last name: " + (rx.PrescriberLastNameSnapshot ?? "Not recorded"), text);
        Assert.Contains("Treatment type: " + (rx.TreatmentType ?? "Not recorded"), text);
        Assert.Equal(0, await main.Locator("input, select, textarea, form, text").CountAsync());
    }

    [Fact]
    public async Task AllergyProductionControlsPreserveDinAndLegacyTypeAcrossApplicabilityChanges()
    {
        var request = new CreatePatientAllergyRequest { ClinicalFields = new() { DrugApplicability = "Drug", DrugDIN = "00123456" } };
        var html = await Render("/Views/Shared/_AllergyClinicalFields.cshtml", request);
        var page = await Page("<form><select name='AllergenType'><option>Contrast</option><option>Medication</option></select>" + html + "</form>");
        await page.AddScriptTagAsync(new() { Content = File.ReadAllText(Path.Combine(Root, "src/MicroEMR.Web/wwwroot/dist/patients/patient-allergies.js")).Replace("export {};", "") });
        await page.EvaluateAsync("document.dispatchEvent(new Event('DOMContentLoaded'))");
        var applicability = page.Locator("[name='ClinicalFields.DrugApplicability']");
        var din = page.Locator("[name='ClinicalFields.DrugDIN']");
        Assert.Equal(new[] { "Not recorded", "Drug", "Non-drug", "Unknown" }, await applicability.Locator("option").AllTextContentsAsync());
        foreach (var value in new[] { "Non-drug", "Unknown", "", "Drug" })
        {
            await applicability.SelectOptionAsync(value);
            Assert.Equal("00123456", await din.InputValueAsync());
            Assert.False(await din.IsDisabledAsync());
            Assert.False(await page.Locator("[data-allergy-din]").EvaluateAsync<bool>("x => x.classList.contains('d-none')"));
            Assert.Equal("Contrast", await page.Locator("[name='AllergenType']").InputValueAsync());
        }
        await din.FillAsync("");
        await applicability.SelectOptionAsync("Unknown");
        Assert.True(await page.Locator("[data-allergy-din]").EvaluateAsync<bool>("x => x.classList.contains('d-none')"));
        await applicability.SelectOptionAsync("Drug");
        Assert.False(await page.Locator("[data-allergy-din]").EvaluateAsync<bool>("x => x.classList.contains('d-none')"));
    }

    [Theory]
    [InlineData("PatientMedications/Create.cshtml")]
    [InlineData("PatientMedications/Edit.cshtml")]
    [InlineData("Patients/_SummaryQuickActionModals.cshtml")]
    [InlineData("Patients/_EditMedicationModal.cshtml")]
    public async Task MedicationIntentControlsRemainIndependentAndModalLoadsFalseAndUnknown(string view)
    {
        // These literal selects have no Razor expressions. Exercise the actual source controls,
        // without claiming a full authenticated chart or Bootstrap modal end-to-end test.
        var source = File.ReadAllText(Path.Combine(Root, "src/MicroEMR.Web/Views", view));
        string Select(string name) => Regex.Match(source, "<select\\b[^>]*(?:asp-for|name)=\"" + Regex.Escape(name) + "\"[^>]*>.*?</select>", RegexOptions.Singleline).Value.Replace("asp-for=", "name=");
        var intentHtml = Select("PlanFields.LongTermIntent");
        var classificationHtml = Select("Classification");
        Assert.NotEmpty(intentHtml); Assert.NotEmpty(classificationHtml);
        var page = await Page("<form id='editMedicationForm'>" + intentHtml + classificationHtml + "</form>");
        var intent = page.Locator("[name='PlanFields.LongTermIntent']");
        var classification = page.Locator("[name='Classification']");
        await page.AddScriptTagAsync(new() { Content = File.ReadAllText(Path.Combine(Root, "src/MicroEMR.Web/wwwroot/dist/patients/medication-classification.js")).Replace("export function", "function") });
        foreach (var category in new[] { "Ongoing", "Acute", "" })
        foreach (var value in new[] { "true", "false", "" })
        {
            await classification.SelectOptionAsync(category);
            await intent.SelectOptionAsync(value);
            Assert.Equal(category, await classification.InputValueAsync());
            Assert.Equal(value, await intent.InputValueAsync());
            // Start with a different value so a missing/broken loader cannot pass this assertion.
            await intent.SelectOptionAsync(value == "true" ? "false" : "true");
            await page.EvaluateAsync("value => document.querySelector('form').dispatchEvent(new CustomEvent('medication-loaded', {detail:{planFields:{longTermIntent:value}}}))", value == "" ? (bool?)null : bool.Parse(value));
            Assert.Equal(value, await intent.InputValueAsync());
            Assert.Equal(category, await classification.InputValueAsync());
        }
    }

    private async Task<IPage> Page(string html)
    {
        var page = await browser.NewPageAsync();
        await page.RouteAsync("**/*", route => route.AbortAsync()); // No live services or external assets.
        await page.SetContentAsync(html);
        return page;
    }

    private static async Task<string> Render<T>(string path, T model, Action<ModelStateDictionary>? errors = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(PrescriptionDetailViewModel).Assembly.GetName().Name, ContentRootPath = AppContext.BaseDirectory });
        builder.Services.AddControllersWithViews();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddSingleton(CppSelectivePrintTests.Stub<IWebPermissionService>((_, _) => throw new InvalidOperationException("Private permission access not expected")));
        builder.Services.AddSingleton(CppSelectivePrintTests.Stub<IWebPlatformEntitlementAccessor>((_, _) => throw new InvalidOperationException("Anonymous rendering must not request entitlements")));
        await using var app = builder.Build();
        using var scope = app.Services.CreateScope();
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "Browser render test"));
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var view = scope.ServiceProvider.GetRequiredService<ICompositeViewEngine>().GetView(null, path, false);
        Assert.True(view.Success, path);
        var data = new ViewDataDictionary<T>(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };
        data["CanEditPrivateNote"] = false;
        errors?.Invoke(data.ModelState);
        using var writer = new StringWriter();
        await view.View.RenderAsync(new ViewContext(action, view.View, data, new TempDataDictionary(http, scope.ServiceProvider.GetRequiredService<ITempDataProvider>()), writer, new HtmlHelperOptions()));
        return writer.ToString();
    }

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "MicroEMR.sln")) || Directory.Exists(Path.Combine(dir.FullName, "src/MicroEMR.Web/Views"))) return dir.FullName;
        throw new DirectoryNotFoundException("MicroEMR repository root not found");
    }
}
