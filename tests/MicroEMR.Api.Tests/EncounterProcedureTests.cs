using System.Reflection;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PatientClinicalHistory;
using MicroEMR.Application.PatientEncounters;
using MicroEMR.Application.PatientEncounters.Contracts;
using MicroEMR.Application.PatientEncounters.Repositories;
using MicroEMR.Application.ReadAudit;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class EncounterProcedureTests
{
    [Fact]
    public async Task ReadFiltersProcedureSourcesAndSaveUsesOnlyTrustedActor()
    {
        var f = new Fixture();
        var read = (await f.Service.GetAsync(f.Patient, f.Encounter, "trace"))!;
        Assert.True(read.CanManage); Assert.Equal(2, read.Items.Count); Assert.Equal(1, f.Audits);
        var saved = await f.Service.SaveAsync(f.Patient, f.Encounter, Draft());
        Assert.NotNull(saved); Assert.Equal(7, f.Actor); Assert.Equal(1, f.Saves);
    }

    [Theory]
    [InlineData(PermissionKeys.EncountersView)] [InlineData(PermissionKeys.PatientsView)]
    [InlineData(PermissionKeys.EncountersEdit)] [InlineData(PermissionKeys.ClinicalDataManage)]
    public async Task EveryRequiredPermissionIsEnforcedBeforePersistence(string missing)
    {
        var f = new Fixture(); f.Access.Remove(missing);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.SaveAsync(f.Patient, f.Encounter, Draft()));
        Assert.Equal(0, f.Saves);
    }

    [Theory]
    [InlineData("signed")] [InlineData("ownership")] [InlineData("actor")] [InlineData("audit")]
    public async Task StateOwnershipActorAndReadAuditFailClosed(string failure)
    {
        var f = new Fixture { Failure = failure };
        if (failure == "audit") await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.GetAsync(f.Patient, f.Encounter, "trace"));
        else if (failure == "signed") await Assert.ThrowsAsync<EncounterProcedureConflictException>(() => f.Service.SaveAsync(f.Patient, f.Encounter, Draft()));
        else if (failure == "ownership") await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.SaveAsync(f.Patient, f.Encounter, Draft()));
        else await Assert.ThrowsAsync<ClinicalUserResolutionException>(() => f.Service.SaveAsync(f.Patient, f.Encounter, Draft()));
        Assert.Equal(0, f.Saves);
    }

    [Fact]
    public async Task RestrictedAndSignedReadsRemainReadOnlyAndForeignSourceIsNotDisclosed()
    {
        var f = new Fixture(); f.Access.Remove(PermissionKeys.ClinicalDataManage);
        Assert.False((await f.Service.GetAsync(f.Patient, f.Encounter, "trace"))!.CanManage);
        f.Failure = "signed"; Assert.False((await f.Service.GetAsync(f.Patient, f.Encounter, "trace"))!.CanManage);
        f.Failure = "foreign-source";
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetAsync(f.Patient, f.Encounter, "trace"));
    }

    [Theory]
    [InlineData("uid")] [InlineData("encounter-version")] [InlineData("resource-version")]
    [InlineData("description")] [InlineData("notes")] [InlineData("date")]
    public void InvalidInputsCannotBecomeClinicalWrites(string error)
    {
        var input = Draft();
        if (error == "uid") input.HistoryUid = Guid.Empty;
        if (error == "encounter-version") input.EncounterRowVersion = "bad";
        if (error == "resource-version") { input.IsNew = false; input.HistoryRowVersion = "bad"; }
        if (error == "description") input.Description = " ";
        if (error == "notes") input.Notes = new string('x', 1001);
        if (error == "date") input.ProcedureDate = new() { Month = 2 };
        if (error.EndsWith("version", StringComparison.Ordinal)) Assert.Throws<EncounterProcedureConflictException>(() => EncounterProcedureService.Validate(input));
        else Assert.Throws<ArgumentException>(() => EncounterProcedureService.Validate(input));
    }

    [Fact]
    public void WebAntiforgeryAndApiAuthenticationAreRequired()
    {
        Assert.NotNull(typeof(MicroEMR.Web.Controllers.EncounterProceduresController).GetMethod("Save")!
            .GetCustomAttribute<Microsoft.AspNetCore.Mvc.ValidateAntiForgeryTokenAttribute>());
        Assert.NotEmpty(typeof(MicroEMR.Api.Controllers.EncounterProceduresController)
            .GetCustomAttributes<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>());
    }

    internal static SaveEncounterProcedureRequest Draft() => new()
    {
        HistoryUid = Guid.NewGuid(), IsNew = true, EncounterRowVersion = "AQIDBAUGBwg=",
        Description = "TEST ONLY procedure", ProcedureDate = new() { Year = 2000 }, Notes = "TEST ONLY notes"
    };

    private sealed class Fixture
    {
        public Guid Patient = Guid.NewGuid(), Encounter = Guid.NewGuid();
        public string Failure = ""; public int Saves, Audits; public long Actor;
        public HashSet<string> Access = [PermissionKeys.EncountersView, PermissionKeys.EncountersEdit, PermissionKeys.PatientsView, PermissionKeys.ClinicalDataManage];
        public EncounterProcedureService Service;
        public Fixture()
        {
            Service = new(CppSelectivePrintTests.Stub<IEncounterProcedureRepository>((_, args) =>
            {
                Saves++; Actor = (long)args[3]!; var input = (SaveEncounterProcedureRequest)args[2]!;
                return Task.FromResult<PatientClinicalHistoryResponse?>(Row("Procedure", input.HistoryUid));
            }), CppSelectivePrintTests.Stub<IPatientEncounterRepository>((_, _) => Task.FromResult<PatientEncounterDetailsResponse?>(new()
            { PatientUid = Failure == "ownership" ? Guid.NewGuid() : Patient, EncounterUid = Encounter, Status = Failure == "signed" ? "Signed" : "Open", RowVersion = "AQIDBAUGBwg=" })),
            CppSelectivePrintTests.Stub<IPatientClinicalHistoryRepository>((_, _) => Task.FromResult<IReadOnlyList<PatientClinicalHistoryResponse>>(
                [Row("Procedure"), Row("Surgical"), Row("Medical")])),
            CppSelectivePrintTests.Stub<ICurrentUserPermissionService>((_, _) => Task.FromResult<IReadOnlySet<string>>(Access)),
            CppSelectivePrintTests.Stub<IAuthenticatedClinicalUserAccessor>((_, _) => Failure == "actor" ? throw new ClinicalUserResolutionException("Unavailable") : Task.FromResult(7L)),
            CppSelectivePrintTests.Stub<IPatientChartReadAuditService>((_, _) => { if (Failure == "audit") throw new InvalidOperationException(); Audits++; return Task.FromResult(Guid.NewGuid()); }));
        }
        private PatientClinicalHistoryResponse Row(string kind, Guid? uid = null) => new()
        { PatientUid = Failure == "foreign-source" ? Guid.NewGuid() : Patient, HistoryUid = uid ?? Guid.NewGuid(), HistoryType = kind, Description = "TEST ONLY", Status = "Active", RowVersion = "AQIDBAUGBwg=" };
    }
}
