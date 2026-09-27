using System.Reflection;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Web.Authorization;
using Xunit;
using ApiCareTeam = MicroEMR.Api.Controllers.PatientCareTeamController;
using WebCareTeam = MicroEMR.Web.Controllers.PatientCareTeamController;

namespace MicroEMR.Api.Tests;

public sealed class PatientCareTeamPermissionTests
{
    [Theory]
    [InlineData(nameof(ApiCareTeam.List))]
    [InlineData(nameof(ApiCareTeam.Types))]
    [InlineData(nameof(ApiCareTeam.Add))]
    [InlineData(nameof(ApiCareTeam.Update))]
    [InlineData(nameof(ApiCareTeam.End))]
    public void ApiCareTeamOperationsRequireProvidersView(string method)
    {
        var policies = typeof(ApiCareTeam).GetMethod(method)!
            .GetCustomAttributes<RequirePermissionAttribute>()
            .Select(attribute => attribute.Policy);

        Assert.Contains(PermissionPolicyProvider.Prefix + PermissionKeys.ProvidersView, policies);
    }

    [Theory]
    [InlineData(nameof(WebCareTeam.List))]
    [InlineData(nameof(WebCareTeam.FormData))]
    [InlineData(nameof(WebCareTeam.Add))]
    [InlineData(nameof(WebCareTeam.Update))]
    [InlineData(nameof(WebCareTeam.End))]
    public void WebCareTeamOperationsRequireProvidersView(string method)
    {
        var policies = typeof(WebCareTeam).GetMethod(method)!
            .GetCustomAttributes<RequireWebPermissionAttribute>()
            .Select(attribute => attribute.Policy);

        Assert.Contains(WebPermissionPolicyProvider.Prefix + PermissionKeys.ProvidersView, policies);
    }
}
