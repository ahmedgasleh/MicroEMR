using MicroEMR.Application.PatientReferrals;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Writer;

namespace MicroEMR.Infrastructure.PatientReferrals;

public sealed class ReferralPdfAssembler(ILogger<ReferralPdfAssembler> logger) : IReferralPdfAssembler
{
    public Task<byte[]> CombineAsync(IReadOnlyList<byte[]> parts, CancellationToken token = default)
    {
        try
        {
            var builder = new PdfDocumentBuilder();
            foreach (var bytes in parts)
            {
                token.ThrowIfCancellationRequested();
                using var document = PdfDocument.Open(bytes);
                var catalog = document.Structure.Catalog.CatalogDictionary;
                // The page importer does not copy interactive annotations or document-level forms.
                // Reject those sources instead of producing an incomplete clinical report.
                if (document.NumberOfPages == 0 || catalog.Data.Keys.Any(x => x is "AcroForm" or "OCProperties"))
                    throw Unsupported();
                for (var page = 1; page <= document.NumberOfPages; page++)
                {
                    token.ThrowIfCancellationRequested();
                    if (document.GetPage(page).Dictionary.Data.Keys.Any(x => x == "Annots")) throw Unsupported();
                    builder.AddPage(document, page);
                }
            }
            return Task.FromResult(builder.Build());
        }
        catch (OperationCanceledException) { throw; }
        catch (ReferralClinicalSelectionRuleException) { throw; }
        catch (Exception exception)
        {
            logger.LogWarning("Selected referral PDF import failed ({ExceptionType}).", exception.GetType().Name);
            throw Unsupported();
        }
    }

    private static ReferralClinicalSelectionRuleException Unsupported() => new(
        "A selected PDF cannot be included completely. Use a readable, flattened PDF without interactive annotations or forms, or remove its selection before previewing or sending.");
}
