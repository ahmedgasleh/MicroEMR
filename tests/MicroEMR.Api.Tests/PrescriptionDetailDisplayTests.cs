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
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Web.Models;
using MicroEMR.Web.Authorization;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PrescriptionDetailDisplayTests
{
    public static IEnumerable<object?[]> RecordedNames()
    {
        foreach (var status in new[] { "Draft", "Finalized", "Cancelled", "Superseded" })
        {
            yield return [status, "Recorded brand", "Independent product display", "Different generic"];
            yield return [status, "Provider-typed <medicine> & name", "Independent product display", "Different generic"];
            yield return [status, "Recorded brand", "", "Different generic"];
            yield return [status, "", "Independent product display", "Different generic"];
            yield return [status, null, "Independent product display", "Different generic"];
            yield return [status, "Recorded brand", "Independent product display", null];
        }
    }

    [Theory]
    [MemberData(nameof(RecordedNames))]
    public async Task ReadOnlyDetailsDisplaysRecordedNameWithoutSubstitution(
        string status, string? productName, string productDisplay, string? genericName)
    {
        // Render the compiled production view without starting a server or accessing a database.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(PrescriptionDetailViewModel).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.Services.AddControllersWithViews();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddSingleton(CppSelectivePrintTests.Stub<IWebPermissionService>((_, _) => throw new InvalidOperationException("Anonymous rendering must not request permissions")));
        builder.Services.AddSingleton(CppSelectivePrintTests.Stub<IWebPlatformEntitlementAccessor>((_, _) => throw new InvalidOperationException("Anonymous rendering must not request entitlements")));
        await using var app = builder.Build();
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var http = new DefaultHttpContext { RequestServices = services };
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "Detail test"));
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var view = services.GetRequiredService<ICompositeViewEngine>()
            .GetView(null, "/Views/PatientPrescriptions/Detail.cshtml", isMainPage: false);
        Assert.True(view.Success, string.Join(Environment.NewLine, view.SearchedLocations ?? []));
        var model = new PrescriptionDetailViewModel(new PatientPrescriptionResponse
        {
            Status = status,
            ProductName = productName!, // Exercise legacy/null input despite the non-null DTO contract.
            ProductDisplayText = productDisplay,
            GenericName = genericName,
            StrengthValue = genericName is null ? null : 325,
            StrengthUnit = genericName is null ? null : "MG",
            DoseAmount = genericName is null ? null : 2,
            DoseUnit = genericName is null ? null : "tablets",
            MedicationForm = genericName is null ? null : "TABLET <form>",
            Route = "ORAL",
            FrequencyCode = "ONCE_DAILY",
            FrequencyDisplay = "Once daily",
            Prn = true,
            DateFields = genericName is null ? null : new PrescriptionDateFields
            {
                WrittenDate = new PrescriptionDateValue { Precision = "Year", Year = 2026 },
                StartDate = new PrescriptionDateValue { Precision = "Month", Year = 2026, Month = 10 }
            },
            Quantity = 30,
            QuantityUnit = "tablets",
            InitialDaysSupply = genericName is null ? null : 15,
            AuthorizedRepeats = 3,
            RefillQuantity = genericName is null ? null : 60,
            RefillQuantityUnit = genericName is null ? null : "tablets",
            RefillDaysSupply = genericName is null ? null : 30,
            NotesToPharmacist = genericName is null ? null : "Pharmacist <instructions> & review",
            Indication = genericName is null ? null : "Recorded <indication>",
            ProductIdentifierNamespace = genericName is null ? null : "recorded-product-system",
            ProductIdentifierValue = genericName is null ? null : "00559393",
            PrescriptionUid = Guid.Parse("4cd092dc-f4ea-47a1-a66f-bde9dc6ab9f0"),
            SupersedesPrescriptionUid = genericName is null ? null : Guid.Parse("c41c35e1-d32b-4eba-85a2-780cfd91adf5"),
            SupersededByPrescriptionUid = genericName is null ? null : Guid.Parse("2f092923-39cb-4bbd-8244-f840217c125a")
        }, null, []);
        var data = new ViewDataDictionary<PrescriptionDetailViewModel>(
            new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };
        using var writer = new StringWriter();
        var context = new ViewContext(action, view.View, data,
            new TempDataDictionary(http, services.GetRequiredService<ITempDataProvider>()),
            writer, new HtmlHelperOptions());
        await view.View.RenderAsync(context);
        var html = writer.ToString();
        Assert.Contains("<body class=\"app-body\">", html);
        Assert.Contains("id=\"appSidebar\"", html);
        Assert.Contains("class=\"content-panel-header\"", html);
        // The app shell includes a logout form; clinical content remains read-only.
        var mainStart = html.IndexOf("<main class=\"app-main\">", StringComparison.Ordinal);
        Assert.True(mainStart >= 0);
        html = html[mainStart..html.IndexOf("</main>", mainStart, StringComparison.Ordinal)];

        Assert.Contains($"<p><strong>Product/Brand Name:</strong> {HtmlEncoder.Default.Encode(productName ?? "")}</p>", html);
        Assert.Contains($"<strong>{HtmlEncoder.Default.Encode(productDisplay)}</strong>", html);
        Assert.Contains($"<span class=\"badge text-bg-secondary\">{status}</span>", html);
        Assert.Contains($"<strong>Generic name:</strong> {HtmlEncoder.Default.Encode(genericName ?? "Not recorded")}", html);
        var expected = genericName is null
            ? new Dictionary<string, string>
            {
                ["Strength"] = "Not recorded", ["Dose"] = "Not recorded", ["Form"] = "Not recorded",
                ["Written date"] = "Not recorded", ["Start date"] = "Not recorded",
                ["Initial days' supply"] = "Not recorded", ["Refill quantity"] = "Not recorded",
                ["Refill days' supply"] = "Not recorded", ["Notes to pharmacist"] = "Not recorded",
                ["Product identifier"] = "Not recorded", ["Prior prescription reference"] = "Not recorded"
            }
            : new Dictionary<string, string>
            {
                ["Strength"] = "325 MG", ["Dose"] = "2 tablets", ["Form"] = "TABLET <form>",
                ["Written date"] = "2026", ["Start date"] = "2026-10",
                ["Initial days' supply"] = "15", ["Refill quantity"] = "60 tablets",
                ["Refill days' supply"] = "30", ["Notes to pharmacist"] = "Pharmacist <instructions> & review",
                ["Indication"] = "Recorded <indication>", ["Product identifier namespace"] = "recorded-product-system",
                ["Product identifier"] = "00559393", ["Prior prescription reference"] = "c41c35e1-d32b-4eba-85a2-780cfd91adf5",
                ["Replaced by prescription"] = "2f092923-39cb-4bbd-8244-f840217c125a"
            };
        foreach (var (label, value) in expected)
            Assert.Contains($"<strong>{label}:</strong> {HtmlEncoder.Default.Encode(value)}", html);
        Assert.Contains("<strong>Initial quantity:</strong> 30 tablets", html);
        Assert.Contains("<strong>Authorized refills:</strong> 3", html);
        Assert.Contains("<strong>Route:</strong> ORAL", html);
        Assert.Contains("<strong>Frequency:</strong> Once daily (ONCE_DAILY)", html);
        Assert.Contains("<strong>As needed (PRN):</strong> Yes", html);
        Assert.Contains("<strong>Prescription identifier:</strong> 4cd092dc-f4ea-47a1-a66f-bde9dc6ab9f0", html);
        Assert.DoesNotContain("Private prescription note", html);
        Assert.DoesNotContain("<form>", html);
        Assert.DoesNotContain("<instructions>", html);
        Assert.DoesNotContain("<input", html);
        Assert.DoesNotContain("<form", html);
        if (productName?.Contains('<') == true)
            Assert.DoesNotContain(productName, html);
    }
}
