using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using MicroEMR.Application.PatientPrescriptions;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PrescriptionRefillInstructionsTests
{
    [Theory]
    [InlineData(30, 30, 30, 30, 2)]
    [InlineData(10, 10, 30, 30, 2)]
    [InlineData(30, 30, 90, 90, 1)]
    [InlineData(30, 30, null, null, 0)]
    [InlineData(30, 30, 30, 30, 3)]
    [InlineData(30, null, 90, null, 2)]
    [InlineData(30, 30, null, 90, 2)]
    public void IndependentValuesAreValidAndSurviveJson(int initial, int? initialDays, int? refill, int? refillDays, int repeats)
    {
        var request = Draft(initial, initialDays, refill, refillDays, repeats);
        Assert.Empty(Validate(request));
        var copy = JsonSerializer.Deserialize<CreatePrescriptionDraftRequest>(JsonSerializer.Serialize(request))!;
        Assert.Equal(request.Quantity, copy.Quantity);
        Assert.Equal(initialDays, copy.InitialDaysSupply);
        Assert.Equal(request.RefillQuantity, copy.RefillQuantity);
        Assert.Equal(request.RefillQuantityUnit, copy.RefillQuantityUnit);
        Assert.Equal(refillDays, copy.RefillDaysSupply);
        Assert.Equal(repeats, copy.AuthorizedRepeats);
    }

    [Theory]
    [InlineData("initial-zero")]
    [InlineData("refill-zero")]
    [InlineData("refill-negative")]
    [InlineData("refill-precision")]
    [InlineData("refill-days-zero")]
    [InlineData("unit-missing")]
    [InlineData("amount-missing")]
    [InlineData("unit-too-long")]
    [InlineData("zero-count-with-instructions")]
    [InlineData("negative-count")]
    public void InvalidTechnicalValuesAreRejected(string invalid)
    {
        var request = Draft();
        switch (invalid)
        {
            case "initial-zero": request.InitialDaysSupply = 0; break;
            case "refill-zero": request.RefillQuantity = 0; break;
            case "refill-negative": request.RefillQuantity = -1; break;
            case "refill-precision": request.RefillQuantity = 1.0001m; break;
            case "refill-days-zero": request.RefillDaysSupply = 0; break;
            case "unit-missing": request.RefillQuantityUnit = " "; break;
            case "amount-missing": request.RefillQuantity = null; break;
            case "unit-too-long": request.RefillQuantityUnit = new string('a', 51); break;
            case "zero-count-with-instructions": request.AuthorizedRepeats = 0; break;
            case "negative-count": request.AuthorizedRepeats = -1; break;
        }
        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public void MissingLegacyValuesAreNotInferredAndDirectionsAreNotParsed()
    {
        var legacy = JsonSerializer.Deserialize<PrescriptionDraftRequest>("""
            {"Quantity":30,"QuantityUnit":"tablet","AuthorizedRepeats":3}
            """)!;
        Assert.Null(legacy.InitialDaysSupply);
        Assert.Null(legacy.RefillQuantity);
        Assert.Null(legacy.RefillQuantityUnit);
        Assert.Null(legacy.RefillDaysSupply);
        var request = Draft();
        request.InitialDaysSupply = 20;
        request.Directions = "Take one tablet daily, or as directed during a taper.";
        Assert.Empty(Validate(request));
    }

    internal static CreatePrescriptionDraftRequest Draft(int quantity = 10, int? initialDays = 10,
        int? refillQuantity = 30, int? refillDays = 30, int repeats = 2) => new()
    {
        ProductName = "Synthetic medication", ProductDisplayText = "Synthetic medication tablet",
        Route = "Oral", FrequencyCode = "ONCE_DAILY", Directions = "Take one tablet daily.",
        Quantity = quantity, QuantityUnit = "tablet", AuthorizedRepeats = repeats,
        InitialDaysSupply = initialDays, RefillQuantity = refillQuantity,
        RefillQuantityUnit = refillQuantity.HasValue ? "tablet" : null, RefillDaysSupply = refillDays,
        PrescribedDate = new DateOnly(2026, 10, 11)
    };

    private static List<ValidationResult> Validate(object request)
    {
        var errors = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), errors, true);
        return errors;
    }
}
