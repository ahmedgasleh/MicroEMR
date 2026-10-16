using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Application.ClinicalOutput;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace MicroEMR.Infrastructure.ClinicalOutput;

public sealed class PlaywrightPdfRenderer(ILogger<PlaywrightPdfRenderer> logger) : IPdfRenderer, IPrescriptionPdfRenderer, IAsyncDisposable
{
    private readonly SemaphoreSlim _startupLock = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public Task<byte[]> RenderAsync(string html, CancellationToken cancellationToken = default) => RenderCoreAsync(html, null, cancellationToken);

    public Task<byte[]> RenderPrescriptionAsync(PrescriptionPdfLayout layout, CancellationToken token = default) => RenderCoreAsync(layout.Body, layout, token);

    private async Task<byte[]> RenderCoreAsync(string html, PrescriptionPdfLayout? layout, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(html)) throw new ArgumentException("Printable HTML is required.", nameof(html));
        try
        {
            var browser = await GetBrowserAsync(cancellationToken);
            await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                JavaScriptEnabled = false,
                ServiceWorkers = ServiceWorkerPolicy.Block
            });
            var page = await context.NewPageAsync();
            await page.RouteAsync("**/*", route => route.AbortAsync("blockedbyclient"));
            await page.SetContentAsync(html, new PageSetContentOptions { WaitUntil = WaitUntilState.Load });
            return await page.PdfAsync(new PagePdfOptions
            {
                Format = "Letter",
                DisplayHeaderFooter = layout is not null,
                HeaderTemplate = layout?.Header,
                FooterTemplate = layout?.Footer,
                PrintBackground = true,
                PreferCSSPageSize = true,
                Margin = new Margin { Top = layout is null ? "0.65in" : "1in", Right = "0.65in", Bottom = layout is null ? "0.65in" : "1.5in", Left = "0.65in" }
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (PlaywrightException exception)
        {
            logger.LogError(exception, "Chromium could not generate the PDF preview.");
            throw new PdfRenderingException(
                "PDF preview is temporarily unavailable. Verify that the configured Playwright Chromium browser is installed.", exception);
        }
    }

    private async Task<IBrowser> GetBrowserAsync(CancellationToken cancellationToken)
    {
        if (_browser?.IsConnected == true) return _browser;
        await _startupLock.WaitAsync(cancellationToken);
        try
        {
            if (_browser?.IsConnected == true) return _browser;
            _playwright ??= await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            return _browser;
        }
        finally { _startupLock.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null) await _browser.DisposeAsync();
        _playwright?.Dispose();
        _startupLock.Dispose();
    }
}
