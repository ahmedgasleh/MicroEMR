using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.Tenancy;
using MicroEMR.Infrastructure;
using MicroEMR.Infrastructure.Provisioning;

if (args.Length != 3 ||
    !string.Equals(args[0], "provision-tenant-database", StringComparison.Ordinal) ||
    !string.Equals(args[1], "--tenant-key", StringComparison.Ordinal) ||
    string.IsNullOrWhiteSpace(args[2]))
{
    Console.Error.WriteLine(
        "Usage: provision-tenant-database --tenant-key <tenant-key>");
    return 2;
}

var tenantKey = args[2].Trim();
var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["TenantProvisioning:SqlAssetsPath"] =
            Path.Combine(AppContext.BaseDirectory, "database")
    })
    .Build();

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration);
services.AddLogging(builder => builder.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss ";
}));
services.AddMicroEmrPlatformInfrastructure();
services.AddMicroEmrTenantProvisioning();

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var tenantCatalog = scope.ServiceProvider.GetRequiredService<ITenantCatalog>();
var databaseResolver = scope.ServiceProvider.GetRequiredService<ITenantDatabaseResolver>();
var runner = scope.ServiceProvider.GetRequiredService<ITenantDatabaseMigrationRunner>();

var tenant = await tenantCatalog.GetByKeyAsync(tenantKey);
if (tenant is null)
{
    Console.Error.WriteLine("The requested tenant was not found.");
    return 3;
}

var assignment = await databaseResolver.ResolveAsync(tenant.TenantUid);
if (assignment is null)
{
    Console.Error.WriteLine("The requested tenant has no database assignment.");
    return 4;
}

try
{
    var result = await runner.ProvisionAsync(new TenantDatabaseProvisioningRequest(
        tenant.TenantUid,
        tenant.TenantKey,
        assignment.DatabaseServerKey,
        assignment.DatabaseName,
        assignment.SecretReference));

    Console.WriteLine(
        $"Provisioning result: {result.Status}; schema version: {result.CurrentSchemaVersion}; applied migrations: {result.AppliedMigrations.Count}.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Provisioning failed: {exception.Message}");
    return 1;
}

public partial class Program;
