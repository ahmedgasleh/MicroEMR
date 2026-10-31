using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Infrastructure.Scheduling;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class SchedulingRoomProjectionReadTests(NextAvailableSqlDatabase db) : IClassFixture<NextAvailableSqlDatabase>
{
    [SchedulingSearchSqlFact]
    public async Task CalendarReadsReturnRoomWithoutDuplicatesOrWritesAndKeepTenantAndRoomFilters()
    {
        var (patient, provider, room) = await db.Seed();
        var start = new DateTime(2030, 1, 4, 9, 0, 0, DateTimeKind.Utc);
        var assigned = await db.Appointments.CreateAsync(new()
        {
            PatientUid = patient, PrimaryResourceUid = provider, RoomResourceUid = room,
            StartDateTimeUtc = start, EndDateTimeUtc = start.AddMinutes(15)
        }, 7);
        var unassigned = await db.Appointments.CreateAsync(new()
        {
            PatientUid = patient, PrimaryResourceUid = provider,
            StartDateTimeUtc = start.AddMinutes(30), EndDateTimeUtc = start.AddMinutes(45)
        }, 7);
        var before = await Snapshot();

        var appointments = await db.Reads.GetAppointmentsAsync(start, start.AddHours(1), provider);
        Assert.Equal(new[] { assigned.AppointmentUid, unassigned.AppointmentUid }, appointments.Select(x => x.AppointmentUid));
        Assert.Equal(room, appointments[0].RoomResourceUid);
        Assert.Equal("Test room", appointments[0].RoomResourceName);
        Assert.Equal(provider, appointments[0].PrimaryResourceUid);
        Assert.Null(appointments[1].RoomResourceUid);
        Assert.Null(appointments[1].RoomResourceName);
        var roomOnly = Assert.Single(await db.Reads.GetAppointmentsAsync(start, start.AddHours(1), room));
        Assert.Equal(assigned.AppointmentUid, roomOnly.AppointmentUid);
        Assert.Equal(provider, roomOnly.PrimaryResourceUid);
        Assert.Equal(room, roomOnly.RoomResourceUid);
        Assert.Empty(await db.Reads.GetAppointmentsAsync(start, start.AddHours(1), Guid.NewGuid()));
        var otherTenant = new SchedulingReadRepository(new NextAvailableSqlDatabase.Factory(db.OtherConnection),
            NullLogger<SchedulingReadRepository>.Instance);
        Assert.Empty(await otherTenant.GetAppointmentsAsync(start, start.AddHours(1), room));
        Assert.Equal(before, await Snapshot());

        async Task<string> Snapshot()
        {
            await using var connection = new SqlConnection(db.Connection);
            await connection.OpenAsync();
            // Fixture database only; compare all rows including history and audit, not just counts.
            await using var query = new SqlCommand("""
                SELECT (SELECT * FROM dbo.ScheduleAppointment ORDER BY ScheduleAppointmentId FOR JSON PATH) Appointments,
                    (SELECT * FROM dbo.AuditLog ORDER BY AuditLogId FOR JSON PATH) Audit,
                    (SELECT * FROM dbo.AppointmentHistory ORDER BY AppointmentHistoryId FOR JSON PATH) History
                FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;
                """, connection);
            return (string)(await query.ExecuteScalarAsync())!;
        }
    }
}
