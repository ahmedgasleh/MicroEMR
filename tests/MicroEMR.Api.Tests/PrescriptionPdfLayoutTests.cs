using System.Text.Json;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Infrastructure.ClinicalOutput;
using Microsoft.Extensions.Logging.Abstractions;
using UglyToad.PdfPig;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PrescriptionPdfLayoutTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    [InlineData(40)]
    public async Task PrescriptionFitsNormalPageAndKeepsLongContentClearOfPageFurniture(int noteLines)
    {
        var notes = noteLines == 0 ? "Mock packaging; no real supply."
            : noteLines == 20 ? string.Join("\n", Enumerable.Repeat("TEST ONLY ? mock note line for page review.", noteLines))
            : string.Join("\n", Enumerable.Range(1, noteLines).Select(i => $"Mock note line {i}."));
        using var snapshot = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            SignatureVersion = 1, PrescriberDisplayName = "Test Physician", PrescriberCredential = "Physician | 12345",
            SignedAtUtc = "2026-10-16T14:57:06", CreatedAtUtc = "2026-10-16T14:22:33", CreatedByDisplayName = "Test User",
            ClinicName = "Mock Clinic - TEST ONLY", ClinicAddressLine1 = "20 Mock Clinic Lane", ClinicAddressLine2 = "",
            ClinicCity = "Toronto", ClinicProvince = "ON", ClinicPostalCode = "M5V 0A1", ClinicPhone = "416-555-0100",
            PatientName = "Mock Patient", DateOfBirth = "1990-01-01", HealthCardNumber = "TEST ONLY",
            PatientAddressLine1 = "19 Mock Lane", PatientAddressLine2 = "", PatientCity = "Toronto",
            PatientProvince = "ON", PatientPostalCode = "M5V 0A1", PatientPhone = "416-555-0101",
            PrescriptionUid = Guid.Parse("11111111-2222-3333-4444-555555555555"), PrescribedDate = "2026-10-16",
            ProductDisplayText = "Mock Medicine", StrengthValue = "5.000000", StrengthUnit = "mg", MedicationForm = "Tablet",
            DoseAmount = "1.000000", DoseUnit = "tablet", Route = "Oral", FrequencyDisplay = "Once daily", Prn = false,
            Directions = "TEST ONLY: Take one mock tablet daily.", StartDate = "2026-10-16", Quantity = "10.000",
            QuantityUnit = "tablets", InitialDaysSupply = 10, AuthorizedRepeats = 2, RefillQuantity = "30.000",
            RefillQuantityUnit = "tablets", RefillDaysSupply = 30, NotesToPharmacist = notes, Indication = "Mock indication end marker"
        }));
        var layout = PrescriptionOutputService.Compose(snapshot.RootElement,
            new(Guid.NewGuid(), DateTimeOffset.Parse("2026-10-16T15:00:00Z"), new("test-subject", "Test User", "layout-test")));
        await using var renderer = new PlaywrightPdfRenderer(NullLogger<PlaywrightPdfRenderer>.Instance);
        var bytes = await renderer.RenderPrescriptionAsync(layout);
        using var document = PdfDocument.Open(bytes);
        if (noteLines == 40) Assert.True(document.NumberOfPages > 1);
        else Assert.Equal(1, document.NumberOfPages);
        foreach (var page in document.GetPages())
        {
            Assert.Contains("Electronically signed by Test Physician", page.Text);
            Assert.Contains("Mock Clinic", page.Text);
            Assert.Contains("physical printing not confirmed", page.Text);
            Assert.Contains("Local structured prescription. Not electronically transmitted.", page.Text);
            // Header is above the one-inch body margin; footer below its 1.5-inch margin.
            foreach (var word in page.GetWords())
            {
                Assert.InRange(word.BoundingBox.Left, 45, 568);
                Assert.InRange(word.BoundingBox.Right, 45, 568);
                Assert.InRange(word.BoundingBox.Bottom, 12, 780);
                Assert.False(word.BoundingBox.Bottom < 108 && word.BoundingBox.Top > 108,
                    "Body content crosses into the reserved signature/footer area.");
            }
        }
        var text = string.Join("\n", document.GetPages().Select(p => p.Text));
        Assert.Contains("Mock indication end marker", text);
        if (noteLines == 40) Assert.Contains("Mock note line 40.", text);
        await File.WriteAllBytesAsync(Path.Combine(AppContext.BaseDirectory,
            $"prescription-layout-{noteLines}-lines.pdf"), bytes);
    }
}
