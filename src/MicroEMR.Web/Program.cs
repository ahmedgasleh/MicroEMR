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
using MicroEMR.Web.Services.Scheduling;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddHttpContextAccessor();

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
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultControllerRoute();

app.Run();
