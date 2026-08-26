using MicroEMR.Application.PatientAllergies.Services;
using MicroEMR.Application.PatientDocuments.Services;
using MicroEMR.Application.PatientEncounters.Services;
using MicroEMR.Application.PatientMedications.Services;
using MicroEMR.Application.PatientPrescriptions;
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
using MicroEMR.Application.Templates.Output;
using MicroEMR.Application.Templates.Variables;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientClinicalHistory;
using MicroEMR.Application.ReadAudit;
using MicroEMR.Application.SecurityAudit;
using MicroEMR.Application.PatientImmunizations;
using MicroEMR.Application.ClinicalDataMigration;
using MicroEMR.Application.Cds;
using MicroEMR.Application.Cdm;
using MicroEMR.Application.PatientCpp;

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
        services.AddScoped<IPatientPrescriptionService, PatientPrescriptionService>();
        services.AddScoped<IPatientProblemService, PatientProblemService>();
        services.AddScoped<IPatientClinicalHistoryService, PatientClinicalHistoryService>();
        services.AddScoped<IPatientImmunizationService, PatientImmunizationService>();
        services.AddScoped<IPatientVitalService, PatientVitalService>();
        services.AddScoped<IPatientReferralService, PatientReferralService>();
        services.AddScoped<IReferralStatusTransitionService, ReferralStatusTransitionService>();
        services.AddScoped<IReferralDocumentService, ReferralDocumentService>();
        services.AddScoped<IPatientFileService, PatientFileService>();
        services.AddScoped<IClinicConfigurationService, ClinicConfigurationService>();
        services.AddScoped<ITenantUserAdministrationService, TenantUserAdministrationService>();
        services.AddScoped<IAccessProfileService, AccessProfileService>();
        services.AddScoped<ICurrentUserPermissionService, CurrentUserPermissionService>();
        services.AddScoped<ISchedulingReadService, SchedulingReadService>();
        services.AddScoped<ISchedulingAppointmentService, SchedulingAppointmentService>();
        services.AddScoped<IAppointmentStatusTransitionService, AppointmentStatusTransitionService>();
        services.AddScoped<IAppointmentStatusReportService, AppointmentStatusReportService>();
        services.AddScoped<IPatientTaskOverdueService, PatientTaskOverdueService>();
        services.AddSingleton<ITemplateDefinitionValidator, TemplateDefinitionValidator>();
        services.AddSingleton<ITemplateDefinitionSerializer, TemplateDefinitionSerializer>();
        services.AddSingleton<ITemplateInstanceRuntime, TemplateInstanceRuntime>();
        services.AddSingleton<ITemplateVariableResolver, TemplateVariableResolver>();
        services.AddSingleton<ITemplateOutputBuilder, TemplateOutputBuilder>();
        services.AddSingleton<ITemplateHtmlRenderer, TemplateHtmlRenderer>();
        services.AddSingleton<ITemplateAuthorizationService, TemplateAuthorizationService>();
        services.AddScoped<ITemplateAdministrationService, TemplateAdministrationService>();
        services.AddSingleton<IClinicalPrintLayoutRenderer, ClinicalPrintLayoutRenderer>();
        services.AddScoped<IClinicalPdfPreviewService, ClinicalPdfPreviewService>();
        services.AddScoped<IClinicalArtifactService, ClinicalArtifactService>();
        services.AddScoped<IPatientChartReadAuditService, PatientChartReadAuditService>();
        services.AddScoped<IStructuredReadAuditService, StructuredReadAuditService>();
        services.AddScoped<IPlatformSecurityAuditReviewService, PlatformSecurityAuditReviewService>();
        services.AddOptions<ClinicalDataMigrationOptions>();
        services.AddScoped<IClinicalDataMigrationValidationService, ClinicalDataMigrationValidationService>();
        services.AddScoped<IClinicalDataMigrationImportService, ClinicalDataMigrationImportService>();
        services.AddSingleton<ICdsRuleRegistry, CdsRuleRegistry>();
        services.AddScoped<ICdsEvaluationService, CdsEvaluationService>();
        services.AddSingleton<ICdmProgramRegistry, CdmProgramRegistry>();
        services.AddScoped<ICdmEnrollmentService, CdmEnrollmentService>();
        services.AddScoped<IPatientCppService, PatientCppService>();

        return services;
    }
}
