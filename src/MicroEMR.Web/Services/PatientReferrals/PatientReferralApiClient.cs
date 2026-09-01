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
    Task<PatientReferralDetailsViewModel?> UpdateDraftAsync(Guid patientUid,Guid referralUid,
        UpdatePatientReferralDraftViewModel request,CancellationToken cancellationToken=default) => throw new NotSupportedException();
    Task<IReadOnlyList<ReferralProviderViewModel>> GetProvidersAsync(CancellationToken cancellationToken=default) => Task.FromResult<IReadOnlyList<ReferralProviderViewModel>>([]);
    Task<byte[]?> GetLetterAsync(Guid patientUid,Guid referralUid,bool preview,CancellationToken cancellationToken=default) => Task.FromResult<byte[]?>(null);

    Task<PatientReferralDetailsViewModel?> MarkSentAsync(Guid patientUid, Guid referralUid,
        string rowVersion, CancellationToken cancellationToken = default);
    Task<PatientReferralDetailsViewModel?> MarkResponseReceivedAsync(Guid patientUid, Guid referralUid,
        string rowVersion, CancellationToken cancellationToken = default);
    Task<PatientReferralDetailsViewModel?> CloseAsync(Guid patientUid, Guid referralUid,
        string rowVersion, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReferralSupportingDocumentViewModel>> GetLinkedDocumentsAsync(
        Guid patientUid, Guid referralUid, CancellationToken cancellationToken = default);
    Task LinkDocumentAsync(Guid patientUid, Guid referralUid, Guid documentUid,
        string rowVersion, CancellationToken cancellationToken = default);
    Task UnlinkDocumentAsync(Guid patientUid, Guid referralUid, Guid documentUid,
        string rowVersion, CancellationToken cancellationToken = default);
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
            request.ReferringProviderUid,
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

    public async Task<PatientReferralDetailsViewModel?> UpdateDraftAsync(Guid patientUid,Guid referralUid,
        UpdatePatientReferralDraftViewModel request,CancellationToken cancellationToken=default)
    {
        using var message=await CreateRequestAsync(HttpMethod.Put,$"api/patients/{patientUid}/referrals/{referralUid}");
        message.Content=JsonContent.Create(new{request.ReferringProviderUid,request.RecipientName,request.RecipientOrganization,
            request.RecipientPhone,request.RecipientFax,request.Reason,request.ClinicalSummary,request.RowVersion});
        using var response=await client.SendAsync(message,cancellationToken);if(response.StatusCode==HttpStatusCode.NotFound)return null;
        await EnsureSuccessAsync(response);return await response.Content.ReadFromJsonAsync<PatientReferralDetailsViewModel>(cancellationToken:cancellationToken);
    }

    public async Task<IReadOnlyList<ReferralProviderViewModel>> GetProvidersAsync(CancellationToken cancellationToken=default)
    {
        using var request=await CreateRequestAsync(HttpMethod.Get,"api/patients/00000000-0000-0000-0000-000000000000/referrals/providers");
        using var response=await client.SendAsync(request,cancellationToken);await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<ReferralProviderViewModel>>(cancellationToken:cancellationToken)??[];
    }

    public async Task<byte[]?> GetLetterAsync(Guid patientUid,Guid referralUid,bool preview,CancellationToken cancellationToken=default)
    {
        var suffix=preview?"/letter/preview":"/letter";
        using var request=await CreateRequestAsync(HttpMethod.Get,$"api/patients/{patientUid}/referrals/{referralUid}{suffix}");
        using var response=await client.SendAsync(request,cancellationToken);if(response.StatusCode==HttpStatusCode.NotFound)return null;
        await EnsureSuccessAsync(response);return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public Task<PatientReferralDetailsViewModel?> MarkSentAsync(
        Guid patientUid, Guid referralUid, string rowVersion,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(patientUid, referralUid, "send", rowVersion, cancellationToken);

    public Task<PatientReferralDetailsViewModel?> MarkResponseReceivedAsync(
        Guid patientUid, Guid referralUid, string rowVersion,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(patientUid, referralUid, "response-received", rowVersion, cancellationToken);

    public Task<PatientReferralDetailsViewModel?> CloseAsync(
        Guid patientUid, Guid referralUid, string rowVersion,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(patientUid, referralUid, "close", rowVersion, cancellationToken);

    public async Task<IReadOnlyList<ReferralSupportingDocumentViewModel>> GetLinkedDocumentsAsync(
        Guid patientUid, Guid referralUid, CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get,
            $"api/patients/{patientUid}/referrals/{referralUid}/documents");
        using var response = await client.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<ReferralSupportingDocumentViewModel>>(
            cancellationToken: cancellationToken) ?? [];
    }

    public Task LinkDocumentAsync(Guid patientUid, Guid referralUid, Guid documentUid,
        string rowVersion, CancellationToken cancellationToken = default) =>
        MutateDocumentAsync(HttpMethod.Post, patientUid, referralUid, documentUid, rowVersion, cancellationToken);

    public Task UnlinkDocumentAsync(Guid patientUid, Guid referralUid, Guid documentUid,
        string rowVersion, CancellationToken cancellationToken = default) =>
        MutateDocumentAsync(HttpMethod.Delete, patientUid, referralUid, documentUid, rowVersion, cancellationToken);

    private async Task MutateDocumentAsync(HttpMethod method, Guid patientUid, Guid referralUid,
        Guid documentUid, string rowVersion, CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(method,
            $"api/patients/{patientUid}/referrals/{referralUid}/documents/{documentUid}");
        request.Content = JsonContent.Create(new { rowVersion });
        using var response = await client.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
    }

    private async Task<PatientReferralDetailsViewModel?> TransitionAsync(
        Guid patientUid, Guid referralUid, string action, string rowVersion,
        CancellationToken cancellationToken)
    {
        using var message = await CreateRequestAsync(HttpMethod.Post,
            $"api/patients/{patientUid}/referrals/{referralUid}/{action}");
        message.Content = JsonContent.Create(new { rowVersion });
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
            HttpStatusCode.Conflict => "The referral changed or is no longer in the expected status. Refresh and try again.",
            _ => "The referral operation could not be completed."
        };

        await response.Content.LoadIntoBufferAsync();
        throw new HttpRequestException(message, null, response.StatusCode);
    }
}
