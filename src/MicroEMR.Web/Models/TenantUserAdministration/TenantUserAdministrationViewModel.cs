namespace MicroEMR.Web.Models.TenantUserAdministration;

using System.ComponentModel.DataAnnotations;

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
    public Guid? AccessProfileUid { get; set; }
    public string? AccessProfileName { get; set; }
}

public sealed class TenantUserAdministrationViewModel
{
    public IReadOnlyList<TenantUserAdministrationItemViewModel> Users { get; init; } = [];
    public IReadOnlyCollection<string> CanonicalRoles { get; init; } = [];
}

public sealed class TenantUserDetailsViewModel
{
    public required TenantUserAdministrationItemViewModel User { get; init; }
    public IReadOnlyList<MicroEMR.Application.AccessProfiles.AccessProfileSummary> AccessProfiles { get; init; } = [];
}

public sealed class AddTenantUserViewModel
{
    [Required, StringLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string LastName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
    [Required] public string InitialRole { get; set; } = string.Empty;
    public bool ProvisionClinicalUser { get; set; } = true;
    public IReadOnlyCollection<string> CanonicalRoles { get; init; } = [];
}

public sealed class AddTenantUserResultViewModel
{
    public required TenantUserAdministrationItemViewModel User { get; init; }
    public bool AuthIdentityCreated { get; init; }
    public bool ClinicalProvisioningFailed { get; init; }
    public string Message { get; init; } = string.Empty;
}
