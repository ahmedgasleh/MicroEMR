using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.Tenancy;

namespace MicroEMR.Application.ClinicConfiguration;

public sealed record ClinicLogo(byte[] PngBytes, int Width, int Height);
public sealed record ClinicLogoRecord(ClinicLogo? Logo, string RowVersion);
public sealed record ClinicLogoState(bool HasLogo, string? RowVersion);

public interface IClinicLogoProcessor
{
    Task<ClinicLogo> ProcessAsync(byte[] content, CancellationToken token = default);
}

public interface IClinicLogoRepository
{
    Task<ClinicLogoRecord?> GetAsync(CancellationToken token = default);
    Task<ClinicLogoRecord> SaveAsync(ClinicLogo? logo, string? rowVersion, long actor, CancellationToken token = default);
}

public sealed class ClinicBrandingService(IClinicLogoRepository repository, IClinicLogoProcessor processor,
    IClinicConfigurationService configuration, ICurrentUserPermissionService permissions,
    IAuthenticatedClinicalUserAccessor actor, ITenantContext tenant)
{
    public const int MaximumLogoBytes = 1024 * 1024;
    public const int MaximumLogoDimension = 2048;

    public async Task<ClinicLogoState> GetStateAsync(CancellationToken token = default)
    {
        await RequireAsync(true, token);
        var current = await repository.GetAsync(token);
        return new(current?.Logo is not null, current?.RowVersion);
    }

    public async Task<ClinicLogo?> GetLogoAsync(CancellationToken token = default)
    {
        await RequireAsync(false, token);
        return (await repository.GetAsync(token))?.Logo;
    }

    public async Task<ClinicLogoState> SaveLogoAsync(byte[]? content, bool remove, string? rowVersion, CancellationToken token = default)
    {
        await RequireAsync(true, token);
        if (remove && content is not null) throw new ArgumentException("Choose either upload or removal.");
        if (!remove && (content is null || content.Length == 0 || content.Length > MaximumLogoBytes))
            throw new ArgumentException("Choose a PNG or JPEG logo up to 1 MiB.");
        if (rowVersion is not null && (Convert.FromBase64String(rowVersion).Length != 8))
            throw new FormatException("Invalid branding version.");
        var userId = await actor.GetRequiredUserIdAsync(token);
        var logo = remove ? null : await processor.ProcessAsync(content!, token);
        var saved = await repository.SaveAsync(logo, rowVersion, userId, token);
        return new(saved.Logo is not null, saved.RowVersion);
    }

    public async Task<ClinicPrintHeader> GetPrintHeaderAsync(string title, CancellationToken token = default)
    {
        await RequireAsync(false, token);
        var clinic = await configuration.GetAsync(token);
        return new(new(string.IsNullOrWhiteSpace(clinic.LegalName) ? clinic.ClinicName : clinic.LegalName,
            clinic.AddressLine1, clinic.AddressLine2, clinic.City, clinic.ProvinceState, clinic.PostalCode,
            clinic.Phone, clinic.Fax, clinic.Email), title, (await repository.GetAsync(token))?.Logo, clinic.Country);
    }

    private async Task RequireAsync(bool manage, CancellationToken token)
    {
        if (tenant.TenantUid == Guid.Empty) throw new UnauthorizedAccessException();
        var effective = await permissions.GetEffectivePermissionsAsync(token);
        if (!effective.Contains(PermissionKeys.ClinicSettingsManage) && (manage || !effective.Contains(PermissionKeys.PatientsView)))
            throw new UnauthorizedAccessException();
    }
}
