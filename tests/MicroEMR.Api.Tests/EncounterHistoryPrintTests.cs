using MicroEMR.Web.Models.PatientEncounters;
using MicroEMR.Web.Models.Patients;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class EncounterHistoryPrintTests
{
    [Fact]
    public void PrintModelUsesInclusiveRangeAndChronologicalOrder()
    {
        var patient = new PatientDetailsResponse { PatientUid = Guid.NewGuid(), FirstName = "Ada", LastName = "Lovelace", ChartNumber = "C-1" };
        var first = LocalEncounter(patient.PatientUid, new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Local));
        var last = LocalEncounter(patient.PatientUid, new DateTime(2026, 8, 31, 17, 0, 0, DateTimeKind.Local));
        var outside = LocalEncounter(patient.PatientUid, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Local));

        var model = EncounterHistoryPrintViewModel.Create(patient, [last, outside, first],
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

        Assert.Equal([first.EncounterUid, last.EncounterUid], model.Encounters.Select(x => x.EncounterUid));
    }

    [Fact]
    public void PrintWorkflowUsesTrustedPatientRouteAndEncounterViewPermission()
    {
        var root = Root();
        var controller = File.ReadAllText(Path.Combine(root, "src", "MicroEMR.Web", "Controllers", "PatientEncountersController.cs"));
        var chart = File.ReadAllText(Path.Combine(root, "src", "MicroEMR.Web", "Views", "Patients", "Details.cshtml"));
        var print = File.ReadAllText(Path.Combine(root, "src", "MicroEMR.Web", "Views", "PatientEncounters", "PrintHistory.cshtml"));

        Assert.Contains("RequireWebPermission(PermissionKeys.EncountersView)", controller);
        Assert.Contains("GetByUidAsync(patientUid", controller);
        Assert.Contains("GetByPatientUidAsync(patientUid", controller);
        Assert.Contains("asp-action=\"PrintHistory\"", chart);
        Assert.Contains("name=\"startDate\"", chart);
        Assert.Contains("name=\"endDate\"", chart);
        Assert.Contains("window.print()", print);
        Assert.Contains("Layout = \"_AppLayout\"", print);
        Assert.DoesNotContain("@section Styles", print);
        Assert.Contains("@media print", print);
    }

    private static PatientEncounterListItemResponse LocalEncounter(Guid patientUid, DateTime local) => new()
    {
        EncounterUid = Guid.NewGuid(), PatientUid = patientUid, EncounterDateUtc = local.ToUniversalTime(),
        EncounterType = "Office", Status = "Signed", CreatedAt = local.ToUniversalTime()
    };

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
