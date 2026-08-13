namespace MicroEMR.Web.Models.TenantUserAdministration;

public sealed class TenantUserAdministrationItemViewModel
{
    public string AuthUserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool AuthUserActive { get; set; }
    public string MembershipStatus { get; set; } = string.Empty;
    public IReadOnlyCollection<string> TenantRoles { get; set; } = [];
    public bool ClinicalUserProvisioned { get; set; }
    public long? ClinicalUserId { get; set; }
    public bool? ClinicalUserActive { get; set; }
    public DateTimeOffset? MembershipUpdatedAt { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public bool IsCurrentUser { get; set; }
}

public sealed class TenantUserAdministrationViewModel
{
    public IReadOnlyList<TenantUserAdministrationItemViewModel> Users { get; init; } = [];
    public IReadOnlyCollection<string> CanonicalRoles { get; init; } = [];
}

public sealed class TenantUserDetailsViewModel
{
    public required TenantUserAdministrationItemViewModel User { get; init; }
}
