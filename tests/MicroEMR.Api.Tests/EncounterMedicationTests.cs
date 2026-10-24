using System.Reflection;
using System.Text.Json;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PatientEncounters;
using MicroEMR.Application.PatientEncounters.Contracts;
using MicroEMR.Application.PatientEncounters.Repositories;
using MicroEMR.Application.PatientMedications.Contracts;
using MicroEMR.Application.PatientMedications.Repositories;
using MicroEMR.Application.ReadAudit;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class EncounterMedicationTests
{
    [Fact]
    public async Task SelectingExistingMedicationIsReadOnlyAndForgedActorIsNotUsed()
    {
        var f = new Fixture(); var read = (await f.Service.GetAsync(f.Patient, f.Encounter, "trace"))!;
        Assert.Single(read.Items); Assert.True(read.CanManage); Assert.Equal(0, f.Saves); Assert.Equal(1, f.Audits);
        var draft = Draft();
        var json = JsonSerializer.Serialize(draft).TrimEnd('}') + ",\"Actor\":999,\"CreatedBy\":999}";
        var forged = JsonSerializer.Deserialize<SaveEncounterMedicationRequest>(json)!;
        Assert.NotNull(await f.Service.SaveAsync(f.Patient, f.Encounter, forged)); Assert.Equal(7, f.Actor);
    }

    [Theory]
    [InlineData(PermissionKeys.EncountersView)] [InlineData(PermissionKeys.PatientsView)]
    [InlineData(PermissionKeys.EncountersEdit)] [InlineData(PermissionKeys.ClinicalDataManage)]
    public async Task EachPermissionIsRequiredBeforeMedicationWrites(string missing)
    {
        var f = new Fixture(); f.Access.Remove(missing);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.SaveAsync(f.Patient, f.Encounter, Draft())); Assert.Equal(0, f.Saves);
    }

    [Theory]
    [InlineData("signed")] [InlineData("ownership")] [InlineData("actor")] [InlineData("audit")]
    public async Task ContextActorAndReadAuditFailClosed(string failure)
    {
        var f = new Fixture { Failure = failure };
        if (failure == "signed") await Assert.ThrowsAsync<EncounterMedicationConflictException>(() => f.Service.SaveAsync(f.Patient, f.Encounter, Draft()));
        else if (failure == "ownership") await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.SaveAsync(f.Patient, f.Encounter, Draft()));
        else if (failure == "actor") await Assert.ThrowsAsync<ClinicalUserResolutionException>(() => f.Service.SaveAsync(f.Patient, f.Encounter, Draft()));
        else await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.GetAsync(f.Patient, f.Encounter, "trace"));
        Assert.Equal(0, f.Saves);
    }

    [Fact]
    public async Task SignedRestrictedAndForeignSourceReadBehaviorIsExplicit()
    {
        var f = new Fixture { Failure = "signed" }; Assert.False((await f.Service.GetAsync(f.Patient, f.Encounter, "trace"))!.CanManage);
        f.Failure = ""; f.Access.Remove(PermissionKeys.ClinicalDataManage); Assert.False((await f.Service.GetAsync(f.Patient, f.Encounter, "trace"))!.CanManage);
        f.Failure = "foreign"; await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetAsync(f.Patient, f.Encounter, "trace"));
    }

    [Theory]
    [InlineData("request")] [InlineData("action")] [InlineData("name")] [InlineData("existing-create")]
    [InlineData("dates")] [InlineData("classification")]
    public void InvalidOrAmbiguousMedicationActionsAreRejected(string failure)
    {
        var input = Draft();
        if (failure == "request") input.RequestUid = Guid.Empty;
        if (failure == "action") input.Action = "Prescribe";
        if (failure == "name") input.Medication!.MedicationName = " ";
        if (failure == "existing-create") input.MedicationUid = Guid.NewGuid();
        if (failure == "dates") { input.Medication!.StartDate = new(2020, 2, 1); input.Medication.EndDate = new(2020, 1, 1); }
        if (failure == "classification") input.Medication!.Classification = "Assumed ongoing";
        Assert.Throws<ArgumentException>(() => EncounterMedicationService.Validate(input));
    }

    [Fact]
    public void BothVersionsAndWebAntiforgeryAreRequired()
    {
        var input = Draft(); input.EncounterRowVersion = "bad";
        Assert.Throws<EncounterMedicationConflictException>(() => EncounterMedicationService.Validate(input));
        input = Draft(); input.Action = "Update"; input.MedicationUid = Guid.NewGuid(); input.MedicationRowVersion = "bad";
        Assert.Throws<EncounterMedicationConflictException>(() => EncounterMedicationService.Validate(input));
        Assert.NotNull(typeof(MicroEMR.Web.Controllers.EncounterMedicationsController).GetMethod("Save")!.GetCustomAttribute<Microsoft.AspNetCore.Mvc.ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public void ClinicalHistoryDisplayDoesNotExposeTechnicalReplayReceipt()
    {
        var row = new PatientEncounterHistoryResponse { ActionType = "CppMedicationCreate", ActionDescription = "request|hash|source" };
        Assert.Equal("Medication Summary entry recorded from this encounter.", row.DisplayDescription);
        row.ActionType = "DiagnosesSaved"; Assert.Equal(row.ActionDescription, row.DisplayDescription);
    }

    internal static SaveEncounterMedicationRequest Draft() => new()
    { RequestUid = Guid.NewGuid(), Action = "Create", EncounterRowVersion = "AQIDBAUGBwg=", Medication = new() { MedicationName = "TEST ONLY medication", Classification = "Ongoing" } };

    private sealed class Fixture
    {
        public Guid Patient = Guid.NewGuid(), Encounter = Guid.NewGuid(); public string Failure = "";
        public int Saves, Audits; public long Actor;
        public HashSet<string> Access = [PermissionKeys.EncountersView, PermissionKeys.EncountersEdit, PermissionKeys.PatientsView, PermissionKeys.ClinicalDataManage];
        public EncounterMedicationService Service;
        public Fixture()
        {
            Service = new(CppSelectivePrintTests.Stub<IEncounterMedicationRepository>((_, args) =>
            { Saves++; Actor = (long)args[3]!; return Task.FromResult<PatientMedicationDetailsResponse?>(new() { PatientUid = Patient, MedicationUid = Guid.NewGuid() }); }),
            CppSelectivePrintTests.Stub<IPatientEncounterRepository>((_, _) => Task.FromResult<PatientEncounterDetailsResponse?>(new() { PatientUid = Failure == "ownership" ? Guid.NewGuid() : Patient, EncounterUid = Encounter, Status = Failure == "signed" ? "Signed" : "Open", RowVersion = "AQIDBAUGBwg=" })),
            CppSelectivePrintTests.Stub<IPatientMedicationRepository>((_, _) => Task.FromResult<IReadOnlyList<PatientMedicationListItemResponse>>([new() { PatientUid = Failure == "foreign" ? Guid.NewGuid() : Patient, MedicationUid = Guid.NewGuid(), Status = "Active" }])),
            CppSelectivePrintTests.Stub<ICurrentUserPermissionService>((_, _) => Task.FromResult<IReadOnlySet<string>>(Access)),
            CppSelectivePrintTests.Stub<IAuthenticatedClinicalUserAccessor>((_, _) => Failure == "actor" ? throw new ClinicalUserResolutionException("Unavailable") : Task.FromResult(7L)),
            CppSelectivePrintTests.Stub<IPatientChartReadAuditService>((_, _) => { if (Failure == "audit") throw new InvalidOperationException(); Audits++; return Task.FromResult(Guid.NewGuid()); }));
        }
    }
}
