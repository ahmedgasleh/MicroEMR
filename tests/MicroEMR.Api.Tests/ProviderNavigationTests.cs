using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ProviderNavigationTests
{
    [Fact]
    public void SidebarShowsProviderAdministrationForUsersWithProviderViewPermission()
    {
        var source = File.ReadAllText(Path.Combine(
            Root(), "src", "MicroEMR.Web", "Views", "Shared", "_Sidebar.cshtml"));

        Assert.Contains("Can(PermissionKeys.ProvidersView)", source);
        Assert.Contains("asp-controller=\"Providers\"", source);
        Assert.Contains("<span class=\"sidebar-text\">Providers</span>", source);
    }

    [Fact]
    public void WebRegistersProviderAdministrationApiClientWithTokenRefresh()
    {
        var source = File.ReadAllText(Path.Combine(
            Root(), "src", "MicroEMR.Web", "Program.cs"));

        Assert.Contains("using MicroEMR.Web.Services.Providers;", source);
        Assert.Contains(
            "AddApiTokenRefresh(builder.Services.AddHttpClient<IProviderAdministrationApiClient, ProviderAdministrationApiClient>(ConfigureApiClient));",
            source);
    }

    [Fact]
    public void ProviderControllerUsesWebHostPermissionService()
    {
        var source = File.ReadAllText(Path.Combine(
            Root(), "src", "MicroEMR.Web", "Controllers", "ProvidersController.cs"));

        Assert.Contains("IWebPermissionService permissions", source);
        Assert.Contains("permissions.HasAsync(PermissionKeys.ProvidersManage", source);
        Assert.DoesNotContain("ICurrentUserPermissionService", source);
    }

    [Theory]
    [InlineData("Index.cshtml")]
    [InlineData("Edit.cshtml")]
    [InlineData("Link.cshtml")]
    public void ProviderPagesUseApplicationShell(string file)
    {
        var source = File.ReadAllText(Path.Combine(
            Root(), "src", "MicroEMR.Web", "Views", "Providers", file));

        Assert.Contains("Layout=\"_AppLayout\"", source);
        Assert.Contains("class=\"content-panel\"", source);
    }

    private static string Root() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
