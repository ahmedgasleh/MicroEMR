using System.Security.Cryptography;
using MicroEMR.Application.PatientEncounters.Repositories;
using MicroEMR.Application.PatientFiles;
using MicroEMR.Application.Tenancy;
using Microsoft.Extensions.Logging;

namespace MicroEMR.Application.ClinicalOutput;

public sealed class ClinicalArtifactService(
    IClinicalOutputArtifactRepository artifacts,
    IPatientEncounterRepository encounters,
    IClinicalPdfPreviewService pdf,
    IPatientFileStorage storage,
    ITenantContext tenant,
    ILogger<ClinicalArtifactService> logger) : IClinicalArtifactService
{
    public async Task<ClinicalOutputArtifact?> EnsureEncounterFinalPdfAsync(Guid encounterUid, long? actorUserId,
        CancellationToken token = default)
    {
        var existing = await artifacts.GetFinalBySourceAsync(ClinicalArtifactTypes.Encounter, encounterUid, token);
        if (existing is not null) return existing;
        var encounter = await encounters.GetByUidAsync(encounterUid, token);
        if (encounter is null) return null;
        if (!string.Equals(encounter.Status, "Signed", StringComparison.OrdinalIgnoreCase)
            || !encounter.TemplateVersionUid.HasValue || encounter.StructuredDataJson is null)
            throw new InvalidOperationException("A final PDF can be created only for a signed schema-driven encounter.");

        var artifactUid = Guid.NewGuid();
        var key = $"tenants/{tenant.TenantUid:N}/clinical-artifacts/encounters/{encounter.PatientUid:N}/{encounterUid:N}/{artifactUid:N}.pdf";
        var saved = false;
        try
        {
            var bytes = await pdf.RenderSignedEncounterAsync(encounterUid, token);
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            await using (var content = new MemoryStream(bytes, writable: false))
                await storage.SaveAsync(content, key, token);
            saved = true;
            try
            {
                var created = await artifacts.CreateAsync(new(artifactUid, encounter.PatientUid,
                    ClinicalArtifactTypes.Encounter, encounterUid, encounter.TemplateVersionUid.Value,
                    ClinicalArtifactTypes.FinalPdf, ClinicalArtifactTypes.FileSystem, key, "application/pdf",
                    bytes.LongLength, hash, actorUserId), token);
                logger.LogInformation("Final PDF artifact {ArtifactUid} created for encounter {EncounterUid}, version {TemplateVersionUid}, provider {StorageProvider}, size {FileSizeBytes}, SHA-256 {Sha256}.",
                    created.ArtifactUid, encounterUid, created.TemplateVersionUid, created.StorageProvider, created.FileSizeBytes, created.Sha256);
                return created;
            }
            catch
            {
                await storage.DeleteAsync(key, CancellationToken.None);
                saved = false;
                var concurrent = await artifacts.GetFinalBySourceAsync(ClinicalArtifactTypes.Encounter, encounterUid, CancellationToken.None);
                if (concurrent is not null) return concurrent;
                throw;
            }
        }
        catch (Exception exception)
        {
            if (saved) await storage.DeleteAsync(key, CancellationToken.None);
            logger.LogError(exception, "Final PDF artifact generation failed for encounter {EncounterUid}.", encounterUid);
            try
            {
                await artifacts.RecordFailureAsync(encounter.PatientUid, ClinicalArtifactTypes.Encounter, encounterUid,
                    encounter.TemplateVersionUid.Value, actorUserId, exception.GetType().Name, CancellationToken.None);
            }
            catch (Exception auditException)
            {
                logger.LogError(auditException, "Could not record final PDF failure metadata for encounter {EncounterUid}.", encounterUid);
            }
            throw;
        }
    }

    public async Task<ClinicalArtifactContent?> OpenEncounterFinalPdfAsync(Guid encounterUid, CancellationToken token = default)
    {
        var encounter = await encounters.GetByUidAsync(encounterUid, token);
        if (encounter is null) return null;
        var artifact = await artifacts.GetFinalBySourceAsync(ClinicalArtifactTypes.Encounter, encounterUid, token);
        if (artifact is null || artifact.PatientUid != encounter.PatientUid) return null;
        if (!await storage.ExistsAsync(artifact.StorageKey, token))
            throw new FileNotFoundException("The final PDF artifact content is unavailable.");
        return new(await storage.OpenReadAsync(artifact.StorageKey, token),
            $"encounter-{encounterUid:N}.pdf", artifact.MimeType, artifact.FileSizeBytes);
    }
}
