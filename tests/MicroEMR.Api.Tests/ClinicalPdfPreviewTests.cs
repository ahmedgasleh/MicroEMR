using MicroEMR.Application.ClinicalOutput;
using Xunit;
using MicroEMR.Infrastructure.ClinicalOutput;
using Microsoft.Extensions.Logging.Abstractions;

namespace MicroEMR.Api.Tests;

public sealed class ClinicalPdfPreviewTests
{
    [Fact]
    public async Task PlaywrightRenderer_ProducesPdfBytes()
    {
        await using var renderer = new PlaywrightPdfRenderer(NullLogger<PlaywrightPdfRenderer>.Instance);
        var pdf = await renderer.RenderAsync("<!DOCTYPE html><html><body><h1>Preview</h1></body></html>");
        Assert.True(pdf.Length > 100);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, 5));
    }
    [Fact]
    public void PrintLayout_UsesLetterStaticHtmlAndSafeTitle()
    {
        var html = new ClinicalPrintLayoutRenderer().Render("Title <script>x</script>",
            "<article class=\"template-document\">safe</article>");

        Assert.StartsWith("<!DOCTYPE html>", html.TrimStart(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@page { size: Letter; margin: 0.65in; }", html);
        Assert.Contains("break-inside: avoid", html);
        Assert.Contains("Title &lt;script&gt;x&lt;/script&gt;", html);
        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PreviewImplementation_HasNoPersistenceOrArbitrarySchemaInput()
    {
        var root = Root();
        var service = File.ReadAllText(Path.Combine(root, "src", "MicroEMR.Application", "ClinicalOutput", "ClinicalPdfPreviewService.cs"));
        Assert.Contains("GetByUidAsync(documentUid", service);
        Assert.Contains("GetByUidAsync(encounterUid", service);
        Assert.Contains("GetByUidAsync(document.TemplateVersionUid.Value", service);
        Assert.Contains("GetByUidAsync(encounter.TemplateVersionUid.Value", service);
        Assert.Contains("record TemplatePreviewRequest(string? StructuredDataJson)", service);
        Assert.DoesNotContain("UpdateDraftAsync", service);
        Assert.DoesNotContain("UpdateStructuredDataAsync", service);
        Assert.DoesNotContain("SignAsync", service);
    }

    [Fact]
    public void PopupPreview_UsesBlobRefreshHideAndCleanup()
    {
        var root = Root();
        var documentScript = File.ReadAllText(Path.Combine(root, "src", "MicroEMR.Web", "ClientApp", "patient-documents", "runtime.ts"));
        var encounterView = File.ReadAllText(Path.Combine(root, "src", "MicroEMR.Web", "Views", "Patients", "Details.cshtml"));
        Assert.Contains("URL.createObjectURL", documentScript);
        Assert.Contains("URL.revokeObjectURL", documentScript);
        Assert.Contains("Refresh Preview", documentScript);
        Assert.Contains("hideDocumentPreviewButton", documentScript);
        Assert.Contains("form.dataset.documentUid", documentScript);
        Assert.Contains("RequestVerificationToken", documentScript);
        Assert.Contains("URL.createObjectURL", encounterView);
        Assert.Contains("URL.revokeObjectURL", encounterView);
        Assert.Contains("modal-xl", encounterView);
        Assert.Contains("hideEncounterPreviewButton", encounterView);
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MicroEMR.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
