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
using MicroEMR.Web.Services.PatientProblems;
using MicroEMR.Web.Services.PatientVitals;
using MicroEMR.Web.Services.Scheduling;
using System.Globalization;
using MicroEMR.Web.Services.EncounterSoapTemplates;
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

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICurrentPatientContext, CurrentPatientContext>();

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

builder.Services.AddHttpClient<
    IPatientApiClient,
    PatientApiClient>(ConfigureApiClient);
builder.Services.AddHttpClient<IEncounterSoapTemplateApiClient, EncounterSoapTemplateApiClient>(ConfigureApiClient);
builder.Services.AddHttpClient<IPatientChartAlertApiClient, PatientChartAlertApiClient>(ConfigureApiClient);
builder.Services.AddHttpClient<IPatientResultApiClient, PatientResultApiClient>(ConfigureApiClient);
builder.Services.AddHttpClient<IPatientTaskApiClient, PatientTaskApiClient>(ConfigureApiClient);
builder.Services.AddHttpClient<IPatientReferralApiClient, PatientReferralApiClient>(ConfigureApiClient);
builder.Services.AddHttpClient<IPatientFileApiClient, PatientFileApiClient>(ConfigureApiClient);
builder.Services.AddHttpClient<IClinicConfigurationApiClient, ClinicConfigurationApiClient>(ConfigureApiClient);
builder.Services.AddHttpClient<ITenantUserAdministrationApiClient, TenantUserAdministrationApiClient>(ConfigureApiClient);
builder.Services.AddHttpClient<IAppointmentStatusReportApiClient, AppointmentStatusReportApiClient>(ConfigureApiClient);
builder.Services.AddHttpClient<ITemplateAdministrationApiClient, TemplateAdministrationApiClient>(ConfigureApiClient);

builder.Services.AddHttpClient<
    IPatientAllergyApiClient,
    PatientAllergyApiClient>(ConfigureApiClient);

builder.Services.AddHttpClient<
    IPatientDocumentApiClient,
    PatientDocumentApiClient>(ConfigureApiClient);

builder.Services.AddHttpClient<
    IPatientEncounterApiClient,
    PatientEncounterApiClient>(ConfigureApiClient);

builder.Services.AddHttpClient<
    IPatientMedicationApiClient,
    PatientMedicationApiClient>(ConfigureApiClient);

builder.Services.AddHttpClient<
    IPatientProblemApiClient,
    PatientProblemApiClient>(ConfigureApiClient);

builder.Services.AddHttpClient<IPatientVitalApiClient, PatientVitalApiClient>(ConfigureApiClient);

builder.Services.AddHttpClient<
    ISchedulingApiClient,
    SchedulingApiClient>(ConfigureApiClient);

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "MicroEMR.Web";
        options.Events.OnValidatePrincipal = context =>
        {
            var expiresAtValue = context.Properties.GetTokenValue("expires_at");

            if (DateTimeOffset.TryParse(
                    expiresAtValue,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var expiresAt) &&
                expiresAt <= DateTimeOffset.UtcNow)
            {
                context.RejectPrincipal();
            }

            return Task.CompletedTask;
        };
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

        options.ClientSecret =
            builder.Configuration ["Authentication:ClientSecret"];

        options.ResponseType = OpenIdConnectResponseType.Code;
        options.UsePkce = true;

        options.SaveTokens = true;
        options.UseTokenLifetime = true;
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
});

var app = builder.Build();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultControllerRoute();

app.Run();
