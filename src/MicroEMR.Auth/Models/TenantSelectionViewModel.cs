using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Auth.Models;

public sealed class TenantSelectionViewModel
{
    [Required]
    public string SelectionId { get; init; } = string.Empty;

    public IReadOnlyList<TenantSelectionOptionViewModel> Tenants { get; init; } = [];

    [Required(ErrorMessage = "Choose a clinic to continue.")]
    public Guid? SelectedTenantUid { get; set; }
}

public sealed record TenantSelectionOptionViewModel(Guid TenantUid, string TenantKey, string DisplayName);
