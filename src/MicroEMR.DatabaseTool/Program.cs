using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.PlatformAdministration;
using MicroEMR.Application.Tenancy;
using MicroEMR.Infrastructure;
using MicroEMR.Infrastructure.Provisioning;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Infrastructure.ClinicalUsers;
using MicroEMR.Infrastructure.Tenancy;

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
services.AddScoped<ITenantContextAccessor, TenantContextAccessor>();
services.AddScoped<ITenantContext>(serviceProvider =>
    serviceProvider.GetRequiredService<ITenantContextAccessor>().Current
    ?? throw new InvalidOperationException("Tenant context has not been established."));
services.AddScoped<ITenantSqlConnectionFactory, TenantSqlConnectionFactory>();
services.AddScoped<IClinicalUserRepository, ClinicalUserRepository>();
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
    if (group == "tenant" && action == "migration-status")
        return await MigrationStatusAsync(options, tenants, services);
    if (group == "tenant" && action == "connection-diagnose")
        return await ConnectionDiagnoseAsync(Required(options, "tenant-key"), tenants, services);
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
    if (group == "tenant" && action == "user-map-auth-subject")
        return await MapClinicalUserAsync(options, tenants, services);
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
    for (var i = 0; i < input.Length; i++) { if (!input[i].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"Unknown argument: {input[i]}"); var key = input[i][2..]; if (key is "default" or "all") result[key] = "true"; else { if (++i >= input.Length || input[i].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"Option --{key} requires a value."); result[key] = input[i]; } }
    return result;
}
static string Required(Dictionary<string, string> options, string key) => options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException($"Required option --{key} was not supplied.");
static void Confirm(Dictionary<string, string> options, string key) { if (!options.TryGetValue("confirm", out var confirmation) || !string.Equals(confirmation, key, StringComparison.Ordinal)) throw new ArgumentException($"This operation requires --confirm {key}."); }
static async Task<PlatformTenantDetails> RequiredTenant(IPlatformTenantAdministrationService service, string key) => await service.GetTenantByKeyAsync(key) ?? throw new InvalidOperationException("The requested tenant was not found.");
static void PrintTenant(PlatformTenantDetails t) { Console.WriteLine($"TenantUid: {t.TenantUid}\nTenantKey: {t.TenantKey}\nDisplayName: {t.DisplayName}\nTenantStatus: {t.TenantStatus}\nTimeZone: {t.DefaultTimeZoneId}\nDatabaseServerKey: {t.DatabaseServerKey ?? "-"}\nDatabaseName: {t.DatabaseName ?? "-"}\nDatabaseStatus: {t.DatabaseStatus ?? "Unassigned"}\nSchemaVersion: {t.CurrentSchemaVersion ?? "-"}\nLastMigrationAt: {t.LastMigrationAt?.ToString("O") ?? "-"}"); }
static void PrintMemberships(IEnumerable<PlatformMembershipInfo> rows) { Console.WriteLine("UserId\tTenantKey\tStatus\tDefault\tRoles"); foreach (var x in rows) Console.WriteLine($"{x.UserId}\t{x.TenantKey}\t{x.MembershipStatus}\t{x.IsDefaultTenant}\t{string.Join(',', x.Roles)}"); }
static async Task<int> ProvisionAsync(string tenantKey, IServiceProvider services) { ArgumentException.ThrowIfNullOrWhiteSpace(tenantKey); var catalog = services.GetRequiredService<ITenantCatalog>(); var resolver = services.GetRequiredService<ITenantDatabaseResolver>(); var runner = services.GetRequiredService<ITenantDatabaseMigrationRunner>(); var tenant = await catalog.GetByKeyAsync(tenantKey) ?? throw new InvalidOperationException("The requested tenant was not found."); var assignment = await resolver.ResolveAsync(tenant.TenantUid) ?? throw new InvalidOperationException("The requested tenant has no database assignment."); var result = await runner.ProvisionAsync(new(tenant.TenantUid, tenant.TenantKey, assignment.DatabaseServerKey, assignment.DatabaseName, assignment.SecretReference)); Console.WriteLine($"Provisioning result: {result.Status}; schema version: {result.CurrentSchemaVersion}; applied migrations: {result.AppliedMigrations.Count}."); return 0; }
static async Task<int> MapClinicalUserAsync(
    Dictionary<string, string> options,
    IPlatformTenantAdministrationService tenants,
    IServiceProvider services)
{
    var tenantKey = Required(options, "tenant-key");
    Confirm(options, tenantKey);
    var tenant = await RequiredTenant(tenants, tenantKey);
    var rawClinicalUserId = Required(options, "clinical-user-id");
    if (!long.TryParse(rawClinicalUserId, out var clinicalUserId) || clinicalUserId <= 0)
        throw new ArgumentException("--clinical-user-id must be a positive integer.");
    var authSubject = Required(options, "auth-subject");

    var identityLookup = services.GetRequiredService<IIdentityUserLookup>();
    if (!identityLookup.IsAvailable)
        throw new InvalidOperationException("Auth user validation is not configured.");
    if (!await identityLookup.ExistsAsync(authSubject))
        throw new InvalidOperationException("The Auth subject does not identify an existing Auth user.");

    var contextAccessor = services.GetRequiredService<ITenantContextAccessor>();
    contextAccessor.SetTenant(new TenantContext(tenant.TenantUid, tenant.TenantKey, tenant.DisplayName));
    try
    {
        var mapped = await services.GetRequiredService<IClinicalUserRepository>()
            .SetAuthSubjectIdAsync(clinicalUserId, authSubject);
        Console.WriteLine($"Tenant: {tenant.TenantKey}");
        Console.WriteLine($"Clinical UserId: {mapped.UserId}");
        Console.WriteLine($"Clinical UserUid: {mapped.UserUid}");
        Console.WriteLine($"Auth subject mapped: {mapped.AuthSubjectId}");
        return 0;
    }
    finally
    {
        contextAccessor.Clear();
    }
}
static async Task<int> MigrationStatusAsync(Dictionary<string, string> options, IPlatformTenantAdministrationService tenants, IServiceProvider services)
{
    var all = options.ContainsKey("all");
    var hasKey = options.TryGetValue("tenant-key", out var tenantKey);
    if (all == hasKey) throw new ArgumentException("Supply exactly one of --tenant-key or --all.");

    IReadOnlyList<PlatformTenantDetails> targets;
    if (all)
    {
        var active = (await tenants.GetTenantsAsync())
            .Where(x => string.Equals(x.TenantStatus, "Active", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var details = new List<PlatformTenantDetails>(active.Length);
        foreach (var tenant in active)
            details.Add(await tenants.GetTenantAsync(tenant.TenantUid)
                ?? throw new InvalidOperationException($"Tenant '{tenant.TenantKey}' could not be loaded."));
        targets = details;
    }
    else targets = [await RequiredTenant(tenants, tenantKey!)];

    var resolver = services.GetRequiredService<ITenantDatabaseResolver>();
    var statusService = services.GetRequiredService<ITenantMigrationStatusService>();
    var allCurrent = true;
    foreach (var tenant in targets)
    {
        var assignment = await resolver.ResolveAsync(tenant.TenantUid);
        TenantMigrationStatusReport report;
        if (assignment is null)
        {
            var request = new TenantMigrationStatusRequest(tenant.TenantUid, tenant.TenantKey, "-", "-", "-",
                tenant.DatabaseStatus ?? "Unassigned", tenant.CurrentSchemaVersion, tenant.LastMigrationAt);
            report = new(request, 0, false, false, [], [], [], [], null, tenant.CurrentSchemaVersion, false,
                "The tenant has no database assignment.");
        }
        else
        {
            var request = new TenantMigrationStatusRequest(
                tenant.TenantUid, tenant.TenantKey, assignment.DatabaseServerKey, assignment.DatabaseName,
                assignment.SecretReference, assignment.DatabaseStatus, tenant.CurrentSchemaVersion, tenant.LastMigrationAt);
            report = await statusService.InspectAsync(request);
        }
        PrintMigrationStatus(report);
        allCurrent &= report.IsCurrent;
    }
    return allCurrent ? 0 : 3;
}
static async Task<int> ConnectionDiagnoseAsync(string tenantKey, IPlatformTenantAdministrationService tenants, IServiceProvider services)
{
    var tenant = await RequiredTenant(tenants, tenantKey);
    var assignment = await services.GetRequiredService<ITenantDatabaseResolver>().ResolveAsync(tenant.TenantUid)
        ?? throw new InvalidOperationException("The requested tenant has no database assignment.");
    var secret = await services.GetRequiredService<MicroEMR.Infrastructure.Tenancy.ITenantDatabaseSecretProvider>()
        .ResolveAsync(assignment.SecretReference);
    var result = await TenantConnectionDiagnostics.DiagnoseAsync(
        secret.ConnectionString, assignment.DatabaseName,
        "ConfigurationTenantDatabaseSecretProvider (environment variables/user secrets)");
    PrintConnectionDiagnostic(tenant, result);
    return result.ConnectionOpened && result.SchemaMigrationAccessible ? 0 : 4;
}
static void PrintConnectionDiagnostic(PlatformTenantDetails tenant, TenantConnectionDiagnosticResult result)
{
    var p = result.Properties;
    Console.WriteLine($"Tenant: {tenant.TenantKey}");
    Console.WriteLine($"Database status: {tenant.DatabaseStatus ?? "Unassigned"}");
    Console.WriteLine($"Server: {p.Server}");
    Console.WriteLine($"Database: {p.Database}");
    Console.WriteLine($"Authentication mode: {p.AuthenticationMode}");
    Console.WriteLine($"Integrated security: {p.IntegratedSecurity}");
    Console.WriteLine($"User ID configured: {p.UserIdConfigured}");
    Console.WriteLine($"Encrypt: {p.Encrypt}");
    Console.WriteLine($"TrustServerCertificate: {p.TrustServerCertificate}");
    Console.WriteLine($"Connection timeout: {p.ConnectionTimeout}");
    Console.WriteLine($"HostNameInCertificate: {p.HostNameInCertificate ?? "not set"}");
    Console.WriteLine($"Secret source: {p.SecretSource}");
    Console.WriteLine($"Windows identity: {result.WindowsIdentity}");
    Console.WriteLine($".NET runtime: {result.RuntimeVersion}");
    Console.WriteLine($"Microsoft.Data.SqlClient: {result.SqlClientVersion}");
    Console.WriteLine($"Connection opened: {result.ConnectionOpened}");
    Console.WriteLine($"TLS succeeded: {result.TlsSucceeded}");
    Console.WriteLine($"Authentication succeeded: {result.AuthenticationSucceeded}");
    Console.WriteLine($"SchemaMigration accessible: {result.SchemaMigrationAccessible}");
    Console.WriteLine($"Failure stage: {result.FailureStage}");
    if (result.SqlServerVersion is not null) Console.WriteLine($"SQL Server version: {result.SqlServerVersion}");
    if (result.AuthenticationScheme is not null) Console.WriteLine($"Authentication scheme: {result.AuthenticationScheme}");
    if (result.ExceptionType is not null) Console.WriteLine($"Exception type: {result.ExceptionType}");
    if (result.SqlErrorNumber is not null) Console.WriteLine($"SQL error number: {result.SqlErrorNumber}");
    if (result.SafeErrorMessage is not null) Console.WriteLine($"Error: {result.SafeErrorMessage}");
}
static void PrintMigrationStatus(TenantMigrationStatusReport report)
{
    Console.WriteLine($"Tenant: {report.Tenant.TenantKey}");
    Console.WriteLine($"Tenant UID: {report.Tenant.TenantUid}");
    Console.WriteLine($"Database status: {report.Tenant.DatabaseStatus}");
    Console.WriteLine($"Database identity: {(report.DatabaseIdentityValid ? "Valid" : "Invalid or unavailable")}");
    Console.WriteLine($"Manifest migrations: {report.ManifestMigrationCount}");
    Console.WriteLine($"Applied migrations: {report.MatchingMigrationIds.Count + report.UnexpectedMigrationIds.Count + report.HashMismatches.Count}");
    Console.WriteLine($"Current schema version: {report.CurrentSchemaVersion ?? "-"}");
    Console.WriteLine($"Current: {(report.IsCurrent ? "YES" : "NO")}");
    PrintItems("Missing", report.MissingMigrationIds);
    PrintItems("Unexpected applied", report.UnexpectedMigrationIds);
    PrintItems("Hash mismatches", report.HashMismatches.Select(x => x.MigrationId));
    Console.WriteLine($"Latest applied: {report.LatestAppliedMigration?.MigrationId ?? "none"}");
    Console.WriteLine($"Last migration failure: {report.LastFailure}");
    if (report.InspectionError is not null) Console.WriteLine($"Inspection error: {report.InspectionError}");
    Console.WriteLine();
}
static void PrintItems(string heading, IEnumerable<string> items)
{
    var values = items.ToArray();
    Console.WriteLine($"{heading}: {(values.Length == 0 ? "none" : string.Empty)}");
    foreach (var value in values) Console.WriteLine($"  {value}");
}
static int Usage() { Console.Error.WriteLine("Commands: tenant list|show|create|assign-database|provision|migration-status|connection-diagnose|user-map-auth-subject|suspend|activate|archive|members; tenant user-map-auth-subject --tenant-key KEY --clinical-user-id ID --auth-subject SUBJECT --confirm KEY; tenant migration-status --tenant-key KEY|--all; tenant connection-diagnose --tenant-key KEY; membership add|activate|suspend|revoke|set-default|clear-default|list; tenant-role add|remove|list"); return 2; }

public partial class Program;
