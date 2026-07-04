using MicroEMR.Application.PatientDocuments.Repositories;
using MicroEMR.Application.PatientEncounters.Repositories;
using MicroEMR.Application.Patients.Repositories;
using MicroEMR.Infrastructure.PatientDocuments;
using MicroEMR.Infrastructure.PatientEncounters;
using MicroEMR.Infrastructure.Patients;
using Microsoft.Extensions.DependencyInjection;

namespace MicroEMR.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMicroEmrInfrastructure(
        this IServiceCollection services)
    {
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IPatientDocumentRepository, PatientDocumentRepository>();
        services.AddScoped<IPatientEncounterRepository, PatientEncounterRepository>();

        return services;
    }
}
