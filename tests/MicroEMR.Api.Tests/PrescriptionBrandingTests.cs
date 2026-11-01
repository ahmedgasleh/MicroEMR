using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.ClinicConfiguration;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Infrastructure.ClinicalOutput;
using MicroEMR.Infrastructure.ClinicConfiguration;
using UglyToad.PdfPig;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PrescriptionBrandingDatabase : IAsyncLifetime
{
    internal DispensingLocationReferenceDatabase Baseline { get; } = new();
    internal PrescriptionDateDatabase Db => Baseline.Db;
    internal static readonly byte[] Pixel = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
    internal ClinicLogo Logo { get; private set; } = null!;
    public async Task InitializeAsync()
    {
        await Baseline.InitializeAsync();
        try
        {
            // Native decoder is the existing upload processor; snapshots copy its canonical output.
            Logo = await CreateLogo(false);
            foreach (var other in new[] { false, true })
            {
                await using var connection = await Db.Factory(other).OpenConnectionAsync();
                await PrescriptionPrivateNoteFixture.Batches(connection,
                    "ALTER TABLE dbo.ClinicProfile ADD Fax nvarchar(50),Email nvarchar(254),Country nvarchar(100);");
                foreach (var file in new[] { "0104-clinic-branding-logo.sql", "0105-prescription-branding-snapshot.sql" })
                    await PrescriptionPrivateNoteFixture.Batches(connection, PrescriptionPrivateNoteFixture.Sql("tenant-clinical/migrations/" + file));
            }
        }
        catch { await Baseline.DisposeAsync(); throw; }
    }
    internal static async Task<ClinicLogo> CreateLogo(bool large)
    {
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();
        var encoded = await page.EvaluateAsync<string>("""
            large => {
                const canvas=document.createElement('canvas');canvas.width=large?1024:240;canvas.height=large?250:80;
                const context=canvas.getContext('2d');
                if(large){const image=context.createImageData(canvas.width,canvas.height);let seed=1234567;
                    for(let i=0;i<image.data.length;i++){seed=(Math.imul(seed,1664525)+1013904223)|0;image.data[i]=(seed>>>24);}
                    context.putImageData(image,0,0);
                }else{context.fillStyle='#16697a';context.fillRect(0,0,240,80);context.fillStyle='white';context.font='bold 18px Arial';context.fillText('SYNTHETIC CLINIC',28,46);}
                return canvas.toDataURL('image/png').split(',')[1];
            }
            """, large);
        return await new ClinicLogoProcessor().ProcessAsync(Convert.FromBase64String(encoded));
    }
    internal async Task LogoAsync(bool present, bool other = false, ClinicLogo? image = null)
    {
        image ??= Logo;
        await using var connection = await Db.Factory(other).OpenConnectionAsync();
        await using var command = new SqlCommand("dbo.ClinicBrandingLogo_Save", connection) { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("@PngBytes", SqlDbType.VarBinary, -1).Value = present ? image.PngBytes : DBNull.Value;
        command.Parameters.Add("@Width", SqlDbType.Int).Value = present ? image.Width : DBNull.Value;
        command.Parameters.Add("@Height", SqlDbType.Int).Value = present ? image.Height : DBNull.Value;
        command.Parameters.AddWithValue("@UpdatedBy", 7);
        await using var version = new SqlCommand("SELECT RowVersion FROM dbo.ClinicBrandingLogo WHERE ClinicBrandingLogoId=1", connection);
        command.Parameters.Add("@ExpectedRowVersion", SqlDbType.Binary, 8).Value = await version.ExecuteScalarAsync() ?? DBNull.Value;
        await command.ExecuteNonQueryAsync();
    }
    public Task DisposeAsync() => Baseline.DisposeAsync();
}

public sealed class PrescriptionBrandingTests(PrescriptionBrandingDatabase fixture) : IClassFixture<PrescriptionBrandingDatabase>
{
    private PrescriptionDateDatabase Db => fixture.Db;
    private static readonly PrescriptionOutputRequester Requester = new("test-subject-7", "Synthetic requester", "branding-test");
    private static readonly string Samples = Path.Combine(FindRoot(), ".tmp", "product-improvement-009", "step03");
    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root unavailable.");
    }
    private static CreatePrescriptionDraftRequest Draft()
    {
        var request = PrescriptionRefillInstructionsTests.Draft();
        request.ProductDisplayText = "Synthetic medicine";
        request.StrengthValue = 5; request.StrengthUnit = "mg"; request.DoseAmount = 1; request.DoseUnit = "tablet";
        request.InitialDaysSupply = 10; request.RefillQuantity = 60; request.RefillQuantityUnit = "tablet";
        request.RefillDaysSupply = 20; request.AuthorizedRepeats = 2;
        request.MedicationForm = "tablet"; request.NotesToPharmacist = "Synthetic pharmacist note";
        return request;
    }
    private async Task<(Guid Patient, PatientPrescriptionResponse Prescription, PrescriptionArtifact Artifact)> Finalize(bool signed, CreatePrescriptionDraftRequest? request = null)
    {
        var patient = await Db.Stores.SeedPatient();
        var repository = Db.Repository();
        var draft = await repository.CreateAsync(patient, request ?? Draft(), 7);
        var final = (signed ? await repository.SignAndFinalizeAsync(patient, draft.PrescriptionUid, draft.RowVersion, 7)
            : await repository.FinalizeAsync(patient, draft.PrescriptionUid, draft.RowVersion, 7))!;
        return (patient, final, (await repository.GetArtifactAsync(patient, final.PrescriptionUid))!);
    }
    private PrescriptionOutputService Service(IPrescriptionPdfRenderer renderer) => new(Db.Repository(), Db.Repository(),
        new PatientPrescriptionService(Db.Repository(), new LegacyCapture()), renderer, TimeProvider.System);
    private sealed class LegacyCapture : IPdfRenderer
    {
        public Task<byte[]> RenderAsync(string html, CancellationToken cancellationToken = default) => Task.FromResult(Encoding.UTF8.GetBytes(html));
    }
    private sealed class Capture : IPrescriptionPdfRenderer
    {
        internal PrescriptionPdfLayout? Layout;
        public Task<byte[]> RenderPrescriptionAsync(PrescriptionPdfLayout layout, CancellationToken token = default)
        { Layout = layout; return Task.FromResult(Encoding.UTF8.GetBytes(layout.Body + layout.Header + layout.Footer)); }
    }

    [Theory] [InlineData(true, true)] [InlineData(true, false)] [InlineData(false, true)] [InlineData(false, false)]
    public async Task SignedAndUnsignedFreezeBrandingAndKeepClinicalValues(bool signed, bool logo)
    {
        await fixture.LogoAsync(logo);
        await Db.Stores.Execute("UPDATE dbo.ClinicProfile SET LegalName=N'Frozen <clinic>',Phone=N'416-555-0123',Fax=NULL,Email=NULL,Country=N'Canada';");
        var row = await Finalize(signed);
        using var document = JsonDocument.Parse(row.Artifact.Json);
        var snapshot = document.RootElement;
        Assert.Equal(3, snapshot.GetProperty("PrescriptionOutputVersion").GetInt32());
        Assert.Equal(signed ? 2 : 1, snapshot.GetProperty("PrescriptionContentVersion").GetInt32());
        var branding = snapshot.GetProperty("Branding");
        Assert.Equal("Frozen <clinic>", branding.GetProperty("Name").GetString());
        Assert.Equal("416-555-0123", branding.GetProperty("Phone").GetString());
        Assert.Equal(logo ? JsonValueKind.String : JsonValueKind.Null, branding.GetProperty("PngBase64").ValueKind);
        if (logo) Assert.Equal(fixture.Logo.PngBytes, branding.GetProperty("PngBase64").GetBytesFromBase64());
        var request = Draft();
        Assert.Equal(request.Directions, snapshot.GetProperty("Directions").GetString());
        Assert.Equal(request.Quantity, snapshot.GetProperty("Quantity").GetDecimal());
        Assert.Equal(60, snapshot.GetProperty("RefillQuantity").GetDecimal());
        Assert.Equal(20, snapshot.GetProperty("RefillDaysSupply").GetInt32());
        Assert.Equal(2, snapshot.GetProperty("AuthorizedRepeats").GetInt32());
        Assert.Equal(signed, snapshot.TryGetProperty("SignatureVersion", out _));
        if (signed) Assert.Equal(row.Prescription.FinalizedAtUtc, snapshot.GetProperty("SignedAtUtc").GetDateTime());
        var capture = new Capture(); var service = Service(capture);
        await service.GenerateAsync(row.Patient, row.Prescription.PrescriptionUid, Requester);
        var first = capture.Layout!;
        Assert.Contains("Frozen &lt;clinic&gt;", first.Header);
        Assert.Equal(logo, first.Header.Contains("data:image/png;base64,"));
        Assert.Contains("Refill", first.Body);
        Assert.Equal(signed, first.Footer.Contains("Electronically signed by"));
        await fixture.LogoAsync(!logo);
        await Db.Stores.Execute("UPDATE dbo.ClinicProfile SET LegalName=N'Changed current clinic',Phone=N'999';");
        await service.GenerateAsync(row.Patient, row.Prescription.PrescriptionUid, Requester with { Name = "Second reader" });
        Assert.Equal(first.Header, capture.Layout!.Header); Assert.Equal(first.Body, capture.Layout.Body);
        Assert.NotEqual(first.Footer, capture.Layout.Footer);
        Assert.Equal(row.Artifact, await Db.Repository().GetArtifactAsync(row.Patient, row.Prescription.PrescriptionUid));
        Assert.Equal(row.Prescription.RowVersion, (await Db.Repository().GetAsync(row.Patient, row.Prescription.PrescriptionUid))!.RowVersion);
        Assert.Equal(2, await Db.Stores.AuditCount(row.Prescription.PrescriptionUid, "PrescriptionPrintOutputGenerated"));
    }

    [Fact]
    public async Task MigrationPreservesLegacyAndVersionTwoJsonAndRenderDispatch()
    {
        var store = Db.Stores;
        var service = Service(new Capture());
        var first = await service.GenerateAsync(store.LegacyPatient, store.LegacyPrescription, Requester);
        Assert.Contains("<dt>Quantity / repeats</dt>", Encoding.UTF8.GetString(first!));
        Assert.DoesNotContain("clinic-letterhead", Encoding.UTF8.GetString(first!));
        var capture = new Capture();
        await Service(capture).GenerateAsync(store.PriorSignedPatient, store.PriorSignedPrescription, Requester);
        Assert.False(capture.Layout!.MeasureHeader);
        var before = capture.Layout;
        await fixture.LogoAsync(true);
        await Db.Stores.Execute("UPDATE dbo.ClinicProfile SET LegalName=N'Live logo must not enrich old artifacts';");
        Assert.Equal(first, await service.GenerateAsync(store.LegacyPatient, store.LegacyPrescription, Requester));
        await Service(capture).GenerateAsync(store.PriorSignedPatient, store.PriorSignedPrescription, Requester);
        Assert.Equal(before.Header, capture.Layout!.Header); Assert.Equal(before.Body, capture.Layout.Body);
        Assert.Equal(store.LegacyJson, (await Db.Repository().GetArtifactAsync(store.LegacyPatient, store.LegacyPrescription))!.Json);
        Assert.Equal(store.PriorSignedJson, (await Db.Repository().GetArtifactAsync(store.PriorSignedPatient, store.PriorSignedPrescription))!.Json);
    }

    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task BrandingCaptureFailureRollsBackFinalization(bool signed)
    {
        await fixture.LogoAsync(true);
        await Db.Stores.Execute("UPDATE dbo.ClinicBrandingLogo SET PngBytes=CONVERT(varbinary(max),REPLICATE('x',100)),Width=1,Height=1;");
        try
        {
            var patient = await Db.Stores.SeedPatient(); var repo = Db.Repository();
            var draft = await repo.CreateAsync(patient, Draft(), 7);
            var exception = await Assert.ThrowsAsync<SqlException>(() => signed
                ? repo.SignAndFinalizeAsync(patient, draft.PrescriptionUid, draft.RowVersion, 7)
                : repo.FinalizeAsync(patient, draft.PrescriptionUid, draft.RowVersion, 7));
            Assert.Equal(51813, exception.Number);
            Assert.Equal(draft.RowVersion, (await repo.GetAsync(patient, draft.PrescriptionUid))!.RowVersion);
            Assert.Null(await repo.GetArtifactAsync(patient, draft.PrescriptionUid));
            Assert.Equal(0, await Db.Stores.AuditCount(draft.PrescriptionUid, "PrescriptionFinalized"));
        }
        finally { await fixture.LogoAsync(false); }
    }

    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task AuditFailureKeepsDraftAndNoArtifact(bool signed)
    {
        await fixture.LogoAsync(false);
        var patient = await Db.Stores.SeedPatient(); var repo = Db.Repository(); var draft = await repo.CreateAsync(patient, Draft(), 7);
        await Db.Stores.Execute("CREATE TRIGGER dbo.BrandingAuditFault ON dbo.AuditLog AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE ActionName='PrescriptionFinalized') THROW 53999,'Synthetic audit fault',1; END;");
        try
        {
            await Assert.ThrowsAsync<SqlException>(() => signed ? repo.SignAndFinalizeAsync(patient, draft.PrescriptionUid, draft.RowVersion, 7)
                : repo.FinalizeAsync(patient, draft.PrescriptionUid, draft.RowVersion, 7));
            Assert.Equal("Draft", (await repo.GetAsync(patient, draft.PrescriptionUid))!.Status);
            Assert.Null(await repo.GetArtifactAsync(patient, draft.PrescriptionUid));
        }
        finally { await Db.Stores.Execute("DROP TRIGGER dbo.BrandingAuditFault;"); }
    }

    [Fact]
    public async Task RealEndpointReadAndFinalizePermissionsAndTenantIsolationRemainUnchanged()
    {
        await fixture.LogoAsync(true);
        var row = await Finalize(true);
        await using var host = await PrescriptionDateHost.Start(Db, false);
        using var reader = host.Client("reader"); using var denied = host.Client("no-view");
        var route = $"api/patients/{row.Patient}/prescriptions/{row.Prescription.PrescriptionUid}";
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync(route + "/artifact")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync(route + "/artifact")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync(route + "/sign-and-finalize", new PrescriptionSigningRequest { ConfirmSignature = true, RowVersion = row.Prescription.RowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync(route + "/finalize", new PrescriptionTransitionRequest { RowVersion = row.Prescription.RowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reader.GetAsync($"api/patients/{Guid.NewGuid()}/prescriptions/{row.Prescription.PrescriptionUid}/artifact")).StatusCode);
        await using var other = await PrescriptionDateHost.Start(Db, true); using var foreign = other.Client("reader");
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.GetAsync(route + "/artifact")).StatusCode);
        Assert.Null(await Db.Repository(true).GetArtifactAsync(row.Patient, row.Prescription.PrescriptionUid));
    }

    [Fact]
    public async Task NewTenantSnapshotUsesItsOwnBrandingAndFailsClosedOnMalformedPayload()
    {
        await using var connection = await Db.Factory(true).OpenConnectionAsync();
        await PrescriptionPrivateNoteFixture.Batches(connection, "UPDATE dbo.ClinicProfile SET LegalName=N'Second tenant only';");
        await fixture.LogoAsync(false, true);
        var patient = await Db.SeedOtherPatient(); var repository = Db.Repository(true);
        var draft = await repository.CreateAsync(patient, Draft(), 7);
        var row = (await repository.FinalizeAsync(patient, draft.PrescriptionUid, draft.RowVersion, 7))!;
        var artifact = (await repository.GetArtifactAsync(patient, row.PrescriptionUid))!;
        var node = JsonNode.Parse(artifact.Json)!;
        Assert.Equal("Second tenant only", node["Branding"]!["Name"]!.GetValue<string>());
        node["Branding"]!["PngBase64"] = new string('A', 1398105);
        using (var json = JsonDocument.Parse(node.ToJsonString()))
            Assert.Throws<InvalidOperationException>(() => PrescriptionBrandingSnapshot.Compose(json.RootElement, new(Guid.NewGuid(), DateTimeOffset.UtcNow, Requester)));
        node["Branding"]!["PngBase64"] = null; node["Branding"]!["Width"] = 3;
        using (var json = JsonDocument.Parse(node.ToJsonString()))
            Assert.Throws<InvalidOperationException>(() => PrescriptionBrandingSnapshot.Compose(json.RootElement, new(Guid.NewGuid(), DateTimeOffset.UtcNow, Requester)));
    }

    [Fact]
    public async Task LargeCanonicalLogoFitsSnapshotAndRoundTripsWithinBound()
    {
        var logo = await PrescriptionBrandingDatabase.CreateLogo(true);
        Assert.InRange(logo.PngBytes.Length, 700000, ClinicBrandingService.MaximumLogoBytes);
        await fixture.LogoAsync(true, image: logo);
        var row = await Finalize(true);
        using var json = JsonDocument.Parse(row.Artifact.Json);
        Assert.Equal(logo.PngBytes, json.RootElement.GetProperty("Branding").GetProperty("PngBase64").GetBytesFromBase64());
        Assert.InRange(row.Artifact.Json.Length, 900000, 1450000);
        var layout = PrescriptionBrandingSnapshot.Compose(json.RootElement, new(Guid.NewGuid(), DateTimeOffset.UtcNow, Requester));
        Assert.Contains("data:image/png;base64,", layout.Header);
        await fixture.LogoAsync(false);
    }

    [Fact]
    public async Task NativePdfSamplesPreserveContentAndClearHeaderAndSignatureAreas()
    {
        Directory.CreateDirectory(Samples);
        await using var renderer = new PlaywrightPdfRenderer(NullLogger<PlaywrightPdfRenderer>.Instance);
        var results = new List<object>();
        var legacy = await new PatientPrescriptionService(Db.Repository(), renderer).RenderArtifactPdfAsync(Db.Stores.LegacyPatient, Db.Stores.LegacyPrescription);
        await File.WriteAllBytesAsync(Path.Combine(Samples, "prescription-legacy.pdf"), legacy!);
        foreach (var name in new[] { "signed", "unsigned", "text-only", "long-clinic", "multipage" })
        {
            await fixture.LogoAsync(name != "text-only");
            var longClinic = name == "long-clinic";
            await Db.Stores.Execute("UPDATE dbo.ClinicProfile SET LegalName=N'" + (longClinic ? string.Concat(Enumerable.Repeat("Synthetic Long Clinic ", 9)) : "Synthetic Clinic") +
                "',Phone=NULL,Fax=NULL,Email=NULL,Country=N'Canada';");
            var request = Draft();
            if (name == "multipage")
            {
                request.Directions = string.Join("\n", Enumerable.Range(1, 40).Select(i => $"DIRECTION-{i:00} test only"));
                request.NotesToPharmacist = string.Join("\n", Enumerable.Range(1, 40).Select(i => $"PHARMACIST-{i:00} test"));
            }
            var row = await Finalize(name != "unsigned", request);
            using var snapshot = JsonDocument.Parse(row.Artifact.Json);
            var layout = PrescriptionBrandingSnapshot.Compose(snapshot.RootElement, new(Guid.NewGuid(), DateTimeOffset.UtcNow, Requester));
            var bytes = await renderer.RenderPrescriptionAsync(layout);
            await File.WriteAllBytesAsync(Path.Combine(Samples, "prescription-" + name + ".pdf"), bytes);
            using var pdf = PdfDocument.Open(bytes);
            var allText = string.Join("\n", pdf.GetPages().Select(page => page.Text));
            Assert.Contains("Refill", allText); Assert.Contains("60", allText);
            Assert.Contains("Synthetic medicine", allText);
            Assert.Equal(name != "unsigned", allText.Contains("Electronically signed by"));
            var images = pdf.GetPages().Sum(page => page.GetImages().Count());
            Assert.Equal(name == "text-only" ? 0 : pdf.NumberOfPages, images);
            foreach (var page in pdf.GetPages())
            {
                Assert.Contains(longClinic ? "Synthetic Long Clinic" : "Synthetic Clinic", page.Text);
                Assert.Contains("Prescription", page.Text); Assert.Contains("Patient:", page.Text);
                Assert.Contains("physical printing not confirmed", page.Text);
                if (name != "unsigned") Assert.Contains("Electronically signed by", page.Text);
                foreach (var letter in page.Letters)
                {
                    Assert.InRange(letter.GlyphRectangle.Left, 40, page.Width - 40);
                    Assert.InRange(letter.GlyphRectangle.Right, 40, page.Width - 40);
                    Assert.InRange(letter.GlyphRectangle.Bottom, 10, page.Height - 10);
                    Assert.False(letter.GlyphRectangle.Bottom < 108 && letter.GlyphRectangle.Top > 108);
                }
                // Chromium quantizes the 110px by 36.67px display box to whole CSS pixels.
                foreach (var image in page.GetImages())
                {
                    Assert.InRange(image.Bounds.Width / image.Bounds.Height, 2.95, 3.05);
                    Assert.InRange(image.Bounds.Width, 1, 83);
                    Assert.InRange(image.Bounds.Height, 1, 40);
                }
            }
            if (name == "multipage")
            {
                Assert.True(pdf.NumberOfPages > 1);
                for (var i = 1; i <= 40; i++) { Assert.Contains($"DIRECTION-{i:00}", allText); Assert.Contains($"PHARMACIST-{i:00}", allText); }
            }
            results.Add(new { Name = name, Pages = pdf.NumberOfPages, Images = images, Bytes = bytes.Length });
        }
        await File.WriteAllTextAsync(Path.Combine(Samples, "pdf-results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    }
}
