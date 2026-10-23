using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using MicroEMR.Api;
using MicroEMR.Api.Controllers;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Web.Authorization;

namespace MicroEMR.Api.Tests;

public sealed class Step147SwaggerTests
{
    [Fact]
    public void Production_controller_document_generates_without_opening_a_database()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(PatientsController).Assembly.GetName().Name,
            EnvironmentName = "Testing"
        });
        builder.Services.AddControllers().ConfigureApplicationPartManager(parts =>
        {
            parts.ApplicationParts.Clear();
            parts.ApplicationParts.Add(new AssemblyPart(typeof(PatientsController).Assembly));
        });
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(ApiSwaggerConfiguration.Configure);
        using var services = builder.Services.BuildServiceProvider();
        var document = services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        Assert.NotEmpty(document.Paths);
        Assert.Contains("RescheduleAppointmentRequest", document.Components!.Schemas!.Keys);
        Assert.Contains("SchedulingContractRescheduleAppointmentRequest", document.Components.Schemas.Keys);
        Assert.Contains(document.Components.Schemas.Values,
            schema => schema.Properties?.ContainsKey("paperCopyRequiredBeforeDispense") == true);
        Assert.Contains(document.Components.Schemas.Values,
            schema => schema.Properties?.ContainsKey("complianceAssessment") == true);
    }

    [Theory]
    [InlineData(false, true, false, 0)]
    [InlineData(true, false, false, 1)]
    [InlineData(true, true, true, 1)]
    public async Task Permission_lookup_requires_authentication_and_retains_permission_decision(
        bool authenticated, bool allowed, bool expectedSuccess, int expectedCalls)
    {
        var permissions = new Permissions(allowed);
        var requirement = new WebPermissionRequirement(PermissionKeys.PatientsView);
        var user = new ClaimsPrincipal(authenticated
            ? new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test-user")], "test")
            : new ClaimsIdentity());
        var context = new AuthorizationHandlerContext([requirement], user, null);
        await new WebPermissionHandler(permissions).HandleAsync(context);
        Assert.Equal(expectedSuccess, context.HasSucceeded);
        Assert.Equal(expectedCalls, permissions.Calls);
    }

    private sealed class Permissions(bool allowed) : IWebPermissionService
    {
        public int Calls { get; private set; }
        public Task<bool> HasAsync(string key, CancellationToken token = default)
        {
            Calls++;
            return Task.FromResult(allowed);
        }
        public Task<IReadOnlySet<string>> GetAsync(CancellationToken token = default) =>
            throw new InvalidOperationException("Unexpected permission-list lookup.");
    }
}
