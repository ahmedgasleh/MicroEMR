using System.Reflection;
using MicroEMR.Application.PatientEncounters.Contracts;
using MicroEMR.Application.PatientEncounters.Repositories;
using MicroEMR.Application.PatientEncounters.Services;
using MicroEMR.Application.Scheduling;
using MicroEMR.Application.Scheduling.Repositories;
using MicroEMR.Application.Scheduling.Services;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class EncounterSignAppointmentCompletionTests
{
    [Fact]
    public async Task Sign_UsesCanonicalEncounterStartedToCompletedTransition()
    {
        var patientUid = Guid.NewGuid();
        var encounterUid = Guid.NewGuid();
        AppointmentStatus? expectedStatus = null;
        AppointmentStatus? completedStatus = null;
        var response = new PatientEncounterDetailsResponse
        {
            PatientUid = patientUid,
            EncounterUid = encounterUid,
            Status = "Signed"
        };
        var repository = Proxy<IPatientEncounterRepository>((method, arguments) =>
        {
            if (method.Name != nameof(IPatientEncounterRepository.SignAsync))
                throw new NotSupportedException(method.Name);

            expectedStatus = (AppointmentStatus)arguments![3]!;
            completedStatus = (AppointmentStatus)arguments[4]!;
            return Task.FromResult<PatientEncounterDetailsResponse?>(response);
        });
        var schedulingRepository = Proxy<ISchedulingAppointmentRepository>(
            (method, _) => throw new NotSupportedException(method.Name));
        var service = new PatientEncounterService(
            repository,
            schedulingRepository,
            new AppointmentStatusTransitionService());

        var result = await service.SignAsync(patientUid, encounterUid, 42);

        Assert.Same(response, result);
        Assert.Equal(AppointmentStatus.Seen, expectedStatus);
        Assert.Equal(AppointmentStatus.Completed, completedStatus);
    }

    [Fact]
    public void SignProcedure_IsAtomicIdempotentAndHistoryProtected()
    {
        var migration = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "database",
            "tenant-clinical",
            "migrations",
            "0016-complete-appointment-after-sign.sql"));

        Assert.Contains("BEGIN TRANSACTION", migration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UPDLOCK, HOLDLOCK", migration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@ExpectedAppointmentStatus", migration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@CompletedAppointmentStatus", migration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IF @AppointmentUid IS NOT NULL", migration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IF @EncounterStatus = N'Signed'", migration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@AppointmentStatus = @ExpectedAppointmentStatus", migration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AppointmentHistory_Create", migration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PatientEncounterHistory_Create", migration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("THROW 51085", migration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("THROW 51086", migration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ROLLBACK TRANSACTION", migration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COMMIT TRANSACTION", migration, StringComparison.OrdinalIgnoreCase);

        var encounterUpdate = migration.IndexOf(
            "UPDATE dbo.PatientEncounter",
            StringComparison.OrdinalIgnoreCase);
        var appointmentValidation = migration.IndexOf(
            "IF @AppointmentStatus IS NULL",
            StringComparison.OrdinalIgnoreCase);
        var appointmentUpdate = migration.IndexOf(
            "UPDATE dbo.ScheduleAppointment",
            StringComparison.OrdinalIgnoreCase);
        var commit = migration.LastIndexOf(
            "COMMIT TRANSACTION",
            StringComparison.OrdinalIgnoreCase);

        Assert.True(appointmentValidation < encounterUpdate);
        Assert.True(encounterUpdate < appointmentUpdate);
        Assert.True(appointmentUpdate < commit);
        Assert.Equal(1, CountOccurrences(migration, "EXEC dbo.AppointmentHistory_Create"));
    }

    private static int CountOccurrences(string value, string expected)
    {
        var count = 0;
        var startIndex = 0;
        while ((startIndex = value.IndexOf(
                   expected,
                   startIndex,
                   StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            startIndex += expected.Length;
        }

        return count;
    }

    private static T Proxy<T>(Func<MethodInfo, object?[]?, object?> handler)
        where T : class
    {
        var proxy = DispatchProxy.Create<T, InterfaceProxy>();
        ((InterfaceProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    public class InterfaceProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            Handler(targetMethod!, args);
    }
}
