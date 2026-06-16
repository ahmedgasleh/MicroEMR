using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Auth.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<AppModule> AppModules => Set<AppModule>();
        public DbSet<AppPermission> AppPermissions => Set<AppPermission>();
        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
        public DbSet<AppMenuButton> AppMenuButtons => Set<AppMenuButton>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<AppModule>()
        .HasKey(x => x.ModuleId);

            builder.Entity<AppPermission>()
                .HasKey(x => x.PermissionId);

            builder.Entity<RolePermission>()
                .HasKey(x => x.RolePermissionId);

            builder.Entity<AppMenuButton>()
                .HasKey(x => x.MenuButtonId);

            builder.Entity<AppModule>()
                .HasIndex(x => x.ModuleCode)
                .IsUnique();

            builder.Entity<AppPermission>()
                .HasIndex(x => x.PermissionCode)
                .IsUnique();

            builder.Entity<AppMenuButton>()
                .HasIndex(x => x.ButtonCode)
                .IsUnique();
        }
    }

    public class AppModule
    {
        [Key]
        public int ModuleId { get; set; }
        public string ModuleCode { get; set; } = "";
        public string ModuleName { get; set; } = "";
        public int DisplayOrder { get; set; }
    }

    public class AppPermission
    {
        [Key]
        public int PermissionId { get; set; }
        public string PermissionCode { get; set; } = "";
        public string PermissionName { get; set; } = "";
        public int ModuleId { get; set; }
    }

    public class RolePermission
    {
        [Key]
        public int RolePermissionId { get; set; }
        public string RoleId { get; set; } = "";
        public int PermissionId { get; set; }
    }

    public class AppMenuButton
    {
        [Key]
        public int MenuButtonId { get; set; }
        public string ButtonCode { get; set; } = "";
        public string ButtonText { get; set; } = "";
        public int ModuleId { get; set; }
        public string RequiredPermissionCode { get; set; } = "";
        public string? Url { get; set; }
        public string? IconCss { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
