using MicroEMR.Application.PatientAllergies.Repositories;
using MicroEMR.Application.PatientDocuments.Repositories;
using MicroEMR.Application.PatientEncounters.Repositories;
using MicroEMR.Application.PatientMedications.Repositories;
using MicroEMR.Application.PatientProblems.Repositories;
using MicroEMR.Application.PatientVitals.Repositories;
using MicroEMR.Application.Patients.Repositories;
using MicroEMR.Application.Scheduling.Repositories;
using MicroEMR.Infrastructure.PatientAllergies;
using MicroEMR.Infrastructure.PatientDocuments;
using MicroEMR.Infrastructure.PatientEncounters;
using MicroEMR.Infrastructure.PatientMedications;
using MicroEMR.Infrastructure.PatientProblems;
using MicroEMR.Infrastructure.PatientVitals;
using MicroEMR.Infrastructure.Patients;
using MicroEMR.Infrastructure.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using MicroEMR.Application.EncounterSoapTemplates;
using MicroEMR.Infrastructure.EncounterSoapTemplates;
using MicroEMR.Application.PatientChartAlerts;
using MicroEMR.Infrastructure.PatientChartAlerts;
using MicroEMR.Application.PatientResults;
using MicroEMR.Infrastructure.PatientResults;
using MicroEMR.Application.PatientTasks;
using MicroEMR.Infrastructure.PatientTasks;
using MicroEMR.Application.Tenancy;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMicroEmrInfrastructure(
        this IServiceCollection services)
    {
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IEncounterSoapTemplateRepository, EncounterSoapTemplateRepository>();
        services.AddScoped<IPatientChartAlertRepository, PatientChartAlertRepository>();
        services.AddScoped<IPatientResultRepository, PatientResultRepository>();
        services.AddScoped<IPatientTaskRepository, PatientTaskRepository>();
        services.AddScoped<IPatientAllergyRepository, PatientAllergyRepository>();
            services.AddScoped<IPatientDocumentRepository, PatientDocumentRepository>();
            services.AddScoped<IPatientEncounterRepository, PatientEncounterRepository>();
            services.AddScoped<IPatientMedicationRepository, PatientMedicationRepository>();
            services.AddScoped<IPatientProblemRepository, PatientProblemRepository>();
            services.AddScoped<IPatientVitalRepository, PatientVitalRepository>();
            services.AddScoped<ISchedulingReadRepository, SchedulingReadRepository>();
            services.AddScoped<ISchedulingAppointmentRepository, SchedulingAppointmentRepository>();
            services.AddScoped<ITenantCatalog, SqlTenantCatalog>();
            services.AddScoped<ITenantDatabaseResolver, SqlTenantDatabaseResolver>();

            return services;
        }
}
