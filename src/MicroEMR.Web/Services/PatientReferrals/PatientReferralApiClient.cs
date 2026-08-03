using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using MicroEMR.Web.Models.PatientReferrals;

namespace MicroEMR.Web.Services.PatientReferrals;

public interface IPatientReferralApiClient
{
    Task<IReadOnlyList<PatientReferralListItemViewModel>> GetByPatientUidAsync(
        Guid patientUid,
        CancellationToken cancellationToken = default);

    Task<PatientReferralDetailsViewModel?> GetByUidAsync(
        Guid patientUid,
        Guid referralUid,
        CancellationToken cancellationToken = default);

    Task<PatientReferralDetailsViewModel?> CreateAsync(
        Guid patientUid,
        CreatePatientReferralViewModel request,
        CancellationToken cancellationToken = default);
}

public sealed class PatientReferralApiClient(
    HttpClient client,
    IHttpContextAccessor httpContextAccessor) : IPatientReferralApiClient
{
    public async Task<IReadOnlyList<PatientReferralListItemViewModel>> GetByPatientUidAsync(
        Guid patientUid,
        CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(
            HttpMethod.Get,
            $"api/patients/{patientUid}/referrals");
        using var response = await client.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<PatientReferralListItemViewModel>>(
            cancellationToken: cancellationToken) ?? [];
    }

    public async Task<PatientReferralDetailsViewModel?> GetByUidAsync(
        Guid patientUid,
        Guid referralUid,
        CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(
            HttpMethod.Get,
            $"api/patients/{patientUid}/referrals/{referralUid}");
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<PatientReferralDetailsViewModel>(
            cancellationToken: cancellationToken);
    }

    public async Task<PatientReferralDetailsViewModel?> CreateAsync(
        Guid patientUid,
        CreatePatientReferralViewModel request,
        CancellationToken cancellationToken = default)
    {
        using var message = await CreateRequestAsync(
            HttpMethod.Post,
            $"api/patients/{patientUid}/referrals");
        message.Content = JsonContent.Create(new
        {
            request.RecipientName,
            request.RecipientOrganization,
            request.RecipientPhone,
            request.RecipientFax,
            request.Reason,
            request.ClinicalSummary
        });

        using var response = await client.SendAsync(message, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<PatientReferralDetailsViewModel>(
            cancellationToken: cancellationToken);
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string uri)
    {
        var request = new HttpRequestMessage(method, uri);
        var context = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("The authenticated request context is unavailable.");
        var token = await context.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("The API access token is unavailable.");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        var message = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => "Please correct the referral information.",
            HttpStatusCode.Forbidden => "Your account is not provisioned for clinical changes in this clinic.",
            HttpStatusCode.Unauthorized => "Your session is no longer authorized.",
            HttpStatusCode.NotFound => "The patient or referral was not found.",
            _ => "The referral operation could not be completed."
        };

        await response.Content.LoadIntoBufferAsync();
        throw new HttpRequestException(message, null, response.StatusCode);
    }
}
