using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.Patients.Contracts;
using MicroEMR.Application.Patients.Services;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PatientRegistrationAuthorizationTests
{
    [Fact]
    public async Task ProviderSelectionRequiresExistingViewPermissionsBeforePatientCreation()
    {
        var registration = new RecordingRegistration();
        var authorization = new DenyProviderPermission();
        var controller = new PatientRegistrationController(registration, authorization)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var response = await controller.Register(
            new RegisterPatientRequest(new CreatePatientRequest(), Guid.NewGuid(), null, null),
            CancellationToken.None);

        Assert.IsType<ForbidResult>(response.Result);
        Assert.False(registration.Called);
        Assert.Contains(PermissionPolicyProvider.Prefix + PermissionKeys.PatientsView, authorization.CheckedPolicies);
        Assert.Contains(PermissionPolicyProvider.Prefix + PermissionKeys.ProvidersView, authorization.CheckedPolicies);
    }

    private sealed class RecordingRegistration : IPatientRegistrationService
    {
        public bool Called { get; private set; }
        public Task<RegisterPatientResult> RegisterAsync(RegisterPatientRequest request, long actorUserId,
            CancellationToken cancellationToken = default)
        {
            Called = true;
            throw new NotSupportedException();
        }
    }

    private sealed class DenyProviderPermission : IAuthorizationService
    {
        public List<string> CheckedPolicies { get; } = [];
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource,
            IEnumerable<IAuthorizationRequirement> requirements) => throw new NotSupportedException();

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
        {
            CheckedPolicies.Add(policyName);
            return Task.FromResult(policyName.EndsWith(PermissionKeys.ProvidersView, StringComparison.Ordinal)
                ? AuthorizationResult.Failed()
                : AuthorizationResult.Success());
        }
    }
}
