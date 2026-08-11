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
using MicroEMR.Infrastructure.Provisioning;
using MicroEMR.Application.PlatformAdministration;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Infrastructure.ClinicalUsers;
using MicroEMR.Application.PatientReferrals;
using MicroEMR.Infrastructure.PatientReferrals;
using MicroEMR.Application.PatientFiles;
using MicroEMR.Infrastructure.PatientFiles;
using MicroEMR.Application.ClinicConfiguration;
using MicroEMR.Infrastructure.ClinicConfiguration;
using MicroEMR.Application.TenantUserAdministration;
using MicroEMR.Application.Reporting;
using MicroEMR.Infrastructure.Reporting;
using MicroEMR.Application.Templates.Repositories;

namespace MicroEMR.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMicroEmrPlatformInfrastructure(
        this IServiceCollection services)
    {
        services.AddScoped<ITenantCatalog, SqlTenantCatalog>();
        services.AddScoped<ITenantDatabaseResolver, SqlTenantDatabaseResolver>();
        services.AddScoped<
            IUserTenantMembershipRepository,
            SqlUserTenantMembershipRepository>();
        services.AddScoped<IPlatformTenantAdministrationService, SqlPlatformTenantAdministrationService>();
        services.AddScoped<IPlatformMembershipAdministrationService, SqlPlatformMembershipAdministrationService>();
        services.AddScoped<IIdentityUserLookup, SqlIdentityUserLookup>();
        services.AddScoped<IIdentityUserProfileLookup>(serviceProvider =>
            serviceProvider.GetRequiredService<IIdentityUserLookup>() as IIdentityUserProfileLookup
            ?? throw new InvalidOperationException("Identity user profile lookup is unavailable."));

        return services;
    }

    public static IServiceCollection AddMicroEmrInfrastructure(
        this IServiceCollection services)
    {
        services.AddScoped<ITenantSqlConnectionFactory, TenantSqlConnectionFactory>();
        services.AddScoped<IClinicalUserRepository, ClinicalUserRepository>();
        services.AddScoped<ITenantMembershipLifecycleRepository, SqlTenantMembershipLifecycleRepository>();
        services.AddScoped<ITenantRoleManagementRepository, SqlTenantRoleManagementRepository>();
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IEncounterSoapTemplateRepository, EncounterSoapTemplateRepository>();
        services.AddScoped<IPatientChartAlertRepository, PatientChartAlertRepository>();
        services.AddScoped<IPatientResultRepository, PatientResultRepository>();
        services.AddScoped<IPatientTaskRepository, PatientTaskRepository>();
        services.AddScoped<IPatientReferralRepository, PatientReferralRepository>();
        services.AddScoped<IReferralDocumentRepository, ReferralDocumentRepository>();
        services.AddScoped<IPatientFileRepository, PatientFileRepository>();
        services.AddScoped<IClinicProfileRepository, ClinicProfileRepository>();
        services.AddSingleton<IPatientFileStorage, LocalPatientFileStorage>();
        services.AddScoped<IPatientAllergyRepository, PatientAllergyRepository>();
            services.AddScoped<IPatientDocumentRepository, PatientDocumentRepository>();
            services.AddScoped<IDocumentTemplateVersionRepository, DocumentTemplateVersionRepository>();
            services.AddScoped<ITemplateAdministrationRepository, TemplateAdministrationRepository>();
            services.AddScoped<IPatientEncounterRepository, PatientEncounterRepository>();
            services.AddScoped<IPatientMedicationRepository, PatientMedicationRepository>();
            services.AddScoped<IPatientProblemRepository, PatientProblemRepository>();
            services.AddScoped<IPatientVitalRepository, PatientVitalRepository>();
            services.AddScoped<ISchedulingReadRepository, SchedulingReadRepository>();
            services.AddScoped<ISchedulingAppointmentRepository, SchedulingAppointmentRepository>();
            services.AddScoped<IAppointmentStatusReportRepository, AppointmentStatusReportRepository>();
            services.AddMicroEmrPlatformInfrastructure();
            services.AddMicroEmrTenantProvisioning();

            return services;
        }

    public static IServiceCollection AddMicroEmrTenantProvisioning(
        this IServiceCollection services)
    {
        services.AddScoped<ITenantDatabaseSecretProvider, ConfigurationTenantDatabaseSecretProvider>();
        services.AddScoped<ITenantDatabaseMigrationSource, FileTenantDatabaseMigrationSource>();
        services.AddScoped<ITenantProvisioningStatusRepository, SqlTenantProvisioningStatusRepository>();
        services.AddScoped<ITenantDatabaseMigrationRunner, TenantDatabaseMigrationRunner>();
        services.AddScoped<ITenantMigrationStatusReader, SqlTenantMigrationStatusReader>();
        services.AddScoped<ITenantMigrationStatusService, TenantMigrationStatusService>();
        return services;
    }
}
