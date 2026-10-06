using System.Net;
using System.Net.Http.Json;
using MicroEMR.Application.PatientReferrals;

namespace MicroEMR.Web.Services.PatientReferrals;

public sealed partial class PatientReferralApiClient
{
    public async Task<ReferralClinicalOptionsResponse> GetClinicalOptionsAsync(Guid patientUid,CancellationToken token=default)
    {
        using var request=await CreateRequestAsync(HttpMethod.Get,$"api/patients/{patientUid}/referrals/clinical-options");
        using var response=await client.SendAsync(request,token); await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<ReferralClinicalOptionsResponse>(cancellationToken:token)
            ?? throw new HttpRequestException("Clinical choices could not be loaded.");
    }
    public async Task<PatientReferralClinicalSelectionsResponse?> GetClinicalSelectionsAsync(Guid patientUid,Guid referralUid,CancellationToken token=default)
    {
        using var request=await CreateRequestAsync(HttpMethod.Get,$"api/patients/{patientUid}/referrals/{referralUid}/clinical-selections");
        using var response=await client.SendAsync(request,token); if(response.StatusCode==HttpStatusCode.NotFound)return null;
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<PatientReferralClinicalSelectionsResponse>(cancellationToken:token);
    }
    public async Task<PatientReferralClinicalSelectionsResponse?> ReplaceClinicalSelectionsAsync(Guid patientUid,Guid referralUid,ReplacePatientReferralClinicalSelectionsRequest selections,CancellationToken token=default)
    {
        using var request=await CreateRequestAsync(HttpMethod.Put,$"api/patients/{patientUid}/referrals/{referralUid}/clinical-selections");
        request.Content=JsonContent.Create(selections);
        using var response=await client.SendAsync(request,token); if(response.StatusCode==HttpStatusCode.NotFound)return null;
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<PatientReferralClinicalSelectionsResponse>(cancellationToken:token);
    }
}
