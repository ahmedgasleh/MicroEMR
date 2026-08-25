using MicroEMR.Auth.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using MicroEMR.Auth.Services.Tenancy;
using MicroEMR.Auth.Services.SecurityAudit;
using MicroEMR.Infrastructure;
using MicroEMR.Auth.Services.PlatformEntitlements;
using MicroEMR.Auth;


var builder = WebApplication.CreateBuilder(args);
var authServerConnection = builder.Configuration.GetConnectionString("AuthServerConnection");
if (string.IsNullOrWhiteSpace(authServerConnection))
    throw new InvalidOperationException(
        "Required connection string 'ConnectionStrings:AuthServerConnection' is not configured.");

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddMicroEmrPlatformInfrastructure();
builder.Services.AddScoped<
    IUserTenantMembershipService,
    UserTenantMembershipService>();
builder.Services.AddScoped<IUserTenantResolver, UserTenantResolver>();
builder.Services.AddScoped<ITenantClaimEnricher, TenantClaimEnricher>();
builder.Services.AddScoped<TenantSelectionSecurityAuditRecorder>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSingleton<IPendingTenantSelectionStore, DistributedPendingTenantSelectionStore>();
builder.Services.AddScoped<IPlatformEntitlementClaimService, PlatformEntitlementClaimService>();
builder.Services.AddScoped<IPlatformRefreshAuthorizationService, PlatformRefreshAuthorizationService>();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(
        authServerConnection);

    options.UseOpenIddict();
});

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;

        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
            .UseDbContext<ApplicationDbContext>();
    })
   .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("/connect/authorize");
        options.SetTokenEndpointUris("/connect/token");
        options.SetEndSessionEndpointUris("/connect/logout");
        options.SetUserInfoEndpointUris("/connect/userinfo");

        options.AllowAuthorizationCodeFlow();
        options.AllowRefreshTokenFlow();

        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(5));
        options.SetRefreshTokenLifetime(TimeSpan.FromDays(14));

        options.RequireProofKeyForCodeExchange();
        options.DisableAccessTokenEncryption();

        options.RegisterScopes(
            OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.Email,
            OpenIddictConstants.Scopes.Roles,
            OpenIddictConstants.Scopes.OfflineAccess,
            "microemr_api");

        options.AddDevelopmentEncryptionCertificate();
        options.AddDevelopmentSigningCertificate();

        options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            .EnableEndSessionEndpointPassthrough()
            .EnableUserInfoEndpointPassthrough();
    });
builder.Services.AddHostedService<SeedData>();

var app = builder.Build();



// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseMiddleware<SafeRequestTelemetryMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
