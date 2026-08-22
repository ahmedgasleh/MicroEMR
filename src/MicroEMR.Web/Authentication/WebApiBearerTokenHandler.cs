using System.Net.Http.Headers;

namespace MicroEMR.Web.Authentication;

public sealed class WebApiBearerTokenHandler(
    IHttpContextAccessor contextAccessor,
    IWebSessionTokenService tokenService) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var context = contextAccessor.HttpContext
            ?? throw new InvalidOperationException("The authenticated request context is unavailable.");
        var accessToken = await tokenService.GetValidAccessTokenAsync(context, cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await base.SendAsync(request, cancellationToken);
    }
}
