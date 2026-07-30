using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using MicroEMR.Infrastructure.Patients;
using MicroEMR.Infrastructure.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ClinicalRepositoryArchitectureTests
{
    [Fact]
    public void ClinicalRepositoriesUseOnlyTenantAwareConnections()
    {
        var repositoryTypes = typeof(PatientRepository).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type => type.Name.EndsWith("Repository", StringComparison.Ordinal))
            .Where(type => type.Namespace?.StartsWith("MicroEMR.Infrastructure", StringComparison.Ordinal) == true)
            .Where(type => type.Namespace is not "MicroEMR.Infrastructure.Tenancy" and
                not "MicroEMR.Infrastructure.Provisioning")
            .ToArray();

        Assert.NotEmpty(repositoryTypes);
        foreach (var repositoryType in repositoryTypes)
        {
            var parameters = repositoryType.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Select(parameter => parameter.ParameterType)
                .ToArray();

            Assert.Contains(typeof(ITenantSqlConnectionFactory), parameters);
            Assert.DoesNotContain(typeof(IConfiguration), parameters);
            Assert.DoesNotContain(typeof(SqlConnection), parameters);
            Assert.DoesNotContain(typeof(string), parameters);
        }
    }

    [Fact]
    public void PlatformRepositoriesDoNotUseTenantClinicalConnectionFactory()
    {
        var platformRepositories = new[]
        {
            typeof(SqlTenantCatalog),
            typeof(SqlTenantDatabaseResolver),
            typeof(SqlUserTenantMembershipRepository)
        };

        foreach (var repositoryType in platformRepositories)
        {
            Assert.DoesNotContain(
                repositoryType.GetConstructors().SelectMany(constructor => constructor.GetParameters()),
                parameter => parameter.ParameterType == typeof(ITenantSqlConnectionFactory));
        }
    }
}
