using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.PatientDocuments;
using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.PatientDocuments.Repositories;
using MicroEMR.Application.PatientDocuments.Services;
using MicroEMR.Application.PatientFiles;
using MicroEMR.Application.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ConsultationSigningTests
{
    private const string Version = "AAAAAAAAAAE=";
    private readonly Guid patient = Guid.NewGuid(), document = Guid.NewGuid(), tenant = Guid.NewGuid();
    private static readonly byte[] PdfBytes = "%PDF-1.7 preserved clinical content"u8.ToArray();

    [Theory]
    [InlineData("TO")]
    [InlineData("TO", "CC")]
    public async Task SavesPdfBeforeReturningArtifactMetadataAndUsesLockedSnapshots(params string[] types)
    {
        var snapshot = Snapshot(types);
        var storage = new Storage();
        var pdf = Proxy<IClinicalPdfPreviewService>((method, args) =>
        {
            Assert.Equal(nameof(IClinicalPdfPreviewService.RenderConsultationFinalAsync), method.Name);
            Assert.Same(snapshot, args![0]);
            return Task.FromResult(PdfBytes);
        });
        var repository = Proxy<IConsultationSigningRepository>((_, args) => Commit(args!));
        async Task<PatientDocumentDetailsResponse> Commit(object?[] args)
        {
            Assert.Equal(patient, args[0]); Assert.Equal(document, args[1]);
            Assert.Equal(Version, args[2]); Assert.Equal(42L, args[3]);
            var create = (Func<ConsultationSigningContext, CancellationToken, Task<CreateClinicalOutputArtifact>>)args[4]!;
            var artifact = await create(snapshot, default);
            Assert.Equal(PdfBytes, storage.Bytes);
            Assert.Equal(storage.Key, artifact.StorageKey);
            Assert.StartsWith($"tenants/{tenant:N}/clinical-artifacts/patient-documents/{patient:N}/{document:N}/", artifact.StorageKey);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(PdfBytes)).ToLowerInvariant(), artifact.Sha256);
            Assert.Equal(ClinicalArtifactTypes.PatientDocument, artifact.SourceType);
            Assert.Equal(42L, artifact.CreatedBy);
            return snapshot.Document;
        }
        Assert.Same(snapshot.Document, await Service(repository, pdf, storage).SignAsync(patient, document, new(Version), 42));
    }

    [Theory]
    [InlineData()]
    [InlineData("CC")]
    public async Task MissingToNeverRendersOrStoresPdf(params string[] types)
    {
        var repository = Proxy<IConsultationSigningRepository>((_, args) => Invoke(args!, Snapshot(types)));
        await Assert.ThrowsAsync<ArgumentException>(() => Service(repository, null!, new Storage())
            .SignAsync(patient, document, new(Version), 42));
    }

    [Fact]
    public async Task StaleVersionNeverInvokesRenderingOrStorage()
    {
        var repository = Proxy<IConsultationSigningRepository>((_, _) =>
            Task.FromException<PatientDocumentDetailsResponse>(new PatientDocumentConcurrencyException("stale")));
        var storage = new Storage();
        await Assert.ThrowsAsync<PatientDocumentConcurrencyException>(() => Service(repository, null!, storage)
            .SignAsync(patient, document, new(Version), 42));
        Assert.Null(storage.Bytes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RenderOrStorageFailureDoesNotReturnAnArtifact(bool failRender)
    {
        var repository = Proxy<IConsultationSigningRepository>((_, args) => Invoke(args!, Snapshot("TO")));
        var pdf = Proxy<IClinicalPdfPreviewService>((_, _) => failRender
            ? Task.FromException<byte[]>(new IOException("render failed")) : Task.FromResult(PdfBytes));
        var storage = new Storage { FailSave = !failRender };
        await Assert.ThrowsAsync<IOException>(() => Service(repository, pdf, storage)
            .SignAsync(patient, document, new(Version), 42));
        Assert.Null(storage.Bytes);
    }

    [Fact]
    public async Task AmbiguousCommitDoesNotDeleteAnAlreadySavedClinicalPdf()
    {
        var repository = Proxy<IConsultationSigningRepository>((_, args) => FailCommit(args!));
        async Task<PatientDocumentDetailsResponse> FailCommit(object?[] args)
        {
            await Invoke(args, Snapshot("TO"));
            throw new IOException("commit result lost");
        }
        var storage = new Storage();
        var pdf = Proxy<IClinicalPdfPreviewService>((_, _) => Task.FromResult(PdfBytes));
        await Assert.ThrowsAsync<IOException>(() => Service(repository, pdf, storage)
            .SignAsync(patient, document, new(Version), 42));
        Assert.Equal(PdfBytes, storage.Bytes);
    }

    [Fact]
    public async Task FinalReadUsesStoredBytesWithoutRenderingAndRejectsWrongPatientOrTenantKey()
    {
        var signed = Snapshot("TO").Document;
        signed.Status = "Signed"; signed.FinalizedAt = DateTime.UtcNow;
        var documents = Proxy<IPatientDocumentRepository>((_, _) => Task.FromResult<PatientDocumentDetailsResponse?>(signed));
        var key = $"tenants/{tenant:N}/clinical-artifacts/patient-documents/{patient:N}/{document:N}/final.pdf";
        var artifact = new ClinicalOutputArtifact(Guid.NewGuid(), patient, "PatientDocument", document,
            signed.TemplateVersionUid!.Value, "FinalPdf", "FileSystem", key, "application/pdf", PdfBytes.Length, "hash", "Available", 42, DateTime.UtcNow);
        var artifacts = Proxy<IClinicalOutputArtifactRepository>((_, _) => Task.FromResult<ClinicalOutputArtifact?>(artifact));
        var storage = new Storage(); await storage.SaveAsync(new MemoryStream(PdfBytes), key);
        var service = new ConsultationSigningService(null!, documents, null!, artifacts, storage,
            new TenantContext(tenant, "test", "Test"), NullLogger<ConsultationSigningService>.Instance);
        Assert.Null(await service.OpenFinalPdfAsync(Guid.NewGuid(), document));
        var result = await service.OpenFinalPdfAsync(patient, document);
        using var output = new MemoryStream(); await result!.Content.CopyToAsync(output);
        Assert.Equal(PdfBytes, output.ToArray());
        artifact = artifact with { StorageKey = "tenants/another-tenant/final.pdf" };
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.OpenFinalPdfAsync(patient, document));
    }

    [Fact]
    public async Task SignedDocumentRejectsContentUpdateBeforeTemplateProcessing()
    {
        var signed = Snapshot("TO").Document; signed.Status = "Signed";
        var repository = Proxy<IPatientDocumentRepository>((method, _) => method.Name == nameof(IPatientDocumentRepository.GetByUidAsync)
            ? Task.FromResult<PatientDocumentDetailsResponse?>(signed) : throw new InvalidOperationException("Mutation invoked"));
        var service = new PatientDocumentService(repository, null!, null!, null!, null!);
        await Assert.ThrowsAsync<PatientDocumentNotDraftException>(() => service.UpdateDraftAsync(document, new(), 42));
    }

    [Fact]
    public async Task SignedDetailsKeepRecipientSnapshotsWithoutConsultingMutableTemplateOrProviderData()
    {
        var snapshot = Snapshot("TO", "CC");
        snapshot.Document.Status = "Signed";
        snapshot.Document.FinalizedAt = snapshot.SignedAtUtc;
        var repository = Proxy<IPatientDocumentRepository>((method, _) => method.Name switch
        {
            nameof(IPatientDocumentRepository.GetByUidAsync) => Task.FromResult<PatientDocumentDetailsResponse?>(snapshot.Document),
            nameof(IPatientDocumentRepository.GetRecipientsAsync) => Task.FromResult(snapshot.Recipients),
            _ => throw new InvalidOperationException("Final details must not consult live template data")
        });
        var service = new PatientDocumentService(repository, null!, null!, null!, null!);
        var result = await service.GetByUidAsync(document);
        Assert.Same(snapshot.Recipients, result!.FinalRecipients);
        Assert.True(result.IsConsultationReport);
    }

    [Fact]
    public async Task ApiIgnoresSpoofedSignerAndUsesResolvedClinicalActor()
    {
        var request = JsonSerializer.Deserialize<SignConsultationRequest>(
            "{\"RowVersion\":\"AAAAAAAAAAE=\",\"ActorUserId\":999,\"SignerDisplayName\":\"Spoof\",\"SignedAt\":\"2000-01-01\"}")!;
        var service = Proxy<IConsultationSigningService>((_, args) =>
        {
            Assert.Equal(42L, args![3]);
            return Task.FromResult(Snapshot("TO").Document);
        });
        var controller = new MicroEMR.Api.Controllers.PatientDocumentsController(null!, null!, null!, null!, null!, null!)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        ClinicalUserActorContext.Set(controller.HttpContext, 42);
        Assert.IsType<OkObjectResult>(await controller.SignConsultation(patient, document, request, service, default));
    }

    [Fact]
    public void SqlLocksAndCommitsArtifactStatusAndAuditWithoutChangingRecipients()
    {
        var sql = File.ReadAllText(Path.Combine(Root(), "db/tenant-clinical/migrations/0064-consultation-report-signing.sql"));
        Assert.Contains("WITH(UPDLOCK, HOLDLOCK)", sql);
        Assert.Contains("@Version <> @ExpectedRowVersion", sql);
        Assert.Contains("t.TemplateType = N'CONSULTATION_REPORT'", sql);
        Assert.Contains("u.UserId = @ActorUserId AND u.IsActive = 1", sql);
        Assert.Contains("SYSUTCDATETIME()", sql);
        Assert.Contains("N'PatientDocumentSigned'", sql);
        Assert.Contains("INSERT dbo.ClinicalOutputArtifact", sql);
        Assert.Contains("DocumentStatus = N'Signed'", sql);
        Assert.DoesNotContain("UPDATE dbo.PatientDocumentRecipient", sql);
        Assert.DoesNotContain("PatientCareTeam", sql);
        Assert.DoesNotContain("PatientReferral", sql);
    }

    private ConsultationSigningContext Snapshot(params string[] types) => new(new()
    {
        PatientUid = patient, DocumentUid = document, Status = "Draft", RowVersion = Version,
        TemplateUid = Guid.NewGuid(), TemplateVersionUid = Guid.NewGuid(), StructuredDataJson = "{}"
    }, types.Select((type, i) => new PatientDocumentRecipientResponse(Guid.NewGuid(), i + 1, type,
        Guid.NewGuid(), "Saved recipient", "Saved clinic", "416-555-0100")).ToArray(),
        Guid.NewGuid(), "Authenticated signer", new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc));

    private ConsultationSigningService Service(IConsultationSigningRepository repository, IClinicalPdfPreviewService pdf, Storage storage) =>
        new(repository, null!, pdf, null!, storage, new TenantContext(tenant, "test", "Test"), NullLogger<ConsultationSigningService>.Instance);

    private static async Task<PatientDocumentDetailsResponse> Invoke(object?[] args, ConsultationSigningContext snapshot)
    {
        await ((Func<ConsultationSigningContext, CancellationToken, Task<CreateClinicalOutputArtifact>>)args[4]!)(snapshot, default);
        return snapshot.Document;
    }

    internal static T Proxy<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, ConsultationRecipientServiceTests.TestProxy>();
        ((ConsultationRecipientServiceTests.TestProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    internal static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MicroEMR.slnx"))) directory = directory.Parent;
        return directory!.FullName;
    }

    private sealed class Storage : IPatientFileStorage
    {
        public byte[]? Bytes; public string? Key; public bool FailSave;
        public async Task SaveAsync(Stream content, string key, CancellationToken token = default)
        {
            if (FailSave) throw new IOException("storage failed");
            using var output = new MemoryStream(); await content.CopyToAsync(output, token); Bytes = output.ToArray(); Key = key;
        }
        public Task<Stream> OpenReadAsync(string key, CancellationToken token = default) => Task.FromResult<Stream>(new MemoryStream(Bytes!));
        public Task<bool> ExistsAsync(string key, CancellationToken token = default) => Task.FromResult(Bytes is not null);
        public Task DeleteAsync(string key, CancellationToken token = default) => throw new InvalidOperationException("Never delete a possibly committed artifact");
    }
}
