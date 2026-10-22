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
        foreach (var status in new[] { "Draft", "Finalized" })
        {
            yield return [status, "Recorded brand", "Independent product display", "Different generic"];
            yield return [status, "Provider-typed <medicine> & name", "Independent product display", "Different generic"];
            yield return [status, "Recorded brand", "", "Different generic"];
            yield return [status, "", "Independent product display", "Different generic"];
            yield return [status, null, "Independent product display", "Different generic"];
        }
    }

    [Theory]
    [MemberData(nameof(RecordedNames))]
    public async Task ReadOnlyDetailsDisplaysRecordedNameWithoutSubstitution(
        string status, string? productName, string productDisplay, string genericName)
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
            GenericName = genericName
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
        Assert.DoesNotContain(genericName, html);
        Assert.DoesNotContain("<input", html);
        Assert.DoesNotContain("<form", html);
        if (productName?.Contains('<') == true)
            Assert.DoesNotContain(productName, html);
    }
}
