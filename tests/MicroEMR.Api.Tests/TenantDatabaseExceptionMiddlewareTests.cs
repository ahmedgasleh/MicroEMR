using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Api.Middleware;
using MicroEMR.Application.Tenancy;
using MicroEMR.Infrastructure.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class TenantDatabaseExceptionMiddlewareTests
{
    [Fact]
    public async Task ConnectionFailureReturnsSafeServiceUnavailableResponse()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new TenantDatabaseExceptionMiddleware(
            _ => throw new TenantDatabaseConnectionException(
                "Server=secret;Database=TenantSecret;Password=secret"),
            NullLogger<TenantDatabaseExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context, new TenantContextAccessor());

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.DoesNotContain("TenantSecret", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Server=", body, StringComparison.OrdinalIgnoreCase);
    }
}
