using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using MicroEMR.Application.PatientCpp;

namespace MicroEMR.Web.Services.Patients;

public interface ICppPrintApiClient
{
    Task<CppPrintOptions> GetOptionsAsync(Guid patientUid, CancellationToken token = default);
    Task<byte[]?> PrintAsync(Guid patientUid, CppPrintRequest request, CancellationToken token = default);
}
public sealed class CppPrintApiClient(HttpClient http, IHttpContextAccessor contexts) : ICppPrintApiClient
{
    public async Task<CppPrintOptions> GetOptionsAsync(Guid patientUid, CancellationToken token = default)
    {
        using var response = await Send(HttpMethod.Get,patientUid,null,token);
        return await response.Content.ReadFromJsonAsync<CppPrintOptions>(cancellationToken:token)
            ?? throw new HttpRequestException("CPP print options returned no response.");
    }
    public async Task<byte[]?> PrintAsync(Guid patientUid, CppPrintRequest request, CancellationToken token = default)
    {
        using var response = await Send(HttpMethod.Post,patientUid,request,token);
        return response.StatusCode == HttpStatusCode.NotFound ? null : await response.Content.ReadAsByteArrayAsync(token);
    }
    private async Task<HttpResponseMessage> Send(HttpMethod method,Guid patientUid,CppPrintRequest? input,CancellationToken cancellation)
    {
        var context = contexts.HttpContext ?? throw new InvalidOperationException("An HTTP context is required.");
        var token = await context.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(token)) throw new UnauthorizedAccessException("The access token is missing.");
        using var message = new HttpRequestMessage(method,$"api/patients/{patientUid}/cpp/print"+(method == HttpMethod.Get ? "/options" : ""));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer",token);
        if (input is not null) message.Content = JsonContent.Create(input);
        var response = await http.SendAsync(message,cancellation);
        if (response.IsSuccessStatusCode || (method == HttpMethod.Post && response.StatusCode == HttpStatusCode.NotFound)) return response;
        using (response)
        {
            string detail = "CPP printing could not complete.";
            try
            {
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation));
                if (json.RootElement.TryGetProperty("detail",out var value)) detail = value.GetString() ?? detail;
            }
            catch (JsonException) { }
            throw new HttpRequestException(detail,null,response.StatusCode);
        }
    }
}
