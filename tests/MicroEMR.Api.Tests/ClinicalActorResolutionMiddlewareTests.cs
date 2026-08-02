using System.Security.Claims;
using MicroEMR.Api.Middleware;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ClinicalActorResolutionMiddlewareTests
{
    [Fact]
    public async Task UnmappedAuthenticatedMutationReturns403AndDoesNotExecute()
    {
        var executed = false;
        var middleware = new ClinicalUserActorResolutionMiddleware(
            _ => { executed = true; return Task.CompletedTask; },
            NullLogger<ClinicalUserActorResolutionMiddleware>.Instance);
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "unmapped")], "Test")),
            Request = { Method = HttpMethods.Post },
            Response = { Body = new MemoryStream() }
        };

        await middleware.InvokeAsync(
            context,
            new RejectingAccessor(),
            new TenantContext(Guid.NewGuid(), "tenant", "Tenant"));

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(executed);
    }

    private sealed class RejectingAccessor : IAuthenticatedClinicalUserAccessor
    {
        public Task<long> GetRequiredUserIdAsync(CancellationToken cancellationToken = default) =>
            throw new ClinicalUserResolutionException("Unmapped.");
    }
}
