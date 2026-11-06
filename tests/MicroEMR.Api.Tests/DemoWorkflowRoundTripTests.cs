using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.ReadAudit;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PatientFiles;
using MicroEMR.Application.Patients.Contracts;
using MicroEMR.Application.Patients.Repositories;
using MicroEMR.Application.PatientVitals;
using MicroEMR.Application.PatientVitals.Contracts;
using MicroEMR.Infrastructure.PatientFiles;
using MicroEMR.Infrastructure.PatientVitals;
using Xunit;

namespace MicroEMR.Api.Tests;

// Real repositories/procedures and local storage; no shared development tenant or login.
public sealed class DemoWorkflowRoundTripTests(DemoWorkflowDatabase db) : IClassFixture<DemoWorkflowDatabase>
{
    [Fact]
    public async Task VitalsSaveReopenPreservesMeasurementsBmiOwnershipAuditAndConcurrency()
    {
        var patient = await db.Clinical.SeedPatient(false);
        var otherPatient = await db.Clinical.SeedPatient(false);
        var repository = new PatientVitalRepository(db.Clinical.Factory(false), NullLogger<PatientVitalRepository>.Instance);
        var recordedAt = new DateTime(2026, 10, 20, 14, 30, 0, DateTimeKind.Utc);
        var created = (await repository.CreateAsync(patient, new CreatePatientVitalRequest
        {
            RecordedAt = recordedAt, BloodPressureSystolic = 120, BloodPressureDiastolic = 80,
            HeightCm = 180, WeightKg = 81, Notes = "TEST ONLY demo vitals"
        }, 7))!;
        var reopened = (await repository.GetByUidAsync(patient, created.PatientVitalUid))!;
        Assert.Equal(recordedAt, reopened.RecordedAt);
        Assert.Equal(120, reopened.BloodPressureSystolic);
        Assert.Equal(80, reopened.BloodPressureDiastolic);
        Assert.Equal(180m, reopened.HeightCm);
        Assert.Equal(81m, reopened.WeightKg);
        Assert.Equal(25m, reopened.Bmi);
        Assert.Equal("TEST ONLY demo vitals", reopened.Notes);
        Assert.Null(await repository.GetByUidAsync(otherPatient, created.PatientVitalUid));
        var update = new UpdatePatientVitalRequest
        {
            RecordedAt = recordedAt, HeightCm = 180, WeightKg = 90,
            Notes = "TEST ONLY revised vitals", RowVersion = reopened.RowVersion
        };
        var updated = (await repository.UpdateAsync(patient, created.PatientVitalUid, update, 7))!;
        Assert.Equal(27.78m, updated.Bmi);
        await Assert.ThrowsAsync<PatientVitalConcurrencyException>(() => repository.UpdateAsync(patient, created.PatientVitalUid, update, 7));
        var invalid = await Assert.ThrowsAsync<SqlException>(() => repository.CreateAsync(patient,
            new CreatePatientVitalRequest { RecordedAt = recordedAt, OxygenSaturation = 101 }, 7));
        Assert.Equal(51403, invalid.Number);
        Assert.Single(await repository.GetByPatientUidAsync(patient));
        Assert.Equal(2, await db.Audits("PatientVital", created.PatientVitalUid));
    }

    [Fact]
    public async Task DocumentUploadListOpenRetainsBytesMetadataPatientOwnershipAndAudit()
    {
        var patient = await db.Clinical.SeedPatient(false);
        var otherPatient = await db.Clinical.SeedPatient(false);
        var repository = new PatientFileRepository(db.Clinical.Factory(false));
        var storage = new LocalPatientFileStorage(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["PatientFileStorage:LocalRootPath"] = db.StorageRoot }).Build());
        var patients = CppSelectivePrintTests.Stub<IPatientRepository>((method, args) =>
            method == "GetByUidAsync" ? Task.FromResult<PatientDetailsResponse?>(
                (Guid)args[0]! == patient ? new() { PatientUid = patient } : null)
                : throw new InvalidOperationException("Unexpected patient repository call."));
        var actor = CppSelectivePrintTests.Stub<IAuthenticatedClinicalUserAccessor>((_, _) => Task.FromResult(7L));
        var service = new PatientFileService(repository, storage, patients, actor, db.Clinical.Tenant(false),
            Options.Create(new PatientFileUploadOptions()), NullLogger<PatientFileService>.Instance);
        var bytes = Encoding.UTF8.GetBytes("TEST ONLY harmless demo document\nSynthetic patient content.");
        using var input = new MemoryStream(bytes);
        var uploaded = await service.UploadAsync(patient,
            new(input, "demo.txt", "text/plain", bytes.Length, "TEST ONLY", "Report", "Synthetic report"));
        var listed = Assert.Single(await service.GetByPatientUidAsync(patient));
        Assert.Equal(uploaded.FileUid, listed.FileUid);
        Assert.Equal(patient, listed.PatientUid);
        var stored = (await repository.GetByUidAsync(patient, uploaded.FileUid))!;
        Assert.NotEqual(Guid.Empty, uploaded.FileUid);
        Assert.Equal(uploaded.FileUid, stored.FileUid);
        Assert.Equal($"tenants/{db.Clinical.Tenant(false).TenantUid:N}/" + PatientFileNaming.StorageKey(patient, uploaded.FileUid), stored.StorageKey);
        Assert.True(await storage.ExistsAsync(stored.StorageKey));
        Assert.Null(await service.OpenContentAsync(otherPatient, uploaded.FileUid));
        var otherTenant = new PatientFileRepository(db.Clinical.Factory(true));
        Assert.Null(await otherTenant.GetByUidAsync(patient, uploaded.FileUid));
        Assert.Equal(1, await db.Audits("PatientFile", uploaded.FileUid));
        var reopened = await service.OpenContentAsync(patient, uploaded.FileUid);
        Assert.True(reopened is not null,
            $"Upload/list/audit and stored bytes succeeded, but owned OpenContentAsync returned null. " +
            $"Metadata FileUid={uploaded.FileUid}; stored key={stored.StorageKey}; " +
            $"expected key=tenants/{db.Clinical.Tenant(false).TenantUid:N}/" + PatientFileNaming.StorageKey(patient, uploaded.FileUid));
        await using (reopened!.Content)
        {
            using var copy = new MemoryStream();
            await reopened.Content.CopyToAsync(copy);
            Assert.Equal(bytes, copy.ToArray());
        }
        Assert.Equal("demo.txt", reopened.FileName);
        Assert.Equal("text/plain", reopened.ContentType);
        Assert.Equal(bytes.Length, reopened.Length);
        var events = new List<(string Action, Guid File, Guid Patient)>();
        var audit = CppSelectivePrintTests.Stub<IStructuredReadAuditService>((method, args) =>
        {
            Assert.Equal("RecordAsync", method);
            events.Add(((string)args[0]!, (Guid)args[2]!, (Guid)args[3]!));
            return Task.FromResult(Guid.NewGuid());
        });
        var controller = new PatientFilesController(service, audit, NullLogger<PatientFilesController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var download = Assert.IsType<FileStreamResult>(await controller.Content(patient, uploaded.FileUid, default));
        await using (download.FileStream)
        {
            using var copy = new MemoryStream();
            await download.FileStream.CopyToAsync(copy);
            Assert.Equal(bytes, copy.ToArray());
        }
        Assert.Equal((ReadAuditActions.PatientFileDownloaded, uploaded.FileUid, patient), Assert.Single(events));
        var archived = await service.ArchiveAsync(patient, uploaded.FileUid, uploaded.RowVersion);
        Assert.Equal("Archived", archived.Status);
        await Assert.ThrowsAsync<PatientFileConcurrencyException>(() => service.RestoreAsync(patient, uploaded.FileUid, uploaded.RowVersion));
        var restored = await service.RestoreAsync(patient, uploaded.FileUid, archived.RowVersion);
        Assert.Equal("Active", restored.Status);
        Assert.Equal(stored.StorageKey, (await repository.GetByUidAsync(patient, uploaded.FileUid))!.StorageKey);
        Assert.Equal(3, await db.Audits("PatientFile", uploaded.FileUid));
        var afterRestore = (await service.OpenContentAsync(patient, uploaded.FileUid))!;
        await using (afterRestore.Content)
        {
            using var copy = new MemoryStream();
            await afterRestore.Content.CopyToAsync(copy);
            Assert.Equal(bytes, copy.ToArray());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FileCreateProcedureRejectsNullOrEmptyGuidWithoutRowsOrAudit(bool nullGuid)
    {
        var patient = await db.Clinical.SeedPatient(false);
        await using var sql = await db.Clinical.Factory(false).OpenConnectionAsync();
        await using var command = new SqlCommand("EXEC dbo.PatientFile_Create @PatientUid=@Patient, @OriginalFileName=N'demo.txt', @StorageKey=@Key, @ContentType=N'text/plain', @FileSizeBytes=1, @Category=N'Report', @Title=N'TEST ONLY', @UploadedBy=7, @FileUid=@File", sql);
        command.Parameters.AddWithValue("@Patient", patient);
        command.Parameters.AddWithValue("@Key", "test-only/" + Guid.NewGuid().ToString("N"));
        command.Parameters.Add("@File", System.Data.SqlDbType.UniqueIdentifier).Value = nullGuid ? DBNull.Value : Guid.Empty;
        var failure = await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(51705, failure.Number);
        Assert.Empty(await new PatientFileRepository(db.Clinical.Factory(false)).GetByPatientUidAsync(patient));
        await using var count = new SqlCommand("SELECT COUNT(*) FROM dbo.AuditLog WHERE PatientId=(SELECT PatientId FROM dbo.Patient WHERE PatientUid=@Patient)", sql);
        count.Parameters.AddWithValue("@Patient", patient);
        Assert.Equal(0, (int)(await count.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task RepositoryRejectsEmptyAndDuplicateFileIdentityWithoutOverwritingMetadataOrAudit()
    {
        var patient = await db.Clinical.SeedPatient(false);
        var repository = new PatientFileRepository(db.Clinical.Factory(false));
        CreatePatientFileMetadata Metadata(Guid uid) => new()
        { FileUid = uid, OriginalFileName = "demo.txt", StorageKey = "test-only/" + Guid.NewGuid().ToString("N"), ContentType = "text/plain", FileSizeBytes = 1, Category = "Report", Title = "TEST ONLY" };
        await Assert.ThrowsAsync<ArgumentException>(() => repository.CreateAsync(patient, Metadata(Guid.Empty), 7));
        var uid = Guid.NewGuid();
        var original = await repository.CreateAsync(patient, Metadata(uid), 7);
        Assert.Equal(uid, original.FileUid);
        var failure = await Assert.ThrowsAsync<SqlException>(() => repository.CreateAsync(patient, Metadata(uid), 7));
        Assert.Contains(failure.Number, new[] { 2627, 2601 });
        Assert.Equal(original.StorageKey, (await repository.GetByUidAsync(patient, uid))!.StorageKey);
        Assert.Single(await repository.GetByPatientUidAsync(patient));
        Assert.Equal(1, await db.Audits("PatientFile", uid));
    }
}

public sealed class DemoWorkflowDatabase : IAsyncLifetime
{
    internal FamilyHistoryAcceptanceDatabase Clinical { get; } = new();
    internal string StorageRoot { get; } = Path.Combine(Path.GetTempPath(), "microemr-demo-files-" + Guid.NewGuid().ToString("N"));
    public async Task InitializeAsync()
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("MICROEMR_FAMILY_HISTORY_TEST_CONNECTION")
            ?? @"Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=true");
        if (!connection.DataSource.Equals(@"(localdb)\MSSQLLocalDB", StringComparison.OrdinalIgnoreCase)
            || connection.InitialCatalog != "master" || !connection.IntegratedSecurity)
            throw new InvalidOperationException("Demo fixtures require user-local disposable LocalDB only.");
        await Clinical.InitializeAsync();
        try
        {
            await using var sql = await Clinical.Factory(false).OpenConnectionAsync();
            await using (var identity = new SqlCommand("SELECT CONVERT(int,SERVERPROPERTY('IsLocalDB'))", sql))
                Assert.Equal(1, Convert.ToInt32(await identity.ExecuteScalarAsync()));
            foreach (var file in new[] { "patient_vital_stored_procedures.sql", "tenant-clinical/migrations/0020-vitals-stability.sql",
                "tenant-clinical/migrations/0025-patient-files-foundation.sql", "tenant-clinical/migrations/0026-patient-file-lifecycle-security.sql",
                "tenant-clinical/migrations/0040-patient-file-external-report-metadata.sql",
                "tenant-clinical/migrations/0106-patient-file-upload-identity.sql" })
                foreach (var batch in Regex.Split(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "database", file)),
                    @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Where(value => !string.IsNullOrWhiteSpace(value)))
                    await new SqlCommand(batch, sql).ExecuteNonQueryAsync();
            // The second database needs file schema to test a real foreign-tenant lookup.
            await using var other = await Clinical.Factory(true).OpenConnectionAsync();
            foreach (var file in new[] { "0025-patient-files-foundation.sql", "0040-patient-file-external-report-metadata.sql" })
                foreach (var batch in Regex.Split(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "database", "tenant-clinical", "migrations", file)),
                    @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Where(value => !string.IsNullOrWhiteSpace(value)))
                    await new SqlCommand(batch, other).ExecuteNonQueryAsync();
        }
        catch { await DisposeAsync(); throw; }
    }
    internal async Task<int> Audits(string entity, Guid uid)
    {
        await using var sql = await Clinical.Open(false);
        await using var command = new SqlCommand("SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityName=@Entity AND EntityId=CONVERT(nvarchar(100),@Uid) AND UserId=7", sql);
        command.Parameters.AddWithValue("@Entity", entity);
        command.Parameters.AddWithValue("@Uid", uid);
        return (int)(await command.ExecuteScalarAsync())!;
    }
    public async Task DisposeAsync()
    {
        await Clinical.DisposeAsync();
        if (Directory.Exists(StorageRoot)) Directory.Delete(StorageRoot, true); // This fixture's generated harmless bytes only.
    }
}
