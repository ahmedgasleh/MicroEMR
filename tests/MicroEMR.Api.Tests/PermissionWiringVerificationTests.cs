using MicroEMR.Application.AccessProfiles;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PermissionWiringVerificationTests
{
    [Fact]
    public void SchedulerDisablesSelectionAndMovementWithoutManagePermission()
    {
        var view = Read("src", "MicroEMR.Web", "Views", "Scheduling", "Index.cshtml");
        Assert.Contains("PermissionKeys.SchedulingManage", view);
        Assert.Contains("eventMoveHandling: canManageScheduling ? \"Update\" : \"Disabled\"", view);
        Assert.Contains("timeRangeSelectedHandling: canManageScheduling ? \"Enabled\" : \"Disabled\"", view);
    }

    [Fact]
    public void RepresentativeActionsAreHiddenBeforeUnauthorizedInteraction()
    {
        var patients = Read("src", "MicroEMR.Web", "Views", "Patients", "Details.cshtml");
        Assert.Contains("canEditEncounters", patients);
        Assert.Contains("canSignEncounters", patients);
        Assert.Contains("canManageDocuments", patients);

        var reports = Read("src", "MicroEMR.Web", "Views", "Reports", "AppointmentStatus.cshtml");
        Assert.Contains("PermissionKeys.ReportsExport", reports);

        var users = Read("src", "MicroEMR.Web", "Views", "TenantUserAdministration", "Index.cshtml");
        Assert.Contains("PermissionKeys.UsersManage", users);
        Assert.Contains("PermissionKeys.UsersManageAccess", users);
    }

    [Fact]
    public void DirectWebWriteRoutesRequireFeaturePermissions()
    {
        var patients = Read("src", "MicroEMR.Web", "Controllers", "PatientsController.cs");
        Assert.Contains("RequireWebPermission(PermissionKeys.PatientsEdit)", patients);
        var encounters = Read("src", "MicroEMR.Web", "Controllers", "PatientEncountersController.cs");
        Assert.Contains("RequireWebPermission(PermissionKeys.EncountersSign)", encounters);
        var documents = Read("src", "MicroEMR.Web", "Controllers", "PatientDocumentsController.cs");
        Assert.Contains("RequireWebPermission(PermissionKeys.DocumentsManage)", documents);
    }

    private static string Read(params string[] path) => File.ReadAllText(Path.Combine([Root(), .. path]));
    private static string Root() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
