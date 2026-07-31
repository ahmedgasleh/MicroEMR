using Microsoft.AspNetCore.Authentication.JwtBearer;
using MicroEMR.Application;
using MicroEMR.Infrastructure;
using Microsoft.OpenApi;
using Microsoft.AspNetCore.Authorization;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.Middleware;
using MicroEMR.Application.Tenancy;
using MicroEMR.Api.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
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
            new TenantRoleRequirement("ClinicAdministrator")));
    options.AddPolicy(TenantAuthorizationPolicies.SchedulingStatusManager,
        policy => policy.AddRequirements(new AnyTenantRoleRequirement(
            "Scheduler", "MedicalAssistant", "Nurse", "ClinicAdministrator")));
    options.AddPolicy(TenantAuthorizationPolicies.EncounterStarter,
        policy => policy.AddRequirements(new AnyTenantRoleRequirement(
            "Physician", "Nurse", "ClinicAdministrator")));
});
builder.Services.AddSingleton<IAuthorizationHandler, TenantRoleAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, AnyTenantRoleAuthorizationHandler>();

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

app.MapHealthChecks("/health/platform");
app.MapControllers();

app.Run();
