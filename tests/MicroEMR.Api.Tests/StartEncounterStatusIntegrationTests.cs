using System.Reflection;
using MicroEMR.Application.PatientEncounters;
using MicroEMR.Application.PatientEncounters.Contracts;
using MicroEMR.Application.PatientEncounters.Repositories;
using MicroEMR.Application.PatientEncounters.Services;
using MicroEMR.Application.Scheduling;
using MicroEMR.Application.Scheduling.Repositories;
using MicroEMR.Application.Scheduling.Services;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class StartEncounterStatusIntegrationTests
{
    [Theory]
    [InlineData(AppointmentStatus.Scheduled)]
    [InlineData(AppointmentStatus.Arrived)]
    [InlineData(AppointmentStatus.CheckedIn)]
    [InlineData(AppointmentStatus.Roomed)]
    public async Task StartFromAppointment_EligibleStatusUsesExistingEncounterRepository(
        AppointmentStatus status)
    {
        var appointmentUid = Guid.NewGuid();
        var response = Response(appointmentUid, wasCreated: true);
        var encounterCalls = 0;
        var service = CreateService(
            status,
            (_, _) =>
            {
                encounterCalls++;
                return Task.FromResult<StartEncounterFromAppointmentResponse?>(response);
            });

        var result = await service.StartFromAppointmentAsync(appointmentUid, 42);

        Assert.Same(response, result);
        Assert.Equal(1, encounterCalls);
    }

    [Fact]
    public async Task StartFromAppointment_EncounterStartedStatusReusesLinkedEncounter()
    {
        var appointmentUid = Guid.NewGuid();
        var response = Response(appointmentUid, wasCreated: false);
        var service = CreateService(
            AppointmentStatus.Seen,
            (_, _) => Task.FromResult<StartEncounterFromAppointmentResponse?>(response));

        var result = await service.StartFromAppointmentAsync(appointmentUid, 42);

        Assert.NotNull(result);
        Assert.False(result.WasCreated);
    }

    [Theory]
    [InlineData(AppointmentStatus.Cancelled, typeof(AppointmentCancelledException))]
    [InlineData(AppointmentStatus.NoShow, typeof(AppointmentNoShowException))]
    [InlineData(AppointmentStatus.Completed, typeof(AppointmentCompletedException))]
    public async Task StartFromAppointment_TerminalStatusRejectsBeforeEncounterRepository(
        AppointmentStatus status,
        Type expectedException)
    {
        var encounterCalls = 0;
        var service = CreateService(
            status,
            (_, _) =>
            {
                encounterCalls++;
                return Task.FromResult<StartEncounterFromAppointmentResponse?>(null);
            });

        var exception = await Record.ExceptionAsync(() =>
            service.StartFromAppointmentAsync(Guid.NewGuid(), 42));

        Assert.IsType(expectedException, exception);
        Assert.Equal(0, encounterCalls);
    }

    [Fact]
    public async Task StartFromAppointment_MissingAppointmentDoesNotCallEncounterRepository()
    {
        var encounterCalls = 0;
        var service = CreateService(
            null,
            (_, _) =>
            {
                encounterCalls++;
                return Task.FromResult<StartEncounterFromAppointmentResponse?>(null);
            });

        var result = await service.StartFromAppointmentAsync(Guid.NewGuid(), 42);

        Assert.Null(result);
        Assert.Equal(0, encounterCalls);
    }

    [Fact]
    public void StartEncounterMigration_IsAtomicIdempotentAndHistoryProtected()
    {
        var migrationPath = Path.Combine(
            AppContext.BaseDirectory,
            "database",
            "tenant-clinical",
            "migrations",
            "0015-start-encounter-status.sql");
        var migration = File.ReadAllText(migrationPath);
        var baseEncounterScript = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "database",
            "patient_encounter_stored_procedures.sql"));

        Assert.Contains("BEGIN TRANSACTION", migration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UPDLOCK, HOLDLOCK", migration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IF @EncounterUid IS NOT NULL", migration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AppointmentStatus = N'Seen'", migration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AppointmentHistory_Create", migration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PatientEncounterHistory_Create", migration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COMMIT TRANSACTION", migration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UX_PatientEncounter_AppointmentUid", baseEncounterScript, StringComparison.OrdinalIgnoreCase);

        var existingEncounterBranch = migration.IndexOf(
            "IF @EncounterUid IS NOT NULL",
            StringComparison.OrdinalIgnoreCase);
        var statusUpdate = migration.IndexOf(
            "AppointmentStatus = N'Seen'",
            StringComparison.OrdinalIgnoreCase);
        Assert.True(statusUpdate > existingEncounterBranch);
    }

    private static PatientEncounterService CreateService(
        AppointmentStatus? status,
        Func<Guid, long?, Task<StartEncounterFromAppointmentResponse?>> start)
    {
        var encounterRepository = Proxy<IPatientEncounterRepository>((method, arguments) =>
            method.Name == nameof(IPatientEncounterRepository.StartFromAppointmentAsync)
                ? start((Guid)arguments![0]!, (long?)arguments[1])
                : throw new NotSupportedException(method.Name));
        var schedulingRepository = Proxy<ISchedulingAppointmentRepository>((method, _) =>
            method.Name == nameof(ISchedulingAppointmentRepository.GetStatusAsync)
                ? Task.FromResult(status)
                : throw new NotSupportedException(method.Name));

        return new PatientEncounterService(
            encounterRepository,
            schedulingRepository,
            new AppointmentStatusTransitionService());
    }

    private static T Proxy<T>(Func<MethodInfo, object?[]?, object?> handler)
        where T : class
    {
        var proxy = DispatchProxy.Create<T, InterfaceProxy>();
        ((InterfaceProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    private static StartEncounterFromAppointmentResponse Response(
        Guid appointmentUid,
        bool wasCreated) => new()
    {
        EncounterUid = Guid.NewGuid(),
        PatientUid = Guid.NewGuid(),
        AppointmentUid = appointmentUid,
        Status = "Open",
        WasCreated = wasCreated
    };

    public class InterfaceProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            Handler(targetMethod!, args);
    }
}
