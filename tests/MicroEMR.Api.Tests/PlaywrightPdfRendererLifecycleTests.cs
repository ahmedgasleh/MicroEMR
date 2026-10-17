using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Infrastructure;
using MicroEMR.Infrastructure.ClinicalOutput;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PlaywrightPdfRendererLifecycleTests
{
    [Fact]
    public async Task ServiceProviderCanDisposeAllAliasesAfterGeneratingPdf()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMicroEmrInfrastructure();
        var provider = services.BuildServiceProvider();
        var renderer = provider.GetRequiredService<PlaywrightPdfRenderer>();
        try
        {
            Assert.Same(renderer, provider.GetRequiredService<IPdfRenderer>());
            Assert.Same(renderer, provider.GetRequiredService<IPrescriptionPdfRenderer>());
            var bytes = await renderer.RenderAsync("<!DOCTYPE html><html><body>TEST ONLY - shutdown fixture</body></html>");
            Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
        }
        finally
        {
            await provider.DisposeAsync();
        }

        await renderer.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => renderer.RenderAsync("<p>After shutdown</p>"));
    }

    [Fact]
    public async Task DisposalBeforeStartupCanBeRepeatedConcurrentlyAndPreventsStartup()
    {
        var renderer = new PlaywrightPdfRenderer(NullLogger<PlaywrightPdfRenderer>.Instance);
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => renderer.DisposeAsync().AsTask()));
        await renderer.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => renderer.RenderAsync("<p>After shutdown</p>"));
    }
}
