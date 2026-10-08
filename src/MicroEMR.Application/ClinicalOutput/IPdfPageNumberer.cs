namespace MicroEMR.Application.ClinicalOutput;

public interface IPdfPageNumberer
{
    Task<byte[]> NumberAsync(byte[] generatedPdf, CancellationToken token = default);
}
