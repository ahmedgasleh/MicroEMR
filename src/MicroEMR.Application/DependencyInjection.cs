using MicroEMR.Application.PatientDocuments.Services;
using MicroEMR.Application.PatientEncounters.Services;
using MicroEMR.Application.Patients.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MicroEMR.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddMicroEmrApplication(
        this IServiceCollection services)
    {
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<IPatientDocumentService, PatientDocumentService>();
        services.AddScoped<IPatientEncounterService, PatientEncounterService>();

        return services;
    }
}
