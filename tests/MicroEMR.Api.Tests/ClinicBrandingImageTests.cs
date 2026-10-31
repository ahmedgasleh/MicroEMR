using System.Buffers.Binary;
using Microsoft.Playwright;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.ClinicConfiguration;
using MicroEMR.Infrastructure.ClinicalOutput;
using MicroEMR.Infrastructure.ClinicConfiguration;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ClinicBrandingImageTests
{
    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    public async Task ActualPngAndJpegDecodeToSanitizedPng(string type)
    {
        var original = await Image(type,40,20);
        // A trailing payload must never survive canonicalization.
        var content = original.Concat("<script>untrusted</script>"u8.ToArray()).ToArray();
        var decoded = await new ClinicLogoProcessor().ProcessAsync(content);
        Assert.Equal(40,decoded.Width); Assert.Equal(20,decoded.Height);
        Assert.Equal(new byte[]{137,80,78,71,13,10,26,10},decoded.PngBytes.Take(8));
        Assert.DoesNotContain("untrusted",System.Text.Encoding.UTF8.GetString(decoded.PngBytes));
    }

    [Theory]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>")]
    [InlineData("<html><script>alert(1)</script></html>")]
    [InlineData("MZ executable renamed.png")]
    [InlineData("GIF89a")]
    public async Task ContentTypeSpoofingAndUnsupportedImagesAreRejected(string content) =>
        await Assert.ThrowsAsync<ArgumentException>(()=>new ClinicLogoProcessor().ProcessAsync(System.Text.Encoding.UTF8.GetBytes(content)));

    [Fact]
    public async Task ForgedDimensionsAndCorruptImageDataAreRejected()
    {
        var png = await Image("image/png",40,20);
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(16,4),2049);
        await Assert.ThrowsAsync<ArgumentException>(()=>new ClinicLogoProcessor().ProcessAsync(png));
        var corrupt = (await Image("image/png",40,20)).Take(33).ToArray();
        await Assert.ThrowsAsync<ArgumentException>(()=>new ClinicLogoProcessor().ProcessAsync(corrupt));
        await Assert.ThrowsAsync<ArgumentException>(()=>new ClinicLogoProcessor().ProcessAsync(new byte[ClinicBrandingService.MaximumLogoBytes+1]));
    }

    [Fact]
    public async Task HeaderLogoScalesWithoutDistortionAndDemoRendersUsingExistingPdfEngine()
    {
        var logo = await new ClinicLogoProcessor().ProcessAsync(await Image("image/png",1000,300));
        var header = new ClinicPrintHeader(new("Demonstration Clinic", "100 Test Street", null,"Test City","ON","T0T 0T0","555-0100",null,null),"Clinical correspondence — header demonstration",logo,"Canada");
        var html = ClinicPrintHeaderRenderer.RenderDocument(header);
        using var playwright=await Playwright.CreateAsync();
        await using var browser=await playwright.Chromium.LaunchAsync(new(){Headless=true});
        var page=await browser.NewPageAsync();
        await page.SetContentAsync(html);
        var size=await page.Locator(".clinic-letterhead-logo").EvaluateAsync<double[]>("image=>{const r=image.getBoundingClientRect();return [r.width,r.height,image.naturalWidth,image.naturalHeight]}");
        Assert.InRange(size[0],1,147); Assert.InRange(size[1],1,87); Assert.Equal(1000.0/300,size[0]/size[1],2);
        await using var renderer=new PlaywrightPdfRenderer(NullLogger<PlaywrightPdfRenderer>.Instance);
        var pdf=await renderer.RenderAsync(html);
        using(var document=UglyToad.PdfPig.PdfDocument.Open(pdf))
        {
            Assert.Single(document.GetPages());
            Assert.Contains("Demonstration Clinic",document.GetPage(1).Text);
            Assert.Contains("header demonstration",document.GetPage(1).Text);
        }
        var root=Directory.GetCurrentDirectory();
        var output=Path.Combine(root,".tmp/product-improvement-009");Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output,"header-demo.html"),html);
        await File.WriteAllBytesAsync(Path.Combine(output,"header-demo.pdf"),pdf);
        await page.ScreenshotAsync(new(){Path=Path.Combine(output,"header-demo.png"),FullPage=true});
    }

    private static async Task<byte[]> Image(string type,int width,int height)
    {
        using var playwright=await Playwright.CreateAsync();
        await using var browser=await playwright.Chromium.LaunchAsync(new(){Headless=true});
        var page=await browser.NewPageAsync();
        var data=await page.EvaluateAsync<string>("({type,width,height})=>{const c=document.createElement('canvas');c.width=width;c.height=height;const x=c.getContext('2d');x.fillStyle='#176b7c';x.fillRect(0,0,width,height);return c.toDataURL(type).split(',')[1]}",new{type,width,height});
        return Convert.FromBase64String(data);
    }
}
