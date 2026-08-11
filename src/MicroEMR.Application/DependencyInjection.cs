using MicroEMR.Application.PatientAllergies.Services;
using MicroEMR.Application.PatientDocuments.Services;
using MicroEMR.Application.PatientEncounters.Services;
using MicroEMR.Application.PatientMedications.Services;
using MicroEMR.Application.PatientProblems.Services;
using MicroEMR.Application.PatientVitals.Services;
using MicroEMR.Application.Patients.Services;
using MicroEMR.Application.Scheduling.Services;
using Microsoft.Extensions.DependencyInjection;
using MicroEMR.Application.PatientReferrals;
using MicroEMR.Application.PatientFiles;
using MicroEMR.Application.ClinicConfiguration;
using MicroEMR.Application.TenantUserAdministration;
using MicroEMR.Application.Reporting;
using MicroEMR.Application.PatientTasks;
using MicroEMR.Application.Templates.Serialization;
using MicroEMR.Application.Templates.Validation;
using MicroEMR.Application.Templates.Services;
using MicroEMR.Application.Templates.Runtime;

namespace MicroEMR.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddMicroEmrApplication(
        this IServiceCollection services)
    {
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<IPatientAllergyService, PatientAllergyService>();
        services.AddScoped<IPatientDocumentService, PatientDocumentService>();
        services.AddScoped<IDocumentTemplateVersionService, DocumentTemplateVersionService>();
        services.AddScoped<IPatientEncounterService, PatientEncounterService>();
        services.AddScoped<IPatientMedicationService, PatientMedicationService>();
        services.AddScoped<IPatientProblemService, PatientProblemService>();
        services.AddScoped<IPatientVitalService, PatientVitalService>();
        services.AddScoped<IPatientReferralService, PatientReferralService>();
        services.AddScoped<IReferralStatusTransitionService, ReferralStatusTransitionService>();
        services.AddScoped<IReferralDocumentService, ReferralDocumentService>();
        services.AddScoped<IPatientFileService, PatientFileService>();
        services.AddScoped<IClinicConfigurationService, ClinicConfigurationService>();
        services.AddScoped<ITenantUserAdministrationService, TenantUserAdministrationService>();
        services.AddScoped<ISchedulingReadService, SchedulingReadService>();
        services.AddScoped<ISchedulingAppointmentService, SchedulingAppointmentService>();
        services.AddScoped<IAppointmentStatusTransitionService, AppointmentStatusTransitionService>();
        services.AddScoped<IAppointmentStatusReportService, AppointmentStatusReportService>();
        services.AddScoped<IPatientTaskOverdueService, PatientTaskOverdueService>();
        services.AddSingleton<ITemplateDefinitionValidator, TemplateDefinitionValidator>();
        services.AddSingleton<ITemplateDefinitionSerializer, TemplateDefinitionSerializer>();
        services.AddSingleton<ITemplateInstanceRuntime, TemplateInstanceRuntime>();
        services.AddSingleton<ITemplateAuthorizationService, TemplateAuthorizationService>();
        services.AddScoped<ITemplateAdministrationService, TemplateAdministrationService>();

        return services;
    }
}
