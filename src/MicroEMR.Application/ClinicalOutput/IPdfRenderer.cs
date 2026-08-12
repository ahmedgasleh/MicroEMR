namespace MicroEMR.Application.ClinicalOutput;

public interface IPdfRenderer
{
    Task<byte[]> RenderAsync(string html, CancellationToken cancellationToken = default);
}

public sealed class PdfRenderingException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
