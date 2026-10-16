using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Infrastructure.ClinicalOutput;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PrescriptionPrintTests(PrescriptionRefillSqlDatabase db) : IClassFixture<PrescriptionRefillSqlDatabase>
{
    private static readonly PrescriptionOutputRequester Requester = new("test-subject-7", "Test actor 7", "step93-test");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SigningPolicyRequiresEffectivePrescribingPermission(bool allowed)
    {
        var requirement = new PermissionRequirement(PermissionKeys.PrescriptionsPrescribe);
        var user = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim("sub", "test-subject-7")], "test"));
        var context = new Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext([requirement], user, null);
        await new PermissionAuthorizationHandler(new Permissions(allowed), NullLogger<PermissionAuthorizationHandler>.Instance).HandleAsync(context);
        Assert.Equal(allowed, context.HasSucceeded);
    }

    [Fact]
    public async Task SigningRequiresIntentCurrentOwnerProviderAndVersion()
    {
        var patient = await db.SeedPatient();
        var request = Draft();
        var created = await db.Repository.CreateAsync(patient, request, 7);
        var service = Service(new CapturePdf());
        await Assert.ThrowsAsync<ArgumentException>(() => service.SignAndFinalizeAsync(patient, created.PrescriptionUid,
            new() { RowVersion = created.RowVersion }, 7));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SignAndFinalizeAsync(patient, created.PrescriptionUid,
            new() { ConfirmSignature = true, RowVersion = "bad" }, 7));
        await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(() => db.Repository.SignAndFinalizeAsync(patient, created.PrescriptionUid, created.RowVersion, 8));
        await Assert.ThrowsAsync<PrescriberProviderMappingException>(() => db.Repository.SignAndFinalizeAsync(patient, created.PrescriptionUid, created.RowVersion, 99));
        var foreign = await db.SeedPatient();
        await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(() => db.Repository.SignAndFinalizeAsync(foreign, created.PrescriptionUid, created.RowVersion, 7));
        await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(() => db.OtherRepository.SignAndFinalizeAsync(patient, created.PrescriptionUid, created.RowVersion, 7));
        request.RowVersion = created.RowVersion;
        var updated = (await db.Repository.UpdateAsync(patient, created.PrescriptionUid, request, 7))!;
        await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(() => db.Repository.SignAndFinalizeAsync(patient, created.PrescriptionUid, created.RowVersion, 7));
        Assert.Equal(0, await db.AuditCount(created.PrescriptionUid, "PrescriptionFinalized"));
        var signed = (await service.SignAndFinalizeAsync(patient, created.PrescriptionUid,
            new() { ConfirmSignature = true, RowVersion = updated.RowVersion }, 7))!;
        Assert.Equal("Finalized", signed.Status);
        await Assert.ThrowsAsync<PatientPrescriptionConcurrencyException>(() => db.Repository.SignAndFinalizeAsync(patient, signed.PrescriptionUid, signed.RowVersion, 7));
        Assert.Equal(1, await db.AuditCount(signed.PrescriptionUid, "PrescriptionFinalized"));
        var method = typeof(PatientPrescriptionsController).GetMethod("SignAndFinalize")!;
        Assert.Contains(method.GetCustomAttributes(typeof(RequirePermissionAttribute), true).Cast<RequirePermissionAttribute>(),
            a => a.Policy == "Permission:" + PermissionKeys.PrescriptionsPrescribe);
    }

    [Fact]
    public async Task NewFieldsSignedSnapshotAndRepeatedOutputRemainFrozenAndCorrelated()
    {
        var patient = await db.SeedPatient();
        await db.Execute($"UPDATE dbo.Patient SET HealthCardNumber=N'TEST-PC0405-ONLY',PhoneNumber=N'416-555-0101',AddressLine1=N'10 Mock Print Lane',City=N'Toronto',Province=N'ON',PostalCode=N'M5V 0A1' WHERE PatientUid='{patient}';");
        var created = await db.Repository.CreateAsync(patient, Draft(), 7);
        var renderer = new CapturePdf();
        var service = Service(renderer);
        var signed = (await db.Repository.SignAndFinalizeAsync(patient, created.PrescriptionUid, created.RowVersion, 7))!;
        Assert.Equal("tablet", signed.MedicationForm);
        Assert.Equal("TEST ONLY <b>mock packaging</b>", signed.NotesToPharmacist);
        var artifact = (await db.Repository.GetArtifactAsync(patient, signed.PrescriptionUid))!;
        using var json = JsonDocument.Parse(artifact.Json);
        Assert.Equal(2, json.RootElement.GetProperty("PrescriptionOutputVersion").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("SignatureVersion").GetInt32());
        Assert.Equal(7, json.RootElement.GetProperty("SignerUserId").GetInt64());
        Assert.Equal(signed.FinalizedAtUtc, json.RootElement.GetProperty("SignedAtUtc").GetDateTime());
        Assert.Equal("2026-10-17", json.RootElement.GetProperty("StartDate").GetString());
        await service.GenerateAsync(patient, signed.PrescriptionUid, Requester);
        var first = renderer.Last!;
        Assert.Contains("Medication form</dt><dd>tablet", first.Body);
        Assert.Contains("Strength</dt><dd>5.000000", first.Body);
        Assert.Contains("Dose</dt><dd>1.000000", first.Body);
        Assert.Contains("2026-10-17", first.Body);
        Assert.Contains("&lt;b&gt;mock packaging&lt;/b&gt;", first.Body);
        Assert.Contains("Mock clinic", first.Header);
        Assert.Contains("TEST-PC0405-ONLY", first.Header);
        Assert.Contains("10 Mock Print Lane", first.Header);
        Assert.Contains("416-555-0101", first.Header);
        Assert.Contains("Electronically signed by Test prescriber", first.Footer);
        Assert.Contains("Test actor 7", first.Footer);
        Assert.Contains("UTC", first.Footer);
        await db.Execute("UPDATE dbo.ClinicProfile SET Phone=N'416-555-0199';");
        await service.GenerateAsync(patient, signed.PrescriptionUid, Requester with { Subject = "view-only-subject", Name = "View-only requester" });
        var second = renderer.Last!;
        Assert.Contains("View-only requester", second.Footer);
        Assert.NotEqual(first.Footer, second.Footer);
        Assert.Equal(first.Body, second.Body);
        Assert.Equal(first.Header, second.Header);
        Assert.Equal(artifact, await db.Repository.GetArtifactAsync(patient, signed.PrescriptionUid));
        Assert.Equal(signed.RowVersion, (await db.Repository.GetAsync(patient, signed.PrescriptionUid))!.RowVersion);
        Assert.Equal(2, await db.AuditCount(signed.PrescriptionUid, "PrescriptionPrintOutputGenerated"));
        Assert.Equal(1, await db.Read<int>("SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@Uid) AND ActionName=N'PrescriptionPrintOutputGenerated' AND UserId IS NULL AND NewValue LIKE N'%view-only-subject%'", signed.PrescriptionUid));
        Assert.Null(await service.GenerateAsync(await db.SeedPatient(), signed.PrescriptionUid, Requester));
        Assert.Equal(2, await db.AuditCount(signed.PrescriptionUid, "PrescriptionPrintOutputGenerated"));
    }

    [Fact]
    public async Task AuditAndRenderingFailuresNeverReturnSuccessfulOutputOrDuplicateLifecycle()
    {
        var patient = await db.SeedPatient();
        var created = await db.Repository.CreateAsync(patient, Draft(), 7);
        await db.Repository.SignAndFinalizeAsync(patient, created.PrescriptionUid, created.RowVersion, 7);
        var failing = new CapturePdf { Fail = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(failing).GenerateAsync(patient, created.PrescriptionUid, Requester));
        Assert.Equal(0, await db.AuditCount(created.PrescriptionUid, "PrescriptionPrintOutputGenerated"));
        await db.Execute("CREATE TRIGGER dbo.PrintAuditFailure ON dbo.AuditLog AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE ActionName=N'PrescriptionPrintOutputGenerated') THROW 51999,'Synthetic output audit failure.',1; END;");
        try
        {
            await Assert.ThrowsAsync<SqlException>(() => Service(new CapturePdf()).GenerateAsync(patient, created.PrescriptionUid, Requester));
            Assert.Equal(0, await db.AuditCount(created.PrescriptionUid, "PrescriptionPrintOutputGenerated"));
        }
        finally { await db.Execute("DROP TRIGGER dbo.PrintAuditFailure;"); }
        await Service(new CapturePdf()).GenerateAsync(patient, created.PrescriptionUid, Requester);
        Assert.Equal(1, await db.AuditCount(created.PrescriptionUid, "PrescriptionPrintOutputGenerated"));
        Assert.Equal(1, await db.AuditCount(created.PrescriptionUid, "PrescriptionFinalized"));
    }

    [Fact]
    public async Task SignedCorrectionCopiesNewFieldsButRequiresItsOwnSignatureAndPreservesSource()
    {
        var patient = await db.SeedPatient();
        var draft = await db.Repository.CreateAsync(patient, Draft(), 7);
        var signed = (await db.Repository.SignAndFinalizeAsync(patient, draft.PrescriptionUid, draft.RowVersion, 7))!;
        var originalArtifact = (await db.Repository.GetArtifactAsync(patient, signed.PrescriptionUid))!;
        var api = new PatientPrescriptionsController(new PatientPrescriptionService(db.Repository, new LegacyPdf()))
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() }
        };
        MicroEMR.Api.ClinicalUsers.ClinicalUserActorContext.Set(api.HttpContext, 7);
        var result = await api.Correction(patient, signed.PrescriptionUid, new() { RowVersion = signed.RowVersion }, default);
        var correction = Assert.IsType<PatientPrescriptionResponse>(Assert.IsType<Microsoft.AspNetCore.Mvc.CreatedAtActionResult>(result.Result).Value);
        Assert.Equal(signed.MedicationForm, correction.MedicationForm);
        Assert.Equal(signed.NotesToPharmacist, correction.NotesToPharmacist);
        Assert.Equal("Draft", correction.Status);
        Assert.Null(correction.FinalizedAtUtc);
        Assert.Null(await db.Repository.GetArtifactAsync(patient, correction.PrescriptionUid));
        await db.Repository.SignAndFinalizeAsync(patient, correction.PrescriptionUid, correction.RowVersion, 7);
        Assert.Equal(originalArtifact, await db.Repository.GetArtifactAsync(patient, signed.PrescriptionUid));
        Assert.Equal("Superseded", (await db.Repository.GetAsync(patient, signed.PrescriptionUid))!.Status);
        Assert.Equal(1, await db.AuditCount(signed.PrescriptionUid, "PrescriptionSuperseded"));
        Assert.Null(await db.OtherRepository.GetArtifactAsync(patient, correction.PrescriptionUid));
    }

    [Fact]
    public async Task SigningAuditFailureRollsBackAndLegacyOutputIsNotRelabelledSigned()
    {
        var patient = await db.SeedPatient();
        var created = await db.Repository.CreateAsync(patient, Draft(), 7);
        await db.Execute("CREATE TRIGGER dbo.SignAuditFailure ON dbo.AuditLog AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE ActionName=N'PrescriptionFinalized') THROW 51999,'Synthetic signing audit failure.',1; END;");
        try
        {
            await Assert.ThrowsAsync<SqlException>(() => db.Repository.SignAndFinalizeAsync(patient, created.PrescriptionUid, created.RowVersion, 7));
            Assert.Equal("Draft", (await db.Repository.GetAsync(patient, created.PrescriptionUid))!.Status);
            Assert.Null(await db.Repository.GetArtifactAsync(patient, created.PrescriptionUid));
        }
        finally { await db.Execute("DROP TRIGGER dbo.SignAuditFailure;"); }
        await db.Repository.FinalizeAsync(patient, created.PrescriptionUid, created.RowVersion, 7);
        var legacy = new PatientPrescriptionService(db.Repository, new LegacyPdf());
        var oldBytes = await legacy.RenderArtifactPdfAsync(patient, created.PrescriptionUid);
        var output = new PrescriptionOutputService(db.Repository, db.Repository, legacy, new CapturePdf(), TimeProvider.System);
        Assert.Equal(oldBytes, await output.GenerateAsync(patient, created.PrescriptionUid, Requester));
        Assert.DoesNotContain("Electronically signed", Encoding.UTF8.GetString(oldBytes!));
        var oldArtifact = (await db.Repository.GetArtifactAsync(db.LegacyPatient, db.LegacyPrescription))!;
        await output.GenerateAsync(db.LegacyPatient, db.LegacyPrescription, Requester);
        Assert.Equal(db.LegacyJson, oldArtifact.Json);
        Assert.Equal(oldArtifact, await db.Repository.GetArtifactAsync(db.LegacyPatient, db.LegacyPrescription));
    }

    [Fact]
    public async Task RealPdfUsesPrescriptionPageFurnitureAndSpansMultiplePages()
    {
        var patient = await db.SeedPatient();
        var request = Draft();
        request.NotesToPharmacist = string.Join("\n", Enumerable.Repeat("TEST ONLY — mock note line for page review.", 23));
        var created = await db.Repository.CreateAsync(patient, request, 7);
        await db.Repository.SignAndFinalizeAsync(patient, created.PrescriptionUid, created.RowVersion, 7);
        await using var renderer = new PlaywrightPdfRenderer(NullLogger<PlaywrightPdfRenderer>.Instance);
        var service = new PrescriptionOutputService(db.Repository, db.Repository,
            new PatientPrescriptionService(db.Repository, renderer), renderer, TimeProvider.System);
        var bytes = (await service.GenerateAsync(patient, created.PrescriptionUid, Requester))!;
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes));
        Assert.True(Regex.Matches(Encoding.Latin1.GetString(bytes), @"/Type\s*/Page\b").Count >= 2);
        // Export only this synthetic output for visual QA; no clinical application database is used.
        var path = Path.Combine(AppContext.BaseDirectory, "step93-prescription-print.pdf");
        await File.WriteAllBytesAsync(path, bytes);
    }

    [Theory]
    [InlineData(101, 0)]
    [InlineData(0, 1001)]
    public void NewClinicalFieldLimitsAreValidated(int formLength, int notesLength)
    {
        var request = Draft(); request.MedicationForm = new('x', formLength); request.NotesToPharmacist = new('x', notesLength);
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, true));
    }

    private PrescriptionOutputService Service(CapturePdf renderer) => new(db.Repository, db.Repository,
        new PatientPrescriptionService(db.Repository, new LegacyPdf()), renderer, TimeProvider.System);
    private static CreatePrescriptionDraftRequest Draft()
    {
        var r = PrescriptionRefillInstructionsTests.Draft();
        r.ProductDisplayText = "Mock medicine"; r.StrengthValue = 5; r.StrengthUnit = "mg";
        r.DoseAmount = 1; r.DoseUnit = "tablet"; r.MedicationForm = "tablet";
        r.NotesToPharmacist = "TEST ONLY <b>mock packaging</b>"; r.StartDate = new(2026, 10, 17);
        return r;
    }
    private sealed class CapturePdf : IPrescriptionPdfRenderer
    {
        public PrescriptionPdfLayout? Last { get; private set; }
        public bool Fail { get; init; }
        public Task<byte[]> RenderPrescriptionAsync(PrescriptionPdfLayout layout, CancellationToken token = default)
        {
            if (Fail) throw new InvalidOperationException("Synthetic render failure.");
            Last = layout; return Task.FromResult(Encoding.UTF8.GetBytes(layout.Body + layout.Header + layout.Footer));
        }
    }
    private sealed class LegacyPdf : MicroEMR.Application.ClinicalOutput.IPdfRenderer
    {
        public Task<byte[]> RenderAsync(string html, CancellationToken cancellationToken = default) => Task.FromResult(Encoding.UTF8.GetBytes(html));
    }

    private sealed class Permissions(bool allowed) : ICurrentUserPermissionService
    {
        public Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(CancellationToken token = default) =>
            Task.FromResult<IReadOnlySet<string>>(allowed ? new HashSet<string> { PermissionKeys.PrescriptionsPrescribe } : new HashSet<string>());
        public Task<bool> HasPermissionAsync(string permissionKey, CancellationToken token = default) => Task.FromResult(allowed && permissionKey == PermissionKeys.PrescriptionsPrescribe);
    }
}
