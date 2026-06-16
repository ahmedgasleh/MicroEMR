using Microsoft.AspNetCore.Identity;

namespace MicroEMR.Auth.Data
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }
        public bool IsActive { get; set; } = true;

        public int? ClinicId { get; set; }
    }
}
