using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using MicroEMR.Application.Scheduling;
using MicroEMR.Application.Scheduling.Services;
using Xunit;

namespace MicroEMR.Api.Tests;

// Opt-in acceptance only. Normal test runs never connect to the persistent database or open a browser.
public sealed class SchedulingStatusLiveAcceptance
{
    [AuthorizedStatusAcceptanceFact]
    public async Task DashboardPolicyAndRealPersistenceRace()
    {
        var path = Environment.GetEnvironmentVariable("MICROEMR_STATUS_ACCEPTANCE_CONFIG")!;
        var options = JsonSerializer.Deserialize<StatusAcceptanceOptions>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        options.Validate(Environment.GetEnvironmentVariable("MICROEMR_STATUS_ACCEPTANCE_CONSENT"));
        var sql = Environment.GetEnvironmentVariable("MICROEMR_STATUS_ACCEPTANCE_SQL")
            ?? throw new InvalidOperationException("Provide the explicitly approved SQL observer/concurrency connection in the environment, never in the configuration file.");
        StatusAcceptanceOptions.ValidateConnection(sql);
        var observer = new StatusAcceptanceObserver(sql);
        await observer.Preflight(options);

        var receipt = new StatusAcceptanceReceipt();
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        if (root is null) throw new InvalidOperationException("Repository evidence directory is unavailable.");
        var output = Path.Combine(root.FullName, ".tmp", "status-live-acceptance", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        void Save() => File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }));
        async Task Check(string name, Func<Task> action)
        {
            try { await action(); receipt.Checks.Add(new(name, "PASS")); Save(); }
            catch { receipt.Checks.Add(new(name, "FAIL — inspect the runner failure locally; no exception/response contents are persisted")); Save(); throw; }
        }
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = false, Channel = "chrome" });
        var managerGate = new StatusAcceptanceRequestGate();
        var viewerGate = new StatusAcceptanceRequestGate();
        await using var manager = await browser.NewContextAsync();
        await using var viewer = await browser.NewContextAsync();
        // Contexts are isolated and never exported as storageState, HAR, trace, cookies or tokens.
        await manager.RouteAsync("**/*", route => Guard(route, managerGate));
        await viewer.RouteAsync("**/*", route => Guard(route, viewerGate));
        var managerPage = await manager.NewPageAsync();
        var viewerPage = await viewer.NewPageAsync();
        Save();
        try
        {
            Console.WriteLine("Sign into the first browser window using the approved manager and select local-dev-fresh. No password is read by the harness.");
            await SignIn(managerPage);
            receipt.Audiences["manager"] = await observer.VerifyAudience(managerPage, "admin@microemr.local", true, options.ManagerActorId); Save();
            Console.WriteLine("Sign into the second window as the explicitly selected viewer (rx.tenantb@example.invalid for this checkpoint) and select local-dev-fresh. Do not change permissions.");
            await SignIn(viewerPage);
            receipt.Audiences["viewer"] = await observer.VerifyAudience(viewerPage, options.ExpectedViewerEmail, false, null); Save();
            await Check("normal-manager-and-viewer-tenant-selection", async () =>
            {
                await Tenant(managerPage); await Tenant(viewerPage);
                await Assertions.Expect(Selector(managerPage, options.ScheduledAppointmentUid)).ToHaveValueAsync(options.ResumeRemaining ? "Arrived" : "Scheduled");
                await Assertions.Expect(Selector(managerPage, options.CompletedAppointmentUid)).ToHaveValueAsync("Completed");
                await Assertions.Expect(Selector(managerPage, options.RaceAppointmentUid)).ToHaveValueAsync("Scheduled");
            });
            if (!options.ResumeRemaining)
            {
                await Check("Scheduled-to-Completed-rejected-and-dropdown-reverted", () => Rejected(managerPage, managerGate, observer, options.ScheduledAppointmentUid, "Scheduled", "Completed"));
                await Check("Completed-to-Scheduled-rejected-and-dropdown-reverted", () => Rejected(managerPage, managerGate, observer, options.CompletedAppointmentUid, "Completed", "Scheduled"));
            }
            var allowedSource = options.ResumeRemaining ? "Arrived" : "Scheduled";
            var allowedTarget = options.ResumeRemaining ? "Seen" : "Arrived";
            new AppointmentStatusTransitionService().EnsureCanTransition(
                options.ResumeRemaining ? AppointmentStatus.Arrived : AppointmentStatus.Scheduled,
                options.ResumeRemaining ? AppointmentStatus.Seen : AppointmentStatus.Arrived);
            // Do the viewer probe before either intentionally successful state change.
            await Check("viewer-direct-Web-request-denied-without-success-record", async () =>
            {
                var before = await observer.Snapshot(options.ScheduledAppointmentUid);
                receipt.Audiences["viewer"] = await observer.VerifyAudience(viewerPage, options.ExpectedViewerEmail, false, null); Save();
                viewerGate.Arm(options.ScheduledAppointmentUid, allowedTarget);
                var response = viewerPage.WaitForResponseAsync(IsStatusResponse);
                await viewerPage.EvaluateAsync("""
                    async ({uid,target}) => {
                        const token = document.querySelector('#dashboardAntiforgeryForm input[name="__RequestVerificationToken"]')?.value;
                        if (!token) throw new Error('Dashboard antiforgery token unavailable');
                        await fetch('/Home/UpdateAppointmentStatus', {method:'POST', headers:{'Content-Type':'application/x-www-form-urlencoded','RequestVerificationToken':token},
                            body:new URLSearchParams({AppointmentUid:uid,Status:target})});
                    }
                    """, new { uid = options.ScheduledAppointmentUid.ToString(), target = allowedTarget });
                var result = await response;
                receipt.HttpStatuses["viewer"] = result.Status;
                receipt.Unchanged["viewer"] = before == await observer.Snapshot(options.ScheduledAppointmentUid); Save();
                Assert.Equal(403, result.Status); // A 401/login or proxy 502 is not relabelled viewer-denial PASS.
                Assert.True(before == await observer.Snapshot(options.ScheduledAppointmentUid), "Viewer request changed appointment, success audit or history.");
            });
            await Check("valid-manager-transition-one-attributed-audit-and-history-and-refreshed-status", async () =>
            {
                var before = await observer.Snapshot(options.ScheduledAppointmentUid);
                var result = await Select(managerPage, managerGate, options.ScheduledAppointmentUid, allowedTarget);
                receipt.HttpStatuses["allowed-transition"] = result.Status; Save();
                Assert.Equal(200, result.Status);
                var body = await result.JsonAsync(); Assert.True(body!.Value.GetProperty("success").GetBoolean());
                await observer.AssertChange(before, options.ScheduledAppointmentUid, allowedSource, allowedTarget, options.ManagerActorId);
                await managerPage.ReloadAsync(); await Tenant(managerPage);
                await Assertions.Expect(Selector(managerPage, options.ScheduledAppointmentUid)).ToHaveValueAsync(allowedTarget);
            });
            await Check("actual-HTTP-409-after-server-validation-and-competing-persistence-write", async () =>
            {
                var before = await observer.Snapshot(options.RaceAppointmentUid);
                await StatusAcceptanceRace.Run(sql, options.RaceAppointmentUid, options.ManagerActorId,
                    async () =>
                    {
                        var result = await Select(managerPage, managerGate, options.RaceAppointmentUid, "Arrived");
                        receipt.HttpStatuses["concurrency"] = result.Status; Save();
                        Assert.Equal(409, result.Status);
                        await SafeConflict(result);
                        await Assertions.Expect(Selector(managerPage, options.RaceAppointmentUid)).ToHaveValueAsync("Scheduled");
                    });
                await observer.AssertChange(before, options.RaceAppointmentUid, "Scheduled", "Seen", options.ManagerActorId);
                await managerPage.ReloadAsync(); await Tenant(managerPage);
                await Assertions.Expect(Selector(managerPage, options.RaceAppointmentUid)).ToHaveValueAsync("Seen");
            });
            Assert.Equal(0, managerGate.BlockedWrites + viewerGate.BlockedWrites);
            receipt.Outcome = "PASS";
        }
        catch
        {
            throw new InvalidOperationException("Live acceptance did not complete. See the sanitized results.json and inspect only the approved fixtures; raw responses and exceptions are not attached.");
        }
        finally
        {
            receipt.BlockedWrites = managerGate.BlockedWrites + viewerGate.BlockedWrites;
            // Preserve evidence of partial runs; never reset statuses or delete audit/history.
            foreach (var uid in options.Appointments)
            {
                try { receipt.FinalStatuses[uid.ToString()] = (await observer.Snapshot(uid)).Status; }
                catch { receipt.FinalStatuses[uid.ToString()] = "UNAVAILABLE"; }
            }
            Save();
            Console.WriteLine("Sanitized acceptance receipt: " + output);
        }
    }

    private static Task Guard(IRoute route, StatusAcceptanceRequestGate gate)
    {
        var request = route.Request;
        var uri = new Uri(request.Url);
        // Never retrieve login/OIDC request bodies: only the armed scheduling form needs inspection.
        var body = uri.GetLeftPart(UriPartial.Authority) == StatusAcceptanceOptions.Web
            && uri.AbsolutePath == "/Home/UpdateAppointmentStatus" && request.Method == "POST" ? request.PostData : null;
        return gate.Allow(request.Url, request.Method, body) ? route.ContinueAsync() : route.AbortAsync();
    }
    private static ILocator Selector(IPage page, Guid uid) => page.Locator($"select.appointment-status-select[data-appointment-uid='{uid}']");
    private static bool IsStatusResponse(IResponse response) => new Uri(response.Url).AbsolutePath == "/Home/UpdateAppointmentStatus" && response.Request.Method == "POST";
    private static async Task SignIn(IPage page)
    {
        page.SetDefaultTimeout(15000);
        await page.BringToFrontAsync();
        await page.GotoAsync(StatusAcceptanceOptions.Web + "/");
        await page.WaitForURLAsync(url => new Uri(url).GetLeftPart(UriPartial.Authority) == StatusAcceptanceOptions.Web && !new Uri(url).AbsolutePath.StartsWith("/Account/", StringComparison.Ordinal), new() { Timeout = 300000 });
        await page.Locator("meta[name='microemr-tenant-id']").WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 300000 });
        await Tenant(page);
    }
    private static async Task Tenant(IPage page) => Assert.Equal(StatusAcceptanceOptions.TenantUid.ToString(),
        (await page.Locator("meta[name='microemr-tenant-id']").GetAttributeAsync("content"))?.ToLowerInvariant());
    private static async Task<IResponse> Select(IPage page, StatusAcceptanceRequestGate gate, Guid uid, string target)
    {
        await Tenant(page);
        gate.Arm(uid, target);
        var response = page.WaitForResponseAsync(IsStatusResponse, new() { Timeout = 30000 });
        await Selector(page, uid).SelectOptionAsync(target);
        var result = await response;
        await Assertions.Expect(Selector(page, uid)).ToBeEnabledAsync();
        return result;
    }
    private static async Task SafeConflict(IResponse response)
    {
        var text = await response.TextAsync();
        StatusAcceptanceAssertions.SafeConflict(response.Status, text);
    }
    private static async Task Rejected(IPage page, StatusAcceptanceRequestGate gate, StatusAcceptanceObserver observer, Guid uid, string source, string target)
    {
        var before = await observer.Snapshot(uid);
        var result = await Select(page, gate, uid, target);
        await SafeConflict(result);
        await Assertions.Expect(Selector(page, uid)).ToHaveValueAsync(source);
        Assert.True(before == await observer.Snapshot(uid), "Rejected request changed appointment, success audit or history.");
    }
}

public sealed class AuthorizedStatusAcceptanceFactAttribute : FactAttribute
{
    public AuthorizedStatusAcceptanceFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("MICROEMR_STATUS_ACCEPTANCE_CONSENT") != StatusAcceptanceOptions.Consent)
            Skip = "Requires separately authorized persistent writes, existing disposable appointments, normal interactive sessions and the documented configuration.";
    }
}
public sealed class StatusAcceptanceOptions
{
    public const string Web = "https://localhost:7002";
    public const string Auth = "https://localhost:7179";
    public const string Consent = "OWNER_APPROVED_STATUS_ACCEPTANCE_WRITES";
    public static readonly Guid TenantUid = Guid.Parse("f94c6ecd-4417-483e-8a94-0507cf8384c2");
    public Guid ScheduledAppointmentUid { get; set; }
    public Guid CompletedAppointmentUid { get; set; }
    public Guid RaceAppointmentUid { get; set; }
    public long ManagerActorId { get; set; }
    public bool ResumeRemaining { get; set; }
    public string ExpectedViewerEmail { get; set; } = "";
    public bool ExistingAppointmentsApproved { get; set; }
    public bool DirectCompetingStoredProcedureWriteApproved { get; set; }
    public Guid[] Appointments => [ScheduledAppointmentUid, CompletedAppointmentUid, RaceAppointmentUid];
    public void Validate(string? consent)
    {
        if (consent != Consent || !ExistingAppointmentsApproved || !DirectCompetingStoredProcedureWriteApproved)
            throw new InvalidOperationException("Explicit appointment-write and concurrency-setup authorization is required; configuration is not owner authorization by itself.");
        if (Appointments.Any(x => x == Guid.Empty) || Appointments.Distinct().Count() != 3 || ManagerActorId <= 0)
            throw new InvalidOperationException("Provide three distinct approved appointment UIDs and the expected manager clinical actor ID.");
    }
    public static void ValidateConnection(string connection)
    {
        var builder = new SqlConnectionStringBuilder(connection);
        if (builder.InitialCatalog != "MicroEMR_LocalDev_Fresh" || builder.DataSource is not ("192.168.50.1,50013" or "EC2AMAZ-TC26CQK\\TELEMED52"))
            throw new InvalidOperationException("Only the named development database and endpoint are allowed.");
    }
}
public sealed class StatusAcceptanceRequestGate
{
    private (Guid Uid, string Status)? armed;
    public int BlockedWrites { get; private set; }
    public void Arm(Guid uid, string status)
    {
        if (armed.HasValue) throw new InvalidOperationException("An earlier status request was not consumed.");
        armed = (uid, status);
    }
    public bool Allow(string url, string method, string? body)
    {
        var uri = new Uri(url);
        var origin = uri.GetLeftPart(UriPartial.Authority);
        if (origin is not (StatusAcceptanceOptions.Web or StatusAcceptanceOptions.Auth))
        { if (method is not ("GET" or "HEAD")) BlockedWrites++; return false; }
        if (method is "GET" or "HEAD")
        {
            // Do not let accidental chart navigation create unrelated read-audit activity.
            var path = uri.AbsolutePath;
            return (origin == StatusAcceptanceOptions.Web && path is "/" or "/Home" or "/Home/Index" or "/Account/Login" or "/signin-oidc")
                || (origin == StatusAcceptanceOptions.Auth && path is "/Account/Login" or "/Account/SelectTenant" or "/connect/authorize")
                || path.StartsWith("/lib/", StringComparison.Ordinal) || path.StartsWith("/dist/", StringComparison.Ordinal)
                || path.StartsWith("/css/", StringComparison.Ordinal) || path.StartsWith("/js/", StringComparison.Ordinal)
                || path.StartsWith("/images/", StringComparison.Ordinal) || path.StartsWith("/fonts/", StringComparison.Ordinal) || path == "/favicon.ico";
        }
        if (method == "POST" && ((origin == StatusAcceptanceOptions.Auth && uri.AbsolutePath is "/Account/Login" or "/Account/SelectTenant" or "/connect/authorize")
            || (origin == StatusAcceptanceOptions.Web && uri.AbsolutePath == "/signin-oidc"))) return true;
        if (method == "POST" && origin == StatusAcceptanceOptions.Web && uri.AbsolutePath == "/Home/UpdateAppointmentStatus" && armed is { } expected)
        {
            var fields = FormFields(body ?? "");
            if (fields.TryGetValue("AppointmentUid", out var value) && Guid.TryParse(value, out var uid) && uid == expected.Uid
                && fields.GetValueOrDefault("Status") == expected.Status)
            { armed = null; return true; }
        }
        BlockedWrites++; return false;
    }
    private static Dictionary<string, string> FormFields(string body)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        if (body.StartsWith("--", StringComparison.Ordinal))
        {
            foreach (Match match in Regex.Matches(body, "name=\"(AppointmentUid|Status)\"\\r?\\n\\r?\\n([^\\r\\n]*)")) fields[match.Groups[1].Value] = match.Groups[2].Value;
        }
        else foreach (var pair in body.Split('&'))
        {
            var split = pair.IndexOf('=');
            if (split > 0) fields[Uri.UnescapeDataString(pair[..split].Replace('+', ' '))] = Uri.UnescapeDataString(pair[(split + 1)..].Replace('+', ' '));
        }
        return fields;
    }
}
public static class StatusAcceptanceAssertions
{
    public static void SafeConflict(int status, string body)
    {
        Assert.Equal(409, status);
        using var document = JsonDocument.Parse(body);
        Assert.False(document.RootElement.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("message").GetString()));
        foreach (var forbidden in new[] { "SqlException", "StackTrace", " at MicroEMR.", "Set-Cookie", "Authorization:", "D:\\MasterProgram" })
            Assert.DoesNotContain(forbidden, body, StringComparison.OrdinalIgnoreCase);
    }
}
public sealed record StatusAcceptanceSnapshot(string Status, string State, string Audit, string History);
public sealed class StatusAcceptanceObserver(string connectionString)
{
    public async Task Preflight(StatusAcceptanceOptions options)
    {
        await using var connection = new SqlConnection(connectionString); await connection.OpenAsync();
        await using var query = new SqlCommand("""
            SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),DB_NAME(),
                (SELECT COUNT(*) FROM dbo.TenantDatabaseIdentity),
                (SELECT TenantUid FROM dbo.TenantDatabaseIdentity),
                (SELECT COUNT(*) FROM sys.parameters WHERE object_id=OBJECT_ID('dbo.ScheduleAppointment_UpdateStatus') AND name='@ExpectedStatus'),
                (SELECT COUNT(*) FROM dbo.ApplicationUser WHERE UserId=@actor AND IsActive=1),
                CASE WHEN HAS_PERMS_BY_NAME(NULL,NULL,'VIEW SERVER STATE')=1 OR HAS_PERMS_BY_NAME(NULL,NULL,'VIEW SERVER PERFORMANCE STATE')=1 THEN 1 ELSE 0 END;
            """, connection);
        query.Parameters.AddWithValue("@actor", options.ManagerActorId);
        await using (var reader = await query.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync());
            Assert.Equal("EC2AMAZ-TC26CQK\\TELEMED52", reader.GetString(0)); Assert.Equal("MicroEMR_LocalDev_Fresh", reader.GetString(1));
            Assert.Equal(1, reader.GetInt32(2)); Assert.Equal(StatusAcceptanceOptions.TenantUid, reader.GetGuid(3));
            Assert.Equal(1, reader.GetInt32(4)); Assert.Equal(1, reader.GetInt32(5)); Assert.Equal(1, reader.GetInt32(6));
        }
        foreach (var uid in options.Appointments) Assert.Equal(uid == options.CompletedAppointmentUid ? "Completed" : options.ResumeRemaining && uid == options.ScheduledAppointmentUid ? "Arrived" : "Scheduled", (await Snapshot(uid)).Status);
    }
    // Correlate the server-rendered email claim to the existing subject mapping, then use
    // the application's effective-permission procedure, including user Allow/Deny overrides.
    public async Task<StatusAcceptanceAudience> VerifyAudience(IPage page, string expectedEmail, bool manager, long? expectedActor)
    {
        Assert.False(string.IsNullOrWhiteSpace(expectedEmail), "An explicitly selected existing audience is required.");
        Assert.Equal(StatusAcceptanceOptions.TenantUid.ToString(), await page.Locator("meta[name='microemr-tenant-id']").GetAttributeAsync("content"));
        var email = (await page.Locator(".user-dropdown-header .small.text-muted").TextContentAsync())?.Trim();
        Assert.True(string.Equals(expectedEmail, email, StringComparison.OrdinalIgnoreCase), "Authenticated email does not match the selected audience; mutation is blocked.");
        if (!manager) Assert.False(string.Equals(email, "admin@microemr.local", StringComparison.OrdinalIgnoreCase));
        await using var clinical = new SqlConnection(connectionString); await clinical.OpenAsync();
        await using var identity = new SqlCommand("SELECT UserId,AuthSubjectId FROM dbo.ApplicationUser WHERE IsActive=1 AND (Username=@email OR Email=@email)", clinical);
        identity.Parameters.AddWithValue("@email", email!);
        string subject;
        await using (var reader = await identity.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync(), "Existing authenticated clinical identity not mapped.");
            if (expectedActor.HasValue) Assert.Equal(expectedActor.Value, reader.GetInt64(0));
            subject = reader.GetString(1); Assert.False(await reader.ReadAsync(), "Ambiguous subject mapping.");
        }
        var platformSql = Environment.GetEnvironmentVariable("MICROEMR_STATUS_ACCEPTANCE_PLATFORM_SQL")
            ?? throw new InvalidOperationException("Existing platform permission inspection connection is required.");
        var builder = new SqlConnectionStringBuilder(platformSql);
        Assert.Equal("MicroEMR_Platform", builder.InitialCatalog);
        Assert.True(builder.DataSource is "192.168.50.1,50013" or "EC2AMAZ-TC26CQK\\TELEMED52");
        await using var platform = new SqlConnection(platformSql); await platform.OpenAsync();
        await using (var target = new SqlCommand("SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),DB_NAME()", platform))
        await using (var reader = await target.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync()); Assert.Equal("EC2AMAZ-TC26CQK\\TELEMED52", reader.GetString(0)); Assert.Equal("MicroEMR_Platform", reader.GetString(1));
        }
        await using var command = new SqlCommand("dbo.AccessProfile_GetEffective", platform) { CommandType = CommandType.StoredProcedure };
        command.Parameters.AddWithValue("@TenantUid", StatusAcceptanceOptions.TenantUid); command.Parameters.AddWithValue("@UserId", subject);
        var permissions = new HashSet<string>(StringComparer.Ordinal);
        await using (var reader = await command.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync(), "Tenant membership unavailable.");
            do { Assert.Equal("Active", reader.GetString(0)); if (!reader.IsDBNull(1)) permissions.Add(reader.GetString(1)); } while (await reader.ReadAsync());
        }
        Assert.Contains("Scheduling.View", permissions);
        Assert.Equal(manager, permissions.Contains("Scheduling.Manage"));
        return new(email!, subject, StatusAcceptanceOptions.TenantUid, permissions.Order().ToArray());
    }
    public async Task<StatusAcceptanceSnapshot> Snapshot(Guid uid)
    {
        await using var connection = new SqlConnection(connectionString); await connection.OpenAsync();
        await using var query = new SqlCommand("""
            SELECT AppointmentStatus,
                (SELECT AppointmentUid,AppointmentStatus,UpdatedBy,UpdatedAt,IsDeleted FROM dbo.ScheduleAppointment WHERE AppointmentUid=@p FOR JSON PATH,INCLUDE_NULL_VALUES),
                (SELECT * FROM dbo.AuditLog WHERE EntityName='ScheduleAppointment' AND EntityId=CONVERT(nvarchar(100),@p) ORDER BY AuditLogId FOR JSON PATH,INCLUDE_NULL_VALUES),
                (SELECT * FROM dbo.AppointmentHistory WHERE AppointmentUid=@p ORDER BY AppointmentHistoryId FOR JSON PATH,INCLUDE_NULL_VALUES)
            FROM dbo.ScheduleAppointment WHERE AppointmentUid=@p AND IsDeleted=0;
            """, connection);
        query.Parameters.AddWithValue("@p", uid);
        await using var reader = await query.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "Approved appointment is unavailable; no fixture will be created.");
        return new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3));
    }
    public async Task AssertChange(StatusAcceptanceSnapshot before, Guid uid, string source, string target, long actor)
    {
        var after = await Snapshot(uid); Assert.Equal(target, after.Status);
        using var oldAudit = JsonDocument.Parse(before.Audit); using var audit = JsonDocument.Parse(after.Audit);
        using var oldHistory = JsonDocument.Parse(before.History); using var history = JsonDocument.Parse(after.History);
        var oldIds = oldAudit.RootElement.EnumerateArray().Select(x => x.GetProperty("AuditLogId").GetInt64()).ToHashSet();
        var auditChanges = audit.RootElement.EnumerateArray().Where(x => !oldIds.Contains(x.GetProperty("AuditLogId").GetInt64())).ToArray();
        Assert.True(auditChanges.Length == 1, "Expected exactly one new success audit; raw audit contents are not printed.");
        var newAudit = auditChanges[0];
        Assert.Equal("UpdateStatus", newAudit.GetProperty("ActionName").GetString()); Assert.Equal(actor, newAudit.GetProperty("UserId").GetInt64());
        Assert.Equal(source, newAudit.GetProperty("OldValue").GetString()); Assert.Equal(target, newAudit.GetProperty("NewValue").GetString());
        var oldHistoryIds = oldHistory.RootElement.EnumerateArray().Select(x => x.GetProperty("AppointmentHistoryId").GetInt64()).ToHashSet();
        var historyChanges = history.RootElement.EnumerateArray().Where(x => !oldHistoryIds.Contains(x.GetProperty("AppointmentHistoryId").GetInt64())).ToArray();
        Assert.True(historyChanges.Length == 1, "Expected exactly one new history event; raw history contents are not printed.");
        var newHistory = historyChanges[0];
        Assert.Equal("StatusChanged", newHistory.GetProperty("ActionType").GetString()); Assert.Equal(actor, newHistory.GetProperty("CreatedBy").GetInt64());
        Assert.Equal(source, newHistory.GetProperty("OldStatus").GetString()); Assert.Equal(target, newHistory.GetProperty("NewStatus").GetString());
    }
}
public static class StatusAcceptanceRace
{
    public static async Task Run(string sql, Guid uid, long actor, Func<Task> request)
    {
        new AppointmentStatusTransitionService().EnsureCanTransition(AppointmentStatus.Scheduled, AppointmentStatus.Seen);
        await using var holder = new SqlConnection(sql); await holder.OpenAsync();
        await using var transaction = (SqlTransaction)await holder.BeginTransactionAsync();
        await using (var locked = new SqlCommand("SELECT AppointmentStatus,@@SPID FROM dbo.ScheduleAppointment WITH(UPDLOCK,HOLDLOCK) WHERE AppointmentUid=@p AND IsDeleted=0", holder, transaction))
        {
            locked.Parameters.AddWithValue("@p", uid);
            await using var reader = await locked.ExecuteReaderAsync(); Assert.True(await reader.ReadAsync()); Assert.Equal("Scheduled", reader.GetString(0));
        }
        await using var spidQuery = new SqlCommand("SELECT @@SPID", holder, transaction);
        var blocker = Convert.ToInt32(await spidQuery.ExecuteScalarAsync());
        var pending = request();
        try
        {
            await using var observer = new SqlConnection(sql); await observer.OpenAsync();
            var deadline = DateTime.UtcNow.AddSeconds(10); var waiting = false;
            while (DateTime.UtcNow < deadline && !pending.IsCompleted)
            {
                await using var query = new SqlCommand("""
                    SELECT COUNT(*) FROM sys.dm_exec_requests r CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
                    WHERE r.blocking_session_id=@blocker AND r.database_id=DB_ID() AND r.wait_type LIKE 'LCK_M_%'
                        AND t.objectid=OBJECT_ID('dbo.ScheduleAppointment_UpdateStatus');
                    """, observer);
                query.Parameters.AddWithValue("@blocker", blocker);
                if (Convert.ToInt32(await query.ExecuteScalarAsync()) > 0) { waiting = true; break; }
                await Task.Delay(50);
            }
            Assert.True(waiting, "Persistence wait was not proven. Do not call this race a PASS; inspect the retained final fixture state.");
            await using var change = new SqlCommand("dbo.ScheduleAppointment_UpdateStatus", holder, transaction) { CommandType = CommandType.StoredProcedure };
            change.Parameters.AddWithValue("@AppointmentUid", uid); change.Parameters.AddWithValue("@AppointmentStatus", "Seen");
            change.Parameters.AddWithValue("@ExpectedStatus", "Scheduled"); change.Parameters.AddWithValue("@UpdatedBy", actor);
            await change.ExecuteNonQueryAsync(); await transaction.CommitAsync();
            await pending;
        }
        catch
        {
            if (transaction.Connection is not null) await transaction.RollbackAsync();
            // Releasing the lock can let an already-authorized request finish. Never silently undo its clinical history.
            try { await pending; } catch { }
            throw;
        }
    }
}
public sealed record StatusAcceptanceCheck(string Name, string Result);
public sealed record StatusAcceptanceAudience(string Email, string Subject, Guid TenantUid, string[] EffectivePermissions);
public sealed class StatusAcceptanceReceipt
{
    public string Outcome { get; set; } = "INCOMPLETE";
    public List<StatusAcceptanceCheck> Checks { get; } = [];
    public Dictionary<string, string> FinalStatuses { get; } = [];
    public Dictionary<string, StatusAcceptanceAudience> Audiences { get; } = [];
    public Dictionary<string, int> HttpStatuses { get; } = [];
    public Dictionary<string, bool> Unchanged { get; } = [];
    public int BlockedWrites { get; set; }
}
