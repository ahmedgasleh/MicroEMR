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
        services.AddScoped<ISchedulingReadService, SchedulingReadService>();
        services.AddScoped<ISchedulingAppointmentService, SchedulingAppointmentService>();
        services.AddScoped<IAppointmentStatusTransitionService, AppointmentStatusTransitionService>();

        return services;
    }
}
