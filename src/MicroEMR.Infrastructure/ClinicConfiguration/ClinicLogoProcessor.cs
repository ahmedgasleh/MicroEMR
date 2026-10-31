using System.Buffers.Binary;
using Microsoft.Playwright;
using MicroEMR.Application.ClinicConfiguration;

namespace MicroEMR.Infrastructure.ClinicConfiguration;

// Decode with the existing Chromium dependency, then re-encode pixels only. Uploaded metadata/trailing payloads are discarded.
public sealed class ClinicLogoProcessor : IClinicLogoProcessor
{
    public async Task<ClinicLogo> ProcessAsync(byte[] content, CancellationToken token = default)
    {
        var (mime, width, height) = Inspect(content);
        token.ThrowIfCancellationRequested();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ServiceWorkers = ServiceWorkerPolicy.Block });
        var page = await context.NewPageAsync();
        await page.RouteAsync("**/*", route => route.AbortAsync());
        // No uploaded HTML/JS is executed; only this fixed decoder runs, with no network access.
        var png = await page.EvaluateAsync<string?>("""
            async ({source,width,height}) => {
                const image=new Image(); image.src=source;
                try { await image.decode(); } catch { return null; }
                if(image.naturalWidth!==width || image.naturalHeight!==height) return null;
                const canvas=document.createElement('canvas');canvas.width=width;canvas.height=height;
                canvas.getContext('2d').drawImage(image,0,0);
                return canvas.toDataURL('image/png').split(',')[1];
            }
            """, new { source = $"data:{mime};base64,{Convert.ToBase64String(content)}", width, height }).WaitAsync(TimeSpan.FromSeconds(10), token);
        if (png is null) throw new ArgumentException("The logo must be a valid PNG or JPEG image.");
        var bytes = Convert.FromBase64String(png);
        if (bytes.Length > ClinicBrandingService.MaximumLogoBytes)
            throw new ArgumentException("The decoded logo is too large. Choose a smaller image.");
        return new(bytes, width, height);
    }

    private static (string Mime, int Width, int Height) Inspect(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes.Length > ClinicBrandingService.MaximumLogoBytes)
            throw new ArgumentException("Choose a logo up to 1 MiB.");
        var data = bytes.AsSpan();
        if (data.Length >= 33 && data[..8].SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}) &&
            BinaryPrimitives.ReadUInt32BigEndian(data.Slice(8,4)) == 13 && data.Slice(12,4).SequenceEqual("IHDR"u8))
            return Dimensions("image/png", BinaryPrimitives.ReadUInt32BigEndian(data.Slice(16,4)), BinaryPrimitives.ReadUInt32BigEndian(data.Slice(20,4)));
        if (data.Length > 4 && data[0] == 255 && data[1] == 216)
        {
            var offset = 2;
            while (offset + 4 <= data.Length)
            {
                if (data[offset++] != 255) break;
                while (offset < data.Length && data[offset] == 255) offset++;
                if (offset >= data.Length) break;
                var marker = data[offset++];
                if (marker is 0xDA or 0xD9) break;
                if (offset + 2 > data.Length) break;
                var length = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset,2));
                if (length < 2 || offset + length > data.Length) break;
                if (marker is 0xC0 or 0xC1 or 0xC2 && length >= 8)
                    return Dimensions("image/jpeg", BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset+5,2)), BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset+3,2)));
                offset += length;
            }
        }
        throw new ArgumentException("The logo must be a valid PNG or JPEG image.");
    }

    private static (string, int, int) Dimensions(string mime, uint width, uint height)
    {
        if (width is 0 or > ClinicBrandingService.MaximumLogoDimension || height is 0 or > ClinicBrandingService.MaximumLogoDimension)
            throw new ArgumentException("Logo dimensions must be between 1 and 2048 pixels on each side.");
        return (mime, (int)width, (int)height);
    }
}
