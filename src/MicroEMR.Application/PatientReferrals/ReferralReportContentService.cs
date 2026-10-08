using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.PatientDocuments.Services;
using MicroEMR.Application.PatientFiles;
using MicroEMR.Application.ReadAudit;
using MicroEMR.Application.Templates.Runtime;

namespace MicroEMR.Application.PatientReferrals;

public sealed class ReferralReportContentService(IPatientDocumentService documents, IPatientFileService files,
    IConsultationSigningService consultations, ITemplateInstanceRuntime runtime,
    ICurrentUserPermissionService permissions, IStructuredReadAuditService audit) : IReferralReportContentService
{
    private const long MaxSourceBytes = 26_214_400;

    public async Task<IReadOnlyList<ReferralClinicalOption>> GetFileOptionsAsync(Guid patientUid, CancellationToken token = default)
    {
        if (!(await permissions.GetEffectivePermissionsAsync(token)).Contains(PermissionKeys.DocumentsView)) return [];
        return (await files.GetByPatientUidAsync(patientUid, token))
            .Where(x => x.PatientUid == patientUid && x.Status == "Active")
            .Select(x => new ReferralClinicalOption("FILE", null, null, null,
                $"{x.Title ?? x.OriginalFileName} ({x.Category})", x.UploadedAtUtc, x.AuthorName, x.Status, x.FileUid)).ToArray();
    }

    public async Task<ReferralReportComposition> ComposeAsync(Guid patientUid,
        IReadOnlyList<ReferralDocumentLinkResponse> links, IReadOnlyList<PatientReferralClinicalSelectionResponse> selections,
        CancellationToken token = default)
    {
        try { return await ComposeCoreAsync(patientUid, links, selections, token); }
        catch (IOException) { throw Unavailable(); }
        catch (KeyNotFoundException) { throw Unavailable(); }
    }

    private async Task<ReferralReportComposition> ComposeCoreAsync(Guid patientUid,
        IReadOnlyList<ReferralDocumentLinkResponse> links, IReadOnlyList<PatientReferralClinicalSelectionResponse> selections,
        CancellationToken token)
    {
        var selectedFiles = selections.Where(x => x.SelectionKind == ReferralClinicalSelectionKinds.File).ToArray();
        if (links.Count == 0 && selectedFiles.Length == 0) return new(string.Empty, [], []);
        if (!(await permissions.GetEffectivePermissionsAsync(token)).Contains(PermissionKeys.DocumentsView))
            throw new UnauthorizedAccessException("Selected report access is restricted.");
        var html = new StringBuilder("<h2>Selected consultation / external reports</h2>");
        var appendices = new List<ReferralReportAppendix>();
        var sources = new List<ReferralReportSnapshot>();
        var correlation = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");

        foreach (var link in links)
        {
            PatientDocumentDetailsResponse? doc;
            try { doc = await documents.GetByUidAsync(link.DocumentUid, token); }
            catch (Exception error) when (error is InvalidOperationException or TemplateInstanceValidationException)
            { throw Unavailable(); }
            if (doc is null || doc.PatientUid != patientUid || doc.DocumentUid != link.DocumentUid || doc.Status == "Archived")
                throw Unavailable();
            var heading = Heading(doc.Title, doc.DocumentType, doc.Status, doc.DocumentUid,
                doc.SignerDisplayNameSnapshot ?? doc.CreatedByDisplayName, doc.FinalizedAt ?? doc.CreatedAt);
            await audit.RecordAsync(ReadAuditActions.PatientDocumentViewed, ReadAuditResourceTypes.PatientDocument,
                doc.DocumentUid, patientUid, correlation, token);
            if (doc.IsConsultationReport && doc.Status == "Signed")
            {
                ClinicalArtifactContent content;
                try { content = await consultations.OpenFinalPdfAsync(patientUid, doc.DocumentUid, token) ?? throw Unavailable(); }
                catch (InvalidOperationException) { throw Unavailable(); }
                await using var stream = content.Content;
                if (content.MimeType != "application/pdf") throw Unavailable();
                var bytes = await ReadAsync(stream, content.FileSizeBytes, token);
                await audit.RecordAsync(ReadAuditActions.PatientDocumentDownloaded, ReadAuditResourceTypes.PatientDocument,
                    doc.DocumentUid, patientUid, correlation, token);
                AddPdf("PatientDocument", doc.DocumentUid, doc.Title, doc.DocumentType, doc.Status, doc.RowVersion, heading, bytes);
            }
            else
            {
                var text = doc.Content;
                if (doc.IsStructured)
                {
                    if (doc.TemplateDefinition is null) throw Unavailable();
                    var processed = runtime.Process(doc.TemplateDefinition, doc.StructuredDataJson);
                    if (!processed.IsValid || processed.Data is null) throw Unavailable();
                    text = runtime.RenderSnapshot(doc.TemplateDefinition, processed.Data);
                }
                if (string.IsNullOrWhiteSpace(text)) throw Unavailable();
                html.Append(heading).Append(Text(text));
                sources.Add(Snapshot("PatientDocument", doc.DocumentUid, doc.Title, doc.DocumentType, doc.Status,
                    doc.RowVersion, "text/plain", Encoding.UTF8.GetBytes(text), null));
            }
        }
        foreach (var selection in selectedFiles)
        {
            if (selection.FileUid is not { } uid || uid == Guid.Empty) throw Unavailable();
            var file = await files.GetByUidAsync(patientUid, uid, token);
            if (file is null || file.PatientUid != patientUid || file.FileUid != uid || file.Status != "Active") throw Unavailable();
            var content = await files.OpenContentAsync(patientUid, uid, token) ?? throw Unavailable();
            await using var stream = content.Content;
            if (content.PatientUid != patientUid || content.FileUid != uid || content.ContentType != file.ContentType
                || content.Length != file.FileSizeBytes) throw Unavailable();
            var bytes = await ReadAsync(stream, file.FileSizeBytes, token);
            if (string.IsNullOrWhiteSpace(file.Sha256Hash) ||
                !string.Equals(Hash(bytes), file.Sha256Hash, StringComparison.OrdinalIgnoreCase)) throw Unavailable();
            await audit.RecordAsync(ReadAuditActions.PatientFileDownloaded, ReadAuditResourceTypes.PatientFile,
                uid, patientUid, correlation, token);
            var title = file.Title ?? file.OriginalFileName;
            var heading = Heading(title, file.Category ?? "External report", file.Status, uid, file.AuthorName, null)
                + $"<p>Source organization: {E(file.SourceOrganization)}; Document date: {E(file.DocumentDate?.ToString("yyyy-MM-dd"))}; Received date: {E(file.ReceivedDate?.ToString("yyyy-MM-dd"))}</p>";
            if (file.ContentType == "application/pdf")
                AddPdf("PatientFile", uid, title, file.Category ?? "External report", file.Status, file.RowVersion, heading, bytes);
            else
            {
                html.Append(heading);
                if (file.ContentType == "text/plain")
                {
                    string text;
                    try { text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'); }
                    catch (DecoderFallbackException) { throw new ReferralClinicalSelectionRuleException("Selected text report must be readable UTF-8. Replace it or remove its selection."); }
                    if (string.IsNullOrWhiteSpace(text) || text.Contains('\0')) throw Unavailable();
                    html.Append(Text(text));
                }
                else throw new ReferralClinicalSelectionRuleException("Selected report format cannot be included completely. Use PDF or UTF-8 text, or remove its selection.");
                sources.Add(Snapshot("PatientFile", uid, title, file.Category ?? "External report", file.Status, file.RowVersion, file.ContentType, bytes, null));
            }
        }
        return new(html.ToString(), appendices, sources);

        void AddPdf(string type, Guid uid, string title, string category, string status, string version, string heading, byte[] bytes)
        {
            var number = appendices.Count + 1;
            html.Append(heading).Append($"<p>Full report included in Appendix {number}.</p>");
            appendices.Add(new(number, $"<h1>Appendix {number} — selected report</h1>{heading}<p>The following pages contain the full source PDF.</p>", bytes));
            sources.Add(Snapshot(type, uid, title, category, status, version, "application/pdf", bytes, number));
        }
    }

    private static string Heading(string title, string type, string status, Guid uid, string? author, DateTime? date) =>
        $"<h3>{E(title)}</h3><p>Type: {E(type)}; Status: {E(status)}; Source: {uid:D}; Author: {E(author)}; Date: {E(date?.ToString("yyyy-MM-dd"))}</p>";
    private static string Text(string text) => $"<div style=\"white-space:pre-wrap;overflow-wrap:anywhere\">{E(text)}</div>";
    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "Not recorded");
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static ReferralReportSnapshot Snapshot(string type, Guid uid, string title, string category, string status,
        string version, string mime, byte[] bytes, int? appendix) => new(type, uid, title, category, status, version, mime, bytes.LongLength, Hash(bytes), appendix);
    private static ReferralClinicalSelectionRuleException Unavailable() => new("A selected report is missing, archived, changed or unavailable. Refresh the referral and restore the source or remove its selection before previewing or sending.");
    private static async Task<byte[]> ReadAsync(Stream stream, long expected, CancellationToken token)
    {
        if (expected <= 0 || expected > MaxSourceBytes) throw Unavailable();
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int count;
        while ((count = await stream.ReadAsync(buffer, token)) > 0)
        {
            if (output.Length + count > expected) throw Unavailable();
            await output.WriteAsync(buffer.AsMemory(0, count), token);
        }
        if (output.Length != expected) throw Unavailable();
        return output.ToArray();
    }
}
