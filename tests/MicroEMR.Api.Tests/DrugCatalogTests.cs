using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.DrugCatalog;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Infrastructure.DrugCatalog;
using MicroEMR.Web.Services.PatientPrescriptions;
using Xunit;

namespace MicroEMR.Api.Tests;

// Synthetic bounded rows, using the official positional extract schema; never clinical source truth.
internal static class DpdTestArchive
{
    private static int code = 100000;
    internal static int Next() => Interlocked.Increment(ref code);
    internal static Dictionary<string, string> Files(int code = 101, string name = "TEST DPD", string din = "00000001",
        string ingredient = "TEST INGREDIENT", string strength = "5", string status = "MARKETED", string dosageValue = "", string dosageUnit = "")
    {
        var id = code.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new()
        {
            ["drug.txt"] = Csv(id, "", "Human", din, name, "", "", "", "1", "2026-10-05", "", "", "", "", ""),
            ["ingred.txt"] = Csv(id, "1", ingredient, "", strength, "MG", "", dosageValue, "", dosageUnit, "", "", "", "", ""),
            ["form.txt"] = Csv(id, "34", "Tablet", "Comprimé"),
            ["route.txt"] = Csv(id, "1", "Oral", "Orale"),
            ["status.txt"] = Csv(id, "Y", status, "2026-10-05", "", "", "")
        };
    }
    private static string Csv(params string[] values) => string.Join(",", values.Select(x => "\"" + x.Replace("\"", "\"\"") + "\"")) + "\n";
    internal static byte[] Zip(Dictionary<string, string> files)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
            foreach (var (name, text) in files)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
                writer.Write(text);
            }
        return output.ToArray();
    }
    internal static MemoryStream Stream(int code = 101, string name = "TEST DPD", string din = "00000001", string ingredient = "TEST INGREDIENT", string status = "MARKETED") =>
        new(Zip(Files(code, name, din, ingredient, status: status)));
    internal static void Select(PrescriptionDraftRequest request, CatalogDrug product)
    {
        request.ProductName = request.ProductDisplayText = product.Facts.Name;
        request.ProductIdentifierNamespace = product.Facts.SourceSystem; request.ProductIdentifierValue = product.Facts.Din;
        request.MedicationForm = product.Facts.MedicationForm; request.StrengthValue = product.Facts.StrengthValue; request.StrengthUnit = product.Facts.StrengthUnit;
        request.DrugCatalogSelection = new(product.ProductUid, product.RowVersion);
    }
}

public sealed class DpdExtractTests
{
    [Fact]
    public async Task PositionalUtf8QuotedMappingPreservesDinAndSourceFacts()
    {
        using var input = DpdTestArchive.Stream(name: "TEST, École \"quoted\"", din: "00012345");
        var batch = await new DpdExtractReader().ReadAsync(input); var row = Assert.Single(batch.Products);
        Assert.Equal("00012345", row.Din); Assert.Equal("101", row.SourceProductId);
        Assert.Equal("TEST, École \"quoted\"", row.Name); Assert.Equal(DpdExtractReader.SourceSystem, row.SourceSystem);
        Assert.Equal("TEST INGREDIENT", Assert.Single(row.Ingredients).Name); Assert.Equal(5m, row.StrengthValue); Assert.Equal("MG", row.StrengthUnit);
        Assert.Equal("Tablet", Assert.Single(row.Forms)); Assert.Equal("Oral", Assert.Single(row.Routes));
        Assert.Equal("MARKETED", row.Status); Assert.Equal("2026-10-05", row.SourceUpdatedDate); Assert.True(row.Selectable); Assert.Equal(64, batch.Sha256.Length);
    }
    [Theory]
    [InlineData("5/10", "", "")]
    [InlineData("5", "5", "ML")]
    [InlineData("UNKNOWN", "", "")]
    public async Task UnsupportedOrDenominatorStrengthIsNotInferred(string strength, string dosage, string unit)
    {
        using var input = new MemoryStream(DpdTestArchive.Zip(DpdTestArchive.Files(strength: strength, dosageValue: dosage, dosageUnit: unit)));
        var row = Assert.Single((await new DpdExtractReader().ReadAsync(input)).Products);
        Assert.Null(row.StrengthValue); Assert.Null(row.StrengthUnit); Assert.Equal(strength, row.Ingredients[0].Strength);
        Assert.Equal(dosage, row.Ingredients[0].DosageValue); Assert.Equal(unit, row.Ingredients[0].DosageUnit);
    }
    [Theory]
    [InlineData("missing")]
    [InlineData("columns")]
    [InlineData("duplicate")]
    [InlineData("status")]
    [InlineData("orphan")]
    public async Task IncompleteOrAmbiguousSourceRejects(string kind)
    {
        var files = DpdTestArchive.Files();
        if (kind == "missing") files.Remove("form.txt");
        if (kind == "columns") files["drug.txt"] = "1,2\n";
        if (kind == "duplicate") files["drug.txt"] += files["drug.txt"];
        if (kind == "status") files["status.txt"] += files["status.txt"];
        if (kind == "orphan") files["form.txt"] += "\"999\",\"1\",\"Tablet\",\"\"\n";
        using var input = new MemoryStream(DpdTestArchive.Zip(files));
        await Assert.ThrowsAsync<ArgumentException>(() => new DpdExtractReader().ReadAsync(input));
    }
    [Fact]
    public async Task InvalidArchiveFailsWithoutSourceAccess()
    {
        using var input = new MemoryStream([1, 2, 3]);
        await Assert.ThrowsAsync<ArgumentException>(() => new DpdExtractReader().ReadAsync(input));
    }
    [Fact]
    public async Task InvalidUtf8IsNotDecodedAsReplacementClinicalText()
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            using var stream = zip.CreateEntry("drug.txt").Open(); stream.WriteByte(0xff);
        }
        output.Position = 0;
        await Assert.ThrowsAsync<ArgumentException>(() => new DpdExtractReader().ReadAsync(output));
    }
    [Fact]
    public async Task NullableSourceIdentityIsPreservedWithoutInventingASelectableDrug()
    {
        using var input = DpdTestArchive.Stream(name: "", din: "");
        var facts = Assert.Single((await new DpdExtractReader().ReadAsync(input)).Products);
        Assert.Equal("", facts.Din); Assert.Equal("", facts.Name); Assert.False(facts.Selectable);
    }
    [Fact]
    public async Task WebUnavailableResponseIsNotReportedAsNoMatches()
    {
        using var http = new HttpClient(new Unavailable()) { BaseAddress = new("https://test.invalid/") };
        var controller = new MicroEMR.Web.Controllers.DrugCatalogController(new DrugCatalogApiClient(http));
        var result = Assert.IsType<Microsoft.AspNetCore.Mvc.ObjectResult>(await controller.Lookup("test"));
        Assert.Equal(503, result.StatusCode); Assert.Contains("unavailable", JsonSerializer.Serialize(result.Value));
    }
    private sealed class Unavailable : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }
    [Fact]
    public async Task DisabledEnvironmentCannotAccessCatalogueAndManualInputRemainsAllowed()
    {
        var repository = CppSelectivePrintTests.Stub<IDrugCatalogRepository>((_, _) => throw new InvalidOperationException("No catalogue access expected"));
        var source = CppSelectivePrintTests.Stub<IDrugCatalogSourceReader>((_, _) => throw new InvalidOperationException("No source access expected"));
        var service = new DrugCatalogService(repository, source, NullLogger<DrugCatalogService>.Instance, false);
        await Assert.ThrowsAsync<DrugCatalogUnavailableException>(() => service.SearchAsync("test"));
        await Assert.ThrowsAsync<DrugCatalogUnavailableException>(() => service.GetAsync(Guid.NewGuid()));
        using var stream = new MemoryStream(); await Assert.ThrowsAsync<DrugCatalogUnavailableException>(() => service.ImportAsync(stream, 7));
        var request = PrescriptionDatePrecisionTests.Draft(); await service.ValidateSelectionAsync(request, default);
        request.DrugCatalogSelection = new(Guid.NewGuid(), Convert.ToBase64String(new byte[8]));
        await Assert.ThrowsAsync<DrugCatalogUnavailableException>(() => service.ValidateSelectionAsync(request, default));
    }
}

public sealed class DrugCatalogDatabase : IAsyncLifetime
{
    internal DispensingDirectoryDatabase Existing { get; } = new();
    internal PrescriptionDateDatabase Db => Existing.Db;
    internal DrugCatalogService Catalog(bool other = false) => new(new DrugCatalogRepository(Db.Factory(other)), new DpdExtractReader(), NullLogger<DrugCatalogService>.Instance);
    public async Task InitializeAsync()
    {
        await Existing.InitializeAsync();
        try { foreach (var other in new[] { false, true }) { await using var c = await Db.Factory(other).OpenConnectionAsync(); await PrescriptionPrivateNoteFixture.Batches(c, PrescriptionPrivateNoteFixture.Sql("tenant-clinical/migrations/0100-development-drug-catalog.sql")); } }
        catch { await Existing.DisposeAsync(); throw; }
    }
    public Task DisposeAsync() => Existing.DisposeAsync();
    internal Task<PrescriptionDateHost> Host(bool other = false, bool manage = true) => PrescriptionDateHost.Start(Db, other, services =>
    {
        services.AddScoped<IDrugCatalogRepository>(_ => new DrugCatalogRepository(Db.Factory(other)));
        services.AddScoped<IDrugCatalogSourceReader, DpdExtractReader>(); services.AddScoped<DrugCatalogService>();
        services.AddScoped<ICurrentUserPermissionService>(sp => new Permissions(sp.GetRequiredService<IHttpContextAccessor>(), manage));
    });
    private sealed class Permissions(IHttpContextAccessor http, bool manage) : ICurrentUserPermissionService
    {
        public Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(CancellationToken token = default)
        {
            var keys = new HashSet<string> { PermissionKeys.PatientsView };
            if (http.HttpContext!.User.FindFirst("write")?.Value == "True") { keys.Add(PermissionKeys.PrescriptionsPrescribe); if (manage) keys.Add(PermissionKeys.ClinicSettingsManage); }
            return Task.FromResult<IReadOnlySet<string>>(keys);
        }
        public async Task<bool> HasPermissionAsync(string key, CancellationToken token = default) => (await GetEffectivePermissionsAsync(token)).Contains(key);
    }
    internal async Task<CatalogDrug> Product(bool other = false)
    {
        var code = DpdTestArchive.Next(); var din = "00" + code; var marker = "TEST DPD " + code;
        using var input = DpdTestArchive.Stream(code, marker, din, "INGREDIENT " + code);
        await Catalog(other).ImportAsync(input, 7);
        return Assert.Single((await Catalog(other).SearchAsync(din)).Items);
    }
}

public sealed class DrugCatalogIntegrationTests(DrugCatalogDatabase fixture) : IClassFixture<DrugCatalogDatabase>
{
    private static string Rx(Guid p, Guid? uid = null) => "api/patients/" + p + "/prescriptions" + (uid is null ? "" : "/" + uid);
    [Fact]
    public async Task ImportIsIdempotentRefreshKeepsIdentityAndAuditsOnlyChanges()
    {
        var product = await fixture.Product();
        using var same = DpdTestArchive.Stream(int.Parse(product.Facts.SourceProductId), product.Facts.Name, product.Facts.Din, product.Facts.Ingredients[0].Name);
        var result = await fixture.Catalog().ImportAsync(same, 7); Assert.Equal(new(0, 0, 1), result);
        Assert.Equal(product.RowVersion, (await fixture.Catalog().GetAsync(product.ProductUid))!.RowVersion);
        using var changed = DpdTestArchive.Stream(int.Parse(product.Facts.SourceProductId), product.Facts.Name + " updated", product.Facts.Din, product.Facts.Ingredients[0].Name);
        Assert.Equal(new(0, 1, 0), await fixture.Catalog().ImportAsync(changed, 7));
        var updated = (await fixture.Catalog().GetAsync(product.ProductUid))!;
        Assert.NotEqual(product.RowVersion, updated.RowVersion); Assert.NotEqual(product.ImportSha256, updated.ImportSha256);
        await using var c = await fixture.Db.Factory().OpenConnectionAsync();
        await using var q = new SqlCommand("SELECT UserId,NewValue FROM dbo.AuditLog WHERE EntityName=N'DrugCatalogProduct' AND EntityId=@id ORDER BY AuditLogId", c);
        q.Parameters.AddWithValue("@id", product.ProductUid.ToString()); await using var r = await q.ExecuteReaderAsync(); var count = 0;
        while (await r.ReadAsync()) { Assert.Equal(7L, r.GetInt64(0)); using var audit = JsonDocument.Parse(r.GetString(1)); Assert.Equal(fixture.Db.Tenant(false).TenantUid, audit.RootElement.GetProperty("TenantUid").GetGuid()); count++; }
        Assert.Equal(2, count);
    }
    [Fact]
    public async Task NameIngredientDinSearchAreLiteralAndTenantLocal()
    {
        var product = await fixture.Product();
        foreach (var term in new[] { product.Facts.Name.ToLowerInvariant(), product.Facts.Ingredients[0].Name, product.Facts.Din })
            Assert.Contains((await fixture.Catalog().SearchAsync(term)).Items, p => p.ProductUid == product.ProductUid);
        Assert.Empty((await fixture.Catalog().SearchAsync("%")).Items);
        Assert.Empty((await fixture.Catalog(true).SearchAsync(product.Facts.Din)).Items);
        Assert.Null(await fixture.Catalog(true).GetAsync(product.ProductUid));
    }
    [Fact]
    public async Task SelectedDraftPersistsSnapshotAndManualChangesClearOnlyProvenance()
    {
        var product = await fixture.Product(); var patient = await fixture.Db.Stores.SeedPatient();
        await using var host = await fixture.Host(); using var client = host.Client();
        var request = PrescriptionDatePrecisionTests.Draft(); DpdTestArchive.Select(request, product);
        using var denied = host.Client("reader");
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.PostAsJsonAsync(Rx(patient), request)).StatusCode);
        var response = await client.PostAsJsonAsync(Rx(patient), request); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!;
        Assert.Equal(product.Facts.Din, saved.ProductIdentifierValue); Assert.NotNull(saved.CatalogDrug);
        Assert.Equal(product.Facts.Name, saved.CatalogDrug!.Facts.Name); Assert.Equal(product.ImportSha256, saved.CatalogDrug.ImportSha256);
        var fresh = (await client.GetFromJsonAsync<PatientPrescriptionResponse>(Rx(patient, saved.PrescriptionUid)))!;
        Assert.Equal(product.Facts.Din, fresh.CatalogDrug!.Facts.Din);
        request.DrugCatalogSelection = null; request.RowVersion = saved.RowVersion; request.Directions = "TEST ONLY revised directions";
        response = await client.PutAsJsonAsync(Rx(patient, saved.PrescriptionUid), request); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        saved = (await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!; Assert.NotNull(saved.CatalogDrug);
        request.RowVersion = saved.RowVersion; request.ProductName = request.ProductDisplayText = "TEST ONLY manual change";
        response = await client.PutAsJsonAsync(Rx(patient, saved.PrescriptionUid), request); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        saved = (await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!; Assert.Null(saved.CatalogDrug);
        await using var c = await fixture.Db.Factory().OpenConnectionAsync();
        await using var q = new SqlCommand("SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityId=@id AND ActionName=N'PrescriptionDraftUpdated' AND JSON_VALUE(OldValue,'$.DrugCatalogSnapshot') IS NOT NULL", c);
        q.Parameters.AddWithValue("@id", saved.PrescriptionUid.ToString()); Assert.True(Convert.ToInt32(await q.ExecuteScalarAsync()) >= 1);
        await using var audit = new SqlCommand("SELECT UserId,NewValue FROM dbo.AuditLog WHERE EntityId=@id AND ActionName=N'PrescriptionDraftCreated'", c);
        audit.Parameters.AddWithValue("@id", saved.PrescriptionUid.ToString()); await using var reader = await audit.ExecuteReaderAsync(); Assert.True(await reader.ReadAsync());
        Assert.Equal(7L, reader.GetInt64(0)); using var json = JsonDocument.Parse(reader.GetString(1));
        using var snapshot = JsonDocument.Parse(json.RootElement.GetProperty("DrugCatalogSnapshot").GetString()!);
        Assert.Equal(product.ProductUid, snapshot.RootElement.GetProperty("ProductUid").GetGuid());
        Assert.Equal(product.Facts.Din, snapshot.RootElement.GetProperty("Facts").GetProperty("Din").GetString());
    }
    [Theory]
    [InlineData("stale")]
    [InlineData("mismatch")]
    [InlineData("unavailable")]
    [InlineData("tenant")]
    public async Task InvalidSelectionDoesNotCreateClinicalRecords(string kind)
    {
        var product = await fixture.Product(kind == "tenant"); var patient = await fixture.Db.Stores.SeedPatient();
        var request = PrescriptionDatePrecisionTests.Draft(); DpdTestArchive.Select(request, product);
        if (kind == "mismatch") request.ProductIdentifierValue = "00009999";
        if (kind is "stale" or "unavailable")
        {
            using var updated = DpdTestArchive.Stream(int.Parse(product.Facts.SourceProductId), product.Facts.Name + " changed", product.Facts.Din, status: kind == "unavailable" ? "DORMANT" : "MARKETED");
            await fixture.Catalog().ImportAsync(updated, 7);
        }
        await using var host = await fixture.Host(); using var client = host.Client();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Rx(patient), request)).StatusCode);
        Assert.Empty(await fixture.Db.Repository().ListAsync(patient));
    }
    [Fact]
    public async Task ManualHistoryAndFinalizedArtifactSurviveCatalogueRefresh()
    {
        var product = await fixture.Product(); var patient = await fixture.Db.Stores.SeedPatient();
        var manual = await fixture.Db.Repository().CreateAsync(patient, PrescriptionDatePrecisionTests.Draft(), 7); Assert.Null(manual.CatalogDrug);
        var request = PrescriptionDatePrecisionTests.Draft(); DpdTestArchive.Select(request, product);
        var selected = await fixture.Db.Repository().CreateAsync(patient, request, 7);
        var final = (await fixture.Db.Repository().FinalizeAsync(patient, selected.PrescriptionUid, selected.RowVersion, 7))!;
        var artifact = await fixture.Db.Repository().GetArtifactAsync(patient, final.PrescriptionUid);
        using var changed = DpdTestArchive.Stream(int.Parse(product.Facts.SourceProductId), product.Facts.Name + " new source name", product.Facts.Din);
        await fixture.Catalog().ImportAsync(changed, 7);
        var preserved = (await fixture.Db.Repository().GetAsync(patient, final.PrescriptionUid))!;
        Assert.Equal(product.Facts.Name, preserved.CatalogDrug!.Facts.Name); Assert.Equal(final.RowVersion, preserved.RowVersion);
        Assert.Equal(artifact, await fixture.Db.Repository().GetArtifactAsync(patient, final.PrescriptionUid));
        Assert.Equal(manual.RowVersion, (await fixture.Db.Repository().GetAsync(patient, manual.PrescriptionUid))!.RowVersion);
        request.DrugCatalogSelection = null; request.RowVersion = final.RowVersion;
        await using var host = await fixture.Host(); using var client = host.Client();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(Rx(patient, final.PrescriptionUid), request)).StatusCode);
    }
    [Fact]
    public async Task ImportAndLookupEnforceExistingPermissionsAndTrustedActor()
    {
        await using var host = await fixture.Host(); using var admin = host.Client(); using var reader = host.Client("reader");
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("api/drug-catalog")).StatusCode);
        using var content = new MultipartFormDataContent(); content.Add(new ByteArrayContent(DpdTestArchive.Zip(DpdTestArchive.Files(DpdTestArchive.Next()))), "archive", "test.zip");
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsync("api/drug-catalog/import", content)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync("api/drug-catalog/import", content)).StatusCode);
        await using var prescribingHost = await fixture.Host(manage: false); using var prescriber = prescribingHost.Client();
        Assert.Equal(HttpStatusCode.Forbidden, (await prescriber.PostAsync("api/drug-catalog/import", content)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await prescriber.GetAsync("api/drug-catalog")).StatusCode);
    }
    [Fact]
    public async Task ImportAuditFailureRollsBackBatch()
    {
        var code = DpdTestArchive.Next(); var din = "00" + code;
        await using var c = await fixture.Db.Factory().OpenConnectionAsync();
        await PrescriptionPrivateNoteFixture.Batches(c, "CREATE TRIGGER dbo.CatalogAuditFailure ON dbo.AuditLog AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE EntityName=N'DrugCatalogProduct') THROW 52998,'TEST ONLY audit failure',1; END");
        try { using var data = DpdTestArchive.Stream(code, din: din); await Assert.ThrowsAsync<SqlException>(() => fixture.Catalog().ImportAsync(data, 7)); }
        finally { await PrescriptionPrivateNoteFixture.Batches(c, "DROP TRIGGER dbo.CatalogAuditFailure"); }
        Assert.Empty((await fixture.Catalog().SearchAsync(din)).Items);
    }
    [Fact]
    public async Task ClinicalAuditFailureAndWrongOwnerNeverMutateSelection()
    {
        var product = await fixture.Product(); var patient = await fixture.Db.Stores.SeedPatient();
        var request = PrescriptionDatePrecisionTests.Draft(); DpdTestArchive.Select(request, product);
        await using var c = await fixture.Db.Factory().OpenConnectionAsync();
        await PrescriptionPrivateNoteFixture.Batches(c, "CREATE TRIGGER dbo.CatalogClinicalAuditFailure ON dbo.AuditLog AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE EntityName=N'PatientPrescription') THROW 52998,'TEST ONLY audit failure',1; END");
        try { await Assert.ThrowsAsync<SqlException>(() => fixture.Db.Repository().CreateAsync(patient, request, 7)); }
        finally { await PrescriptionPrivateNoteFixture.Batches(c, "DROP TRIGGER dbo.CatalogClinicalAuditFailure"); }
        Assert.Empty(await fixture.Db.Repository().ListAsync(patient));
        var row = await fixture.Db.Repository().CreateAsync(patient, request, 7); request.RowVersion = row.RowVersion;
        await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(() => fixture.Db.Repository().UpdateAsync(patient, row.PrescriptionUid, request, 8));
        Assert.Equal(row.RowVersion, (await fixture.Db.Repository().GetAsync(patient, row.PrescriptionUid))!.RowVersion);
    }
    [Fact]
    public async Task MalformedRefreshLeavesExistingDataUntouched()
    {
        var product = await fixture.Product(); var files = DpdTestArchive.Files(); files["route.txt"] = "bad\n";
        using var input = new MemoryStream(DpdTestArchive.Zip(files));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Catalog().ImportAsync(input, 7));
        Assert.Equal(product.RowVersion, (await fixture.Catalog().GetAsync(product.ProductUid))!.RowVersion);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RawSqlSelectionGuardRejectsStaleOrMismatchedFacts(bool stale)
    {
        var product = await fixture.Product(); var patient = await fixture.Db.Stores.SeedPatient();
        var request = PrescriptionDatePrecisionTests.Draft(); DpdTestArchive.Select(request, product);
        if (stale)
        {
            using var changed = DpdTestArchive.Stream(int.Parse(product.Facts.SourceProductId), product.Facts.Name + " changed", product.Facts.Din);
            await fixture.Catalog().ImportAsync(changed, 7);
        }
        else request.ProductName = "TEST ONLY mismatched name";
        await Assert.ThrowsAsync<DrugCatalogConflictException>(() => fixture.Db.Repository().CreateAsync(patient, request, 7));
        Assert.Empty(await fixture.Db.Repository().ListAsync(patient));
    }
}

public sealed class DrugCatalogLegacySchemaTests(DispensingDirectoryDatabase fixture) : IClassFixture<DispensingDirectoryDatabase>
{
    [Fact]
    public async Task ManualDraftRemainsUsableBeforeCatalogMigration()
    {
        var patient = await fixture.Db.Stores.SeedPatient();
        await using var host = await fixture.Host(); using var client = host.Client();
        var response = await client.PostAsJsonAsync("api/patients/" + patient + "/prescriptions", PrescriptionDatePrecisionTests.Draft());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var row = (await response.Content.ReadFromJsonAsync<PatientPrescriptionResponse>())!; Assert.Null(row.CatalogDrug);
        Assert.Null((await fixture.Db.Repository().GetAsync(patient, row.PrescriptionUid))!.CatalogDrug);
    }
}
