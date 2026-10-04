using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.PatientDocuments.Repositories;
using MicroEMR.Application.PatientFiles;
using MicroEMR.Application.Tenancy;

namespace MicroEMR.Application.PatientDocuments.Services;

public interface IConsultationSigningService
{
    Task<PatientDocumentDetailsResponse> SignAsync(Guid patientUid, Guid documentUid,
        SignConsultationRequest request, long actorUserId, CancellationToken token = default);
    Task<ClinicalArtifactContent?> OpenFinalPdfAsync(Guid patientUid, Guid documentUid, CancellationToken token = default);
}

public sealed class ConsultationSigningService(IConsultationSigningRepository signing,
    IPatientDocumentRepository documents, IClinicalPdfPreviewService pdf,
    IClinicalOutputArtifactRepository artifacts, IPatientFileStorage storage, ITenantContext tenant,
    ILogger<ConsultationSigningService> logger) : IConsultationSigningService
{
    public async Task<PatientDocumentDetailsResponse> SignAsync(Guid patientUid, Guid documentUid,
        SignConsultationRequest request, long actorUserId, CancellationToken token = default)
    {
        if (patientUid == Guid.Empty || documentUid == Guid.Empty || actorUserId <= 0)
            throw new ArgumentException("Patient, document and authenticated clinician are required.");
        if (string.IsNullOrWhiteSpace(request.RowVersion) ||
            !Convert.TryFromBase64String(request.RowVersion, new byte[8], out var length) || length != 8)
            throw new ArgumentException("A valid document row version is required.");
        try
        {
            return await signing.SignAsync(patientUid, documentUid, request.RowVersion, actorUserId,
                async (context, cancellation) =>
                {
                    if (!context.Recipients.Any(x => x.RecipientType == "TO"))
                        throw new ArgumentException("Save at least one TO recipient before signing.");
                    var bytes = await pdf.RenderConsultationFinalAsync(context, cancellation);
                    if (bytes.Length == 0) throw new InvalidOperationException("The final PDF is empty.");
                    var uid = Guid.NewGuid();
                    var key = $"tenants/{tenant.TenantUid:N}/clinical-artifacts/patient-documents/{patientUid:N}/{documentUid:N}/{uid:N}.pdf";
                    await using var content = new MemoryStream(bytes, writable: false);
                    await storage.SaveAsync(content, key, cancellation);
                    return new(uid, patientUid, ClinicalArtifactTypes.PatientDocument, documentUid,
                        context.Document.TemplateVersionUid!.Value, ClinicalArtifactTypes.FinalPdf,
                        ClinicalArtifactTypes.FileSystem, key, "application/pdf", bytes.LongLength,
                        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), actorUserId);
                }, token);
        }
        catch (Exception exception)
        {
            // Never delete a saved PDF on an ambiguous SQL commit result. A failed transaction can
            // leave an unreferenced file, but cannot leave a Signed document without a saved PDF.
            logger.LogWarning(exception, "Consultation signing did not complete normally for {DocumentUid}. Reload before retrying.", documentUid);
            throw;
        }
    }

    public async Task<ClinicalArtifactContent?> OpenFinalPdfAsync(Guid patientUid, Guid documentUid, CancellationToken token = default)
    {
        var document = await documents.GetByUidAsync(documentUid, token);
        if (document is null || document.PatientUid != patientUid || document.Status != "Signed" || document.FinalizedAt is null)
            return null;
        var artifact = await artifacts.GetFinalBySourceAsync(ClinicalArtifactTypes.PatientDocument, documentUid, token);
        if (artifact is null || artifact.PatientUid != patientUid) return null;
        var prefix = $"tenants/{tenant.TenantUid:N}/clinical-artifacts/patient-documents/{patientUid:N}/{documentUid:N}/";
        if (artifact.StorageProvider != ClinicalArtifactTypes.FileSystem || !artifact.StorageKey.StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Final PDF storage ownership is invalid.");
        return new(await storage.OpenReadAsync(artifact.StorageKey, token),
            $"consultation-{documentUid:N}.pdf", artifact.MimeType, artifact.FileSizeBytes);
    }
}
