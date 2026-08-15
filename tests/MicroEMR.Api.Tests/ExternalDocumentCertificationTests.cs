using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PatientFiles;
using MicroEMR.Application.Patients.Contracts;
using MicroEMR.Application.Patients.Repositories;
using MicroEMR.Application.Tenancy;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Models.PatientFiles;
using Xunit;
using ApiController = MicroEMR.Api.Controllers.PatientFilesController;
using WebController = MicroEMR.Web.Controllers.PatientFilesController;

namespace MicroEMR.Api.Tests;

public sealed class ExternalDocumentCertificationTests
{
    [Fact]
    public async Task ValidExternalReportUploadPreservesMetadataActorTenantAndPatient()
    {
        var repository = new Repository();
        var storage = new Storage();
        var tenant = new Tenant();
        var patientUid = Guid.NewGuid();
        var service = Service(repository, storage, tenant);

        var response = await service.UploadAsync(patientUid, new(
            new MemoryStream("%PDF-1.7 test"u8.ToArray()), "consult.pdf",
            "application/pdf", 13, "Specialist consultation", "Consultation",
            "Cardiology consultation", "Ontario Specialist Clinic", "Dr. Example",
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 4)));

        Assert.Equal(patientUid, repository.PatientUid);
        Assert.Equal(73, repository.Actor);
        Assert.Equal("Cardiology consultation", response.Title);
        Assert.Equal("Ontario Specialist Clinic", response.SourceOrganization);
        Assert.Equal("Dr. Example", response.AuthorName);
        Assert.Equal(new DateOnly(2026, 8, 1), response.DocumentDate);
        Assert.Equal(new DateOnly(2026, 8, 4), response.ReceivedDate);
        Assert.StartsWith($"tenants/{tenant.TenantUid:N}/patients/{patientUid:N}/", storage.Key);
    }

    [Fact]
    public async Task MissingRequiredMetadataAndFutureDatesAreRejectedBeforeStorage()
    {
        var storage = new Storage();
        var service = Service(new Repository(), storage, new Tenant());
        var patientUid = Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentException>(() => service.UploadAsync(patientUid,
            new(new MemoryStream("%PDF-test"u8.ToArray()), "report.pdf", "application/pdf", 9,
                null, null)));
        await Assert.ThrowsAsync<ArgumentException>(() => service.UploadAsync(patientUid,
            new(new MemoryStream("%PDF-test"u8.ToArray()), "report.pdf", "application/pdf", 9,
                null, "Report", "Report", DocumentDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1))));

        Assert.Null(storage.Key);
    }

    [Fact]
    public void WebMetadataModelRequiresTitleAndCategoryAndAllowsHistoricNullableFields()
    {
        var invalid = new UploadPatientFileViewModel();
        var errors = new List<ValidationResult>();
        Validator.TryValidateObject(invalid, new ValidationContext(invalid), errors, true);

        Assert.Contains(errors, x => x.MemberNames.Contains(nameof(invalid.Title)));
        Assert.Contains(errors, x => x.MemberNames.Contains(nameof(invalid.Category)));
        var historic = new PatientFile { OriginalFileName = "legacy.pdf", StorageKey = "legacy",
            ContentType = "application/pdf", RowVersion = "version" };
        Assert.Null(historic.Title);
        Assert.Null(historic.SourceOrganization);
        Assert.Null(historic.DocumentDate);
    }

    [Fact]
    public void ApiAndWebOperationsUseViewAndManagePermissions()
    {
        AssertPermission(typeof(ApiController), PermissionKeys.DocumentsView);
        AssertPermission(typeof(WebController), PermissionKeys.DocumentsView);
        foreach (var name in new[] { nameof(ApiController.Upload), nameof(ApiController.Archive), nameof(ApiController.Restore) })
            AssertPermission(typeof(ApiController).GetMethod(name)!, PermissionKeys.DocumentsManage);
        foreach (var name in new[] { nameof(WebController.Upload), nameof(WebController.Archive), nameof(WebController.Restore) })
            AssertPermission(typeof(WebController).GetMethod(name)!, PermissionKeys.DocumentsManage);
    }

    [Fact]
    public void MigrationIsBackwardCompatiblePatientScopedAtomicAndAudited()
    {
        var sql = File.ReadAllText(Path.Combine(Root(), "db", "tenant-clinical", "migrations",
            "0040-patient-file-external-report-metadata.sql"));
        var manifest = File.ReadAllText(Path.Combine(Root(), "db", "tenant-clinical", "manifest.json"));

        Assert.Contains("0040-patient-file-external-report-metadata", manifest);
        foreach (var column in new[] { "Title NVARCHAR(200) NULL", "SourceOrganization NVARCHAR(200) NULL",
                     "AuthorName NVARCHAR(200) NULL", "DocumentDate DATE NULL", "ReceivedDate DATE NULL" })
            Assert.Contains(column, sql);
        Assert.Contains("WHERE PatientUid = @PatientUid", sql);
        Assert.Contains("AND FileUid = @FileUid", sql);
        Assert.Contains("BEGIN TRANSACTION", sql);
        Assert.Contains("COMMIT TRANSACTION", sql);
        Assert.Contains("INSERT dbo.AuditLog", sql);
        Assert.Contains("@UploadedBy, @PatientId", sql);
        Assert.DoesNotContain("DELETE FROM dbo.PatientFile", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static PatientFileService Service(Repository repository, Storage storage, Tenant tenant) =>
        new(repository, storage, new Patients(), new Actor(), tenant,
            Options.Create(new PatientFileUploadOptions()), NullLogger<PatientFileService>.Instance);

    private static void AssertPermission(MemberInfo member, string permission)
    {
        Assert.Contains(member.GetCustomAttributes(true).OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(),
            x => x.Policy is not null && x.Policy.EndsWith(permission, StringComparison.Ordinal));
    }

    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", ".."));

    private sealed class Repository : IPatientFileRepository
    {
        public Guid PatientUid { get; private set; }
        public long Actor { get; private set; }
        public Task<PatientFile> CreateAsync(Guid patientUid, CreatePatientFileMetadata metadata, long uploadedBy, CancellationToken cancellationToken = default)
        {
            PatientUid = patientUid; Actor = uploadedBy;
            return Task.FromResult(new PatientFile { FileUid = Guid.NewGuid(), PatientUid = patientUid,
                OriginalFileName = metadata.OriginalFileName, StorageKey = metadata.StorageKey,
                ContentType = metadata.ContentType, FileSizeBytes = metadata.FileSizeBytes,
                Description = metadata.Description, Category = metadata.Category, Title = metadata.Title,
                SourceOrganization = metadata.SourceOrganization, AuthorName = metadata.AuthorName,
                DocumentDate = metadata.DocumentDate, ReceivedDate = metadata.ReceivedDate,
                Status = PatientFileStatus.Active, UploadedAtUtc = DateTime.UtcNow,
                UploadedBy = uploadedBy, RowVersion = Convert.ToBase64String(new byte[8]) });
        }
        public Task<IReadOnlyList<PatientFile>> GetByPatientUidAsync(Guid p, CancellationToken c = default) => throw new NotSupportedException();
        public Task<PatientFile?> GetByUidAsync(Guid p, Guid f, CancellationToken c = default) => Task.FromResult<PatientFile?>(null);
        public Task<PatientFile> ArchiveAsync(Guid p, Guid f, string v, long a, CancellationToken c = default) => throw new NotSupportedException();
        public Task<PatientFile> RestoreAsync(Guid p, Guid f, string v, long a, CancellationToken c = default) => throw new NotSupportedException();
    }

    private sealed class Storage : IPatientFileStorage
    {
        public string? Key { get; private set; }
        public async Task SaveAsync(Stream content, string storageKey, CancellationToken cancellationToken = default)
        { Key = storageKey; await content.CopyToAsync(Stream.Null, cancellationToken); }
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) { Key = null; return Task.CompletedTask; }
    }

    private sealed class Patients : IPatientRepository
    {
        public Task<PatientDetailsResponse?> GetByUidAsync(Guid p, CancellationToken c = default) => Task.FromResult<PatientDetailsResponse?>(new());
        public Task<PatientSearchResponse> SearchAsync(string? s, DateOnly? d, int p, int z, bool i, CancellationToken c = default) => throw new NotSupportedException();
        public Task<PatientDetailsResponse> CreateAsync(CreatePatientRequest r, long? a, CancellationToken c = default) => throw new NotSupportedException();
        public Task<PatientDetailsResponse?> UpdateDemographicsAsync(Guid p, UpdatePatientDemographicsRequest r, long? a, CancellationToken c = default) => throw new NotSupportedException();
    }

    private sealed class Actor : IAuthenticatedClinicalUserAccessor
    { public Task<long> GetRequiredUserIdAsync(CancellationToken cancellationToken = default) => Task.FromResult(73L); }
    private sealed class Tenant : ITenantContext
    { public Guid TenantUid { get; } = Guid.NewGuid(); public string TenantKey => "test"; public string DisplayName => "Test"; }
}
