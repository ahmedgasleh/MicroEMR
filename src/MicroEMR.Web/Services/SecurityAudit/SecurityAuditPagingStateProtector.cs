using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using MicroEMR.Web.Models.SecurityAudit;

namespace MicroEMR.Web.Services.SecurityAudit;

public interface ISecurityAuditPagingStateProtector
{
    string Protect(SecurityAuditSearchForm state);
    bool TryUnprotect(string token, out SecurityAuditSearchForm state);
}

public sealed class SecurityAuditPagingStateProtector(IDataProtectionProvider provider)
    : ISecurityAuditPagingStateProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector(
        "MicroEMR.SecurityAuditReview.WebPagingState.v1");

    public string Protect(SecurityAuditSearchForm state) =>
        _protector.Protect(JsonSerializer.Serialize(state));

    public bool TryUnprotect(string token, out SecurityAuditSearchForm state)
    {
        try
        {
            state = JsonSerializer.Deserialize<SecurityAuditSearchForm>(_protector.Unprotect(token))
                ?? throw new JsonException();
            return true;
        }
        catch (Exception exception) when (exception is System.Security.Cryptography.CryptographicException
                                          or JsonException or FormatException)
        {
            state = default!;
            return false;
        }
    }
}
