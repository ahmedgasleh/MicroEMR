using MicroEMR.Web.Services.Patients;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Net.Http.Headers;
using MicroEMR.Web.Services.PatientAllergies;
using MicroEMR.Web.Services.PatientDocuments;
using MicroEMR.Web.Services.PatientEncounters;
using MicroEMR.Web.Services.PatientMedications;
using MicroEMR.Web.Services.PatientPrescriptions;
using MicroEMR.Web.Services.PatientProblems;
using MicroEMR.Web.Services.PatientClinicalHistory;
using MicroEMR.Web.Services.PatientVitals;
using MicroEMR.Web.Services.Scheduling;
using MicroEMR.Web.Services.PatientChartAlerts;
using MicroEMR.Web.Services.PatientResults;
using MicroEMR.Web.Services.PatientTasks;
using MicroEMR.Web.Services.PatientReferrals;
using MicroEMR.Web.Services.PatientFiles;
using MicroEMR.Web.Services.ClinicConfiguration;
using MicroEMR.Web.Authorization;
using MicroEMR.Application.Security;
using MicroEMR.Web.Services.TenantUserAdministration;
using MicroEMR.Web.Services.Reporting;
using MicroEMR.Web.Services.TemplateAdministration;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Authorization;
using MicroEMR.Infrastructure;
using MicroEMR.Web.Authentication;
using MicroEMR.Application.PlatformEntitlements;
using MicroEMR.Web.Services.SecurityAudit;
using MicroEMR.Web.Services.PatientImmunizations;
using MicroEMR.Web;

var builder = WebApplication.CreateBuilder(args);
var oidcClientSecret = builder.Configuration["Authentication:ClientSecret"];
if (string.IsNullOrWhiteSpace(oidcClientSecret))
    throw new InvalidOperationException(
        "Required OpenID Connect Web client secret is not configured.");

builder.Services.AddControllersWithViews();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IWebPermissionService, WebPermissionService>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, WebPermissionHandler>();
builder.Services.AddScoped<IWebPlatformEntitlementAccessor, WebPlatformEntitlementAccessor>();
builder.Services.AddSingleton<ISecurityAuditPagingStateProtector, SecurityAuditPagingStateProtector>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PlatformEntitlementAuthorizationHandler>();
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider, WebPermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationMiddlewareResultHandler, MissingPermissionAuthorizationResultHandler>();
builder.Services.AddMicroEmrPlatformInfrastructure();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICurrentPatientContext, CurrentPatientContext>();
builder.Services.Configure<WebTokenRefreshOptions>(
    builder.Configuration.GetSection(WebTokenRefreshOptions.SectionName));
builder.Services.AddSingleton<ISessionTokenRefreshCoordinator, SessionTokenRefreshCoordinator>();
builder.Services.AddScoped<IRefreshTokenClient, OpenIdConnectRefreshTokenClient>();
builder.Services.AddScoped<IWebSessionTokenService, WebSessionTokenService>();
builder.Services.AddTransient<WebApiBearerTokenHandler>();
builder.Services.AddHttpClient(OpenIdConnectRefreshTokenClient.HttpClientName, client =>
    client.Timeout = TimeSpan.FromSeconds(15));

static void ConfigureApiClient (
    IServiceProvider serviceProvider,
    HttpClient client )
{
    var configuration =
        serviceProvider.GetRequiredService<IConfiguration>();

    var apiBaseUrl =
        configuration ["Api:BaseUrl"]
        ?? throw new InvalidOperationException(
            "The configuration value 'Api:BaseUrl' is missing.");

    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);

    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue(
            "application/json"));
}

static IHttpClientBuilder AddApiTokenRefresh(IHttpClientBuilder client) =>
    client.AddHttpMessageHandler<WebApiBearerTokenHandler>();

AddApiTokenRefresh(builder.Services.AddHttpClient<
    IPatientApiClient,
    PatientApiClient>(ConfigureApiClient));
AddApiTokenRefresh(builder.Services.AddHttpClient<IPatientChartAlertApiClient, PatientChartAlertApiClient>(ConfigureApiClient));
AddApiTokenRefresh(builder.Services.AddHttpClient<IPatientResultApiClient, PatientResultApiClient>(ConfigureApiClient));
AddApiTokenRefresh(builder.Services.AddHttpClient<IPatientTaskApiClient, PatientTaskApiClient>(ConfigureApiClient));
AddApiTokenRefresh(builder.Services.AddHttpClient<IPatientReferralApiClient, PatientReferralApiClient>(ConfigureApiClient));
AddApiTokenRefresh(builder.Services.AddHttpClient<IPatientFileApiClient, PatientFileApiClient>(ConfigureApiClient));
AddApiTokenRefresh(builder.Services.AddHttpClient<IClinicConfigurationApiClient, ClinicConfigurationApiClient>(ConfigureApiClient));
AddApiTokenRefresh(builder.Services.AddHttpClient<ITenantUserAdministrationApiClient, TenantUserAdministrationApiClient>(ConfigureApiClient));
AddApiTokenRefresh(builder.Services.AddHttpClient<IAccessProfileApiClient, AccessProfileApiClient>(ConfigureApiClient));
AddApiTokenRefresh(builder.Services.AddHttpClient<IAppointmentStatusReportApiClient, AppointmentStatusReportApiClient>(ConfigureApiClient));
AddApiTokenRefresh(builder.Services.AddHttpClient<ITemplateAdministrationApiClient, TemplateAdministrationApiClient>(ConfigureApiClient));
AddApiTokenRefresh(builder.Services.AddHttpClient<ISecurityAuditApiClient, SecurityAuditApiClient>(ConfigureApiClient));

AddApiTokenRefresh(builder.Services.AddHttpClient<
    IPatientAllergyApiClient,
    PatientAllergyApiClient>(ConfigureApiClient));

AddApiTokenRefresh(builder.Services.AddHttpClient<
    IPatientDocumentApiClient,
    PatientDocumentApiClient>(ConfigureApiClient));

AddApiTokenRefresh(builder.Services.AddHttpClient<
    IPatientEncounterApiClient,
    PatientEncounterApiClient>(ConfigureApiClient));

AddApiTokenRefresh(builder.Services.AddHttpClient<
    IPatientMedicationApiClient,
    PatientMedicationApiClient>(ConfigureApiClient));
AddApiTokenRefresh(builder.Services.AddHttpClient<IPatientPrescriptionApiClient,PatientPrescriptionApiClient>(ConfigureApiClient));

AddApiTokenRefresh(builder.Services.AddHttpClient<
    IPatientProblemApiClient,
    PatientProblemApiClient>(ConfigureApiClient));
AddApiTokenRefresh(builder.Services.AddHttpClient<IPatientClinicalHistoryApiClient,PatientClinicalHistoryApiClient>(ConfigureApiClient));
AddApiTokenRefresh(builder.Services.AddHttpClient<IPatientImmunizationApiClient,PatientImmunizationApiClient>(ConfigureApiClient));

AddApiTokenRefresh(builder.Services.AddHttpClient<IPatientVitalApiClient, PatientVitalApiClient>(ConfigureApiClient));

AddApiTokenRefresh(builder.Services.AddHttpClient<
    ISchedulingApiClient,
    SchedulingApiClient>(ConfigureApiClient));

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "MicroEMR.Web";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = false;
        options.Events.OnValidatePrincipal = async context =>
            await context.HttpContext.RequestServices
                .GetRequiredService<IWebSessionTokenService>()
                .RefreshCookieTicketAsync(context);
        options.Events.OnRedirectToLogin = context =>
        {
            if (string.Equals(context.Request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
                || context.Request.Headers.Accept.Any(value => value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
                || context.Request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    })
    
    .AddOpenIdConnect(options =>
    {
        options.Authority =
           builder.Configuration ["Authentication:Authority"];

        options.ClientId =
            builder.Configuration ["Authentication:ClientId"];

        options.ClientSecret = oidcClientSecret;

        options.ResponseType = OpenIdConnectResponseType.Code;
        options.UsePkce = true;

        options.SaveTokens = true;
        options.UseTokenLifetime = false;
        //options.GetClaimsFromUserInfoEndpoint = true;

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("roles");
        options.Scope.Add("microemr_api");
        options.Scope.Add("offline_access");

        options.CallbackPath = "/signin-oidc";
        options.SignedOutCallbackPath = "/signout-callback-oidc";
        options.SignedOutRedirectUri = "/Account/Login";

        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.RoleClaimType = "role";
        options.Events.OnSignedOutCallbackRedirect = context =>
        {
            context.Response.Redirect("/Account/Login");
            context.HandleResponse();
            return Task.CompletedTask;
        };
        options.Events.OnRemoteFailure = async context =>
        {
            var error = context.Request.Query["error"].FirstOrDefault();
            var description =
                context.Request.Query["error_description"].FirstOrDefault();

            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(
                    context.HttpContext.RequestAborted);
                error ??= form["error"].FirstOrDefault();
                description ??= form["error_description"].FirstOrDefault();
            }

            if (string.Equals(error, "access_denied", StringComparison.Ordinal))
            {
                var reason = description switch
                {
                    "Your account is not assigned to an active clinic." =>
                        "no-active-clinic",
                    "Your account is assigned to multiple clinics and requires clinic selection." =>
                        "clinic-selection-required",
                    _ => "access-denied"
                };

                context.Response.Redirect(
                    $"/Account/AccessDenied?reason={reason}");
                context.HandleResponse();
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(ClinicConfigurationAuthorization.Policy, policy =>
        policy.RequireClaim(MicroEmrClaimTypes.TenantRole, ClinicConfigurationAuthorization.Role));
    options.AddPolicy(
        PlatformEntitlementPolicies.SecurityAuditView,
        policy => policy.RequireAuthenticatedUser().AddRequirements(
            new PlatformEntitlementRequirement(PlatformEntitlementKeys.SecurityAuditView)));
});

var app = builder.Build();

app.UseStaticFiles();

app.UseRouting();

app.UseMiddleware<SafeRequestTelemetryMiddleware>();

app.UseAuthentication();
app.UseMiddleware<WebSessionReauthenticationMiddleware>();
app.UseAuthorization();

app.MapDefaultControllerRoute();

app.Run();
