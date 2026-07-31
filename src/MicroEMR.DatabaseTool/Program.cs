using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.PlatformAdministration;
using MicroEMR.Application.Tenancy;
using MicroEMR.Infrastructure;
using MicroEMR.Infrastructure.Provisioning;

var configuration = new ConfigurationBuilder().AddEnvironmentVariables().AddUserSecrets<Program>()
    .AddInMemoryCollection(new Dictionary<string, string?> { ["TenantProvisioning:SqlAssetsPath"] = Path.Combine(AppContext.BaseDirectory, "database") }).Build();

if (!configuration.GetValue<bool>("PlatformAdministration:Enabled"))
{
    Console.Error.WriteLine("Platform administration is disabled. Set PlatformAdministration:Enabled=true in trusted local configuration.");
    return 5;
}
if (string.IsNullOrWhiteSpace(configuration["PlatformAdministration:ActorId"]))
{
    Console.Error.WriteLine("PlatformAdministration:ActorId is required for audited administrative execution.");
    return 5;
}

var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration);
services.AddLogging(builder => builder.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; }));
services.AddMicroEmrPlatformInfrastructure();
services.AddMicroEmrTenantProvisioning();
await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();

try
{
    return await RunAsync(args, scope.ServiceProvider);
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    if (args.Contains("--verbose", StringComparer.Ordinal)) Console.Error.WriteLine(exception);
    return 1;
}

static async Task<int> RunAsync(string[] args, IServiceProvider services)
{
    if (args.Length < 2) return Usage();
    var group = args[0]; var action = args[1]; var options = ParseOptions(args.Skip(2));
    var tenants = services.GetRequiredService<IPlatformTenantAdministrationService>();
    var memberships = services.GetRequiredService<IPlatformMembershipAdministrationService>();

    if (group == "tenant" && action == "list")
    {
        Console.WriteLine("TenantKey\tDisplayName\tTenantStatus\tDatabaseStatus\tSchemaVersion");
        foreach (var t in await tenants.GetTenantsAsync()) Console.WriteLine($"{t.TenantKey}\t{t.DisplayName}\t{t.TenantStatus}\t{t.DatabaseStatus ?? "Unassigned"}\t{t.CurrentSchemaVersion ?? "-"}");
        return 0;
    }
    if (group == "tenant" && action == "show") { PrintTenant(await RequiredTenant(tenants, Required(options, "tenant-key"))); return 0; }
    if (group == "tenant" && action == "create")
    {
        var uid = options.TryGetValue("tenant-uid", out var raw) ? Guid.Parse(raw) : Guid.NewGuid();
        var created = await tenants.CreateTenantAsync(new(uid, Required(options, "tenant-key"), Required(options, "display-name"), Required(options, "time-zone")));
        Console.WriteLine($"Tenant created in Provisioning state. TenantUid: {created.TenantUid}"); return 0;
    }
    if (group == "tenant" && action == "assign-database")
    {
        var tenant = await RequiredTenant(tenants, Required(options, "tenant-key"));
        var updated = await tenants.UpdateDatabaseAssignmentAsync(new(tenant.TenantUid, Required(options, "database-server-key"), Required(options, "database-name"), Required(options, "secret-reference")));
        Console.WriteLine($"Database assignment recorded for {updated.TenantKey} in Provisioning state. Secret reference is hidden."); return 0;
    }
    if (group == "tenant" && action is "suspend" or "activate" or "archive")
    {
        var key = Required(options, "tenant-key"); Confirm(options, key); var tenant = await RequiredTenant(tenants, key);
        Console.WriteLine($"{tenant.TenantKey}: {tenant.TenantStatus} -> {action}");
        if (action == "suspend") await tenants.SuspendTenantAsync(tenant.TenantUid); else if (action == "activate") await tenants.ActivateTenantAsync(tenant.TenantUid); else await tenants.ArchiveTenantAsync(tenant.TenantUid);
        Console.WriteLine("Tenant status updated."); return 0;
    }
    if (group == "tenant" && action == "provision") return await ProvisionAsync(Required(options, "tenant-key"), services);
    if (group == "provision-tenant-database" && action == "--tenant-key") return await ProvisionAsync(args.ElementAtOrDefault(2) ?? "", services);
    if (group == "membership" && action == "list") { PrintMemberships(await memberships.GetMembershipsAsync(Required(options, "user-id"))); return 0; }
    if (group == "tenant" && action == "members") { var t = await RequiredTenant(tenants, Required(options, "tenant-key")); PrintMemberships(await memberships.GetTenantMembershipsAsync(t.TenantUid)); return 0; }
    if (group == "membership" && action == "add") { var t = await RequiredTenant(tenants, Required(options, "tenant-key")); await memberships.AddMembershipAsync(new(Required(options, "user-id"), t.TenantUid, options.ContainsKey("default"))); Console.WriteLine("Membership added."); return 0; }
    if (group == "membership" && action is "activate" or "suspend" or "revoke") { var t = await RequiredTenant(tenants, Required(options, "tenant-key")); if (action is "suspend" or "revoke") Confirm(options, t.TenantKey); await memberships.SetMembershipStatusAsync(new(Required(options, "user-id"), t.TenantUid, char.ToUpperInvariant(action[0]) + action[1..] + (action == "revoke" ? "d" : action == "suspend" ? "ed" : ""))); Console.WriteLine("Membership status updated."); return 0; }
    if (group == "membership" && action is "set-default" or "clear-default") { var t = await RequiredTenant(tenants, Required(options, "tenant-key")); await memberships.SetDefaultAsync(new(Required(options, "user-id"), t.TenantUid, action == "set-default")); Console.WriteLine("Default membership updated."); return 0; }
    if (group == "tenant-role" && action is "add" or "remove") { var t = await RequiredTenant(tenants, Required(options, "tenant-key")); var user = Required(options, "user-id"); var role = Required(options, "role"); if (action == "add") await memberships.AddRoleAsync(new(user, t.TenantUid, role)); else { Confirm(options, t.TenantKey); await memberships.RemoveRoleAsync(new(user, t.TenantUid, role)); } Console.WriteLine($"Tenant role {action} completed."); return 0; }
    if (group == "tenant-role" && action == "list") { var t = await RequiredTenant(tenants, Required(options, "tenant-key")); PrintMemberships((await memberships.GetTenantMembershipsAsync(t.TenantUid)).Where(x => x.UserId == Required(options, "user-id"))); return 0; }
    return Usage();
}

static Dictionary<string, string> ParseOptions(IEnumerable<string> values)
{
    var result = new Dictionary<string, string>(StringComparer.Ordinal); var input = values.Where(x => x != "--verbose").ToArray();
    for (var i = 0; i < input.Length; i++) { if (!input[i].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"Unknown argument: {input[i]}"); var key = input[i][2..]; if (key == "default") result[key] = "true"; else { if (++i >= input.Length || input[i].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"Option --{key} requires a value."); result[key] = input[i]; } }
    return result;
}
static string Required(Dictionary<string, string> options, string key) => options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException($"Required option --{key} was not supplied.");
static void Confirm(Dictionary<string, string> options, string key) { if (!options.TryGetValue("confirm", out var confirmation) || !string.Equals(confirmation, key, StringComparison.Ordinal)) throw new ArgumentException($"This operation requires --confirm {key}."); }
static async Task<PlatformTenantDetails> RequiredTenant(IPlatformTenantAdministrationService service, string key) => await service.GetTenantByKeyAsync(key) ?? throw new InvalidOperationException("The requested tenant was not found.");
static void PrintTenant(PlatformTenantDetails t) { Console.WriteLine($"TenantUid: {t.TenantUid}\nTenantKey: {t.TenantKey}\nDisplayName: {t.DisplayName}\nTenantStatus: {t.TenantStatus}\nTimeZone: {t.DefaultTimeZoneId}\nDatabaseServerKey: {t.DatabaseServerKey ?? "-"}\nDatabaseName: {t.DatabaseName ?? "-"}\nDatabaseStatus: {t.DatabaseStatus ?? "Unassigned"}\nSchemaVersion: {t.CurrentSchemaVersion ?? "-"}\nLastMigrationAt: {t.LastMigrationAt?.ToString("O") ?? "-"}"); }
static void PrintMemberships(IEnumerable<PlatformMembershipInfo> rows) { Console.WriteLine("UserId\tTenantKey\tStatus\tDefault\tRoles"); foreach (var x in rows) Console.WriteLine($"{x.UserId}\t{x.TenantKey}\t{x.MembershipStatus}\t{x.IsDefaultTenant}\t{string.Join(',', x.Roles)}"); }
static async Task<int> ProvisionAsync(string tenantKey, IServiceProvider services) { ArgumentException.ThrowIfNullOrWhiteSpace(tenantKey); var catalog = services.GetRequiredService<ITenantCatalog>(); var resolver = services.GetRequiredService<ITenantDatabaseResolver>(); var runner = services.GetRequiredService<ITenantDatabaseMigrationRunner>(); var tenant = await catalog.GetByKeyAsync(tenantKey) ?? throw new InvalidOperationException("The requested tenant was not found."); var assignment = await resolver.ResolveAsync(tenant.TenantUid) ?? throw new InvalidOperationException("The requested tenant has no database assignment."); var result = await runner.ProvisionAsync(new(tenant.TenantUid, tenant.TenantKey, assignment.DatabaseServerKey, assignment.DatabaseName, assignment.SecretReference)); Console.WriteLine($"Provisioning result: {result.Status}; schema version: {result.CurrentSchemaVersion}; applied migrations: {result.AppliedMigrations.Count}."); return 0; }
static int Usage() { Console.Error.WriteLine("Commands: tenant list|show|create|assign-database|provision|suspend|activate|archive|members; membership add|activate|suspend|revoke|set-default|clear-default|list; tenant-role add|remove|list"); return 2; }

public partial class Program;
