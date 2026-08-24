using Microsoft.AspNetCore.Authentication.JwtBearer;
using MicroEMR.Application;
using MicroEMR.Infrastructure;
using Microsoft.OpenApi;
using Microsoft.AspNetCore.Authorization;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.Middleware;
using MicroEMR.Application.Tenancy;
using MicroEMR.Api.HealthChecks;
using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PatientFiles;
using Microsoft.AspNetCore.Http.Features;
using MicroEMR.Application.TenantUserAdministration;
using MicroEMR.Application.PlatformAdministration;
using MicroEMR.Api.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization.Policy;
using MicroEMR.Application.PlatformEntitlements;
using MicroEMR.Application.SecurityAudit;
using MicroEMR.Api.SecurityAudit;
using MicroEMR.Api;
using MicroEMR.Application.ClinicalDataMigration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOptions<PatientFileUploadOptions>()
    .Bind(builder.Configuration.GetSection("PatientFileUpload"))
    .Validate(x => x.MaxFileSizeBytes > 0 && x.MaxFileSizeBytes <= 26_214_400,
        "Patient file size must be between 1 byte and 25 MB.")
    .ValidateOnStart();
builder.Services.Configure<FormOptions>(x => x.MultipartBodyLengthLimit = 27_262_976);
builder.Services.AddHealthChecks()
    .AddCheck<PlatformDatabaseHealthCheck>("platform_database");

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "MicroEMR API",
            Version = "v1",
            Description = "API for MicroEMR patients, scheduling and charts."
        });
});

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.Authority =
            builder.Configuration ["Authentication:Authority"];

        options.Audience =
            builder.Configuration ["Authentication:Audience"];

        options.RequireHttpsMetadata = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        TenantAuthorizationPolicies.ClinicAdministrator,
        policy => policy.AddRequirements(
            new TenantRoleRequirement(TenantRoleCatalog.ClinicAdministrator)));
    options.AddPolicy(
        PlatformEntitlementPolicies.SecurityAuditView,
        policy => policy.RequireAuthenticatedUser().AddRequirements(
            new PlatformEntitlementRequirement(PlatformEntitlementKeys.SecurityAuditView)));
});
builder.Services.AddSingleton<IAuthorizationHandler, TenantRoleAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, PlatformEntitlementAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationMiddlewareResultHandler, MissingPermissionAuthorizationResultHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IAuthenticatedClinicalUserAccessor, AuthenticatedClinicalUserAccessor>();
builder.Services.AddScoped<IAuthenticatedSubjectAccessor, AuthenticatedSubjectAccessor>();
builder.Services.AddSingleton<ISecurityAuditContinuationTokenProtector, SecurityAuditContinuationTokenProtector>();

builder.Services.AddScoped<ITenantContextAccessor, TenantContextAccessor>();
builder.Services.AddScoped<ITenantContext, DeferredTenantContext>();

builder.Services.AddMicroEmrApplication();
builder.Services.AddOptions<ClinicalDataMigrationOptions>()
    .Bind(builder.Configuration.GetSection("ClinicalDataMigration"))
    .Validate(x => x.MaxPatients is > 0 and <= 10_000 && x.MaxProblems is > 0 and <= 50_000,
        "Clinical data migration validation limits are outside the supported range.")
    .ValidateOnStart();
builder.Services.AddMicroEmrInfrastructure();
builder.Services.AddDbContext<AdministrationIdentityDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AuthDatabase")
        ?? builder.Configuration.GetConnectionString("AuthServerConnection")));
builder.Services.AddIdentityCore<AdministrationIdentityUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddEntityFrameworkStores<AdministrationIdentityDbContext>()
    .AddDefaultTokenProviders();
builder.Services.AddScoped<IIdentityUserAdministration, IdentityUserAdministration>();

var app = builder.Build();

app.UseSwagger();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint(
        "/swagger/v1/swagger.json",
        "MicroEMR API v1");

    options.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseMiddleware<TenantDatabaseExceptionMiddleware>();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.UseMiddleware<ClinicalUserActorResolutionMiddleware>();

app.MapHealthChecks("/health/platform");
app.MapControllers();

app.Run();
