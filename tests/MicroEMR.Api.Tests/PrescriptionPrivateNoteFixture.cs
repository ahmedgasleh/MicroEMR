using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using MicroEMR.Infrastructure.SecurityAudit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Api.Middleware;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Application.ReadAudit;
using MicroEMR.Application.SecurityAudit;
using MicroEMR.Application.Tenancy;
using MicroEMR.Application.TenantUserAdministration;
using MicroEMR.Infrastructure.ClinicalUsers;
using MicroEMR.Infrastructure.PatientPrescriptions;
using MicroEMR.Infrastructure.ReadAudit;
using MicroEMR.Infrastructure.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

// Real isolated Platform + two tenant clinical databases; never application tenants.
public sealed class PrescriptionPrivateNoteFixture : IAsyncLifetime
{
    internal PrescriptionDateDatabase Db { get; } = new();
    private readonly string platformName = "MicroEMR_Step121_" + Guid.NewGuid().ToString("N");
    internal string PlatformConnection { get; private set; } = "";
    internal CaptureLogs Logs { get; } = new();
    internal static string Sql(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "database", name));
    internal static async Task Batches(SqlConnection c, string sql)
    {
        foreach (var batch in Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            if (!string.IsNullOrWhiteSpace(batch)) { await using var q = new SqlCommand(batch, c); await q.ExecuteNonQueryAsync(); }
    }
    public async Task InitializeAsync()
    {
        await Db.InitializeAsync();
        foreach (var other in new[] { false, true })
        {
            await using var c = await Db.Factory(other).OpenConnectionAsync();
            await Batches(c, """
                UPDATE dbo.ApplicationUser SET AuthSubjectId=CASE UserId WHEN 7 THEN N'writer' WHEN 8 THEN N'other' ELSE N'inactive' END;
                INSERT dbo.ApplicationUser(UserId,IsActive,AuthSubjectId) VALUES(10,1,N'staff'),(11,1,N'chart'),(12,1,N'no-private'),(13,1,N'inactive-membership'),(14,1,N'no-view');
                UPDATE dbo.ApplicationUser SET DisplayName=COALESCE(DisplayName,N'TEST ONLY staff');
                """);
        }
        await using var tenant = await Db.Factory().OpenConnectionAsync();
        var master = new SqlConnectionStringBuilder(tenant.ConnectionString) { InitialCatalog = "master" };
        PlatformConnection = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = platformName }.ConnectionString;
        await using (var c = new SqlConnection(master.ConnectionString))
        { await c.OpenAsync(); await using var q = new SqlCommand($"CREATE DATABASE [{platformName}]", c); await q.ExecuteNonQueryAsync(); }
        await using var platform = new SqlConnection(PlatformConnection); await platform.OpenAsync();
        await Batches(platform, """
            CREATE TABLE dbo.UserTenantMembership(UserId nvarchar(450),TenantUid uniqueidentifier,MembershipStatus nvarchar(20),UpdatedAt datetime2,RowVersion rowversion);
            CREATE TABLE dbo.AccessProfile(AccessProfileUid uniqueidentifier PRIMARY KEY,TenantUid uniqueidentifier,Name nvarchar(100),IsActive bit,UpdatedBy nvarchar(450),UpdatedAt datetime2,RowVersion rowversion);
            CREATE TABLE dbo.AccessProfilePermission(AccessProfileUid uniqueidentifier,PermissionKey nvarchar(100));
            CREATE TABLE dbo.UserTenantAccessProfile(UserId nvarchar(450),TenantUid uniqueidentifier,AccessProfileUid uniqueidentifier);
            CREATE TABLE dbo.UserPermissionOverride(TenantUid uniqueidentifier,UserId nvarchar(450),PermissionKey nvarchar(100) CONSTRAINT CK_UserPermissionOverride_Key CHECK(PermissionKey<>N''),OverrideState varchar(7),CreatedBy nvarchar(450),UpdatedBy nvarchar(450),UpdatedAt datetime2);
            CREATE TABLE dbo.PlatformAuditEvent(Id uniqueidentifier,Actor nvarchar(450),ActorKind nvarchar(50),ActionName nvarchar(100),TenantUid uniqueidentifier,UserId nvarchar(450),Outcome nvarchar(30),CreatedAt datetime2,Correlation uniqueidentifier,Metadata nvarchar(max));
            GO
            CREATE FUNCTION dbo.AccessManagementAdministrator(@TenantUid uniqueidentifier) RETURNS TABLE AS RETURN
            (SELECT UserId FROM dbo.UserTenantMembership WHERE TenantUid=@TenantUid AND UserId=N'writer' AND MembershipStatus=N'Active');
            GO
            """);
        var effective = Regex.Match(Sql("platform/012_user_permission_overrides.sql"), @"(?s)CREATE OR ALTER PROCEDURE dbo.AccessProfile_GetEffective.*?(?=\r?\nGO)").Value;
        await Batches(platform, effective);
        foreach(var file in new[]{"014_platform_security_denial_audit.sql","015_platform_cross_patient_security_audit.sql","016_platform_unresolved_actor_security_audit.sql","017_platform_tenant_security_audit.sql"})
            await Batches(platform, Regex.Replace(Sql("platform/"+file),@"(?im)^USE MicroEMR_Platform;\s*$",""));
        await Batches(platform, Sql("platform/027_private_prescription_note_permission.sql"));
        foreach (var other in new[] { false, true })
        foreach (var subject in new[] { "writer", "other", "staff", "chart", "no-private", "inactive", "unresolved", "inactive-membership", "no-view" })
        {
            await using var q = new SqlCommand("INSERT dbo.UserTenantMembership(UserId,TenantUid,MembershipStatus) VALUES(@Subject,@Tenant,@Status);", platform);
            q.Parameters.AddWithValue("@Subject", subject); q.Parameters.AddWithValue("@Tenant", Db.Tenant(other).TenantUid);
            q.Parameters.AddWithValue("@Status", subject == "inactive-membership" ? "Inactive" : "Active"); await q.ExecuteNonQueryAsync();
            var keys = new List<string>();
            if (subject != "no-view") keys.Add(PermissionKeys.PatientsView);
            if (subject is not ("chart" or "staff")) keys.Add(PermissionKeys.PrescriptionsPrescribe);
            if (subject is not ("chart" or "no-private")) keys.Add(PermissionKeys.PrescriptionsViewPrivateNotes);
            foreach (var key in keys)
            {
                await using var grant = new SqlCommand("INSERT dbo.UserPermissionOverride(TenantUid,UserId,PermissionKey,OverrideState) VALUES(@Tenant,@Subject,@Key,'Allow')", platform);
                grant.Parameters.AddWithValue("@Tenant", Db.Tenant(other).TenantUid); grant.Parameters.AddWithValue("@Subject", subject); grant.Parameters.AddWithValue("@Key", key); await grant.ExecuteNonQueryAsync();
            }
        }
    }
    internal Task<PrivateNoteHost> Host(bool other = false) => PrivateNoteHost.Start(this, other);
    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        if (!Regex.IsMatch(platformName, "^MicroEMR_Step121_[a-f0-9]{32}$")) throw new InvalidOperationException("Unsafe fixture name.");
        if (PlatformConnection.Length > 0)
        {
            var master = new SqlConnectionStringBuilder(PlatformConnection) { InitialCatalog = "master" };
            await using var c = new SqlConnection(master.ConnectionString); await c.OpenAsync();
            await using var q = new SqlCommand($"IF DB_ID(N'{platformName}') IS NOT NULL BEGIN ALTER DATABASE [{platformName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{platformName}]; END", c); await q.ExecuteNonQueryAsync();
        }
        await Db.DisposeAsync();
    }
    internal sealed class CaptureLogs : ILoggerProvider
    {
        internal readonly System.Collections.Concurrent.ConcurrentQueue<string> Messages = new();
        public ILogger CreateLogger(string categoryName) => new Sink(this);
        public void Dispose() { }
        private sealed class Sink(CaptureLogs owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => owner.Messages.Enqueue(formatter(state, exception) + exception?.ToString());
        }
    }
}

internal sealed class PrivateNoteHost(WebApplication app) : IAsyncDisposable
{
    internal static async Task<PrivateNoteHost> Start(PrescriptionPrivateNoteFixture fixture, bool other)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders(); builder.Logging.AddProvider(fixture.Logs);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:PlatformDatabase"] = fixture.PlatformConnection });
        builder.Services.AddHttpContextAccessor(); builder.Services.AddProblemDetails(); builder.Services.AddExceptionHandler<SafeApiExceptionHandler>();
        builder.Services.AddSingleton(CppSelectivePrintTests.Stub<IAdministrativePlatformConnectionStringProvider>((_,_)=>
            throw new InvalidOperationException("Administrative platform writes are not expected in this isolated request fixture.")));
        builder.Services.AddAuthentication("Private").AddScheme<AuthenticationSchemeOptions, Authentication>("Private", _ => { }); builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>(); builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>(); builder.Services.AddScoped<IAuthorizationMiddlewareResultHandler, MissingPermissionAuthorizationResultHandler>();
        builder.Services.AddScoped<IAuthenticatedSubjectAccessor, Subject>(); builder.Services.AddScoped<IAccessProfileRepository, SqlAccessProfileRepository>(); builder.Services.AddScoped<ICurrentUserPermissionService, CurrentUserPermissionService>();
        builder.Services.AddScoped<ITenantSqlConnectionFactory>(_ => fixture.Db.Factory(other));
        builder.Services.AddScoped<IPatientPrescriptionRepository, PatientPrescriptionRepository>(); builder.Services.AddScoped<IPrescriptionOutputRepository, PatientPrescriptionRepository>();
        builder.Services.AddScoped<IPrescriptionPrivateNoteRepository, PrescriptionPrivateNoteRepository>(); builder.Services.AddScoped<IPrescriptionPrivateNoteService, PrescriptionPrivateNoteService>(); builder.Services.AddScoped<IPrescriptionPrivateNoteRequestContext, PrescriptionPrivateNoteRequestContext>();
        builder.Services.AddScoped<IPdfRenderer, PrescriptionDatePrecisionTests.Capture>(); builder.Services.AddScoped<IPrescriptionPdfRenderer, PrescriptionDatePrecisionTests.Capture>(); builder.Services.AddSingleton(TimeProvider.System); builder.Services.AddScoped<IPatientPrescriptionService, PatientPrescriptionService>(); builder.Services.AddScoped<PrescriptionOutputService>();
        builder.Services.AddScoped<IPatientChartReadAuditService, PatientChartReadAuditService>(); builder.Services.AddScoped<IReadAuditRepository, ReadAuditRepository>();
        builder.Services.AddSingleton(CppSelectivePrintTests.Stub<IAdministrativeTenantSqlConnectionFactory>((_,_)=>
            throw new InvalidOperationException("Administrative writes are not expected in this isolated request fixture.")));
        builder.Services.AddScoped<IClinicalUserRepository, ClinicalUserRepository>(); builder.Services.AddScoped<IAuthenticatedClinicalUserAccessor, AuthenticatedClinicalUserAccessor>();
        builder.Services.AddScoped<ITenantContextAccessor>(_ => { var accessor = new TenantContextAccessor(); accessor.SetTenant(fixture.Db.Tenant(other)); return accessor; });
        builder.Services.AddScoped<IPlatformSecurityAuditRepository, SqlPlatformSecurityAuditRepository>();
        builder.Services.AddControllers().AddApplicationPart(typeof(MicroEMR.Api.Controllers.PatientPrescriptionsController).Assembly);
        var app = builder.Build(); app.UseExceptionHandler(); app.UseRouting(); app.UseAuthentication();
        app.UseMiddleware<PrescriptionPrivateNoteDeniedAuditMiddleware>(); app.UseAuthorization(); app.UseMiddleware<ClinicalUserActorResolutionMiddleware>(); app.MapControllers(); await app.StartAsync(); return new(app);
    }
    internal HttpClient Client(string subject = "writer")
    {
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var client = new HttpClient { BaseAddress = new(address) }; client.DefaultRequestHeaders.Authorization = new("Bearer", subject); return client;
    }
    public async ValueTask DisposeAsync() { await app.StopAsync(); await app.DisposeAsync(); }
    private sealed class Subject(IHttpContextAccessor http) : IAuthenticatedSubjectAccessor
    { public string GetRequiredSubject() => http.HttpContext!.User.FindFirstValue("sub")!; }
    private sealed class Authentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var subject = Request.Headers.Authorization.ToString().Replace("Bearer ", "");
            if (subject.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", subject)], Scheme.Name)), Scheme.Name)));
        }
    }
}
