using Microsoft.AspNetCore.Authentication.JwtBearer;
using MicroEMR.Api.Data.Patients;
using MicroEMR.Api.Services.Patients;
using Microsoft.OpenApi;
using MicroEMR.Api.Models.PatientDocuments;
using MicroEMR.Api.Models.PatientEncounters;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

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

builder.Services.AddAuthorization();

builder.Services.AddScoped<
    IPatientRepository,
    PatientRepository>();

builder.Services.AddScoped<
    IPatientService,
    PatientService>();

builder.Services.AddScoped<
    IPatientDocumentRepository,
    PatientDocumentRepository>();

builder.Services.AddScoped<
    IPatientEncounterRepository,
    PatientEncounterRepository>();

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
app.UseAuthorization();

app.MapControllers();

app.Run();
