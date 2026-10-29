using System.Net.Http.Json;
using MicroEMR.Application.DrugCatalog;

namespace MicroEMR.Web.Services.PatientPrescriptions;

public sealed class DrugCatalogApiClient(HttpClient http)
{
    public async Task<DrugCatalogPage?> Search(string? query, int page, CancellationToken token) =>
        await http.GetFromJsonAsync<DrugCatalogPage>("api/drug-catalog?query=" + Uri.EscapeDataString(query ?? "") + "&page=" + page, token);
    public async Task<CatalogDrug?> Get(Guid uid, CancellationToken token) =>
        await http.GetFromJsonAsync<CatalogDrug>("api/drug-catalog/" + uid, token);
    public async Task<DrugImportResult?> Import(Stream archive, CancellationToken token)
    {
        using var body = new MultipartFormDataContent();
        body.Add(new StreamContent(archive), "archive", "dpd.zip");
        using var response = await http.PostAsync("api/drug-catalog/import", body, token);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("Import rejected. Check the archive format, size and permissions; no automatic retry was made.");
        return await response.Content.ReadFromJsonAsync<DrugImportResult>(cancellationToken: token);
    }
}
