using Microsoft.Data.SqlClient;
using MicroEMR.Application.Scheduling;
using MicroEMR.Infrastructure.Scheduling;
using Xunit;
using Xunit.Sdk;

namespace MicroEMR.Api.Tests;

public sealed class StatusAcceptanceAutomationTests
{
    [Theory]
    [InlineData(null, true, true)]
    [InlineData("yes", true, true)]
    [InlineData(StatusAcceptanceOptions.Consent, false, true)]
    [InlineData(StatusAcceptanceOptions.Consent, true, false)]
    public void MissingScopeApprovalRejectsBeforeAnyConnection(string? consent, bool appointments, bool race)
    {
        var options = Valid(); options.ExistingAppointmentsApproved = appointments; options.DirectCompetingStoredProcedureWriteApproved = race;
        Assert.Throws<InvalidOperationException>(() => options.Validate(consent));
    }
    [Fact]
    public void ValidConfigurationRequiresDistinctNonemptyExplicitFixturesAndActor()
    {
        var options = Valid(); options.Validate(StatusAcceptanceOptions.Consent);
        options.RaceAppointmentUid = options.ScheduledAppointmentUid;
        Assert.Throws<InvalidOperationException>(() => options.Validate(StatusAcceptanceOptions.Consent));
        options = Valid(); options.CompletedAppointmentUid = Guid.Empty;
        Assert.Throws<InvalidOperationException>(() => options.Validate(StatusAcceptanceOptions.Consent));
        options = Valid(); options.ManagerActorId = 0;
        Assert.Throws<InvalidOperationException>(() => options.Validate(StatusAcceptanceOptions.Consent));
    }
    [Theory]
    [InlineData("Server=production;Database=MicroEMR_LocalDev_Fresh;Integrated Security=true")]
    [InlineData("Server=192.168.50.1,50013;Database=another-tenant;Integrated Security=true")]
    public void OtherDatabaseOrServerRejected(string value) => Assert.Throws<InvalidOperationException>(() => StatusAcceptanceOptions.ValidateConnection(value));
    [Fact]
    public void RequestGateAllowsOnlyArmedAppointmentTargetOnceAndNeverUnrelatedMutations()
    {
        var gate = new StatusAcceptanceRequestGate(); var uid = Guid.NewGuid();
        Assert.True(gate.Allow(StatusAcceptanceOptions.Web + "/", "GET", null));
        Assert.True(gate.Allow(StatusAcceptanceOptions.Auth + "/Account/Login", "POST", "not persisted"));
        Assert.True(gate.Allow(StatusAcceptanceOptions.Auth + "/connect/authorize", "POST", "not persisted"));
        Assert.False(gate.Allow(StatusAcceptanceOptions.Web + "/Patients/Details?patientUid=" + Guid.NewGuid(), "GET", null));
        Assert.False(gate.Allow("https://example.com/Home/UpdateAppointmentStatus", "POST", null));
        Assert.False(gate.Allow(StatusAcceptanceOptions.Web + "/PatientPrescriptions/Edit", "POST", null));
        gate.Arm(uid, "Arrived");
        Assert.False(gate.Allow(StatusAcceptanceOptions.Web + "/Home/UpdateAppointmentStatus", "POST", $"AppointmentUid={Guid.NewGuid()}&Status=Arrived"));
        Assert.False(gate.Allow(StatusAcceptanceOptions.Web + "/Home/UpdateAppointmentStatus", "POST", $"AppointmentUid={uid}&Status=Completed"));
        Assert.True(gate.Allow(StatusAcceptanceOptions.Web + "/Home/UpdateAppointmentStatus", "POST", $"AppointmentUid={uid}&Status=Arrived"));
        Assert.False(gate.Allow(StatusAcceptanceOptions.Web + "/Home/UpdateAppointmentStatus", "POST", $"AppointmentUid={uid}&Status=Arrived"));
    }
    [Fact]
    public void RequestGateRecognizesActualDashboardMultipartFormDataWithoutLoggingToken()
    {
        var gate = new StatusAcceptanceRequestGate(); var uid = Guid.NewGuid(); gate.Arm(uid, "Completed");
        var body = $"--boundary\r\nContent-Disposition: form-data; name=\"AppointmentUid\"\r\n\r\n{uid}\r\n--boundary\r\nContent-Disposition: form-data; name=\"Status\"\r\n\r\nCompleted\r\n--boundary--\r\n";
        Assert.True(gate.Allow(StatusAcceptanceOptions.Web + "/Home/UpdateAppointmentStatus", "POST", body));
    }
    [Fact]
    public void SafeConflictDoesNotAcceptSuccessWrongStatusOrDiagnosticLeakage()
    {
        StatusAcceptanceAssertions.SafeConflict(409, "{\"success\":false,\"message\":\"Refresh and try again.\"}");
        Assert.ThrowsAny<XunitException>(() => StatusAcceptanceAssertions.SafeConflict(200, "{\"success\":false,\"message\":\"Refresh\"}"));
        Assert.ThrowsAny<XunitException>(() => StatusAcceptanceAssertions.SafeConflict(409, "{\"success\":true,\"message\":\"Refresh\"}"));
        Assert.ThrowsAny<XunitException>(() => StatusAcceptanceAssertions.SafeConflict(409, "{\"success\":false,\"message\":\"SqlException diagnostic\"}"));
    }
    private static StatusAcceptanceOptions Valid() => new()
    {
        ScheduledAppointmentUid=Guid.NewGuid(), CompletedAppointmentUid=Guid.NewGuid(), RaceAppointmentUid=Guid.NewGuid(), ManagerActorId=7,
        ExistingAppointmentsApproved=true, DirectCompetingStoredProcedureWriteApproved=true
    };
}

// Prove the acceptance harness's new U-lock handshake locally, without a persistent runtime or browser.
public sealed class StatusAcceptanceRaceAutomationTests(NextAvailableSqlDatabase db) : IClassFixture<NextAvailableSqlDatabase>
{
    [SchedulingSearchSqlFact]
    public async Task SharedServiceReadPassesHolderUpdateLockThenMetadataProvesPersistenceWait()
    {
        var (patient, provider, _) = await db.Seed();
        var row = await db.Appointments.CreateAsync(new()
        {
            PatientUid=patient,PrimaryResourceUid=provider,StartDateTimeUtc=new(2030,1,4,9,0,0,DateTimeKind.Utc),EndDateTimeUtc=new(2030,1,4,9,30,0,DateTimeKind.Utc)
        },7);
        var observer = new StatusAcceptanceObserver(db.Connection);
        var before = await observer.Snapshot(row.AppointmentUid);
        await StatusAcceptanceRace.Run(db.Connection, row.AppointmentUid, 7, async () =>
        {
            var service = new MicroEMR.Application.Scheduling.Services.SchedulingAppointmentService(db.Appointments,
                new MicroEMR.Application.Scheduling.Services.AppointmentStatusTransitionService());
            await Assert.ThrowsAsync<AppointmentStatusConcurrencyException>(() => service.UpdateStatusAsync(row.AppointmentUid, new() { Status="Arrived" },7));
        });
        await observer.AssertChange(before, row.AppointmentUid, "Scheduled", "Seen",7);
    }
}
