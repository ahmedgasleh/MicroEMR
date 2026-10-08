using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.PatientDocuments.Services;
using MicroEMR.Application.PatientDocuments.Repositories;
using MicroEMR.Application.PatientFiles;
using MicroEMR.Application.Patients.Repositories;
using MicroEMR.Application.PatientReferrals;
using MicroEMR.Application.ReadAudit;
using MicroEMR.Application.Templates.Definitions;
using MicroEMR.Application.Templates.Runtime;
using MicroEMR.Application.Templates.Serialization;
using MicroEMR.Application.Templates.Validation;
using MicroEMR.Application.Tenancy;
using MicroEMR.Infrastructure.PatientReferrals;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ReferralReportContentTests
{
    [Fact]
    public async Task SelectedStructuredAndTextBodiesAreCompleteEncodedAndDistinctWithoutReadingUnselectedReports()
    {
        var f = new Sources { Structured = true };
        var report = await f.Service.ComposeAsync(f.PatientUid, [f.Link], [f.FileSelection]);
        Assert.Contains("Assessment", report.Html);
        Assert.Contains("Continue 5 mg daily.\nReview in 6 weeks. &lt;script&gt;", report.Html);
        Assert.Contains("External findings\nNo acute change.", report.Html);
        Assert.DoesNotContain("<script>", report.Html);
        Assert.Contains(f.DocumentUid.ToString(), report.Html);
        Assert.Contains(f.FileUid.ToString(), report.Html);
        Assert.Equal(2, report.Sources.Count);
        Assert.Equal(2, f.Audits);
        Assert.Equal(1, f.DocumentReads);
        Assert.Equal(1, f.FileReads);
        Assert.Empty(report.Appendices);
        var none = await f.Service.ComposeAsync(f.PatientUid, [], []);
        Assert.Empty(none.Html);
        Assert.Equal(1, f.DocumentReads);
        Assert.Equal(1, f.FileReads);
    }

    [Fact]
    public async Task SignedConsultationUsesAuthoritativePdfAndUploadedPdfRetainsAllPagesInOrder()
    {
        var f = new Sources { Signed = true, Mime = "application/pdf", FileBytes = Pdf("External first", "External second") };
        var report = await f.Service.ComposeAsync(f.PatientUid, [f.Link], [f.FileSelection]);
        Assert.Equal(2, report.Appendices.Count);
        Assert.Contains("Appendix 1", report.Html);
        Assert.Contains("Appendix 2", report.Html);
        Assert.DoesNotContain("Stored text must not replace signed PDF", report.Html);
        Assert.Equal(f.SignedBytes, report.Appendices[0].PdfContent);
        Assert.Equal(f.FileBytes, report.Appendices[1].PdfContent);
        var combined = await Assembler().CombineAsync([Pdf("Letter"), Pdf("Appendix 1"),
            report.Appendices[0].PdfContent, Pdf("Appendix 2"), report.Appendices[1].PdfContent]);
        using var pdf = PdfDocument.Open(combined);
        Assert.Equal(7, pdf.NumberOfPages);
        Assert.Equal(new[] {"Letter", "Appendix 1", "Signed findings", "Signed conclusion", "Appendix 2", "External first", "External second"},
            pdf.GetPages().Select(x => x.Text).ToArray());
        Assert.Equal(3, f.Audits);
    }

    [Fact]
    public async Task PreviewAndSentArtifactIncludeFullPdfAndRemainIndependentOfEditedArchivedSourcesAndDemographics()
    {
        var sources = new Sources { Signed = true };
        var f = new ReferralFinalizationTests.Fixture(sources.Service, Assembler(),
            Stub<IPdfRenderer>((_, args) => Task.FromResult(Pdf(((string)args[0]!).Contains("<h1>Appendix 1") ? "Appendix 1" : "Referral letter"))));
        sources.PatientUid = f.Patient.PatientUid;
        var preview = await f.Service.PreviewLetterAsync(f.Patient.PatientUid, f.Source.ReferralUid);
        using (var pdf = PdfDocument.Open(preview!))
        {
            Assert.Equal(4, pdf.NumberOfPages);
            Assert.Equal("Signed conclusion", pdf.GetPage(4).Text);
        }
        await f.Send();
        var artifact = f.Artifact!;
        using (var pdf = PdfDocument.Open(artifact.PdfContent))
        {
            Assert.Equal(4, pdf.NumberOfPages);
            Assert.Equal("Signed findings", pdf.GetPage(3).Text);
        }
        using var snapshot = JsonDocument.Parse(artifact.SnapshotJson);
        var source = snapshot.RootElement.GetProperty("ReportSources")[0];
        Assert.Equal(Hash(sources.SignedBytes), source.GetProperty("Sha256").GetString());
        Assert.Equal(1, source.GetProperty("AppendixNumber").GetInt32());
        sources.SignedBytes = Pdf("Changed content"); sources.Failure = "archived-document";
        f.Patient.FirstName = "Changed"; f.Source.Select();
        var reads = sources.DocumentReads;
        var opened = await f.Service.OpenArtifactAsync(f.Patient.PatientUid, f.Source.ReferralUid);
        await using var stream = opened!.Content;
        using var bytes = new MemoryStream(); await stream.CopyToAsync(bytes);
        Assert.Equal(artifact.PdfContent, bytes.ToArray());
        Assert.Equal(Hash(bytes.ToArray()), artifact.Sha256);
        Assert.Equal(reads, sources.DocumentReads);
        Assert.Equal(1, f.StoreCalls);
    }

    [Theory]
    [InlineData("missing-document")]
    [InlineData("wrong-patient-document")]
    [InlineData("wrong-uid-document")]
    [InlineData("archived-document")]
    [InlineData("empty-text")]
    [InlineData("missing-template")]
    [InlineData("template-unavailable")]
    [InlineData("missing-final-pdf")]
    [InlineData("storage")]
    [InlineData("permission")]
    [InlineData("audit")]
    public async Task UnavailableOrRestrictedDocumentCannotFinalizeOrLeaveArtifact(string failure)
    {
        var sources = new Sources { Failure = failure, Signed = failure is "missing-final-pdf" or "storage" };
        var f = new ReferralFinalizationTests.Fixture(sources.Service);
        sources.PatientUid = f.Patient.PatientUid;
        await Assert.ThrowsAnyAsync<Exception>(() => f.Service.PreviewLetterAsync(f.Patient.PatientUid, f.Source.ReferralUid));
        await Assert.ThrowsAnyAsync<Exception>(() => f.Send());
        Assert.Null(f.Artifact); Assert.Equal(0, f.StoreCalls);
        Assert.Equal(ReferralStatus.Draft, f.Current.Status);
        if (failure == "permission") Assert.Equal(0, sources.DocumentReads);
    }

    [Theory]
    [InlineData("missing-file")]
    [InlineData("archived-file")]
    [InlineData("wrong-patient-file")]
    [InlineData("wrong-uid-file")]
    [InlineData("missing-file-content")]
    [InlineData("wrong-content-patient")]
    [InlineData("hash")]
    [InlineData("length")]
    [InlineData("unsupported")]
    [InlineData("invalid-utf8")]
    public async Task SelectedFileContentFailuresAreActionableAndNeverSilentlyOmitted(string failure)
    {
        var f = new Sources { Failure = failure };
        if (failure == "invalid-utf8") f.FileBytes = [255];
        var exception = await Assert.ThrowsAsync<ReferralClinicalSelectionRuleException>(() =>
            f.Service.ComposeAsync(f.PatientUid, [], [f.FileSelection]));
        Assert.Contains("selection", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tenants/", exception.Message);
    }

    [Fact]
    public async Task OptionsOnlyOfferCurrentPatientActiveFilesAndRespectDocumentPermission()
    {
        var f = new Sources();
        var options = await f.Service.GetFileOptionsAsync(f.PatientUid);
        Assert.Single(options); Assert.Equal(f.FileUid, options[0].FileUid);
        f.Failure = "permission";
        Assert.Empty(await f.Service.GetFileOptionsAsync(f.PatientUid));
    }

    [Fact]
    public async Task PersistedFileChoiceIsComposedAndPreservedByExistingFinalization()
    {
        var sources = new Sources();
        var f = new ReferralFinalizationTests.Fixture(sources.Service);
        sources.PatientUid = f.Patient.PatientUid;
        f.Source.Permissions.Add(PermissionKeys.DocumentsView);
        f.Source.Select(new("FILE", FileUid: sources.FileUid));
        var preview = await f.Service.PreviewLetterAsync(f.Patient.PatientUid, f.Source.ReferralUid);
        Assert.Contains("External findings\nNo acute change.", Encoding.UTF8.GetString(preview!));
        await f.Send();
        Assert.Contains("External findings\nNo acute change.", Encoding.UTF8.GetString(f.Artifact!.PdfContent));
        using var snapshot = JsonDocument.Parse(f.Artifact.SnapshotJson);
        Assert.Equal(sources.FileUid, snapshot.RootElement.GetProperty("ClinicalSelections")[0].GetProperty("FileUid").GetGuid());
        var original = f.Artifact.PdfContent.ToArray();
        sources.FileBytes = "Changed content"u8.ToArray(); sources.Failure = "archived-file";
        f.Source.Select(); f.Patient.LastName = "Changed";
        var opened = await f.Service.OpenArtifactAsync(f.Patient.PatientUid, f.Source.ReferralUid);
        await using var stream = opened!.Content;
        using var bytes = new MemoryStream(); await stream.CopyToAsync(bytes);
        Assert.Equal(original, bytes.ToArray());
    }

    [Fact]
    public async Task MissingPersistedFileCannotFinalize()
    {
        var sources = new Sources { Failure = "missing-file" };
        var f = new ReferralFinalizationTests.Fixture(sources.Service);
        sources.PatientUid = f.Patient.PatientUid;
        f.Source.Permissions.Add(PermissionKeys.DocumentsView);
        f.Source.Select(new("FILE", FileUid: sources.FileUid));
        await Assert.ThrowsAsync<ReferralClinicalSelectionRuleException>(() => f.Send());
        Assert.Null(f.Artifact); Assert.Equal(0, f.StoreCalls);
    }

    [Fact]
    public async Task CrossTenantSignedArtifactIsRejectedBeforeStorageReadOrPdfDisclosure()
    {
        var patient = Guid.NewGuid(); var document = Guid.NewGuid(); var tenant = Guid.NewGuid();
        var storageReads = 0;
        var signing = new ConsultationSigningService(null!,
            Stub<IPatientDocumentRepository>((_, args) => Task.FromResult<PatientDocumentDetailsResponse?>(new()
            { PatientUid = patient, DocumentUid = (Guid)args[0]!, Status = "Signed", FinalizedAt = DateTime.UtcNow })), null!,
            Stub<IClinicalOutputArtifactRepository>((_, args) => Task.FromResult<ClinicalOutputArtifact?>(new(Guid.NewGuid(), patient,
                "PatientDocument", (Guid)args[1]!, Guid.NewGuid(), "FinalPdf", "FileSystem",
                $"tenants/{Guid.NewGuid():N}/clinical-artifacts/patient-documents/{patient:N}/{document:N}/report.pdf",
                "application/pdf", 10, "hash", "Final", 7, DateTime.UtcNow))),
            Stub<IPatientFileStorage>((_, _) => { storageReads++; throw new InvalidOperationException("Cross-tenant storage must never be read"); }),
            Stub<ITenantContext>((_, _) => tenant), NullLogger<ConsultationSigningService>.Instance);
        var sources = new Sources(signing) { PatientUid = patient, DocumentUid = document, Signed = true };
        var error = await Assert.ThrowsAsync<ReferralClinicalSelectionRuleException>(() => sources.Service.ComposeAsync(patient, [sources.Link], []));
        Assert.DoesNotContain("tenants/", error.Message);
        Assert.Equal(0, storageReads);
    }

    [Theory]
    [InlineData("other-tenant")]
    [InlineData("other-patient")]
    [InlineData("other-file")]
    public async Task UploadedReportStorageCannotCrossTenantPatientOrFileBoundary(string failure)
    {
        var tenant = Guid.NewGuid(); var patient = Guid.NewGuid(); var file = Guid.NewGuid(); var storageReads = 0;
        var stored = new PatientFile { PatientUid = patient, FileUid = file, OriginalFileName = "report.pdf",
            ContentType = "application/pdf", RowVersion = "version",
            StorageKey = $"tenants/{(failure == "other-tenant" ? Guid.NewGuid() : tenant):N}/" +
                PatientFileNaming.StorageKey(failure == "other-patient" ? Guid.NewGuid() : patient, failure == "other-file" ? Guid.NewGuid() : file) };
        var service = new PatientFileService(Stub<IPatientFileRepository>((_, _) => Task.FromResult<PatientFile?>(stored)),
            Stub<IPatientFileStorage>((_, _) => { storageReads++; throw new InvalidOperationException("Invalid storage must never be read"); }),
            null!, null!, Stub<ITenantContext>((_, _) => tenant), Options.Create(new PatientFileUploadOptions()), NullLogger<PatientFileService>.Instance);
        Assert.Null(await service.OpenContentAsync(patient, file));
        Assert.Equal(0, storageReads);
    }

    [Fact]
    public async Task ImageOnlyPdfPagesRemainCompleteWithoutTextExtraction()
    {
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAEElEQVR4nGP4//8/AwQAWQAp5AX7XiD3SwAAAABJRU5ErkJggg==");
        var builder = new PdfDocumentBuilder();
        builder.AddPage(400, 600).AddPng(png, new PdfRectangle(20, 20, 380, 580));
        builder.AddPage(600, 400).AddPng(png, new PdfRectangle(20, 20, 580, 380));
        var source = builder.Build();
        var merged = await Assembler().CombineAsync([Pdf("Cover"), source]);
        using var original = PdfDocument.Open(source); using var final = PdfDocument.Open(merged);
        Assert.Equal(3, final.NumberOfPages);
        for (var i = 1; i <= 2; i++)
        {
            Assert.Empty(final.GetPage(i + 1).Text);
            Assert.Equal(1, final.GetPage(i + 1).NumberOfImages);
            Assert.Equal(original.GetPage(i).Width, final.GetPage(i + 1).Width);
            Assert.Equal(original.GetPage(i).Height, final.GetPage(i + 1).Height);
            Assert.Equal(original.GetPage(i).GetImages().Single().RawBytes.ToArray(), final.GetPage(i + 1).GetImages().Single().RawBytes.ToArray());
        }
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("annotations")]
    [InlineData("forms")]
    public async Task UnsupportedPdfBlocksPreviewAndAtomicSend(string failure)
    {
        var source = new Sources { Signed = true, SignedBytes = failure == "malformed" ? "Not PDF"u8.ToArray() : InteractivePdf(failure) };
        var f = new ReferralFinalizationTests.Fixture(source.Service, Assembler(),
            Stub<IPdfRenderer>((_, _) => Task.FromResult(Pdf("Letter"))));
        source.PatientUid = f.Patient.PatientUid;
        await Assert.ThrowsAsync<ReferralClinicalSelectionRuleException>(() => f.Service.PreviewLetterAsync(f.Patient.PatientUid, f.Source.ReferralUid));
        await Assert.ThrowsAsync<ReferralClinicalSelectionRuleException>(() => f.Send());
        Assert.Null(f.Artifact); Assert.Equal(0, f.StoreCalls);
    }

    private static byte[] InteractivePdf(string kind)
    {
        // Minimal valid PDF with an annotation or AcroForm: import must fail before discarding it.
        var objects = new[] { $"<< /Type /Catalog /Pages 2 0 R {(kind == "forms" ? "/AcroForm << /Fields [] >>" : "")} >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 600 800] /Resources << >> {(kind == "annotations" ? "/Annots [4 0 R]" : "")} >>",
            "<< /Type /Annot /Subtype /Text /Rect [10 10 50 50] /Contents (Clinical annotation) >>" };
        var text = new StringBuilder("%PDF-1.4\n"); var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++) { offsets.Add(text.Length); text.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n"); }
        var xref = text.Length;
        text.Append($"xref\n0 5\n0000000000 65535 f \n");
        foreach (var offset in offsets) text.Append($"{offset:D10} 00000 n \n");
        text.Append($"trailer\n<< /Size 5 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return Encoding.ASCII.GetBytes(text.ToString());
    }

    private static ReferralPdfAssembler Assembler() => new(NullLogger<ReferralPdfAssembler>.Instance);
    private static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
    private static byte[] Pdf(params string[] pages)
    {
        var builder = new PdfDocumentBuilder(); var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var text in pages) builder.AddPage(PageSize.A4).AddText(text, 12, new PdfPoint(40, 700), font);
        return builder.Build();
    }
    private static T Stub<T>(Func<string, object?[], object> call) where T : class
    {
        var proxy = DispatchProxy.Create<T, ReferralLetterCompositionTests.StrictProxy>();
        ((ReferralLetterCompositionTests.StrictProxy)(object)proxy).Call = call; return proxy;
    }

    private sealed class Sources
    {
        public Guid PatientUid = Guid.NewGuid();
        public Guid DocumentUid = Guid.NewGuid();
        public Guid FileUid = Guid.NewGuid();
        public string Failure = "";
        public bool Signed, Structured;
        public string Mime = "text/plain";
        public byte[] FileBytes = Encoding.UTF8.GetBytes("External findings\nNo acute change.");
        public byte[] SignedBytes = Pdf("Signed findings", "Signed conclusion");
        public int Audits, DocumentReads, FileReads;
        public ReferralDocumentLinkResponse Link => new() { DocumentUid = DocumentUid, Title = "Selected consultation", DocumentType = "ConsultationReport", DocumentStatus = "Draft" };
        public PatientReferralClinicalSelectionResponse FileSelection => new(Guid.NewGuid(), "FILE", null, null, null, DateTime.UtcNow, 7, FileUid);
        public IReferralReportContentService Service { get; }
        public Sources(IConsultationSigningService? signing = null)
        {
            Service = new ReferralReportContentService(
                Stub<IPatientDocumentService>((_, args) =>
                {
                    DocumentReads++;
                    if (Failure == "template-unavailable") throw new InvalidOperationException("Historical template unavailable");
                    return Task.FromResult<PatientDocumentDetailsResponse?>(Failure == "missing-document" ? null : new()
                    {
                        PatientUid = Failure == "wrong-patient-document" ? Guid.NewGuid() : PatientUid,
                        DocumentUid = Failure == "wrong-uid-document" ? Guid.NewGuid() : (Guid)args[0]!,
                        Title = "Selected consultation", DocumentType = "ConsultationReport", IsConsultationReport = Signed,
                        Status = Failure == "archived-document" ? "Archived" : Signed ? "Signed" : "Draft",
                        Content = Failure == "empty-text" ? "" : Signed ? "Stored text must not replace signed PDF" : "Complete consultation findings",
                        RowVersion = "document-version", CreatedByDisplayName = "Dr Specialist",
                        StructuredDataJson = Structured || Failure == "missing-template" ? "{\"schemaVersion\":1,\"values\":{\"assessment\":\"Continue 5 mg daily.\\nReview in 6 weeks. <script>\"}}" : null,
                        TemplateDefinition = Structured ? new() { SchemaVersion = 1, Sections = [new() { Id = "section", Key = "section", Title = "Findings", Fields = [new() { Id = "assessment", Key = "assessment", Type = TemplateFieldTypes.TextArea, Label = "Assessment" }] }] } : null
                    });
                }), Stub<IPatientFileService>((method, _) => method switch
                {
                    "GetByPatientUidAsync" => Task.FromResult<IReadOnlyList<PatientFileResponse>>([File(), File() with { PatientUid = Guid.NewGuid() }, File() with { Status = "Archived" }]),
                    "GetByUidAsync" => Task.FromResult<PatientFileResponse?>(Failure == "missing-file" ? null : File()),
                    "OpenContentAsync" => OpenFile(),
                    _ => throw new InvalidOperationException(method)
                }), signing ?? Stub<IConsultationSigningService>((_, _) => Failure == "storage" ? throw new IOException("internal storage path") :
                    Task.FromResult<ClinicalArtifactContent?>(Failure == "missing-final-pdf" ? null : new(new MemoryStream(SignedBytes), "consultation.pdf", "application/pdf", SignedBytes.Length))),
                new TemplateInstanceRuntime(new TemplateDefinitionSerializer(new TemplateDefinitionValidator())),
                Stub<ICurrentUserPermissionService>((_, _) => Task.FromResult<IReadOnlySet<string>>(Failure == "permission" ? new HashSet<string>() : new HashSet<string> { PermissionKeys.DocumentsView })),
                Stub<IStructuredReadAuditService>((_, args) =>
                {
                    if (Failure == "audit") throw new InvalidOperationException("Audit unavailable");
                    Assert.Equal(PatientUid, args[3]); Audits++; return Task.FromResult(Guid.NewGuid());
                }));
        }
        private PatientFileResponse File() => new(Failure == "wrong-uid-file" ? Guid.NewGuid() : FileUid,
            Failure == "wrong-patient-file" ? Guid.NewGuid() : PatientUid, "external.txt", Failure == "unsupported" ? "image/png" : Mime,
            Failure == "length" ? FileBytes.Length + 1 : FileBytes.Length, ".txt", Failure == "hash" ? "bad" : Hash(FileBytes),
            null, "External report", "Selected external report", "Source clinic", "Dr External", new(2026, 6, 1), new(2026, 6, 2),
            Failure == "archived-file" ? "Archived" : "Active", DateTime.UtcNow, 7, null, null, "file-version");
        private Task<PatientFileContent?> OpenFile()
        {
            FileReads++;
            var data = FileBytes;
            return Task.FromResult<PatientFileContent?>(Failure == "missing-file-content" ? null : new(FileUid,
                Failure == "wrong-content-patient" ? Guid.NewGuid() : PatientUid, new MemoryStream(data), "external.txt", File().ContentType, File().FileSizeBytes));
        }
    }
}
