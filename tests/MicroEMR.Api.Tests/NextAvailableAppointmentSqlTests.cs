using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.Scheduling;
using MicroEMR.Application.Scheduling.Contracts;
using MicroEMR.Infrastructure.Scheduling;
using MicroEMR.Infrastructure.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class NextAvailableAppointmentSqlTests : IClassFixture<NextAvailableSqlDatabase>
{
    private readonly NextAvailableSqlDatabase _db;
    public NextAvailableAppointmentSqlTests(NextAvailableSqlDatabase db) => _db = db;

    [SchedulingSearchSqlFact]
    public async Task AuthoritativeReadsExcludeProviderRoomAndBlocksWithoutOtherProviderContamination()
    {
        var (patient, provider, room) = await _db.Seed();
        var (_, otherProvider, _) = await _db.Seed();
        await _db.Appointments.CreateAsync(Request(patient, provider, room, 8), 7);
        await _db.Appointments.CreateAsync(Request(patient, otherProvider, room, 9), 7);
        await _db.Appointments.CreateBlockedTimeAsync(new() { ResourceUid = provider, StartDateTimeUtc = At(10), EndDateTimeUtc = At(10, 30), Reason = "Test block" }, 7);
        var periods = await _db.Reads.GetAvailabilityBusyPeriodsAsync(provider, room, At(0), At(23));
        Assert.Equal(3, periods.Count);
        Assert.Equal(2, (await _db.Reads.GetAvailabilityBusyPeriodsAsync(provider, null, At(0), At(23))).Count);
        await _db.Appointments.CancelAsync((await _db.Reads.GetAppointmentsAsync(At(8), At(8, 30), provider)).Single().AppointmentUid, new() { CancelReason = "Test" }, 7);
        Assert.Equal(2, (await _db.Reads.GetAvailabilityBusyPeriodsAsync(provider, room, At(0), At(23))).Count);
    }

    [SchedulingSearchSqlFact]
    public async Task FinalBookingRejectsSlotOccupiedOrBlockedSinceAvailabilityReadAndKeepsNormalCreation()
    {
        var (patient, provider, room) = await _db.Seed();
        Assert.Empty(await _db.Reads.GetAvailabilityBusyPeriodsAsync(provider, room, At(0), At(23)));
        var request = Request(patient, provider, room, 11);
        var created = await _db.Appointments.CreateAsync(request, 7);
        Assert.NotEqual(Guid.Empty, created.AppointmentUid);
        await Assert.ThrowsAsync<SchedulingConflictException>(() => _db.Appointments.CreateAsync(request, 7));
        await _db.Appointments.CreateBlockedTimeAsync(new() { ResourceUid = room, StartDateTimeUtc = At(12), EndDateTimeUtc = At(13), Reason = "Test room block" }, 7);
        await Assert.ThrowsAsync<SchedulingBlockedTimeConflictException>(() => _db.Appointments.CreateAsync(Request(patient, provider, room, 12), 7));
        Assert.Single(await _db.Reads.GetAppointmentsAsync(At(0), At(23), provider));
    }

    [SchedulingSearchSqlFact]
    public async Task RealSeparateTenantDatabaseCannotLoadResourcesOccupancyOrCreateForeignBooking()
    {
        var (patient, provider, room) = await _db.Seed();
        await _db.Appointments.CreateAsync(Request(patient, provider, room, 14), 7);
        var reads = new SchedulingReadRepository(new NextAvailableSqlDatabase.Factory(_db.OtherConnection), NullLogger<SchedulingReadRepository>.Instance);
        Assert.DoesNotContain(await reads.GetActiveResourcesAsync(), resource => resource.ResourceUid == provider || resource.ResourceUid == room);
        var service = new MicroEMR.Application.Scheduling.Services.SchedulingReadService(reads, new Clock());
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetNextAvailableAsync(new()
        { ClinicianUid = provider, RoomUid = room, StartDate = new(2030, 1, 4), HorizonDays = 1, TimeZoneId = "UTC" }));
        Assert.Empty(await reads.GetAvailabilityBusyPeriodsAsync(provider, room, At(0), At(23)));
        var appointments = new SchedulingAppointmentRepository(new NextAvailableSqlDatabase.Factory(_db.OtherConnection));
        await Assert.ThrowsAsync<InvalidOperationException>(() => appointments.CreateAsync(Request(patient, provider, room, 14), 7));
        Assert.Single(await _db.Reads.GetAppointmentsAsync(At(0), At(23), provider));
    }

    private static DateTime At(int hour, int minute = 0) => new(2030, 1, 4, hour, minute, 0, DateTimeKind.Utc);
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(At(0)); }
    private static CreateScheduleAppointmentRequest Request(Guid patient, Guid provider, Guid room, int hour) => new()
    { PatientUid = patient, PrimaryResourceUid = provider, RoomResourceUid = room, StartDateTimeUtc = At(hour), EndDateTimeUtc = At(hour, 30), AppointmentType = "Office Visit" };
}

public sealed class SchedulingSearchSqlFactAttribute : FactAttribute
{
    public SchedulingSearchSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MICROEMR_SCHEDULING_SEARCH_TEST_CONNECTION")))
            Skip = "Set MICROEMR_SCHEDULING_SEARCH_TEST_CONNECTION for disposable scheduling SQL tests.";
    }
}

public sealed class NextAvailableSqlDatabase : IAsyncLifetime
{
    private readonly string _name = "MicroEMR_Step71_" + Guid.NewGuid().ToString("N");
    private readonly string _other = "MicroEMR_Step71_" + Guid.NewGuid().ToString("N");
    private readonly string? _server = Environment.GetEnvironmentVariable("MICROEMR_SCHEDULING_SEARCH_TEST_CONNECTION");
    public string Connection => new SqlConnectionStringBuilder(_server) { InitialCatalog = _name }.ConnectionString;
    public string OtherConnection => new SqlConnectionStringBuilder(_server) { InitialCatalog = _other }.ConnectionString;
    public SchedulingReadRepository Reads => new(new Factory(Connection), NullLogger<SchedulingReadRepository>.Instance);
    public SchedulingAppointmentRepository Appointments => new(new Factory(Connection));
    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_server)) return;
        foreach (var name in new[] { _name, _other })
        {
            await using (var master = new SqlConnection(new SqlConnectionStringBuilder(_server) { InitialCatalog = "master" }.ConnectionString))
            { await master.OpenAsync(); await new SqlCommand($"CREATE DATABASE [{name}]", master).ExecuteNonQueryAsync(); }
            await using var sql = new SqlConnection(new SqlConnectionStringBuilder(_server) { InitialCatalog = name }.ConnectionString);
            await sql.OpenAsync();
            await new SqlCommand("""
                CREATE TABLE dbo.Patient(PatientId bigint IDENTITY PRIMARY KEY, PatientUid uniqueidentifier UNIQUE, IsDeleted bit DEFAULT 0,
                    LastName nvarchar(200), FirstName nvarchar(200), ChartNumber nvarchar(100), HealthCardNumber nvarchar(100), DateOfBirth date, GenderIdentity nvarchar(50));
                CREATE TABLE dbo.ApplicationUser(UserId bigint PRIMARY KEY, DisplayName nvarchar(200)); INSERT dbo.ApplicationUser VALUES(7,N'Test clinician');
                CREATE TABLE dbo.PatientEncounter(EncounterUid uniqueidentifier, AppointmentUid uniqueidentifier, EncounterStatus nvarchar(50));
                CREATE TABLE dbo.AuditLog(AuditLogId bigint IDENTITY, UserId bigint, PatientId bigint, ActionName nvarchar(100), EntityName nvarchar(100), EntityId nvarchar(100), OldValue nvarchar(max), NewValue nvarchar(max), CreatedAt datetime2);
                """, sql).ExecuteNonQueryAsync();
            var root = Root();
            foreach (var path in new[] { "db/scheduling_stored_procedures.sql", "db/tenant-clinical/migrations/0042-scheduling-critical-appointments.sql", "db/tenant-clinical/migrations/0070-scheduling-ad-hoc-overlap.sql" })
                foreach (var batch in Regex.Split(File.ReadAllText(Path.Combine(root, path)), @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Where(x => !string.IsNullOrWhiteSpace(x)))
                    await new SqlCommand(batch, sql).ExecuteNonQueryAsync();
        }
    }
    public async Task<(Guid, Guid, Guid)> Seed()
    {
        var patient = Guid.NewGuid(); var provider = Guid.NewGuid(); var room = Guid.NewGuid();
        await using var sql = new SqlConnection(Connection); await sql.OpenAsync();
        using var command = new SqlCommand("""
            INSERT dbo.Patient(PatientUid,IsDeleted,FirstName,LastName,ChartNumber) VALUES(@p,0,N'Test',N'Patient',N'TEST');
            INSERT dbo.ScheduleResource(ResourceUid,ResourceType,DisplayName,IsActive) VALUES(@provider,N'Provider',N'Test provider',1),(@room,N'Room',N'Test room',1);
            """, sql);
        command.Parameters.AddWithValue("@p", patient); command.Parameters.AddWithValue("@provider", provider); command.Parameters.AddWithValue("@room", room);
        await command.ExecuteNonQueryAsync(); return (patient, provider, room);
    }
    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(_server)) return;
        SqlConnection.ClearAllPools();
        foreach (var name in new[] { _name, _other })
        {
            if (!Regex.IsMatch(name, "^MicroEMR_Step71_[a-f0-9]{32}$")) throw new InvalidOperationException("Unsafe test database name.");
            await using var sql = new SqlConnection(new SqlConnectionStringBuilder(_server) { InitialCatalog = "master" }.ConnectionString);
            await sql.OpenAsync();
            await new SqlCommand($"IF DB_ID(N'{name}') IS NOT NULL BEGIN ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]; END", sql).ExecuteNonQueryAsync();
        }
    }
    private static string Root() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent; return directory?.FullName ?? throw new InvalidOperationException("Repository root unavailable."); }
    public sealed class Factory(string connection) : ITenantSqlConnectionFactory
    {
        public async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default) { var sql = new SqlConnection(connection); await sql.OpenAsync(cancellationToken); return sql; }
    }
}
