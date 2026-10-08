using MicroEMR.Application.ClinicalOutput;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace MicroEMR.Infrastructure.ClinicalOutput;

// Applies only to newly generated CPP PDFs with a reserved bottom margin.
public sealed class PdfPageNumberer(ILogger<PdfPageNumberer> logger) : IPdfPageNumberer
{
    public Task<byte[]> NumberAsync(byte[] generatedPdf, CancellationToken token = default)
    {
        try
        {
            using var source = PdfDocument.Open(generatedPdf);
            if (source.NumberOfPages == 0) throw new PdfRenderingException("CPP printing returned no pages.");
            var builder = new PdfDocumentBuilder();
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            for (var i = 1; i <= source.NumberOfPages; i++)
            {
                token.ThrowIfCancellationRequested();
                var page = builder.AddPage(source, i);
                var label = $"{i}/{source.NumberOfPages}";
                page.AddText(label, 9, new PdfPoint(source.GetPage(i).Width / 2 - label.Length * 2.5, 22), font);
            }
            return Task.FromResult(builder.Build());
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error)
        {
            logger.LogWarning("CPP PDF pagination failed ({ExceptionType}).", error.GetType().Name);
            throw new PdfRenderingException("The CPP PDF could not be paginated. Try printing again.");
        }
    }
}
