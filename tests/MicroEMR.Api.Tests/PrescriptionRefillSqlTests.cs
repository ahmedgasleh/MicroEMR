using System.Data;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Infrastructure.PatientPrescriptions;
using MicroEMR.Infrastructure.ClinicalOutput;
using MicroEMR.Infrastructure.Tenancy;
using MicroEMR.Web.Services.PatientPrescriptions;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PrescriptionRefillSqlTests(PrescriptionRefillSqlDatabase db) : IClassFixture<PrescriptionRefillSqlDatabase>
{
    [Theory]
    [InlineData(30, 30, 30, 30, 2)]
    [InlineData(10, 10, 30, 30, 2)]
    [InlineData(30, 30, 90, 90, 1)]
    [InlineData(30, 30, null, null, 0)]
    [InlineData(30, 30, 30, 30, 3)]
    [InlineData(30, null, 90, null, 2)]
    [InlineData(30, 30, null, 90, 2)]
    public async Task PersistenceAndFinalizedOutputKeepInitialAndRefillValuesDistinct(int quantity, int? days, int? refill, int? refillDays, int repeats)
    {
        var patient = await db.SeedPatient();
        var renderer = new CaptureRenderer();
        var service = new PatientPrescriptionService(db.Repository, renderer);
        var request = PrescriptionRefillInstructionsTests.Draft(quantity, days, refill, refillDays, repeats);
        var created = await service.CreateAsync(patient, request, 7);
        var reopened = (await db.Repository.GetAsync(patient, created.PrescriptionUid))!;
        Assert.Equal(quantity, reopened.Quantity);
        Assert.Equal(days, reopened.InitialDaysSupply);
        Assert.Equal(refill.HasValue ? (decimal?)refill.Value : null, reopened.RefillQuantity);
        Assert.Equal(request.RefillQuantityUnit, reopened.RefillQuantityUnit);
        Assert.Equal(refillDays, reopened.RefillDaysSupply);
        Assert.Equal(repeats, reopened.AuthorizedRepeats);
        Assert.Single(await db.Repository.ListAsync(patient));

        request.RowVersion = reopened.RowVersion;
        request.InitialDaysSupply = 15;
        if (repeats > 0) request.RefillDaysSupply = 45;
        var saved = (await service.UpdateAsync(patient, reopened.PrescriptionUid, request, 7))!;
        Assert.Equal(15, saved.InitialDaysSupply);
        Assert.Equal(repeats > 0 ? 45 : null, saved.RefillDaysSupply);
        Assert.NotEqual(reopened.RowVersion, saved.RowVersion);
        var finalized = (await service.FinalizeAsync(patient, saved.PrescriptionUid, saved.RowVersion, 7))!;
        var artifact = (await db.Repository.GetArtifactAsync(patient, finalized.PrescriptionUid))!;
        using var document = JsonDocument.Parse(artifact.Json);
        Assert.Equal(15, document.RootElement.GetProperty("InitialDaysSupply").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("RefillInstructionsVersion").GetInt32());
        Assert.Equal(repeats, document.RootElement.GetProperty("AuthorizedRepeats").GetInt32());
        await service.RenderArtifactPdfAsync(patient, finalized.PrescriptionUid);
        Assert.Contains($"<dt>Initial dispense quantity</dt><dd>{quantity}.000 tablet</dd>", renderer.Html);
        Assert.Contains("<dt>Initial days' supply</dt><dd>15</dd>", renderer.Html);
        Assert.Contains($"<dt>Authorized refills</dt><dd>{repeats}</dd>", renderer.Html);
        Assert.Contains("<dt>Refill quantity</dt>", renderer.Html);
        Assert.Contains("<dt>Refill days' supply</dt>", renderer.Html);
        Assert.DoesNotContain("Quantity / repeats", renderer.Html);
        Assert.Equal(1, await db.AuditCount(created.PrescriptionUid, "PrescriptionDraftCreated"));
        Assert.Equal(1, await db.AuditCount(created.PrescriptionUid, "PrescriptionDraftUpdated"));
        Assert.Equal(1, await db.AuditCount(created.PrescriptionUid, "PrescriptionFinalized"));
    }

    [Fact]
    public async Task StaleImmutableForeignPatientForeignTenantAndOtherPrescriberCannotOverwrite()
    {
        var patient = await db.SeedPatient();
        var request = PrescriptionRefillInstructionsTests.Draft();
        var created = await db.Repository.CreateAsync(patient, request, 7);
        request.RowVersion = created.RowVersion;
        request.RefillQuantity = 60;
        var saved = (await db.Repository.UpdateAsync(patient, created.PrescriptionUid, request, 7))!;
        await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(() => db.Repository.UpdateAsync(patient, created.PrescriptionUid, request, 7));
        Assert.Equal(60, (await db.Repository.GetAsync(patient, created.PrescriptionUid))!.RefillQuantity);
        Assert.Equal(1, await db.AuditCount(created.PrescriptionUid, "PrescriptionDraftUpdated"));
        request.RowVersion = saved.RowVersion;
        await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(() => db.Repository.UpdateAsync(patient, created.PrescriptionUid, request, 8));
        await Assert.ThrowsAsync<PrescriberProviderMappingException>(() => db.Repository.CreateAsync(patient, request, 99));
        var foreignPatient = await db.SeedPatient();
        Assert.Null(await db.Repository.GetAsync(foreignPatient, created.PrescriptionUid));
        Assert.Null(await db.Repository.UpdateAsync(foreignPatient, created.PrescriptionUid, request, 7));
        Assert.Null(await db.OtherRepository.GetAsync(patient, created.PrescriptionUid));
        Assert.Null(await db.OtherRepository.UpdateAsync(patient, created.PrescriptionUid, request, 7));
        var finalized = (await db.Repository.FinalizeAsync(patient, created.PrescriptionUid, saved.RowVersion, 7))!;
        var preserved = (await db.Repository.GetArtifactAsync(patient, created.PrescriptionUid))!;
        request.RowVersion = finalized.RowVersion;
        await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(() => db.Repository.UpdateAsync(patient, created.PrescriptionUid, request, 7));
        Assert.Null(await db.Repository.GetArtifactAsync(foreignPatient, created.PrescriptionUid));
        Assert.Null(await db.OtherRepository.GetArtifactAsync(patient, created.PrescriptionUid));
        Assert.Equal(preserved, await db.Repository.GetArtifactAsync(patient, created.PrescriptionUid));
    }

    [Fact]
    public async Task TechnicalFailuresAndAuditFailureRollBackWithoutClinicalSuccess()
    {
        var patient = await db.SeedPatient();
        var request = PrescriptionRefillInstructionsTests.Draft();
        request.RefillQuantityUnit = null;
        await Assert.ThrowsAsync<SqlException>(() => db.Repository.CreateAsync(patient, request, 7));
        Assert.Empty(await db.Repository.ListAsync(patient));
        request.RefillQuantityUnit = "tablet";
        request.AuthorizedRepeats = 0;
        await Assert.ThrowsAsync<SqlException>(() => db.Repository.CreateAsync(patient, request, 7));
        Assert.Empty(await db.Repository.ListAsync(patient));
        request.AuthorizedRepeats = 2;
        var created = await db.Repository.CreateAsync(patient, request, 7);
        request.RowVersion = created.RowVersion;
        request.RefillDaysSupply = 50;
        await db.Execute("CREATE TRIGGER dbo.RefillAuditFailure ON dbo.AuditLog AFTER INSERT AS BEGIN THROW 51999,'Synthetic audit failure.',1; END");
        try
        {
            await Assert.ThrowsAsync<SqlException>(() => db.Repository.UpdateAsync(patient, created.PrescriptionUid, request, 7));
            Assert.Equal(30, (await db.Repository.GetAsync(patient, created.PrescriptionUid))!.RefillDaysSupply);
            Assert.Equal(0, await db.AuditCount(created.PrescriptionUid, "PrescriptionDraftUpdated"));
            await Assert.ThrowsAsync<SqlException>(() => db.Repository.FinalizeAsync(patient, created.PrescriptionUid, created.RowVersion, 7));
            Assert.Null(await db.Repository.GetArtifactAsync(patient, created.PrescriptionUid));
            Assert.Equal("Draft", (await db.Repository.GetAsync(patient, created.PrescriptionUid))!.Status);
            Assert.Equal(0, await db.AuditCount(created.PrescriptionUid, "PrescriptionFinalized"));
        }
        finally { await db.Execute("DROP TRIGGER dbo.RefillAuditFailure"); }
    }

    [Fact]
    public async Task FinalizedRefillInstructionsRenderThroughExistingPdfRenderer()
    {
        var patient = await db.SeedPatient();
        await using var renderer = new PlaywrightPdfRenderer(NullLogger<PlaywrightPdfRenderer>.Instance);
        var service = new PatientPrescriptionService(db.Repository, renderer);
        var draft = await service.CreateAsync(patient, PrescriptionRefillInstructionsTests.Draft(), 7);
        await service.FinalizeAsync(patient, draft.PrescriptionUid, draft.RowVersion, 7);
        var bytes = (await service.RenderArtifactPdfAsync(patient, draft.PrescriptionUid))!;
        Assert.True(bytes.Length > 100);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
    }

    [Fact]
    public async Task WebEditAndApiCorrectionCopyIndependentInstructions()
    {
        var patient = await db.SeedPatient();
        var service = new PatientPrescriptionService(db.Repository, new CaptureRenderer());
        var created = await service.CreateAsync(patient, PrescriptionRefillInstructionsTests.Draft(), 7);
        using var handler = new DraftResponseHandler(JsonSerializer.Serialize(created, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var web = new MicroEMR.Web.Controllers.PatientPrescriptionsController(new PatientPrescriptionApiClient(http))
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        web.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        var edit = Assert.IsType<JsonResult>(await web.Edit(patient, created.PrescriptionUid, default));
        var model = Assert.IsType<PrescriptionDraftRequest>(edit.Value);
        Assert.Equal(created.InitialDaysSupply, model.InitialDaysSupply);
        Assert.Equal(created.RefillQuantity, model.RefillQuantity);
        Assert.Equal(created.RefillQuantityUnit, model.RefillQuantityUnit);
        Assert.Equal(created.RefillDaysSupply, model.RefillDaysSupply);
        var finalized = (await service.FinalizeAsync(patient, created.PrescriptionUid, created.RowVersion, 7))!;
        var api = new MicroEMR.Api.Controllers.PatientPrescriptionsController(service)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        ClinicalUserActorContext.Set(api.HttpContext, 7);
        var result = await api.Correction(patient, finalized.PrescriptionUid, new() { RowVersion = finalized.RowVersion }, default);
        var correction = Assert.IsType<PatientPrescriptionResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Equal(finalized.InitialDaysSupply, correction.InitialDaysSupply);
        Assert.Equal(finalized.RefillQuantity, correction.RefillQuantity);
        Assert.Equal(finalized.RefillQuantityUnit, correction.RefillQuantityUnit);
        Assert.Equal(finalized.RefillDaysSupply, correction.RefillDaysSupply);
        Assert.Equal(finalized.PrescriptionUid, correction.SupersedesPrescriptionUid);
        Assert.Equal("Finalized", (await db.Repository.GetAsync(patient, finalized.PrescriptionUid))!.Status);
    }

    [Fact]
    public async Task LegacyUpgradeAndCorrectionRetainOldSnapshotAndRendering()
    {
        var legacy = (await db.Repository.GetAsync(db.LegacyPatient, db.LegacyPrescription))!;
        Assert.Null(legacy.InitialDaysSupply);
        Assert.Null(legacy.RefillQuantity);
        Assert.Null(legacy.RefillDaysSupply);
        Assert.Equal(db.LegacyRowVersion, legacy.RowVersion);
        var artifact = (await db.Repository.GetArtifactAsync(db.LegacyPatient, db.LegacyPrescription))!;
        Assert.Equal(db.LegacyJson, artifact.Json);
        var renderer = new CaptureRenderer();
        var service = new PatientPrescriptionService(db.Repository, renderer);
        await service.RenderArtifactPdfAsync(db.LegacyPatient, db.LegacyPrescription);
        var originalHtml = renderer.Html;
        Assert.Contains("<dt>Quantity / repeats</dt><dd>30.000 tablet / 1</dd>", originalHtml);
        Assert.DoesNotContain("Initial days' supply", originalHtml);
        Assert.DoesNotContain("Not recorded", originalHtml);
        var correction = PrescriptionRefillInstructionsTests.Draft();
        correction.SupersedesPrescriptionUid = legacy.PrescriptionUid;
        var draft = await service.CreateAsync(db.LegacyPatient, correction, 7);
        await service.FinalizeAsync(db.LegacyPatient, draft.PrescriptionUid, draft.RowVersion, 7);
        Assert.Equal("Superseded", (await db.Repository.GetAsync(db.LegacyPatient, legacy.PrescriptionUid))!.Status);
        Assert.Equal(artifact, await db.Repository.GetArtifactAsync(db.LegacyPatient, legacy.PrescriptionUid));
        await service.RenderArtifactPdfAsync(db.LegacyPatient, legacy.PrescriptionUid);
        Assert.Equal(originalHtml, renderer.Html);
    }

    private sealed class CaptureRenderer : IPdfRenderer
    {
        public string Html { get; private set; } = "";
        public Task<byte[]> RenderAsync(string html, CancellationToken cancellationToken = default)
        { Html = html; return Task.FromResult(Encoding.UTF8.GetBytes(html)); }
    }
    private sealed class DraftResponseHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }
}

// Match existing disposable LocalDB integration tests. Never target an application database.
public sealed class PrescriptionRefillSqlDatabase : IAsyncLifetime
{
    private readonly string primary = "MicroEMR_Step87_" + Guid.NewGuid().ToString("N");
    private readonly string secondary = "MicroEMR_Step87_" + Guid.NewGuid().ToString("N");
    private readonly string master = Environment.GetEnvironmentVariable("MICROEMR_PRESCRIPTION_REFILL_TEST_CONNECTION")
        ?? @"Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true;Connect Timeout=15";
    private readonly List<string> created = [];
    public Guid LegacyPatient { get; } = Guid.NewGuid();
    public Guid LegacyPrescription { get; private set; }
    public string LegacyJson { get; private set; } = "";
    public string LegacyRowVersion { get; private set; } = "";
    public PatientPrescriptionRepository Repository => new(new Factory(Connection(primary)));
    public PatientPrescriptionRepository OtherRepository => new(new Factory(Connection(secondary)));
    private string Connection(string database) => new SqlConnectionStringBuilder(master) { InitialCatalog = database }.ConnectionString;

    public async Task InitializeAsync()
    {
        foreach (var database in new[] { primary, secondary })
        {
            await Run("master", $"CREATE DATABASE [{database}]");
            created.Add(database);
            await Run(database, """
                CREATE TABLE dbo.Provider(ProviderId bigint IDENTITY PRIMARY KEY,ProviderUid uniqueidentifier UNIQUE NOT NULL,
                    DisplayName nvarchar(200),ProviderType nvarchar(100),BillingNumber nvarchar(100),IsActive bit NOT NULL DEFAULT 1);
                CREATE TABLE dbo.ApplicationUser(UserId bigint PRIMARY KEY,IsActive bit NOT NULL,ProviderId bigint NULL);
                CREATE TABLE dbo.Patient(PatientId bigint IDENTITY PRIMARY KEY,PatientUid uniqueidentifier UNIQUE NOT NULL,
                    IsDeleted bit NOT NULL DEFAULT 0,FirstName nvarchar(100),LastName nvarchar(100),DateOfBirth date);
                CREATE TABLE dbo.AuditLog(AuditLogId bigint IDENTITY PRIMARY KEY,UserId bigint,PatientId bigint,ActionName nvarchar(100),
                    EntityName nvarchar(100),EntityId nvarchar(100),OldValue nvarchar(max),NewValue nvarchar(max),CreatedAt datetime2(0));
                INSERT dbo.Provider(ProviderUid,DisplayName,ProviderType) VALUES(NEWID(),N'Test prescriber',N'Physician'),(NEWID(),N'Other prescriber',N'Physician');
                INSERT dbo.ApplicationUser VALUES(7,1,1),(8,1,2);
                """);
            await Migrate(database, "0050-patient-prescription-foundation.sql");
            if (database == primary)
            {
                await SeedPatient(LegacyPatient);
                // The old procedure does not accept new parameters: create through its original contract.
                await Run(primary, """
                    EXEC dbo.PatientPrescription_CreateDraft @PatientUid=@Uid,@ProductName=N'Legacy',@ProductDisplayText=N'Legacy tablet',
                        @Route=N'Oral',@FrequencyCode=N'ONCE_DAILY',@FrequencyDisplay=N'Once daily',@Prn=0,@Directions=N'Take one daily.',
                        @Quantity=30,@QuantityUnit=N'tablet',@AuthorizedRepeats=1,@PrescribedDate='2026-10-01',@ActorUserId=7;
                    DECLARE @Prescription uniqueidentifier,@Version binary(8);
                    SELECT @Prescription=PrescriptionUid,@Version=RowVersion FROM dbo.PatientPrescription WHERE PatientUid=@Uid;
                    EXEC dbo.PatientPrescription_Finalize @PatientUid=@Uid,@PrescriptionUid=@Prescription,@ActorUserId=7,@RowVersion=@Version;
                    """, LegacyPatient);
                LegacyPrescription = await Scalar<Guid>("SELECT PrescriptionUid FROM dbo.PatientPrescription WHERE PatientUid=@Uid", LegacyPatient);
                LegacyJson = await Scalar<string>("SELECT ArtifactJson FROM dbo.PatientPrescriptionArtifact WHERE PatientUid=@Uid", LegacyPatient);
                LegacyRowVersion = Convert.ToBase64String(await Scalar<byte[]>("SELECT RowVersion FROM dbo.PatientPrescription WHERE PatientUid=@Uid", LegacyPatient));
            }
            await Migrate(database, "0073-prescription-refill-instructions.sql");
        }
    }

    public async Task<Guid> SeedPatient()
    { var patient = Guid.NewGuid(); await SeedPatient(patient); return patient; }
    private Task SeedPatient(Guid patient) => Run(primary, "INSERT dbo.Patient(PatientUid,FirstName,LastName,DateOfBirth) VALUES(@Uid,N'Synthetic',N'Patient','1990-01-01')", patient);
    public Task Execute(string sql) => Run(primary, sql);
    public Task<int> AuditCount(Guid prescription, string action) => Scalar<int>($"SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@Uid) AND ActionName=N'{action}'", prescription);
    private async Task<T> Scalar<T>(string sql, Guid uid)
    {
        await using var connection = new SqlConnection(Connection(primary)); await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Uid", SqlDbType.UniqueIdentifier).Value = uid;
        return (T)(await command.ExecuteScalarAsync())!;
    }
    private async Task Run(string database, string sql, Guid? uid = null)
    {
        await using var connection = new SqlConnection(Connection(database)); await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        if (uid.HasValue) command.Parameters.Add("@Uid", SqlDbType.UniqueIdentifier).Value = uid.Value;
        await command.ExecuteNonQueryAsync();
    }
    private async Task Migrate(string database, string file)
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "database", "tenant-clinical", "migrations", file));
        foreach (var batch in Regex.Split(source, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            if (!string.IsNullOrWhiteSpace(batch)) await Run(database, batch);
    }
    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        foreach (var database in created)
        {
            if (!Regex.IsMatch(database, @"^MicroEMR_Step87_[a-f0-9]{32}$")) throw new InvalidOperationException("Unsafe test database name.");
            await Run("master", $"IF DB_ID(N'{database}') IS NOT NULL BEGIN ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]; END");
        }
    }
    private sealed class Factory(string connectionString) : ITenantSqlConnectionFactory
    {
        public async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            var connection = new SqlConnection(connectionString);
            try { await connection.OpenAsync(cancellationToken); return connection; }
            catch { await connection.DisposeAsync(); throw; }
        }
    }
}
