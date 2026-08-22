using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using MicroEMR.Application.SecurityAudit;

namespace MicroEMR.Api.SecurityAudit;

public sealed class SecurityAuditContinuationTokenProtector(IDataProtectionProvider provider)
    : ISecurityAuditContinuationTokenProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector(
        "MicroEMR.SecurityAuditReview.Continuation.v1");

    public string Protect(SecurityAuditContinuation continuation) =>
        _protector.Protect(JsonSerializer.Serialize(continuation));

    public bool TryUnprotect(string token, out SecurityAuditContinuation continuation)
    {
        try
        {
            continuation = JsonSerializer.Deserialize<SecurityAuditContinuation>(_protector.Unprotect(token))
                ?? throw new JsonException();
            return true;
        }
        catch (Exception exception) when (exception is System.Security.Cryptography.CryptographicException
                                          or JsonException or FormatException)
        {
            continuation = default!;
            return false;
        }
    }
}
