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
});
builder.Services.AddSingleton<IAuthorizationHandler, TenantRoleAuthorizationHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IAuthenticatedClinicalUserAccessor, AuthenticatedClinicalUserAccessor>();
builder.Services.AddScoped<IAuthenticatedSubjectAccessor, AuthenticatedSubjectAccessor>();

builder.Services.AddScoped<ITenantContextAccessor, TenantContextAccessor>();
builder.Services.AddScoped<ITenantContext>(serviceProvider =>
    serviceProvider.GetRequiredService<ITenantContextAccessor>().Current
    ?? throw new InvalidOperationException(
        "Tenant context has not been established for the current operation."));

builder.Services.AddMicroEmrApplication();
builder.Services.AddMicroEmrInfrastructure();

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
