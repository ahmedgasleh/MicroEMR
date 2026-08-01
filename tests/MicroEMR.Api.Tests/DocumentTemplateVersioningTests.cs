using System.Reflection;
using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.PatientDocuments.Repositories;
using MicroEMR.Application.PatientDocuments.Services;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class DocumentTemplateVersioningTests
{
    [Fact]
    public async Task CreateDraft_DelegatesVersionNumberGenerationToRepository()
    {
        var templateUid = Guid.NewGuid();
        var response = Version(templateUid, 2, "Draft");
        var repository = Proxy<IDocumentTemplateVersionRepository>((method, arguments) =>
            method.Name == nameof(IDocumentTemplateVersionRepository.CreateDraftAsync)
                ? Task.FromResult<DocumentTemplateVersionResponse?>(response)
                : throw new NotSupportedException(method.Name));
        var service = new DocumentTemplateVersionService(repository);

        var result = await service.CreateDraftVersionAsync(templateUid, 42);

        Assert.Same(response, result);
    }

    [Fact]
    public async Task UpdateDraft_RejectsInvalidRowVersionBeforePersistence()
    {
        var calls = 0;
        var repository = Proxy<IDocumentTemplateVersionRepository>((method, _) =>
        {
            calls++;
            throw new NotSupportedException(method.Name);
        });
        var service = new DocumentTemplateVersionService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateDraftVersionAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            new UpdateDocumentTemplateVersionRequest { RowVersion = "invalid" }, 42));

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Publish_PreservesTemplateAndVersionRelationship()
    {
        var templateUid = Guid.NewGuid();
        var versionUid = Guid.NewGuid();
        var rowVersion = Convert.ToBase64String(new byte[8]);
        var repository = Proxy<IDocumentTemplateVersionRepository>((method, arguments) =>
        {
            Assert.Equal(nameof(IDocumentTemplateVersionRepository.PublishAsync), method.Name);
            Assert.Equal(templateUid, arguments![0]);
            Assert.Equal(versionUid, arguments[1]);
            Assert.Equal(rowVersion, arguments[2]);
            return Task.FromResult<DocumentTemplateVersionResponse?>(Version(templateUid, 2, "Published"));
        });
        var service = new DocumentTemplateVersionService(repository);

        var result = await service.PublishVersionAsync(
            templateUid, versionUid,
            new ChangeDocumentTemplateVersionStatusRequest { RowVersion = rowVersion }, 42);

        Assert.NotNull(result);
        Assert.Equal("Published", result.Status);
    }

    [Fact]
    public void Migration_BackfillsVersionOneAndPreservesDocumentProvenance()
    {
        var sql = Migration();

        Assert.Contains("CREATE TABLE dbo.DocumentTemplateVersion", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VersionNumber INT NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VersionStatus IN (N'Draft', N'Published', N'Retired')", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UQ_DocumentTemplateVersion_Number UNIQUE (TemplateUid, VersionNumber)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UX_DocumentTemplateVersion_Current", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHERE IsCurrent = 1", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("template.TemplateUid, 1, template.TemplateHtml", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SET TemplateVersionUid = version.TemplateVersionUid", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("version.VersionNumber = 1", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FK_PatientDocument_TemplateVersionUid", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Migration_EnforcesImmutablePublishingAndAtomicSingleCurrentVersion()
    {
        var sql = Migration();

        Assert.Contains("VersionStatus = N'Draft'", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RowVersion = @ExpectedRowVersion", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Published or retired template versions are immutable", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BEGIN TRANSACTION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SET VersionStatus = N'Retired', IsCurrent = 0", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SET VersionStatus = N'Published', IsCurrent = 1", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Published template content cannot be edited in place", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CreateDraft", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UpdateDraft", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("N'Publish'", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DocumentCreation_ResolvesPublishedContentAndStoresExactVersionServerSide()
    {
        var sql = Migration();
        var createStart = sql.LastIndexOf(
            "CREATE OR ALTER PROCEDURE dbo.PatientDocument_Create",
            StringComparison.OrdinalIgnoreCase);
        var createSql = sql[createStart..];

        Assert.Contains("version.IsCurrent = 1", createSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("version.VersionStatus = N'Published'", createSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@ResolvedContent = version.TemplateContent", createSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TemplateUid, TemplateVersionUid", createSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@TemplateUid, @TemplateVersionUid", createSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@ResolvedContent", createSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE dbo.PatientDocumentContent", createSql, StringComparison.OrdinalIgnoreCase);
    }

    private static string Migration() => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "database", "tenant-clinical", "migrations",
        "0017-document-template-versioning.sql"));

    private static DocumentTemplateVersionResponse Version(Guid templateUid, int number, string status) => new()
    {
        TemplateUid = templateUid,
        TemplateVersionUid = Guid.NewGuid(),
        VersionNumber = number,
        Status = status,
        RowVersion = Convert.ToBase64String(new byte[8])
    };

    private static T Proxy<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, InterfaceProxy>();
        ((InterfaceProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    public class InterfaceProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args);
    }
}
