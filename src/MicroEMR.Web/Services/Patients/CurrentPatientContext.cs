using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using MicroEMR.Application.Security;

namespace MicroEMR.Web.Services.Patients;

public interface ICurrentPatientContext
{
    Guid? GetPatientUid();
    void Remember(Guid patientUid);
    void Clear();
}

public sealed class CurrentPatientContext : ICurrentPatientContext
{
    internal const string CookieName = "MicroEMR.CurrentPatient";
    private readonly TimeSpan _maximumAge;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDataProtector _protector;
    private readonly TimeProvider _timeProvider;

    public CurrentPatientContext(
        IHttpContextAccessor httpContextAccessor,
        IDataProtectionProvider dataProtectionProvider,
        TimeProvider timeProvider,
        IConfiguration configuration)
    {
        _httpContextAccessor = httpContextAccessor;
        _protector = dataProtectionProvider.CreateProtector("MicroEMR.CurrentPatient.v1");
        _timeProvider = timeProvider;
        _maximumAge = TimeSpan.FromHours(
            Math.Clamp(configuration.GetValue("PatientChart:RememberedPatientHours", 8), 1, 24));
    }

    public Guid? GetPatientUid()
    {
        var context = _httpContextAccessor.HttpContext;
        var tenantId = GetTenantId(context);
        if (context is null || tenantId is null ||
            !context.Request.Cookies.TryGetValue(CookieName, out var protectedValue))
        {
            return null;
        }

        try
        {
            var value = JsonSerializer.Deserialize<StoredPatientContext>(_protector.Unprotect(protectedValue));
            if (value is null || value.PatientUid == Guid.Empty || value.TenantId != tenantId ||
                _timeProvider.GetUtcNow() - value.SelectedAtUtc > _maximumAge ||
                value.SelectedAtUtc > _timeProvider.GetUtcNow())
            {
                Clear();
                return null;
            }

            return value.PatientUid;
        }
        catch (Exception exception) when (exception is JsonException or System.Security.Cryptography.CryptographicException)
        {
            Clear();
            return null;
        }
    }

    public void Remember(Guid patientUid)
    {
        var context = _httpContextAccessor.HttpContext;
        var tenantId = GetTenantId(context);
        if (context is null || tenantId is null || patientUid == Guid.Empty)
        {
            return;
        }

        var value = new StoredPatientContext(tenantId.Value, patientUid, _timeProvider.GetUtcNow());
        context.Response.Cookies.Append(CookieName, _protector.Protect(JsonSerializer.Serialize(value)),
            new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                MaxAge = _maximumAge
            });
    }

    public void Clear()
    {
        _httpContextAccessor.HttpContext?.Response.Cookies.Delete(CookieName);
    }

    private static Guid? GetTenantId(HttpContext? context) =>
        Guid.TryParse(context?.User.FindFirst(MicroEmrClaimTypes.TenantId)?.Value, out var tenantId)
            ? tenantId
            : null;

    private sealed record StoredPatientContext(Guid TenantId, Guid PatientUid, DateTimeOffset SelectedAtUtc);
}
