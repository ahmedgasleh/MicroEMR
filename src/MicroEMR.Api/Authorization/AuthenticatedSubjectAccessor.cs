using System.Security.Claims;
using MicroEMR.Application.TenantUserAdministration;

namespace MicroEMR.Api.Authorization;

public sealed class AuthenticatedSubjectAccessor(IHttpContextAccessor contextAccessor)
    : IAuthenticatedSubjectAccessor
{
    public string GetRequiredSubject()
    {
        var user = contextAccessor.HttpContext?.User
            ?? throw new UnauthorizedAccessException("The authenticated request context is unavailable.");
        var subject = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(subject)
            ? throw new UnauthorizedAccessException("The authenticated subject is unavailable.")
            : subject;
    }
}
