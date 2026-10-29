using System.Globalization;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.PatientPrescriptions;

namespace MicroEMR.Application.DrugCatalog;

public sealed record DrugIngredient(string Name, string Strength, string Unit, string DosageValue, string DosageUnit, string Basis);
public sealed record DrugFacts(string SourceSystem, string SourceProductId, string Din, string Name,
    string ProductClass, string Status, IReadOnlyList<DrugIngredient> Ingredients,
    IReadOnlyList<string> Forms, IReadOnlyList<string> Routes, string? SourceUpdatedDate, bool SelectionAvailable)
{
    public string? MedicationForm => Forms.Count == 0 ? null : string.Join("; ", Forms);
    // Non-numeric, combination and non-representable strengths remain verbatim in Ingredients.
    public decimal? StrengthValue => Ingredients.Count == 1 && Ingredients[0].DosageValue.Length == 0 && Ingredients[0].DosageUnit.Length == 0 && decimal.TryParse(Ingredients[0].Strength,
        NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) && value > 0 &&
        value <= 999999999999.999999m && decimal.Round(value, 6) == value && Ingredients[0].Unit.Length is > 0 and <= 50 ? value : null;
    public string? StrengthUnit => StrengthValue.HasValue ? Null(Ingredients[0].Unit) : null;
    public bool Selectable => SelectionAvailable && Din.Length > 0 && Name.Trim().Length > 0 && SourceSystem.Length <= 100 && MedicationForm?.Length is not > 100;
    public bool Matches(PrescriptionDraftRequest r) => Null(r.ProductName) == Name && Null(r.ProductDisplayText) == Name &&
        Null(r.ProductIdentifierNamespace) == SourceSystem && Null(r.ProductIdentifierValue) == Din &&
        Null(r.MedicationForm) == MedicationForm && r.StrengthValue == StrengthValue && Null(r.StrengthUnit) == StrengthUnit;
    private static string? Null(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
public sealed record CatalogDrug(Guid ProductUid, DrugFacts Facts, DateTime ImportedAtUtc, string ImportSha256, string RowVersion);
public sealed record DrugCatalogSelection(Guid ProductUid, string RowVersion);
public sealed record DrugCatalogPage(IReadOnlyList<CatalogDrug> Items, int Page, bool HasMore);
public sealed record DrugImportBatch(IReadOnlyList<DrugFacts> Products, string Sha256);
public sealed record DrugImportResult(int Inserted, int Updated, int Unchanged);
public interface IDrugCatalogSourceReader
{
    Task<DrugImportBatch> ReadAsync(Stream input, CancellationToken token = default);
}
public interface IDrugCatalogRepository
{
    Task<DrugImportResult> ImportAsync(DrugImportBatch batch, long actor, CancellationToken token);
    Task<IReadOnlyList<CatalogDrug>> SearchAsync(string query, int offset, int take, CancellationToken token);
    Task<CatalogDrug?> GetAsync(Guid uid, CancellationToken token);
}
public sealed class DrugCatalogConflictException(string message) : Exception(message);
public sealed class DrugCatalogUnavailableException() : Exception("The development drug catalogue is unavailable in this environment. Manual entry remains available.");
public sealed class DrugCatalogService(IDrugCatalogRepository repository, IDrugCatalogSourceReader source, ILogger<DrugCatalogService> logger, bool developmentUse = true)
{
    private void EnsureAvailable() { if (!developmentUse) throw new DrugCatalogUnavailableException(); }
    public async Task<DrugImportResult> ImportAsync(Stream archive, long actor, CancellationToken token = default)
    {
        EnsureAvailable();
        if (actor <= 0) throw new ArgumentException("An active actor is required.");
        var batch = await source.ReadAsync(archive, token);
        if (batch.Products.Count == 0) throw new ArgumentException("The archive contains no drug products.");
        var result = await repository.ImportAsync(batch, actor, token);
        logger.LogInformation("Development drug catalogue import: {Inserted} inserted, {Updated} updated, {Unchanged} unchanged.", result.Inserted, result.Updated, result.Unchanged);
        return result;
    }
    public async Task<DrugCatalogPage> SearchAsync(string? query, int page = 1, CancellationToken token = default)
    {
        EnsureAvailable();
        var text = query?.Trim() ?? "";
        if (text.Length > 200 || text.Any(char.IsControl) || page is < 1 or > 10000) throw new ArgumentException("Invalid catalogue search.");
        var rows = await repository.SearchAsync(text, (page - 1) * 20, 21, token);
        return new(rows.Take(20).ToArray(), page, rows.Count > 20);
    }
    public Task<CatalogDrug?> GetAsync(Guid uid, CancellationToken token = default) { EnsureAvailable(); return repository.GetAsync(uid, token); }
    public async Task ValidateSelectionAsync(PrescriptionDraftRequest request, CancellationToken token)
    {
        if (request.DrugCatalogSelection is not { } selection) return;
        EnsureAvailable();
        Version(selection.RowVersion);
        var product = await repository.GetAsync(selection.ProductUid, token);
        if (product is null || product.RowVersion != selection.RowVersion || !product.Facts.Selectable || !product.Facts.Matches(request))
            throw new DrugCatalogConflictException("Drug catalogue selection changed, is unavailable or does not match the form. Search and select again, or use manual entry.");
    }
    public static byte[] Version(string? value)
    {
        if (value is null || !Convert.TryFromBase64String(value, new byte[8], out var count) || count != 8)
            throw new ArgumentException("A current eight-byte catalogue version is required.");
        return Convert.FromBase64String(value);
    }
}
