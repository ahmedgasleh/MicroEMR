using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Web.Controllers;
using MicroEMR.Web.Models.PatientReferrals;
using MicroEMR.Web.Services.PatientReferrals;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PatientReferralWebTests
{
    [Fact]
    public async Task ListUsesSelectedPatientAndReturnsReferrals()
    {
        var patientUid = Guid.NewGuid();
        var client = new StubReferralApiClient
        {
            ListResult = [new PatientReferralListItemViewModel
            {
                ReferralUid = Guid.NewGuid(), PatientUid = patientUid,
                RecipientName = "Dr. Specialist", Reason = "Reason", Status = "Draft"
            }]
        };
        var controller = CreateController(client);

        var action = await controller.List(patientUid);

        Assert.IsType<JsonResult>(action);
        Assert.Equal(patientUid, client.LastPatientUid);
        Assert.Single(client.ListResult);
        Assert.Equal("Draft", client.ListResult[0].Status);
    }

    [Fact]
    public async Task EmptyListReturnsSuccessfulEmptyCollection()
    {
        var client = new StubReferralApiClient();
        var action = await CreateController(client).List(Guid.NewGuid());

        var json = Assert.IsType<JsonResult>(action);
        var serialized = JsonSerializer.Serialize(json.Value);
        Assert.Contains("\"success\":true", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"referrals\":[]", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateUsesPatientFromFormAndOnlyEditableModelFieldsExist()
    {
        var patientUid = Guid.NewGuid();
        var client = new StubReferralApiClient { CreateResult = Details(patientUid) };
        var controller = CreateController(client);
        var model = ValidCreate(patientUid);

        var action = await controller.Create(model);

        Assert.IsType<JsonResult>(action);
        Assert.Equal(patientUid, client.LastPatientUid);
        Assert.Same(model, client.LastCreateRequest);
        var properties = typeof(CreatePatientReferralViewModel).GetProperties()
            .Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("Status", properties);
        Assert.DoesNotContain("CreatedBy", properties);
        Assert.DoesNotContain("SentAtUtc", properties);
        Assert.DoesNotContain("TenantUid", properties);
    }

    [Fact]
    public async Task CreateValidationFailureDoesNotCallApi()
    {
        var client = new StubReferralApiClient();
        var controller = CreateController(client);

        var action = await controller.Create(new CreatePatientReferralViewModel
        {
            PatientUid = Guid.NewGuid(), RecipientName = " ", Reason = " "
        });

        Assert.IsType<BadRequestObjectResult>(action);
        Assert.Null(client.LastCreateRequest);
    }

    [Fact]
    public async Task DetailsUsesPatientAndReferralCombinationAndHandlesMismatchSafely()
    {
        var patientUid = Guid.NewGuid();
        var referralUid = Guid.NewGuid();
        var client = new StubReferralApiClient { DetailsResult = null };
        var controller = CreateController(client);

        var action = await controller.Details(patientUid, referralUid);

        Assert.IsType<NotFoundObjectResult>(action);
        Assert.Equal(patientUid, client.LastPatientUid);
        Assert.Equal(referralUid, client.LastReferralUid);
    }

    [Fact]
    public void CreateModelValidatesSchemaLengthAndRequiredFields()
    {
        var model = new CreatePatientReferralViewModel
        {
            PatientUid = Guid.NewGuid(),
            RecipientName = new string('x', 201),
            RecipientOrganization = new string('x', 201),
            RecipientPhone = new string('1', 31),
            RecipientFax = new string('1', 31),
            Reason = new string('x', 1001)
        };
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(model, new ValidationContext(model), results, true);

        Assert.Equal(5, results.Count);
    }

    [Fact]
    public async Task ApiClientCreateSendsRoutePatientButNotPatientStatusOrActorInJson()
    {
        var patientUid = Guid.NewGuid();
        var handler = new RecordingHandler(Details(patientUid));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") };
        var context = AuthenticatedContext();
        var client = new PatientReferralApiClient(
            httpClient,
            new HttpContextAccessor { HttpContext = context });

        await client.CreateAsync(patientUid, ValidCreate(patientUid));

        Assert.Equal($"api/patients/{patientUid}/referrals", handler.RequestUri);
        using var payload = JsonDocument.Parse(handler.RequestBody!);
        var names = payload.RootElement.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Equal(6, names.Count);
        Assert.Contains("recipientName", names);
        Assert.Contains("clinicalSummary", names);
        Assert.DoesNotContain("patientUid", names);
        Assert.DoesNotContain("status", names);
        Assert.DoesNotContain("createdBy", names);
        Assert.Equal("Bearer test-token", handler.Authorization);
    }

    [Fact]
    public async Task MarkSentUsesPatientReferralAndRowVersionAndReturnsUpdatedDetails()
    {
        var patientUid = Guid.NewGuid();
        var referralUid = Guid.NewGuid();
        var client = new StubReferralApiClient { DetailsResult = Details(patientUid) };

        var action = await CreateController(client).MarkSent(new ReferralStatusTransitionViewModel
        {
            PatientUid = patientUid, ReferralUid = referralUid, RowVersion = "current-version"
        });

        Assert.IsType<JsonResult>(action);
        Assert.Equal(patientUid, client.LastPatientUid);
        Assert.Equal(referralUid, client.LastReferralUid);
        Assert.Equal("current-version", client.LastRowVersion);
    }

    private static PatientReferralsController CreateController(IPatientReferralApiClient client) =>
        new(client, NullLogger<PatientReferralsController>.Instance);

    private static CreatePatientReferralViewModel ValidCreate(Guid patientUid) => new()
    {
        PatientUid = patientUid,
        RecipientName = "Dr. Specialist",
        RecipientOrganization = "Specialist Clinic",
        RecipientPhone = "555-0100",
        RecipientFax = "555-0101",
        Reason = "Assessment requested",
        ClinicalSummary = "Clinical summary"
    };

    private static PatientReferralDetailsViewModel Details(Guid patientUid) => new()
    {
        ReferralUid = Guid.NewGuid(), PatientUid = patientUid,
        RecipientName = "Dr. Specialist", Reason = "Assessment requested",
        Status = "Draft", CreatedBy = 73, RowVersion = "row-version"
    };

    private static DefaultHttpContext AuthenticatedContext()
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = "test-token" }]);
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "test-user")], "test")),
            properties,
            "test");
        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(new StubAuthenticationService(ticket))
            .BuildServiceProvider();
        return new DefaultHttpContext { RequestServices = services };
    }

    private sealed class StubReferralApiClient : IPatientReferralApiClient
    {
        public IReadOnlyList<PatientReferralListItemViewModel> ListResult { get; init; } = [];
        public PatientReferralDetailsViewModel? DetailsResult { get; init; }
        public PatientReferralDetailsViewModel? CreateResult { get; init; }
        public Guid? LastPatientUid { get; private set; }
        public Guid? LastReferralUid { get; private set; }
        public CreatePatientReferralViewModel? LastCreateRequest { get; private set; }
        public string? LastRowVersion { get; private set; }

        public Task<IReadOnlyList<PatientReferralListItemViewModel>> GetByPatientUidAsync(
            Guid patientUid, CancellationToken cancellationToken = default)
        {
            LastPatientUid = patientUid;
            return Task.FromResult(ListResult);
        }

        public Task<PatientReferralDetailsViewModel?> GetByUidAsync(
            Guid patientUid, Guid referralUid, CancellationToken cancellationToken = default)
        {
            LastPatientUid = patientUid;
            LastReferralUid = referralUid;
            return Task.FromResult(DetailsResult);
        }

        public Task<PatientReferralDetailsViewModel?> CreateAsync(
            Guid patientUid, CreatePatientReferralViewModel request,
            CancellationToken cancellationToken = default)
        {
            LastPatientUid = patientUid;
            LastCreateRequest = request;
            return Task.FromResult(CreateResult);
        }

        public Task<PatientReferralDetailsViewModel?> MarkSentAsync(Guid patientUid, Guid referralUid,
            string rowVersion, CancellationToken cancellationToken = default) =>
            Transition(patientUid, referralUid, rowVersion);
        public Task<PatientReferralDetailsViewModel?> MarkResponseReceivedAsync(Guid patientUid, Guid referralUid,
            string rowVersion, CancellationToken cancellationToken = default) =>
            Transition(patientUid, referralUid, rowVersion);
        public Task<PatientReferralDetailsViewModel?> CloseAsync(Guid patientUid, Guid referralUid,
            string rowVersion, CancellationToken cancellationToken = default) =>
            Transition(patientUid, referralUid, rowVersion);

        private Task<PatientReferralDetailsViewModel?> Transition(
            Guid patientUid, Guid referralUid, string rowVersion)
        {
            LastPatientUid = patientUid;
            LastReferralUid = referralUid;
            LastRowVersion = rowVersion;
            return Task.FromResult(DetailsResult);
        }

        public Task<IReadOnlyList<ReferralSupportingDocumentViewModel>> GetLinkedDocumentsAsync(
            Guid patientUid, Guid referralUid, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ReferralSupportingDocumentViewModel>>([]);
        public Task LinkDocumentAsync(Guid patientUid, Guid referralUid, Guid documentUid,
            string rowVersion, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UnlinkDocumentAsync(Guid patientUid, Guid referralUid, Guid documentUid,
            string rowVersion, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingHandler(PatientReferralDetailsViewModel responseBody) : HttpMessageHandler
    {
        public string? RequestUri { get; private set; }
        public string? RequestBody { get; private set; }
        public string? Authorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.PathAndQuery.TrimStart('/');
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Authorization = request.Headers.Authorization?.ToString();
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = JsonContent.Create(responseBody)
            };
        }
    }

    private sealed class StubAuthenticationService(AuthenticationTicket ticket) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.Success(ticket));
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }
}
