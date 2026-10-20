using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.Tenancy;
using MicroEMR.Application.PlatformAdministration;
using MicroEMR.Application.Templates.Contracts;
using MicroEMR.Infrastructure;
using MicroEMR.Infrastructure.ClinicConfiguration;
using MicroEMR.Infrastructure.ClinicalDataMigration;
using MicroEMR.Infrastructure.ClinicalUsers;
using MicroEMR.Infrastructure.PatientDocuments;
using MicroEMR.Infrastructure.Providers;
using MicroEMR.Infrastructure.Provisioning;
using MicroEMR.Infrastructure.SecurityAudit;
using MicroEMR.Infrastructure.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class RuntimeAdministrativeConnectionTests
{
    private const string Runtime = "Server=test-host;Database=test-tenant;Integrated Security=true;Application Name=runtime-test";
    private const string Administrative = "Server=test-host;Database=test-tenant;Integrated Security=true;Application Name=admin-test";
    private static readonly Guid TenantUid = Guid.NewGuid();

    [Fact]
    public async Task RuntimeAndAdministrativeSecretsRemainDistinctForSameAssignment()
    {
        var configuration = Configuration();
        Assert.Equal(Runtime, (await new ConfigurationTenantDatabaseSecretProvider(configuration).ResolveAsync("selected")).ConnectionString);
        Assert.Equal(Administrative, (await new AdministrativeTenantDatabaseSecretProvider(configuration).ResolveAsync("selected")).ConnectionString);
        Assert.Equal(Runtime, PlatformDatabaseConnection.GetConnectionString(configuration));
        Assert.Equal(Administrative, AdministrativePlatformDatabaseConnection.GetConnectionString(configuration));
        Assert.Equal("auth-independent", configuration.GetConnectionString("AuthDatabase"));
    }

    [Theory]
    [InlineData("ConnectionStrings:AdministrativePlatformDatabase")]
    [InlineData("AdministrativeTenantDatabaseSecrets:selected")]
    public async Task MissingAdministrativeSettingNeverUsesRuntimeValue(string missing)
    {
        var configuration = Configuration();
        configuration[missing] = null;
        var error = missing.StartsWith("ConnectionStrings", StringComparison.Ordinal)
            ? Assert.Throws<InvalidOperationException>(() => AdministrativePlatformDatabaseConnection.GetConnectionString(configuration))
            : await Assert.ThrowsAsync<InvalidOperationException>(() => new AdministrativeTenantDatabaseSecretProvider(configuration).ResolveAsync("selected"));
        Assert.Contains(missing, error.Message);
        Assert.DoesNotContain(Runtime, error.ToString());
    }

    [Fact]
    public async Task MissingRuntimeSecretNeverFallsBackToAdministrativeSecret()
    {
        var configuration = Configuration();
        configuration["TenantDatabaseSecrets:selected"] = null;
        await Assert.ThrowsAsync<TenantDatabaseConnectionException>(() => new ConfigurationTenantDatabaseSecretProvider(configuration).ResolveAsync("selected"));
    }

    [Theory]
    [InlineData("Server=other-host;Database=test-tenant;Integrated Security=true")]
    [InlineData("Server=test-host;Database=other-tenant;Integrated Security=true")]
    [InlineData("Server=test-host;Database=test-tenant;AttachDbFilename=C:\\other.mdf")]
    [InlineData("UnknownOption=DO_NOT_DISCLOSE")]
    public async Task AdministrativeTargetMismatchOrInvalidConfigurationFailsWithoutLeakingValues(string value)
    {
        var configuration = Configuration();
        configuration["AdministrativeTenantDatabaseSecrets:selected"] = value;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new AdministrativeTenantDatabaseSecretProvider(configuration).ResolveAsync("selected"));
        Assert.DoesNotContain(value, error.ToString());
        Assert.DoesNotContain("DO_NOT_DISCLOSE", error.ToString());
        Assert.Null(error.InnerException);
    }

    [Fact]
    public async Task AdministrativeFactoryRejectsWrongTenantBeforeOpeningSql()
    {
        var resolver = new Resolver(new(Guid.NewGuid(), "server-key", "test-tenant", "selected", "Active"));
        var factory = new AdministrativeTenantSqlConnectionFactory(new TenantContext(TenantUid, "key", "Tenant"),
            resolver, Configuration(), NullLogger<MicroEMR.Infrastructure.Tenancy.TenantSqlConnectionFactory>.Instance);
        await Assert.ThrowsAsync<TenantDatabaseConnectionException>(() => factory.OpenConnectionAsync());
        Assert.Equal(TenantUid, resolver.Requested);
    }

    [Fact]
    public async Task AdministrativeFactoryRejectsSecretForDifferentAssignedDatabaseBeforeOpeningSql()
    {
        var resolver = new Resolver(new(TenantUid, "server-key", "another-tenant", "selected", "Active"));
        var factory = new AdministrativeTenantSqlConnectionFactory(new TenantContext(TenantUid, "key", "Tenant"),
            resolver, Configuration(), NullLogger<MicroEMR.Infrastructure.Tenancy.TenantSqlConnectionFactory>.Instance);
        await Assert.ThrowsAsync<TenantDatabaseConnectionException>(() => factory.OpenConnectionAsync());
        Assert.Equal(TenantUid, resolver.Requested);
    }

    [Fact]
    public async Task ClinicalActorLookupFailureDoesNotRetryAdministrativeFactory()
    {
        var normal = new RuntimeProbe(); var admin = new AdministrativeProbe();
        var repository = new ClinicalUserRepository(normal, admin);
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAsync<RouteFailure>(() => repository.GetByAuthSubjectIdAsync("subject", cancellation.Token));
        Assert.Equal(1, normal.Calls); Assert.Equal(0, admin.Calls);
        Assert.Equal(cancellation.Token, normal.Token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActorProvisionAndOperatorMappingUseAdministrativeFactory(bool mapping)
    {
        var normal = new RuntimeProbe(); var admin = new AdministrativeProbe();
        var repository = new ClinicalUserRepository(normal, admin);
        await Assert.ThrowsAsync<RouteFailure>(() => mapping
            ? repository.SetAuthSubjectIdAsync(1, "subject")
            : repository.ProvisionAsync("subject", "username", "display", null));
        Assert.Equal(0, normal.Calls); Assert.Equal(1, admin.Calls);
    }

    [Fact]
    public async Task ClinicProfileReadIsNormalAndSaveIsAdministrative()
    {
        var normal = new RuntimeProbe(); var admin = new AdministrativeProbe();
        var repository = new ClinicProfileRepository(normal, admin, NullLogger<ClinicProfileRepository>.Instance);
        await Assert.ThrowsAsync<RouteFailure>(() => repository.GetAsync());
        Assert.Equal(1, normal.Calls); Assert.Equal(0, admin.Calls);
        await Assert.ThrowsAsync<RouteFailure>(() => repository.SaveAsync(new(), 1));
        Assert.Equal(1, normal.Calls); Assert.Equal(1, admin.Calls);
    }

    [Fact]
    public async Task ProviderReadStaysNormalWhileMutationAndEligibleUsersAreAdministrative()
    {
        var normal = new RuntimeProbe(); var admin = new AdministrativeProbe();
        var repository = new ProviderAdministrationRepository(normal, admin);
        await Assert.ThrowsAsync<RouteFailure>(() => repository.ListAsync("Active"));
        await Assert.ThrowsAsync<RouteFailure>(() => repository.GetAsync(Guid.NewGuid()));
        Assert.Equal(2, normal.Calls); Assert.Equal(0, admin.Calls);
        await Assert.ThrowsAsync<RouteFailure>(() => repository.CreateAsync(new(), 1));
        await Assert.ThrowsAsync<RouteFailure>(() => repository.EligibleUsersAsync(null));
        Assert.Equal(2, normal.Calls); Assert.Equal(2, admin.Calls);
    }

    [Fact]
    public async Task LegacyTemplateWritesAreAdministrativeWhileRetrievalIsNormal()
    {
        var normal = new RuntimeProbe(); var admin = new AdministrativeProbe();
        var repository = new PatientDocumentRepository(normal, admin, NullLogger<PatientDocumentRepository>.Instance);
        await Assert.ThrowsAsync<RouteFailure>(() => repository.GetTemplatesAsync("Active"));
        await Assert.ThrowsAsync<RouteFailure>(() => repository.GetByUidAsync(Guid.NewGuid()));
        Assert.Equal(2, normal.Calls); Assert.Equal(0, admin.Calls);
        await Assert.ThrowsAsync<RouteFailure>(() => repository.CreateTemplateAsync(new(), 1));
        await Assert.ThrowsAsync<RouteFailure>(() => repository.UpdateTemplateAsync(Guid.NewGuid(), new(), 1));
        await Assert.ThrowsAsync<RouteFailure>(() => repository.SetTemplateActiveAsync(Guid.NewGuid(), false, 1));
        Assert.Equal(2, normal.Calls); Assert.Equal(3, admin.Calls);
    }

    [Fact]
    public async Task ModernTemplateAuthoringAndVersionLifecycleRemainNormal()
    {
        var normal = new RuntimeProbe();
        var templates = new TemplateAdministrationRepository(normal);
        await Assert.ThrowsAsync<RouteFailure>(() => templates.CreateAsync(new(), "{}", 1));
        await Assert.ThrowsAsync<RouteFailure>(() => templates.CloneAsync(Guid.NewGuid(), new(), 1));
        await Assert.ThrowsAsync<RouteFailure>(() => templates.UpdateMetadataAsync(Guid.NewGuid(), new(), 1));
        await Assert.ThrowsAsync<RouteFailure>(() => templates.SetActiveAsync(Guid.NewGuid(), new(), 1));
        var versions = new DocumentTemplateVersionRepository(normal, NullLogger<DocumentTemplateVersionRepository>.Instance);
        await Assert.ThrowsAsync<RouteFailure>(() => versions.CreateDraftAsync(Guid.NewGuid(), 1));
        await Assert.ThrowsAsync<RouteFailure>(() => versions.UpdateDraftAsync(Guid.NewGuid(), Guid.NewGuid(), new(), 1));
        await Assert.ThrowsAsync<RouteFailure>(() => versions.PublishAsync(Guid.NewGuid(), Guid.NewGuid(), "unused-before-open", 1));
        await Assert.ThrowsAsync<RouteFailure>(() => versions.RetireAsync(Guid.NewGuid(), Guid.NewGuid(), "unused-before-open", 1));
        Assert.Equal(8, normal.Calls);
    }

    [Fact]
    public async Task ImportRepositoryUsesAdministrativeCapabilityOnly()
    {
        var admin = new AdministrativeProbe();
        var repository = new ClinicalDataMigrationRepository(admin);
        await Assert.ThrowsAsync<RouteFailure>(() => repository.FindPatientMatchAsync("source", "patient", null, "First", "Last", null));
        Assert.Equal(1, admin.Calls);
        Assert.DoesNotContain(typeof(ClinicalDataMigrationRepository).GetConstructors().Single().GetParameters(),
            p => p.ParameterType == typeof(ITenantSqlConnectionFactory));
    }

    [Fact]
    public async Task EffectiveAccessFailureDoesNotRequestAdministrativeConnection()
    {
        var configuration = Configuration();
        // Malformed text fails in SqlConnection construction, without opening any network connection.
        configuration["ConnectionStrings:PlatformDatabase"] = "invalid-without-equals";
        var admin = new PlatformProbe();
        var repository = new SqlAccessProfileRepository(configuration, admin);
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetEffectiveAsync(TenantUid, "subject"));
        Assert.Equal(0, admin.Calls);
        await Assert.ThrowsAsync<RouteFailure>(() => repository.ListAsync(TenantUid));
        Assert.Equal(1, admin.Calls);
    }

    [Fact]
    public async Task PlatformMembershipLifecycleRolesCreationAndSecurityReviewUseAdministrativeProvider()
    {
        var admin = new PlatformProbe();
        var version = Convert.ToBase64String(new byte[8]);
        await Assert.ThrowsAsync<RouteFailure>(() => new SqlTenantMembershipLifecycleRepository(admin)
            .ActivateAsync("subject", TenantUid, version, "actor"));
        await Assert.ThrowsAsync<RouteFailure>(() => new SqlTenantRoleManagementRepository(admin)
            .ReplaceRolesAsync("subject", TenantUid, [], version, "actor"));
        await Assert.ThrowsAsync<RouteFailure>(() => new SqlTenantUserCreationRepository(admin)
            .CreateAsync("subject", TenantUid, "Physician", "actor"));
        await Assert.ThrowsAsync<RouteFailure>(() => new SqlPlatformSecurityAuditReviewRepository(admin)
            .GetByUidAsync(Guid.NewGuid()));
        Assert.Equal(4, admin.Calls);
    }

    [Fact]
    public async Task MembershipListByUserRemainsNormalAndListByTenantIsAdministrative()
    {
        var configuration = Configuration();
        configuration["ConnectionStrings:PlatformDatabase"] = "invalid-without-equals";
        var admin = new PlatformProbe();
        var repository = new SqlPlatformMembershipAdministrationService(configuration, new IdentityLookup(), admin);
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetMembershipsAsync("subject"));
        Assert.Equal(0, admin.Calls);
        await Assert.ThrowsAsync<RouteFailure>(() => repository.GetTenantMembershipsAsync(TenantUid));
        Assert.Equal(1, admin.Calls);
    }

    [Fact]
    public void ApiFactoriesResolveWithoutAdministrativeSecretsAndRemainDifferentCapabilities()
    {
        var configuration = Configuration();
        configuration["ConnectionStrings:AdministrativePlatformDatabase"] = null;
        configuration["AdministrativeTenantDatabaseSecrets:selected"] = null;
        var services = new ServiceCollection();
        services.AddLogging(); services.AddSingleton<IConfiguration>(configuration);
        services.AddScoped<ITenantContext>(_ => new TenantContext(TenantUid, "key", "Tenant"));
        services.AddMicroEmrInfrastructure();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.IsType<TenantSqlConnectionFactory>(scope.ServiceProvider.GetRequiredService<ITenantSqlConnectionFactory>());
        var administrative = scope.ServiceProvider.GetRequiredService<IAdministrativeTenantSqlConnectionFactory>();
        Assert.IsType<AdministrativeTenantSqlConnectionFactory>(administrative);
        Assert.False(administrative is ITenantSqlConnectionFactory);
        // Ordinary reads can construct mixed repositories before administrative settings are deployed.
        scope.ServiceProvider.GetRequiredService<MicroEMR.Application.ClinicalUsers.IClinicalUserRepository>();
        scope.ServiceProvider.GetRequiredService<MicroEMR.Application.ClinicConfiguration.IClinicProfileRepository>();
        scope.ServiceProvider.GetRequiredService<MicroEMR.Application.PatientDocuments.Repositories.IPatientDocumentRepository>();
    }

    [Fact]
    public void ApiRegistrationExcludesDeploymentRunnerAndToolRegistrationRetainsItsConfiguration()
    {
        var services = new ServiceCollection();
        services.AddMicroEmrInfrastructure();
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(ITenantDatabaseMigrationRunner));
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(ITenantProvisioningStatusRepository));
        Assert.Equal(typeof(AdministrativeTenantSqlConnectionFactory), services.Last(x => x.ServiceType == typeof(IAdministrativeTenantSqlConnectionFactory)).ImplementationType);
        Assert.Equal(typeof(AdministrativePlatformConnectionStringProvider), services.Last(x => x.ServiceType == typeof(IAdministrativePlatformConnectionStringProvider)).ImplementationType);
        var tool = new ServiceCollection();
        tool.AddMicroEmrPlatformInfrastructure(); tool.AddMicroEmrTenantProvisioning();
        Assert.Contains(tool, x => x.ServiceType == typeof(ITenantDatabaseMigrationRunner));
        Assert.Equal(typeof(DeploymentTenantSqlConnectionFactory), tool.Last(x => x.ServiceType == typeof(IAdministrativeTenantSqlConnectionFactory)).ImplementationType);
        Assert.Equal(typeof(DeploymentPlatformConnectionStringProvider), tool.Last(x => x.ServiceType == typeof(IAdministrativePlatformConnectionStringProvider)).ImplementationType);
        var configuration = Configuration();
        configuration["ConnectionStrings:AdministrativePlatformDatabase"] = null;
        Assert.Equal(Runtime, new DeploymentPlatformConnectionStringProvider(configuration).GetConnectionString());
    }

    private static IConfigurationRoot Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:PlatformDatabase"] = Runtime,
        ["ConnectionStrings:AdministrativePlatformDatabase"] = Administrative,
        ["TenantDatabaseSecrets:selected"] = Runtime,
        ["AdministrativeTenantDatabaseSecrets:selected"] = Administrative,
        ["ConnectionStrings:AuthDatabase"] = "auth-independent"
    }).Build();

    private sealed class RouteFailure : Exception;
    private sealed class RuntimeProbe : ITenantSqlConnectionFactory
    {
        public int Calls; public CancellationToken Token;
        public Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
        { Calls++; Token = cancellationToken; throw new RouteFailure(); }
    }
    private sealed class AdministrativeProbe : IAdministrativeTenantSqlConnectionFactory
    {
        public int Calls;
        public Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
        { Calls++; throw new RouteFailure(); }
    }
    private sealed class PlatformProbe : IAdministrativePlatformConnectionStringProvider
    {
        public int Calls;
        public string GetConnectionString() { Calls++; throw new RouteFailure(); }
    }
    private sealed class Resolver(TenantDatabaseInfo? assignment) : ITenantDatabaseResolver
    {
        public Guid Requested;
        public Task<TenantDatabaseInfo?> ResolveAsync(Guid tenantUid, CancellationToken cancellationToken = default)
        { Requested = tenantUid; return Task.FromResult(assignment); }
    }
    private sealed class IdentityLookup : IIdentityUserLookup
    {
        public bool IsAvailable => true;
        public Task<bool> ExistsAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
