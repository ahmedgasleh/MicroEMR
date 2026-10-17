using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Api.Middleware;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PatientFamilyHistory;
using MicroEMR.Application.SecurityAudit;
using MicroEMR.Application.Tenancy;
using MicroEMR.Infrastructure.ClinicalUsers;
using MicroEMR.Infrastructure.PatientFamilyHistory;
using MicroEMR.Infrastructure.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

// HTTP authentication and platform assignment are fixture inputs, not a claim of OIDC/browser acceptance.
// Controllers, authorization handler, clinical actor resolution, tenant identity verification,
// Application service, repositories and stored procedures below are production implementations.
public sealed class FamilyHistoryAcceptanceTests(FamilyHistoryAcceptanceDatabase db)
    : IClassFixture<FamilyHistoryAcceptanceDatabase>
{
    [Fact]
    public async Task M8BothTenantDirectionsReturn404WithoutDisclosureOrDatabaseChanges()
    {
        var avery = await db.SeedPatient(false); var blair = await db.SeedPatient(false);
        var casey = await db.SeedPatient(true);
        var writerA = await db.SeedUser(false); var writerB = await db.SeedUser(true);
        await using var tenantA = await FamilyHistoryAcceptanceHost.Start(db, false, writerA);
        await using var tenantB = await FamilyHistoryAcceptanceHost.Start(db, true, writerB);
        using var a = tenantA.Client(writerA); using var b = tenantB.Client(writerB);
        var f = await Create(a, avery, FamilyHistorySqlTests.Draft());
        var draftB = new CreatePatientFamilyHistoryRequest
        {
            Relationship = "Maternal aunt", ConditionDescription = "TEST ONLY - asthma", AgeAtOnset = "12 years",
            LifeStageCode = "C", Treatment = "TEST ONLY - inhaled therapy", Notes = "TEST ONLY - Tenant B fixture"
        };
        var localB = await Create(b, casey, draftB);
        var beforeA = await db.Snapshot(false); var beforeB = await db.Snapshot(true);
        Assert.Equal(localB, Assert.Single((await b.GetFromJsonAsync<PatientFamilyHistoryResponse[]>(Route(casey)))!));
        Assert.Equal(f, Assert.Single((await a.GetFromJsonAsync<PatientFamilyHistoryResponse[]>(Route(avery)))!));
        Assert.Empty((await a.GetFromJsonAsync<PatientFamilyHistoryResponse[]>(Route(blair)))!);

        // Exact M8 route permutations: same-tenant foreign patient, then both tenant directions,
        // with a local patient and a foreign patient UID on each receiving tenant.
        foreach (var (client, patient, foreign) in new[]
        {
            (a, blair, f), (b, casey, f), (b, avery, f),
            (a, avery, localB), (a, casey, localB)
        })
        {
            var route = Route(patient, foreign.FamilyHistoryUid);
            using var get = await client.GetAsync(route);
            await Denied(get, HttpStatusCode.NotFound, foreign);
            using var update = await client.PutAsJsonAsync(route, FamilyHistorySqlTests.Edit(foreign));
            await Denied(update, HttpStatusCode.NotFound, foreign);
            using var archive = await client.PostAsJsonAsync(route + "/archive", new ArchivePatientFamilyHistoryRequest { RowVersion = foreign.RowVersion });
            await Denied(archive, HttpStatusCode.NotFound, foreign);
        }
        // Compare ALL clinical rows/tokens and ALL clinical audit rows in BOTH databases.
        Assert.Equal(beforeA, await db.Snapshot(false));
        Assert.Equal(beforeB, await db.Snapshot(true));
        Assert.Equal(0, await db.ForeignRows(true, f.FamilyHistoryUid));
        Assert.Equal(0, await db.ForeignRows(false, localB.FamilyHistoryUid));
    }

    [Fact]
    public async Task M9HttpSavesCorrelateCompleteAuditAndRejectedRequestsPreserveState()
    {
        var avery = await db.SeedPatient(false); var blair = await db.SeedPatient(false);
        var casey = await db.SeedPatient(true);
        var creator = await db.SeedUser(false); var editor = await db.SeedUser(false);
        var reader = await db.SeedUser(false, false); var writerB = await db.SeedUser(true);
        await using var tenantA = await FamilyHistoryAcceptanceHost.Start(db, false, creator, editor, reader);
        await using var tenantB = await FamilyHistoryAcceptanceHost.Start(db, true, writerB);
        using var a = tenantA.Client(creator); using var e = tenantA.Client(editor);
        using var r = tenantA.Client(reader); using var b = tenantB.Client(writerB);

        var createFrom = await db.Utc(false);
        var f = await Create(a, avery, FamilyHistorySqlTests.Draft());
        var createTo = await db.Utc(false);
        Assert.InRange(f.CreatedAt, createFrom.AddSeconds(-1), createTo.AddSeconds(1));
        var bFrom = await db.Utc(true);
        var localB = await Create(b, casey, new() { Relationship = "Maternal aunt", ConditionDescription = "TEST ONLY - asthma" });
        var bTo = await db.Utc(true);
        var update = FamilyHistorySqlTests.Edit(f); update.Treatment = "TEST ONLY - oral therapy reviewed";
        var updateFrom = await db.Utc(false);
        using var updated = await e.PutAsJsonAsync(Route(avery, f.FamilyHistoryUid), update);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var saved = (await updated.Content.ReadFromJsonAsync<PatientFamilyHistoryResponse>())!;
        var updateTo = await db.Utc(false);
        Assert.Equal(f.CreatedAt, saved.CreatedAt); Assert.Equal(creator.Actor, saved.CreatedBy);
        Assert.Equal(editor.Actor, saved.UpdatedBy); Assert.NotEqual(f.RowVersion, saved.RowVersion);
        Assert.InRange(saved.UpdatedAt!.Value, updateFrom.AddSeconds(-1), updateTo.AddSeconds(1));

        var beforeA = await db.Snapshot(false); var beforeB = await db.Snapshot(true);
        using (var stale = await a.PutAsJsonAsync(Route(avery, f.FamilyHistoryUid), FamilyHistorySqlTests.Edit(f)))
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using (var staleArchive = await a.PostAsJsonAsync(Route(avery, f.FamilyHistoryUid) + "/archive", new { rowVersion = f.RowVersion }))
            Assert.Equal(HttpStatusCode.Conflict, staleArchive.StatusCode);
        using (var wrongScope = await a.PutAsJsonAsync(Route(blair, f.FamilyHistoryUid), FamilyHistorySqlTests.Edit(saved)))
            Assert.Equal(HttpStatusCode.NotFound, wrongScope.StatusCode);
        using (var wrongArchive = await a.PostAsJsonAsync(Route(blair, f.FamilyHistoryUid) + "/archive", new { rowVersion = saved.RowVersion }))
            Assert.Equal(HttpStatusCode.NotFound, wrongArchive.StatusCode);
        using (var forbiddenCreate = await r.PostAsJsonAsync(Route(avery), FamilyHistorySqlTests.Draft()))
            Assert.Equal(HttpStatusCode.Forbidden, forbiddenCreate.StatusCode);
        using (var forbiddenUpdate = await r.PutAsJsonAsync(Route(avery, f.FamilyHistoryUid), FamilyHistorySqlTests.Edit(saved)))
            Assert.Equal(HttpStatusCode.Forbidden, forbiddenUpdate.StatusCode);
        using (var forbiddenArchive = await r.PostAsJsonAsync(Route(avery, f.FamilyHistoryUid) + "/archive", new { rowVersion = saved.RowVersion }))
            Assert.Equal(HttpStatusCode.Forbidden, forbiddenArchive.StatusCode);
        Assert.Equal(beforeA, await db.Snapshot(false)); Assert.Equal(beforeB, await db.Snapshot(true));

        var archiveFrom = await db.Utc(false);
        using var archive = await e.PostAsJsonAsync(Route(avery, f.FamilyHistoryUid) + "/archive", new { rowVersion = saved.RowVersion });
        Assert.Equal(HttpStatusCode.OK, archive.StatusCode);
        var archived = (await archive.Content.ReadFromJsonAsync<PatientFamilyHistoryResponse>())!;
        var archiveTo = await db.Utc(false);
        Assert.Equal(saved.CreatedAt, archived.CreatedAt); Assert.Equal(creator.Actor, archived.CreatedBy);
        Assert.Equal(editor.Actor, archived.UpdatedBy); Assert.NotEqual(saved.RowVersion, archived.RowVersion);
        Assert.InRange(archived.UpdatedAt!.Value, archiveFrom.AddSeconds(-1), archiveTo.AddSeconds(1));
        Assert.Equal("Archived", archived.Status);
        Assert.Equal(archived, await a.GetFromJsonAsync<PatientFamilyHistoryResponse>(Route(avery, f.FamilyHistoryUid)));
        Assert.Equal(archived, Assert.Single((await a.GetFromJsonAsync<PatientFamilyHistoryResponse[]>(Route(avery) + "?status=All"))!));
        var events = await db.Audit(false, f.FamilyHistoryUid);
        Assert.Equal(3, events.Count);
        CheckAudit(Assert.Single(events, x => x.Action == "Create"), creator, avery, f, null, createFrom, createTo);
        CheckAudit(Assert.Single(events, x => x.Action == "Update"), editor, avery, saved, f, updateFrom, updateTo);
        CheckAudit(Assert.Single(events, x => x.Action == "Archive"), editor, avery, archived, saved, archiveFrom, archiveTo);
        CheckAudit(Assert.Single(await db.Audit(true, localB.FamilyHistoryUid)), writerB, casey, localB, null, bFrom, bTo);
        Assert.Equal(beforeB, await db.Snapshot(true));
    }

    private static void CheckAudit(FamilyHistoryAcceptanceDatabase.AuditRow audit, FamilyHistoryAcceptanceDatabase.User user,
        Guid patient, PatientFamilyHistoryResponse after, PatientFamilyHistoryResponse? before, DateTime from, DateTime to)
    {
        Assert.Equal(after.FamilyHistoryUid, audit.Entity); Assert.Equal(patient, audit.Patient);
        Assert.Equal(user.Actor, audit.Actor); Assert.Equal(user.Subject, audit.Subject);
        // AuditLog datetime2(0) rounds to seconds; boundaries are rounded outward.
        Assert.InRange(audit.At, from.AddSeconds(-1), to.AddSeconds(1));
        if (before is null) Assert.Null(audit.Old); else ClinicalJson(before, audit.Old!);
        ClinicalJson(after, audit.New);
    }
    private static void ClinicalJson(PatientFamilyHistoryResponse row, string json)
    {
        using var document = JsonDocument.Parse(json); var value = document.RootElement;
        foreach (var (key, expected) in new Dictionary<string, string?>
        {
            ["Relationship"] = row.Relationship, ["ConditionDescription"] = row.ConditionDescription,
            ["AgeAtOnset"] = row.AgeAtOnset, ["LifeStageCode"] = row.LifeStageCode,
            ["Treatment"] = row.Treatment, ["Notes"] = row.Notes, ["Status"] = row.Status
        }) Assert.Equal(expected, value.GetProperty(key).GetString());
    }
    private static async Task Denied(HttpResponseMessage response, HttpStatusCode status, PatientFamilyHistoryResponse foreign)
    {
        Assert.Equal(status, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(foreign.ConditionDescription, content);
        Assert.DoesNotContain(foreign.FamilyHistoryUid.ToString(), content);
        Assert.DoesNotContain(foreign.RowVersion, content);
    }
    private static string Route(Guid patient, Guid? uid = null) => $"api/patients/{patient}/family-history" + (uid is null ? "" : $"/{uid}");
    private static async Task<PatientFamilyHistoryResponse> Create(HttpClient client, Guid patient, CreatePatientFamilyHistoryRequest draft)
    {
        using var response = await client.PostAsJsonAsync(Route(patient), draft);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PatientFamilyHistoryResponse>())!;
    }
}

public sealed class FamilyHistoryAcceptanceDatabase : IAsyncLifetime
{
    private readonly FamilyHistorySqlDatabase databases = new();
    private readonly Guid tenantA = Guid.NewGuid(), tenantB = Guid.NewGuid();
    internal sealed record User(string Subject, long Actor, bool CanWrite, bool CanRead = true);
    internal sealed record AuditRow(Guid Entity, Guid Patient, long Actor, string Subject, string Action, string? Old, string New, DateTime At);
    internal TenantContext Tenant(bool other) => new(other ? tenantB : tenantA, other ? "fixture-b" : "fixture-a", "Acceptance test tenant");
    internal async Task<SqlConnection> Open(bool other)
    {
        var connection = new SqlConnection(databases.TestConnection(other));
        try { await connection.OpenAsync(); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }
    public async Task InitializeAsync()
    {
        try
        {
            await databases.InitializeAsync();
            foreach (var other in new[] { false, true })
            {
                await using var connection = await Open(other);
                await using var schema = new SqlCommand("""
                    ALTER TABLE dbo.ApplicationUser ADD UserUid uniqueidentifier NOT NULL DEFAULT NEWID(), Username nvarchar(100) NOT NULL DEFAULT N'fixture';
                    CREATE TABLE dbo.TenantDatabaseIdentity(TenantUid uniqueidentifier NOT NULL);
                    INSERT dbo.TenantDatabaseIdentity VALUES(@Tenant);
                    """, connection);
                schema.Parameters.Add("@Tenant", SqlDbType.UniqueIdentifier).Value = Tenant(other).TenantUid;
                await schema.ExecuteNonQueryAsync();
                // Reuse the real subject lookup and mapping procedures, not a replacement lookup.
                var sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "database", "tenant-clinical", "migrations", "0018-clinical-user-auth-subject.sql"));
                foreach (var batch in Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(batch)) continue;
                    await using var command = new SqlCommand(batch, connection); await command.ExecuteNonQueryAsync();
                }
            }
        }
        catch { await databases.DisposeAsync(); throw; }
    }
    public Task DisposeAsync() => databases.DisposeAsync();
    internal async Task<Guid> SeedPatient(bool other)
    {
        var uid = Guid.NewGuid(); await using var connection = await Open(other);
        await using var command = new SqlCommand("INSERT dbo.Patient(PatientUid) VALUES(@Uid)", connection);
        command.Parameters.Add("@Uid", SqlDbType.UniqueIdentifier).Value = uid;
        await command.ExecuteNonQueryAsync(); return uid;
    }
    internal async Task<User> SeedUser(bool other, bool canWrite = true, bool canRead = true)
    {
        var subject = "step97-" + Guid.NewGuid().ToString("N");
        await using var connection = await Open(other);
        await using var command = new SqlCommand("""
            INSERT dbo.ApplicationUser(UserId,IsActive,DisplayName,AuthSubjectId)
            OUTPUT inserted.UserId
            SELECT MAX(UserId)+1,1,N'TEST ONLY acceptance user',@Subject FROM dbo.ApplicationUser;
            """, connection);
        command.Parameters.Add("@Subject", SqlDbType.NVarChar, 450).Value = subject;
        return new(subject, (long)(await command.ExecuteScalarAsync())!, canWrite, canRead);
    }
    internal async Task<DateTime> Utc(bool other)
    {
        await using var connection = await Open(other); await using var command = new SqlCommand("SELECT SYSUTCDATETIME()", connection);
        return (DateTime)(await command.ExecuteScalarAsync())!;
    }
    internal async Task<string> Snapshot(bool other)
    {
        await using var connection = await Open(other);
        await using var command = new SqlCommand("""
            SELECT (SELECT * FROM dbo.PatientFamilyMedicalHistory ORDER BY FamilyHistoryUid FOR JSON PATH,INCLUDE_NULL_VALUES) AS ClinicalRows,
                   (SELECT * FROM dbo.AuditLog ORDER BY AuditLogId FOR JSON PATH,INCLUDE_NULL_VALUES) AS AuditRows
            FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(); var chunks = new System.Text.StringBuilder();
        while (await reader.ReadAsync()) chunks.Append(reader.GetString(0)); return chunks.ToString();
    }
    internal async Task<int> ForeignRows(bool other, Guid uid)
    {
        await using var connection = await Open(other);
        await using var command = new SqlCommand("SELECT COUNT(*) FROM dbo.PatientFamilyMedicalHistory WHERE FamilyHistoryUid=@Uid", connection);
        command.Parameters.Add("@Uid", SqlDbType.UniqueIdentifier).Value = uid;
        return (int)(await command.ExecuteScalarAsync())!;
    }
    internal async Task<List<AuditRow>> Audit(bool other, Guid uid, string entityName = "PatientFamilyMedicalHistory")
    {
        await using var connection = await Open(other);
        await using var command = new SqlCommand("""
            SELECT TRY_CONVERT(uniqueidentifier,l.EntityId),p.PatientUid,l.UserId,u.AuthSubjectId,l.ActionName,l.OldValue,l.NewValue,l.CreatedAt
            FROM dbo.AuditLog l JOIN dbo.Patient p ON p.PatientId=l.PatientId
            JOIN dbo.ApplicationUser u ON u.UserId=l.UserId
            WHERE l.EntityName=@EntityName AND l.EntityId=CONVERT(nvarchar(100),@Uid)
            ORDER BY l.AuditLogId
            """, connection);
        command.Parameters.Add("@Uid", SqlDbType.UniqueIdentifier).Value = uid;
        command.Parameters.Add("@EntityName", SqlDbType.NVarChar, 100).Value = entityName;
        await using var reader = await command.ExecuteReaderAsync(); var rows = new List<AuditRow>();
        while (await reader.ReadAsync()) rows.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetInt64(2), reader.GetString(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetString(6), reader.GetDateTime(7)));
        return rows;
    }
    internal ITenantSqlConnectionFactory Factory(bool other) => new TenantSqlConnectionFactory(Tenant(other),
        new Assignment(this, other), new Secret(databases.TestConnection(other)), NullLogger<TenantSqlConnectionFactory>.Instance);
    private sealed class Assignment(FamilyHistoryAcceptanceDatabase db, bool other) : ITenantDatabaseResolver
    {
        public Task<TenantDatabaseInfo?> ResolveAsync(Guid tenantUid, CancellationToken cancellationToken = default)
        {
            var name = new SqlConnectionStringBuilder(db.databases.TestConnection(other)).InitialCatalog;
            return Task.FromResult<TenantDatabaseInfo?>(tenantUid == db.Tenant(other).TenantUid ? new(tenantUid, "localdb-fixture", name, "test-only", "Active") : null);
        }
    }
    private sealed class Secret(string connection) : ITenantDatabaseSecretProvider
    {
        public Task<TenantDatabaseSecret> ResolveAsync(string secretReference, CancellationToken cancellationToken = default) => Task.FromResult(new TenantDatabaseSecret(connection));
    }
}

internal sealed class FamilyHistoryAcceptanceHost(WebApplication app) : IAsyncDisposable
{
    internal static async Task<FamilyHistoryAcceptanceHost> Start(FamilyHistoryAcceptanceDatabase db, bool other, params FamilyHistoryAcceptanceDatabase.User[] users)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
        builder.Services.AddSingleton(users); builder.Services.AddHttpContextAccessor();
        builder.Services.AddAuthentication("FamilyAcceptance").AddScheme<AuthenticationSchemeOptions, Authentication>("FamilyAcceptance", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        builder.Services.AddScoped<ICurrentUserPermissionService, Permissions>();
        builder.Services.AddScoped<ITenantSqlConnectionFactory>(_ => db.Factory(other));
        builder.Services.AddScoped<IPatientFamilyHistoryRepository, PatientFamilyHistoryRepository>();
        builder.Services.AddScoped<IPatientFamilyHistoryService, PatientFamilyHistoryService>();
        builder.Services.AddScoped<MicroEMR.Application.PatientRiskFactors.IPatientRiskFactorRepository, MicroEMR.Infrastructure.PatientRiskFactors.PatientRiskFactorRepository>();
        builder.Services.AddScoped<MicroEMR.Application.PatientRiskFactors.IPatientRiskFactorService, MicroEMR.Application.PatientRiskFactors.PatientRiskFactorService>();
        builder.Services.AddScoped<IClinicalUserRepository, ClinicalUserRepository>();
        builder.Services.AddScoped<IAuthenticatedClinicalUserAccessor, AuthenticatedClinicalUserAccessor>();
        builder.Services.AddScoped<ITenantContextAccessor>(_ => { var accessor = new TenantContextAccessor(); accessor.SetTenant(db.Tenant(other)); return accessor; });
        var audit = DispatchProxy.Create<IPlatformSecurityAuditRepository, CppSelectivePrintTests.Proxy>();
        ((CppSelectivePrintTests.Proxy)(object)audit).Call = (_, _) => throw new InvalidOperationException("Unexpected platform audit path in family-history fixture.");
        builder.Services.AddSingleton(audit);
        builder.Services.AddControllers().AddApplicationPart(typeof(MicroEMR.Api.Controllers.PatientFamilyHistoryController).Assembly);
        var app = builder.Build();
        app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
        app.UseMiddleware<ClinicalUserActorResolutionMiddleware>(); app.MapControllers();
        try { await app.StartAsync(); return new(app); }
        catch { await app.DisposeAsync(); throw; }
    }
    internal HttpClient Client(FamilyHistoryAcceptanceDatabase.User user)
    {
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var client = new HttpClient { BaseAddress = new Uri(address) };
        client.DefaultRequestHeaders.Authorization = new("Bearer", user.Subject); return client;
    }
    public async ValueTask DisposeAsync() { await app.StopAsync(); await app.DisposeAsync(); }
    private sealed class Authentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder, FamilyHistoryAcceptanceDatabase.User[] users) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var subject = Request.Headers.Authorization.ToString();
            var user = users.SingleOrDefault(x => subject == "Bearer " + x.Subject);
            if (user is null) return Task.FromResult(AuthenticateResult.Fail("Unknown fixture user"));
            var claims = new[] { new Claim("sub", user.Subject), new Claim("fixture-write", user.CanWrite.ToString()), new Claim("fixture-read", user.CanRead.ToString()) };
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }
    private sealed class Permissions(IHttpContextAccessor context) : ICurrentUserPermissionService
    {
        public Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(CancellationToken token = default)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            if (context.HttpContext!.User.Identity?.IsAuthenticated == true)
            {
                if (context.HttpContext.User.FindFirst("fixture-read")?.Value == "True") keys.Add(PermissionKeys.PatientsView);
                if (context.HttpContext.User.FindFirst("fixture-write")?.Value == "True") keys.Add(PermissionKeys.ClinicalDataManage);
            }
            return Task.FromResult<IReadOnlySet<string>>(keys);
        }
        public async Task<bool> HasPermissionAsync(string key, CancellationToken token = default) => (await GetEffectivePermissionsAsync(token)).Contains(key);
    }
}
