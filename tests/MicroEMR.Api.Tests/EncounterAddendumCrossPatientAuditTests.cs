using System.Security.Claims;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.PatientEncounters.Contracts;
using MicroEMR.Application.PatientEncounters.Services;
using MicroEMR.Application.ReadAudit;
using MicroEMR.Application.SecurityAudit;
using MicroEMR.Application.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class EncounterAddendumCrossPatientAuditTests
{
    [Theory]
    [InlineData("matching")]
    [InlineData("mismatched")]
    [InlineData("missing")]
    public async Task WebDetailPassesPatientContextToApiAndPreservesResponseAndAudit(string scenario)
    {
        var requestedPatient = Guid.NewGuid();
        var encounter = new PatientEncounterDetailsResponse
        {
            EncounterUid = Guid.NewGuid(),
            PatientUid = scenario == "matching" ? requestedPatient : Guid.NewGuid()
        };
        var securityAudit = new SecurityAuditRepository();
        var (api, readAudit) = CreateController(
            new EncounterService(scenario == "missing" ? null : encounter), securityAudit, Guid.NewGuid(), true);
        using var handler = new DetailHandler(api, encounter.EncounterUid, requestedPatient);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://microemr.test/") };
        using var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(new TestAuthenticationService()).BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        var client = new MicroEMR.Web.Services.PatientEncounters.PatientEncounterApiClient(
            httpClient, new HttpContextAccessor { HttpContext = context },
            NullLogger<MicroEMR.Web.Services.PatientEncounters.PatientEncounterApiClient>.Instance);
        var web = new MicroEMR.Web.Controllers.PatientEncountersController(
            client, null!, null!, null!,
            NullLogger<MicroEMR.Web.Controllers.PatientEncountersController>.Instance);

        var result = await web.EncounterDetails(requestedPatient, encounter.EncounterUid, CancellationToken.None);

        Assert.Equal(1, handler.Calls);
        if (scenario == "matching")
        {
            Assert.IsType<JsonResult>(result);
            Assert.Equal(1, readAudit.Calls);
            Assert.Empty(securityAudit.CrossPatientEvents);
        }
        else
        {
            Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal(0, readAudit.Calls);
            Assert.Equal(scenario == "mismatched" ? 1 : 0, securityAudit.CrossPatientEvents.Count);
        }
    }

    [Fact]
    public void DetailReadRetainsAuthorizationAndTenantBoundRepository()
    {
        var controllerType = typeof(PatientEncountersController);
        Assert.NotEmpty(controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), true));
        var permission = Assert.Single(controllerType.GetCustomAttributes(typeof(RequirePermissionAttribute), true)
            .Cast<RequirePermissionAttribute>());
        Assert.Equal(PermissionPolicyProvider.Prefix + PermissionKeys.EncountersView, permission.Policy);
        var capability = Assert.Single(controllerType.GetMethod(nameof(PatientEncountersController.GetEncounter))!
            .GetCustomAttributes(typeof(SensitiveCapabilityAttribute), true).Cast<SensitiveCapabilityAttribute>());
        Assert.Equal(SecurityAuditCapabilities.EncounterView, capability.Capability);
        var constructor = Assert.Single(typeof(MicroEMR.Infrastructure.PatientEncounters.PatientEncounterRepository).GetConstructors());
        Assert.Contains(constructor.GetParameters(), parameter =>
            parameter.ParameterType == typeof(MicroEMR.Infrastructure.Tenancy.ITenantSqlConnectionFactory));
    }

    private sealed class DetailHandler(PatientEncountersController api, Guid encounterUid, Guid patientUid) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            Assert.Equal($"/api/patient-encounters/{encounterUid}", request.RequestUri!.AbsolutePath);
            Assert.Equal($"?patientUid={patientUid}", request.RequestUri.Query);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            var result = await api.GetEncounter(encounterUid, token, patientUid);
            var response = Assert.IsAssignableFrom<ObjectResult>(result.Result);
            return new HttpResponseMessage((HttpStatusCode)response.StatusCode!)
            {
                Content = JsonContent.Create(response.Value)
            };
        }
    }

    private sealed class TestAuthenticationService : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            var properties = new AuthenticationProperties();
            properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = "synthetic-test-token" }]);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity("test")), properties, "test")));
        }
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => throw new NotSupportedException();
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DetailReadWithMatchingOrAbsentPatientContextRecordsExactlyOneView(bool patientContextSupplied)
    {
        var encounter = new PatientEncounterDetailsResponse
        {
            EncounterUid = Guid.NewGuid(), PatientUid = Guid.NewGuid()
        };
        var securityAudit = new SecurityAuditRepository();
        var (controller, readAudit) = CreateController(new EncounterService(encounter), securityAudit, Guid.NewGuid(), true);

        var result = await controller.GetEncounter(encounter.EncounterUid, CancellationToken.None,
            patientContextSupplied ? encounter.PatientUid : null);

        Assert.Same(encounter, Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(1, readAudit.Calls);
        Assert.Equal((ReadAuditActions.EncounterViewed, ReadAuditResourceTypes.Encounter,
            encounter.EncounterUid, encounter.PatientUid, "trace-step20b"), readAudit.Recorded);
        Assert.Empty(securityAudit.CrossPatientEvents);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DetailPatientMismatchReturnsNotFoundWithOnlyOwnershipDenial(bool clinicalActorAvailable)
    {
        var encounter = new PatientEncounterDetailsResponse
        {
            EncounterUid = Guid.NewGuid(), PatientUid = Guid.NewGuid()
        };
        var requestedPatient = Guid.NewGuid();
        var tenantUid = Guid.NewGuid();
        var securityAudit = new SecurityAuditRepository();
        var (controller, readAudit) = CreateController(new EncounterService(encounter), securityAudit, tenantUid, clinicalActorAvailable);

        var result = await controller.GetEncounter(encounter.EncounterUid, CancellationToken.None, requestedPatient);

        Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal(0, readAudit.Calls);
        var denial = Assert.Single(securityAudit.CrossPatientEvents);
        Assert.Equal(SecurityAuditCapabilities.EncounterView, denial.Capability);
        Assert.Equal(SecurityAuditResourceTypes.Encounter, denial.ResourceType);
        Assert.Equal(requestedPatient, denial.RequestedPatientUid);
        Assert.Equal(encounter.PatientUid, denial.AuthoritativePatientUid);
        Assert.Equal(encounter.EncounterUid, denial.ResourceUid);
        Assert.Equal(tenantUid, denial.TrustedTenantUid);
        Assert.Equal("oidc-subject-20b", denial.ActorSubject);
        Assert.Equal(clinicalActorAvailable ? 73L : null, denial.ClinicalUserId);
        Assert.Equal("trace-step20b", denial.RequestCorrelationId);
        Assert.Equal(SecurityAuditSourceApplications.Api, denial.SourceApplication);
    }

    [Fact]
    public async Task DetailNotFoundInCurrentTenantCreatesNeitherViewNorOwnershipDenial()
    {
        // The unchanged tenant-bound service returns null for absent or other-tenant resources.
        var securityAudit = new SecurityAuditRepository();
        var (controller, readAudit) = CreateController(new EncounterService(null), securityAudit, Guid.NewGuid(), true);

        var result = await controller.GetEncounter(Guid.NewGuid(), CancellationToken.None, Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal(0, readAudit.Calls);
        Assert.Empty(securityAudit.CrossPatientEvents);
    }

    [Fact]
    public async Task DetailMismatchRemainsRejectedWhenDenialAuditFails()
    {
        var encounter = new PatientEncounterDetailsResponse
        {
            EncounterUid = Guid.NewGuid(), PatientUid = Guid.NewGuid()
        };
        var logger = new RecordingLogger();
        var securityAudit = new SecurityAuditRepository { Failure = new InvalidOperationException("unavailable") };
        var (controller, readAudit) = CreateController(new EncounterService(encounter), securityAudit, Guid.NewGuid(), true, logger);

        var result = await controller.GetEncounter(encounter.EncounterUid, CancellationToken.None, Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal(0, readAudit.Calls);
        Assert.Equal(1, securityAudit.Attempts);
        Assert.Contains(logger.Messages, message => message.Contains("access remained denied", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DetailViewAuditFailureStillFailsClosed()
    {
        var encounter = new PatientEncounterDetailsResponse
        {
            EncounterUid = Guid.NewGuid(), PatientUid = Guid.NewGuid()
        };
        var securityAudit = new SecurityAuditRepository();
        var (controller, readAudit) = CreateController(new EncounterService(encounter), securityAudit, Guid.NewGuid(), true);
        readAudit.Failure = new InvalidOperationException("unavailable");

        var result = await controller.GetEncounter(encounter.EncounterUid, CancellationToken.None, encounter.PatientUid);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        Assert.Equal(1, readAudit.Calls);
        Assert.Empty(securityAudit.CrossPatientEvents);
    }

    [Fact]
    public async Task EmptyDetailPatientContextIsRejectedWithoutAudit()
    {
        var securityAudit = new SecurityAuditRepository();
        var (controller, readAudit) = CreateController(new EncounterService(null), securityAudit, Guid.NewGuid(), true);

        var result = await controller.GetEncounter(Guid.NewGuid(), CancellationToken.None, Guid.Empty);

        Assert.IsType<BadRequestResult>(result.Result);
        Assert.Equal(0, readAudit.Calls);
        Assert.Empty(securityAudit.CrossPatientEvents);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConfirmedMismatchPreservesNotFoundAndRecordsOneGovernedEvent(bool clinicalActorAvailable)
    {
        var requestedPatient = Guid.NewGuid();
        var authoritativePatient = Guid.NewGuid();
        var encounterUid = Guid.NewGuid();
        var tenantUid = Guid.NewGuid();
        var encounters = new EncounterService(new PatientEncounterDetailsResponse
        {
            EncounterUid = encounterUid,
            PatientUid = authoritativePatient
        });
        var securityAudit = new SecurityAuditRepository();
        var (controller, readAudit) = CreateController(encounters, securityAudit, tenantUid, clinicalActorAvailable);

        var result = await controller.GetEncounterAddendums(
            requestedPatient, encounterUid, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
        var recorded = Assert.Single(securityAudit.CrossPatientEvents);
        Assert.Equal("oidc-subject-20b", recorded.ActorSubject);
        Assert.Equal(clinicalActorAvailable ? 73L : null, recorded.ClinicalUserId);
        Assert.Equal(tenantUid, recorded.TrustedTenantUid);
        Assert.Equal(SecurityAuditCapabilities.EncounterView, recorded.Capability);
        Assert.Equal(requestedPatient, recorded.RequestedPatientUid);
        Assert.Equal(authoritativePatient, recorded.AuthoritativePatientUid);
        Assert.Equal(SecurityAuditResourceTypes.Encounter, recorded.ResourceType);
        Assert.Equal(encounterUid, recorded.ResourceUid);
        Assert.Equal(SecurityAuditSourceApplications.Api, recorded.SourceApplication);
        Assert.Equal("trace-step20b", recorded.RequestCorrelationId);
        Assert.Equal(0, encounters.AddendumLookups);
        Assert.Equal(0, readAudit.Calls);
        Assert.Empty(securityAudit.MissingPermissionEvents);
    }

    [Fact]
    public async Task MatchingOwnershipPreservesSuccessfulAddendumListingWithoutSecurityDenial()
    {
        var patientUid = Guid.NewGuid();
        var encounterUid = Guid.NewGuid();
        var encounters = new EncounterService(new PatientEncounterDetailsResponse
        {
            EncounterUid = encounterUid,
            PatientUid = patientUid
        }, [new PatientEncounterAddendumResponse { EncounterAddendumUid = Guid.NewGuid() }]);
        var securityAudit = new SecurityAuditRepository();
        var (controller, _) = CreateController(encounters, securityAudit, Guid.NewGuid(), true);

        var result = await controller.GetEncounterAddendums(patientUid, encounterUid, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<PatientEncounterAddendumResponse>>(ok.Value));
        Assert.Equal(1, encounters.AddendumLookups);
        Assert.Empty(securityAudit.CrossPatientEvents);
    }

    [Fact]
    public async Task MissingResourcePreservesNotFoundWithoutOwnershipInferenceOrAddendumLookup()
    {
        var encounters = new EncounterService(null);
        var securityAudit = new SecurityAuditRepository();
        var (controller, _) = CreateController(encounters, securityAudit, Guid.NewGuid(), true);

        var result = await controller.GetEncounterAddendums(
            Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Empty(securityAudit.CrossPatientEvents);
        Assert.Equal(0, encounters.AddendumLookups);
    }

    [Fact]
    public async Task AuditPersistenceFailureKeepsAccessDeniedAndLogsOperationalFailure()
    {
        var requestedPatient = Guid.NewGuid();
        var encounter = new PatientEncounterDetailsResponse
        {
            EncounterUid = Guid.NewGuid(),
            PatientUid = Guid.NewGuid()
        };
        var encounters = new EncounterService(encounter);
        var securityAudit = new SecurityAuditRepository { Failure = new InvalidOperationException("database unavailable") };
        var logger = new RecordingLogger();
        var (controller, _) = CreateController(encounters, securityAudit, Guid.NewGuid(), true, logger);

        var result = await controller.GetEncounterAddendums(
            requestedPatient, encounter.EncounterUid, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal(1, securityAudit.Attempts);
        Assert.Equal(0, encounters.AddendumLookups);
        Assert.Contains(logger.Messages, message =>
            message.Contains("access remained denied", StringComparison.Ordinal));
    }

    [Fact]
    public void EndpointRetainsPermissionFirstMetadataAndContainsOnlyOneOwnershipEmissionPoint()
    {
        var method = typeof(PatientEncountersController).GetMethod(nameof(PatientEncountersController.GetEncounterAddendums))!;
        var capability = Assert.Single(method.GetCustomAttributes(typeof(SensitiveCapabilityAttribute), true)
            .Cast<SensitiveCapabilityAttribute>());
        Assert.Equal(SecurityAuditCapabilities.EncounterView, capability.Capability);

        var source = File.ReadAllText(Path.Combine(Root(), "src", "MicroEMR.Api", "Controllers",
            "PatientEncountersController.cs"));
        var endpoint = source[source.IndexOf("GetEncounterAddendums", StringComparison.Ordinal)..
            source.IndexOf("CreateEncounterAddendum", StringComparison.Ordinal)];
        Assert.Equal(1, Count(endpoint, "TryRecordCrossPatientOwnershipAsync"));
        Assert.True(endpoint.IndexOf("GetByUidAsync", StringComparison.Ordinal) <
                    endpoint.IndexOf("encounter.PatientUid != patientUid", StringComparison.Ordinal));
        Assert.True(endpoint.IndexOf("encounter.PatientUid != patientUid", StringComparison.Ordinal) <
                    endpoint.IndexOf("TryRecordCrossPatientOwnershipAsync", StringComparison.Ordinal));
        Assert.DoesNotContain("_readAudit.RecordAsync", endpoint);
    }

    private static (PatientEncountersController Controller, ReadAuditService ReadAudit) CreateController(
        EncounterService encounters,
        SecurityAuditRepository securityAudit,
        Guid tenantUid,
        bool clinicalActorAvailable,
        RecordingLogger? logger = null)
    {
        var tenant = new TenantContextAccessor();
        tenant.SetTenant(new TenantContext(tenantUid, "step20b", "Step 20B"));
        var readAudit = new ReadAuditService();
        var controller = new PatientEncountersController(encounters, logger ?? new RecordingLogger(),
            new PdfPreviewService(), new ArtifactService(), readAudit, securityAudit, tenant);
        var context = new DefaultHttpContext { TraceIdentifier = "trace-step20b" };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "oidc-subject-20b")], "test"));
        if (clinicalActorAvailable) ClinicalUserActorContext.Set(context, 73);
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return (controller, readAudit);
    }

    private sealed class EncounterService(
        PatientEncounterDetailsResponse? encounter,
        IReadOnlyList<PatientEncounterAddendumResponse>? addendums = null) : IPatientEncounterService
    {
        public int AddendumLookups { get; private set; }
        public Task<PatientEncounterDetailsResponse?> GetByUidAsync(Guid encounterUid, CancellationToken token = default) =>
            Task.FromResult(encounter?.EncounterUid == encounterUid ? encounter : null);
        public Task<IReadOnlyList<PatientEncounterAddendumResponse>> GetAddendumsAsync(Guid patientUid, Guid encounterUid,
            CancellationToken token = default)
        {
            AddendumLookups++;
            return Task.FromResult(addendums ?? (IReadOnlyList<PatientEncounterAddendumResponse>)[]);
        }
        public Task<IReadOnlyList<PatientEncounterListItemResponse>> GetByPatientUidAsync(Guid p, CancellationToken t = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PatientEncounterHistoryResponse>> GetHistoryAsync(Guid p, Guid e, CancellationToken t = default) => throw new NotSupportedException();
        public Task<PatientEncounterAddendumResponse?> CreateAddendumAsync(Guid p, Guid e, CreateEncounterAddendumRequest r, long? a, CancellationToken t = default) => throw new NotSupportedException();
        public Task<PatientEncounterDetailsResponse> CreateAsync(Guid p, CreatePatientEncounterRequest r, long? a, string? n, CancellationToken t = default) => throw new NotSupportedException();
        public Task<PatientEncounterDetailsResponse?> UpdateNoteAsync(Guid p, Guid e, UpdateEncounterNoteRequest r, long? a, CancellationToken t = default) => throw new NotSupportedException();
        public Task<PatientEncounterDetailsResponse?> UpdateSoapNoteAsync(Guid p, Guid e, UpdateEncounterSoapNoteRequest r, long? a, CancellationToken t = default) => throw new NotSupportedException();
        public Task<PatientEncounterDetailsResponse?> UpdateStructuredDataAsync(Guid p, Guid e, UpdateEncounterStructuredDataRequest r, long? a, CancellationToken t = default) => throw new NotSupportedException();
        public Task<PatientEncounterDetailsResponse?> SignAsync(Guid p, Guid e, long? a, CancellationToken t = default) => throw new NotSupportedException();
        public Task<StartEncounterFromAppointmentResponse?> StartFromAppointmentAsync(Guid a, long? u, CancellationToken t = default) => throw new NotSupportedException();
    }

    private sealed class SecurityAuditRepository : IPlatformSecurityAuditRepository
    {
        public List<CrossPatientOwnershipSecurityEvent> CrossPatientEvents { get; } = [];
        public List<MissingPermissionSecurityEvent> MissingPermissionEvents { get; } = [];
        public Exception? Failure { get; init; }
        public int Attempts { get; private set; }
        public Task RecordCrossPatientOwnershipAsync(CrossPatientOwnershipSecurityEvent securityEvent,
            CancellationToken cancellationToken = default)
        {
            Attempts++;
            if (Failure is not null) throw Failure;
            CrossPatientEvents.Add(securityEvent);
            return Task.CompletedTask;
        }
        public Task RecordMissingPermissionAsync(MissingPermissionSecurityEvent securityEvent,
            CancellationToken cancellationToken = default)
        {
            MissingPermissionEvents.Add(securityEvent);
            return Task.CompletedTask;
        }
        public Task RecordUnresolvedClinicalActorAsync(UnresolvedClinicalActorSecurityEvent securityEvent,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task RecordInvalidTenantMembershipAsync(InvalidTenantMembershipSecurityEvent securityEvent,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ReadAuditService : IStructuredReadAuditService
    {
        public int Calls { get; private set; }
        public Exception? Failure { get; set; }
        public (string Event, string ResourceType, Guid ResourceUid, Guid PatientUid, string Correlation) Recorded { get; private set; }
        public Task<Guid> RecordAsync(string e, string r, Guid u, Guid p, string c, CancellationToken t = default)
        {
            Calls++;
            if (Failure is not null) throw Failure;
            Recorded = (e, r, u, p, c);
            return Task.FromResult(Guid.NewGuid());
        }
        public Task<Guid> RecordAggregateReportAsync(string e, string r, string c, CancellationToken t = default) => throw new NotSupportedException();
    }

    private sealed class PdfPreviewService : IClinicalPdfPreviewService
    {
        public Task<byte[]?> PreviewPatientDocumentAsync(Guid u, TemplatePreviewRequest r, CancellationToken t = default) => throw new NotSupportedException();
        public Task<byte[]?> PreviewEncounterAsync(Guid u, TemplatePreviewRequest r, CancellationToken t = default) => throw new NotSupportedException();
        public Task<byte[]> RenderSignedEncounterAsync(Guid u, CancellationToken t = default) => throw new NotSupportedException();
        public Task<byte[]> RenderConsultationFinalAsync(MicroEMR.Application.PatientDocuments.Contracts.ConsultationSigningContext c, CancellationToken t = default) => throw new NotSupportedException();
    }

    private sealed class ArtifactService : IClinicalArtifactService
    {
        public Task<ClinicalOutputArtifact?> EnsureEncounterFinalPdfAsync(Guid u, long? a, CancellationToken t = default) => throw new NotSupportedException();
        public Task<ClinicalArtifactContent?> OpenEncounterFinalPdfAsync(Guid u, CancellationToken t = default) => throw new NotSupportedException();
    }

    private sealed class RecordingLogger : ILogger<PatientEncountersController>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }

    private static int Count(string source, string value) => source.Split(value, StringSplitOptions.None).Length - 1;
    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", ".."));
}
