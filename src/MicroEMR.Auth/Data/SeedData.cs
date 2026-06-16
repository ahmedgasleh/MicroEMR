using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace MicroEMR.Auth.Data;

public class SeedData : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public SeedData ( IServiceProvider serviceProvider )
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync ( CancellationToken cancellationToken )
    {
        using var scope = _serviceProvider.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var appManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();

        await db.Database.MigrateAsync(cancellationToken);

        await SeedRoles(roleManager);
        await SeedAdminUser(userManager);
        await SeedModulesPermissions(db);
        await SeedRolePermissions(db, roleManager);
        await SeedOpenIddict(appManager, scopeManager, cancellationToken);
    }

    public Task StopAsync ( CancellationToken cancellationToken ) => Task.CompletedTask;

    private static async Task SeedRoles ( RoleManager<IdentityRole> roleManager )
    {
        string [] roles =
        {
            "SystemAdmin",
            "ClinicAdmin",
            "Physician",
            "Nurse",
            "MedicalAssistant",
            "Reception"
        };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    private static async Task SeedAdminUser ( UserManager<ApplicationUser> userManager )
    {
        var email = "admin@microemr.local";

        var user = await userManager.FindByEmailAsync(email);

        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = "System Administrator",
                IsActive = true
            };

            await userManager.CreateAsync(user, "Admin123!");
            await userManager.AddToRoleAsync(user, "SystemAdmin");
        }
    }

    private static async Task SeedModulesPermissions ( ApplicationDbContext db )
    {
        if (!await db.AppModules.AnyAsync())
        {
            db.AppModules.AddRange(
                new AppModule { ModuleCode = "SCHEDULING", ModuleName = "Scheduling", DisplayOrder = 1 },
                new AppModule { ModuleCode = "PATIENT_CHART", ModuleName = "Patient Chart", DisplayOrder = 2 },
                new AppModule { ModuleCode = "ADMIN", ModuleName = "Administration", DisplayOrder = 3 }
            );

            await db.SaveChangesAsync();
        }

        var schedulingId = await db.AppModules
            .Where(x => x.ModuleCode == "SCHEDULING")
            .Select(x => x.ModuleId)
            .FirstAsync();

        var chartId = await db.AppModules
            .Where(x => x.ModuleCode == "PATIENT_CHART")
            .Select(x => x.ModuleId)
            .FirstAsync();

        var adminId = await db.AppModules
            .Where(x => x.ModuleCode == "ADMIN")
            .Select(x => x.ModuleId)
            .FirstAsync();

        if (!await db.AppPermissions.AnyAsync())
        {
            db.AppPermissions.AddRange(
                new AppPermission { PermissionCode = "SCHEDULING_VIEW", PermissionName = "View Scheduling", ModuleId = schedulingId },
                new AppPermission { PermissionCode = "SCHEDULING_BOOK", PermissionName = "Book Appointment", ModuleId = schedulingId },
                new AppPermission { PermissionCode = "CHART_VIEW", PermissionName = "View Patient Chart", ModuleId = chartId },
                new AppPermission { PermissionCode = "CHART_CREATE_DOC", PermissionName = "Create Chart Document", ModuleId = chartId },
                new AppPermission { PermissionCode = "ADMIN_USERS", PermissionName = "Manage Users", ModuleId = adminId }
            );

            await db.SaveChangesAsync();
        }

        if (!await db.AppMenuButtons.AnyAsync())
        {
            db.AppMenuButtons.AddRange(
                new AppMenuButton
                {
                    ButtonCode = "BTN_SCHEDULE",
                    ButtonText = "Scheduling",
                    ModuleId = schedulingId,
                    RequiredPermissionCode = "SCHEDULING_VIEW",
                    Url = "/scheduling",
                    IconCss = "bi bi-calendar",
                    DisplayOrder = 1
                },
                new AppMenuButton
                {
                    ButtonCode = "BTN_CHART",
                    ButtonText = "Patient Chart",
                    ModuleId = chartId,
                    RequiredPermissionCode = "CHART_VIEW",
                    Url = "/chart",
                    IconCss = "bi bi-folder2-open",
                    DisplayOrder = 2
                },
                new AppMenuButton
                {
                    ButtonCode = "BTN_ADMIN",
                    ButtonText = "Admin",
                    ModuleId = adminId,
                    RequiredPermissionCode = "ADMIN_USERS",
                    Url = "/admin",
                    IconCss = "bi bi-gear",
                    DisplayOrder = 3
                }
            );

            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedRolePermissions (
        ApplicationDbContext db,
        RoleManager<IdentityRole> roleManager )
    {
        var adminRole = await roleManager.FindByNameAsync("SystemAdmin");

        if (adminRole == null)
            return;

        var allPermissions = await db.AppPermissions.ToListAsync();

        foreach (var permission in allPermissions)
        {
            bool exists = await db.RolePermissions.AnyAsync(x =>
                x.RoleId == adminRole.Id &&
                x.PermissionId == permission.PermissionId);

            if (!exists)
            {
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId = adminRole.Id,
                    PermissionId = permission.PermissionId
                });
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedOpenIddict (
        IOpenIddictApplicationManager appManager,
        IOpenIddictScopeManager scopeManager,
        CancellationToken cancellationToken )
    {
        if (await scopeManager.FindByNameAsync("microemr_api", cancellationToken) == null)
        {
            await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = "microemr_api",
                DisplayName = "Micro EMR API",
                Resources =
                {
                    "microemr_api"
                }
            }, cancellationToken);
        }

        if (await appManager.FindByClientIdAsync("microemr_web", cancellationToken) == null)
        {
            await appManager.CreateAsync(new OpenIddictApplicationDescriptor
                {
                    ClientId = "microemr_web",
                    DisplayName = "Micro EMR Web Client",
                    ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
                    ClientSecret = "change-this-secret",

                    RedirectUris =
                    {
                        new Uri("https://localhost:7002/signin-oidc")
                    },

                    PostLogoutRedirectUris =
                    {
                        new Uri("https://localhost:7002/signout-callback-oidc")
                    },

                    Permissions =
                    {
                        OpenIddictConstants.Permissions.Endpoints.Authorization,
                        OpenIddictConstants.Permissions.Endpoints.Token,
                        OpenIddictConstants.Permissions.Endpoints.EndSession,

                        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                        OpenIddictConstants.Permissions.GrantTypes.RefreshToken,

                        OpenIddictConstants.Permissions.ResponseTypes.Code,

                        OpenIddictConstants.Permissions.Scopes.Profile,
                        OpenIddictConstants.Permissions.Scopes.Email,
                        OpenIddictConstants.Permissions.Scopes.Roles,
                        OpenIddictConstants.Permissions.Prefixes.Scope + "microemr_api"
                    },

                    Requirements =
                    {
                        OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange
                    }
                }, cancellationToken);
            }
    }
}