using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Web.Services.PatientPrescriptions;
using Xunit;
using WebController = MicroEMR.Web.Controllers.DispensingLocationsController;

namespace MicroEMR.Api.Tests;

public sealed class DispensingLocationCreateBindingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateBindsAndForwardsWithoutClientVersion(bool postEmptyVersion)
    {
        await WithBoundForm(postEmptyVersion, null, async (controller, request, bound, api) =>
        {
            Assert.True(bound, string.Join("; ", controller.ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage)));
            Assert.Null(request.RowVersion);
            var result = Assert.IsType<RedirectToActionResult>(await controller.Add(request, default));
            Assert.Equal("Index", result.ActionName);
            Assert.Equal(1, api.Calls);
        });
    }

    [Theory]
    [InlineData("System")]
    [InlineData("Identifier")]
    [InlineData("FacilityName")]
    [InlineData("FacilityAddress")]
    public async Task CreateStillRejectsBlankRequiredFields(string field)
    {
        await WithBoundForm(true, field, async (controller, request, bound, api) =>
        {
            Assert.False(bound);
            Assert.NotEmpty(controller.ModelState[field]!.Errors);
            Assert.False(controller.ModelState.ContainsKey(nameof(SaveDispensingLocation.RowVersion)) &&
                controller.ModelState[nameof(SaveDispensingLocation.RowVersion)]!.Errors.Count > 0);
            Assert.IsType<ViewResult>(await controller.Add(request, default));
            Assert.Equal(0, api.Calls);
        });
    }

    private static async Task WithBoundForm(bool postEmptyVersion, string? blankField,
        Func<WebController, SaveDispensingLocation, bool, RecordingApi, Task> verify)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(WebController).Assembly);
        await using var app = builder.Build();
        using var scope = app.Services.CreateScope();
        var fields = new Dictionary<string, StringValues>
        {
            ["System"] = "urn:microemr:dev-test", ["Identifier"] = "binding-test",
            ["FacilityName"] = "TEST ONLY location", ["FacilityAddress"] = "100 Test Street"
        };
        if (postEmptyVersion) fields["RowVersion"] = "";
        if (blankField is not null) fields[blankField] = "";
        var form = new FormCollection(fields);
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        http.Request.ContentType = "application/x-www-form-urlencoded";
        http.Request.Form = form;
        using var api = new RecordingApi();
        using var client = new HttpClient(api) { BaseAddress = new Uri("https://test.invalid/") };
        var controller = new WebController(new DispensingLocationApiClient(client))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = http,
                RouteData = new Microsoft.AspNetCore.Routing.RouteData(),
                ActionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()
            }
        };
        var request = new SaveDispensingLocation();
        var bound = await controller.TryUpdateModelAsync(request, "",
            new FormValueProvider(BindingSource.Form, form, CultureInfo.InvariantCulture));
        await verify(controller, request, bound, api);
    }

    private sealed class RecordingApi : HttpMessageHandler
    {
        internal int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/dispensing-locations", request.RequestUri!.AbsolutePath);
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new DispensingLocation(Guid.NewGuid(), "urn:microemr:dev-test",
                    "binding-test", "TEST ONLY location", "100 Test Street", null, true,
                    DateTime.UtcNow, 7, null, null, Convert.ToBase64String(new byte[8])))
            });
        }
    }
}

// Reuse the existing disposable directory databases, never a persistent application tenant.
public sealed class DispensingLocationRowVersionApiTests(DispensingDirectoryDatabase fixture)
    : IClassFixture<DispensingDirectoryDatabase>
{
    private static JsonObject Body() => new()
    {
        ["system"] = "urn:TEST:" + Guid.NewGuid(), ["identifier"] = "test-" + Guid.NewGuid(),
        ["facilityName"] = "TEST ONLY location", ["facilityAddress"] = "100 Test Street"
    };

    [Theory]
    [InlineData("omitted")]
    [InlineData("null")]
    [InlineData("empty")]
    public async Task CreateGeneratesVersionWithoutClientToken(string kind)
    {
        await using var host = await fixture.Host();
        using var client = host.Client();
        var body = Body();
        if (kind != "omitted") body["rowVersion"] = kind == "null" ? null : "";
        var response = await client.PostAsJsonAsync("api/dispensing-locations", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var row = (await response.Content.ReadFromJsonAsync<DispensingLocation>())!;
        Assert.Equal(8, Convert.FromBase64String(row.RowVersion).Length);
        Assert.NotEqual(Convert.ToBase64String(new byte[8]), row.RowVersion);
        Assert.Equal(row, await client.GetFromJsonAsync<DispensingLocation>("api/dispensing-locations/" + row.LocationUid));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("api/dispensing-locations", body)).StatusCode);
    }

    [Theory]
    [InlineData("system")]
    [InlineData("identifier")]
    [InlineData("facilityName")]
    [InlineData("facilityAddress")]
    public async Task CreateStillRejectsMissingRequiredFields(string field)
    {
        await using var host = await fixture.Host();
        using var client = host.Client();
        var body = Body();
        body.Remove(field);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("api/dispensing-locations", body)).StatusCode);
    }

    [Theory]
    [InlineData("omitted")]
    [InlineData("null")]
    [InlineData("empty")]
    [InlineData("malformed")]
    public async Task EditStillRequiresCurrentEightByteVersion(string kind)
    {
        var request = DispensingDirectoryTests.Request();
        var row = await fixture.Directory().SaveAsync(null, "Create", request, 7);
        await using var host = await fixture.Host();
        using var client = host.Client();
        var body = Body();
        body["system"] = row.System;
        body["identifier"] = row.Identifier;
        if (kind != "omitted") body["rowVersion"] = kind == "null" ? null : kind == "empty" ? "" : "bad";
        var response = await client.PutAsJsonAsync("api/dispensing-locations/" + row.LocationUid, body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(row, await fixture.Directory().GetAsync(row.LocationUid));
    }

    [Fact]
    public async Task StaleEditStillReturnsConflictWithoutOverwritingFreshEdit()
    {
        var request = DispensingDirectoryTests.Request();
        var row = await fixture.Directory().SaveAsync(null, "Create", request, 7);
        request.RowVersion = row.RowVersion;
        request.FacilityName = "TEST ONLY fresh edit";
        await using var host = await fixture.Host();
        using var client = host.Client();
        var url = "api/dispensing-locations/" + row.LocationUid;
        var response = await client.PutAsJsonAsync(url, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var edited = (await response.Content.ReadFromJsonAsync<DispensingLocation>())!;
        Assert.NotEqual(row.RowVersion, edited.RowVersion);
        request.FacilityName = "TEST ONLY stale edit";
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(url, request)).StatusCode);
        Assert.Equal(edited, await fixture.Directory().GetAsync(row.LocationUid));
    }
}
