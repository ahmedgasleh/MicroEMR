using MicroEMR.Infrastructure.EncounterSoapTemplates;
using MicroEMR.Infrastructure.PatientAllergies;
using MicroEMR.Infrastructure.PatientChartAlerts;
using MicroEMR.Infrastructure.PatientDocuments;
using MicroEMR.Infrastructure.PatientEncounters;
using MicroEMR.Infrastructure.PatientMedications;
using MicroEMR.Infrastructure.PatientProblems;
using MicroEMR.Infrastructure.PatientResults;
using MicroEMR.Infrastructure.Patients;
using MicroEMR.Infrastructure.PatientTasks;
using MicroEMR.Infrastructure.PatientVitals;
using MicroEMR.Infrastructure.Scheduling;
using MicroEMR.Infrastructure.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ClinicalRepositoryConnectionTests
{
    [Fact]
    public void EveryClinicalRepositoryDependsOnTenantConnectionFactory()
    {
        Type[] repositoryTypes =
        [
            typeof(PatientRepository),
            typeof(PatientAllergyRepository),
            typeof(PatientChartAlertRepository),
            typeof(PatientDocumentRepository),
            typeof(PatientEncounterRepository),
            typeof(PatientMedicationRepository),
            typeof(PatientProblemRepository),
            typeof(PatientResultRepository),
            typeof(PatientTaskRepository),
            typeof(PatientVitalRepository),
            typeof(EncounterSoapTemplateRepository),
            typeof(SchedulingReadRepository),
            typeof(SchedulingAppointmentRepository)
        ];

        foreach (var repositoryType in repositoryTypes)
        {
            var parameters = Assert.Single(repositoryType.GetConstructors())
                .GetParameters();

            Assert.Contains(parameters, parameter =>
                parameter.ParameterType == typeof(ITenantSqlConnectionFactory));
            Assert.DoesNotContain(parameters, parameter =>
                parameter.ParameterType.Name == "IConfiguration");
        }
    }
}
