using Microsoft.AspNetCore.Authorization;
using MicroEMR.Api.Controllers;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ClinicalControllerAuthorizationTests
{
    [Theory]
    [InlineData(typeof(PatientsController))]
    [InlineData(typeof(PatientAllergiesController))]
    [InlineData(typeof(PatientMedicationsController))]
    [InlineData(typeof(PatientEncountersController))]
    [InlineData(typeof(SchedulingController))]
    public void ClinicalControllersRequireAuthenticatedUsers(Type controllerType)
    {
        Assert.NotEmpty(controllerType.GetCustomAttributes(
            typeof(AuthorizeAttribute),
            inherit: true));
        Assert.Empty(controllerType.GetCustomAttributes(
            typeof(AllowAnonymousAttribute),
            inherit: true));

        var anonymousActions = controllerType.GetMethods()
            .Where(method => method.DeclaringType == controllerType)
            .Where(method => method.GetCustomAttributes(
                typeof(AllowAnonymousAttribute),
                inherit: true).Length > 0)
            .Select(method => method.Name)
            .ToArray();

        Assert.Empty(anonymousActions);
    }
}
