using System.Reflection;
using Microsoft.Extensions.Configuration;
using MicroEMR.Application.PatientDocuments;
using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.PatientDocuments.Repositories;
using MicroEMR.Application.PatientDocuments.Services;
using MicroEMR.Infrastructure.Provisioning;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PatientDocumentRecipientFoundationTests
{
    private const string Version = "AAAAAAAAAAE=";

    [Fact]
    public void NewFinalizationMetadataIsNullable()
    {
        var document = new PatientDocumentDetailsResponse();
        Assert.Null(document.FinalizedAt);
        Assert.Null(document.FinalizedByUserId);
        Assert.Null(document.SignerProviderUid);
        Assert.Null(document.SignerDisplayNameSnapshot);
    }

    [Fact]
    public async Task DraftRecipientReplacementPassesOrderedToAndCcAndReturnsNewVersion()
    {
        var patientUid = Guid.NewGuid();
        var documentUid = Guid.NewGuid();
        var toProvider = Guid.NewGuid();
        var ccProvider = Guid.NewGuid();
        var request = new ReplacePatientDocumentRecipientsRequest
        {
            RowVersion = Version,
            Recipients = [new(1, "TO", toProvider), new(2, "CC", ccProvider)]
        };
        var newVersion = "AAAAAAAAAAI=";
        var expected = new PatientDocumentRecipientsResponse(documentUid, newVersion,
            [new(Guid.NewGuid(), 1, "TO", toProvider, "Dr. To", "Clinic", "123"),
             new(Guid.NewGuid(), 2, "CC", ccProvider, "Dr. CC", null, null)]);
        var replacementCalled = false;
        var repository = Proxy((method, args) => method.Name switch
        {
            nameof(IPatientDocumentRepository.GetByUidAsync) => Task.FromResult<PatientDocumentDetailsResponse?>(
                new() { DocumentUid = documentUid, PatientUid = patientUid, Status = "Draft", RowVersion = Version }),
            nameof(IPatientDocumentRepository.ReplaceDraftRecipientsAsync) => Replace(args),
            _ => throw new NotSupportedException(method.Name)
        });
        Task<PatientDocumentRecipientsResponse?> Replace(object?[]? args)
        {
            replacementCalled = true;
            Assert.Equal(patientUid, args![0]);
            Assert.Equal(documentUid, args[1]);
            Assert.Same(request, args![2]);
            Assert.Equal(42L, args[3]);
            return Task.FromResult<PatientDocumentRecipientsResponse?>(expected);
        }
        // The service forwards the validated collection to the tenant repository without changing it.
        var result = await Service(repository).ReplaceDraftRecipientsAsync(patientUid, documentUid, request, 42);
        Assert.True(replacementCalled);
        Assert.Same(expected, result);
        Assert.Equal(["TO", "CC"], result!.Recipients.Select(x => x.RecipientType));
        Assert.Equal(newVersion, result.RowVersion);
    }

    [Fact]
    public async Task WrongPatientAndNonDraftAndStaleVersionNeverReachMutation()
    {
        var patientUid = Guid.NewGuid();
        var documentUid = Guid.NewGuid();
        var document = new PatientDocumentDetailsResponse
        { PatientUid = Guid.NewGuid(), DocumentUid = documentUid, Status = "Draft", RowVersion = Version };
        var repository = Proxy((method, _) => method.Name switch
        {
            nameof(IPatientDocumentRepository.GetByUidAsync) => Task.FromResult<PatientDocumentDetailsResponse?>(document),
            _ => throw new InvalidOperationException("Mutation must not be called.")
        });
        var service = Service(repository);
        var request = new ReplacePatientDocumentRecipientsRequest { RowVersion = Version };
        Assert.Null(await service.GetRecipientsAsync(patientUid, documentUid));
        Assert.Null(await service.ReplaceDraftRecipientsAsync(patientUid, documentUid, request, 42));
        document.PatientUid = patientUid;
        document.Status = "Signed";
        await Assert.ThrowsAsync<PatientDocumentNotDraftException>(() =>
            service.ReplaceDraftRecipientsAsync(patientUid, documentUid, request, 42));
        document.Status = "Draft";
        document.RowVersion = "AAAAAAAAAAM=";
        await Assert.ThrowsAsync<PatientDocumentConcurrencyException>(() =>
            service.ReplaceDraftRecipientsAsync(patientUid, documentUid, request, 42));
    }

    [Theory]
    [InlineData("BCC", 1)]
    [InlineData("TO", 2)]
    public async Task InvalidTypeOrOrderIsRejected(string type, int order)
    {
        var service = Service(Proxy((method, _) => throw new InvalidOperationException(method.Name)));
        var request = new ReplacePatientDocumentRecipientsRequest
        { RowVersion = Version, Recipients = [new(order, type, Guid.NewGuid())] };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReplaceDraftRecipientsAsync(Guid.NewGuid(), Guid.NewGuid(), request, 42));
    }

    [Fact]
    public void MigrationGuardsOwnershipProviderStatusConcurrencyAndHistory()
    {
        var sql = File.ReadAllText(Path.Combine(Root(), "db", "tenant-clinical", "migrations",
            "0062-patient-document-recipient-foundation.sql"));
        Assert.Contains("FinalizedAt DATETIME2(0) NULL", sql);
        Assert.Contains("SignerProviderId BIGINT NULL", sql);
        Assert.Contains("CREATE TABLE dbo.PatientDocumentRecipient", sql);
        Assert.Contains("RecipientType IN (N'TO', N'CC')", sql);
        Assert.Contains("RecipientOrder > 0", sql);
        Assert.Contains("d.PatientUid = @PatientUid AND d.IsDeleted = 0", sql);
        Assert.Contains("@Status <> N'Draft'", sql);
        Assert.Contains("@Version <> @ExpectedRowVersion", sql);
        Assert.Contains("provider.ProviderUid = requested.ProviderUid AND provider.IsActive = 1", sql);
        Assert.Contains("provider.DisplayName, provider.OrganizationName, provider.Fax", sql);
        Assert.Contains("SET IsDeleted = 1, ReplacedAt", sql);
        Assert.Contains("N'ReplaceDraftRecipients'", sql);
        Assert.Contains("ORDER BY r.RecipientOrder, r.PatientDocumentRecipientId", sql);
        Assert.DoesNotContain("DELETE FROM dbo.PatientDocumentRecipient", sql);
        Assert.DoesNotContain("SET DocumentStatus", sql);
    }

    [Fact]
    public async Task TenantMigrationSourceDiscoversTheAdditiveMigration()
    {
        var source = new FileTenantDatabaseMigrationSource(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenantProvisioning:SqlAssetsPath"] = Path.Combine(AppContext.BaseDirectory, "database")
            }).Build());
        var migration = Assert.Single(await source.GetAvailableMigrationsAsync(),
            item => item.MigrationId == "0062-patient-document-recipient-foundation");
        Assert.Contains("CREATE TABLE dbo.PatientDocumentRecipient", migration.Script);
    }

    private static PatientDocumentService Service(IPatientDocumentRepository repository) =>
        new(repository, null!, null!, null!, null!);

    private static IPatientDocumentRepository Proxy(Func<MethodInfo, object?[]?, object?> handler)
    {
        var proxy = DispatchProxy.Create<IPatientDocumentRepository, RepositoryProxy>();
        ((RepositoryProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    public class RepositoryProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args);
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MicroEMR.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
