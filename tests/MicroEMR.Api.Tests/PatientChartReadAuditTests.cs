using Microsoft.AspNetCore.Authorization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.Patients.Contracts;
using MicroEMR.Application.Patients.Services;
using MicroEMR.Application.ReadAudit;
using MicroEMR.Infrastructure.ReadAudit;
using MicroEMR.Infrastructure.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PatientChartReadAuditTests
{
    [Fact]
    public async Task ServiceUsesResolvedClinicalActorAndNarrowStructuredIdentity()
    {
        var repository = new RecordingRepository();
        var service = new PatientChartReadAuditService(repository, new Actor(73));
        var patientUid = Guid.NewGuid();

        var eventUid = await service.RecordOpenedAsync(patientUid, "trace-14");

        Assert.NotEqual(Guid.Empty, eventUid);
        Assert.Equal((patientUid, 73L, "trace-14", "MicroEMR.Api"), repository.Recorded);
    }

    [Fact]
    public async Task SuccessfulEndpointUsesAuthoritativelyResolvedPatientAndCreatesExactlyOneEvent()
    {
        var routePatientUid = Guid.NewGuid();
        var authoritativePatientUid = Guid.NewGuid();
        var audit = new RecordingAuditService();
        var controller = Controller(new PatientService(authoritativePatientUid), audit);

        var result = await controller.RecordChartOpened(routePatientUid);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(1, audit.Calls);
        Assert.Equal(authoritativePatientUid, audit.PatientUid);
        Assert.Equal("step14-trace", audit.CorrelationId);
    }

    [Fact]
    public async Task MissingPatientCreatesNoSuccessfulAuditEvent()
    {
        var audit = new RecordingAuditService();
        var controller = Controller(new PatientService(null), audit);

        Assert.IsType<NotFoundResult>(await controller.RecordChartOpened(Guid.NewGuid()));
        Assert.Equal(0, audit.Calls);
    }

    [Fact]
    public async Task AuditPersistenceFailureFailsClosedAndIsSurfaced()
    {
        var controller = Controller(new PatientService(Guid.NewGuid()),
            new RecordingAuditService { Failure = new InvalidOperationException("database unavailable") });

        var result = Assert.IsType<ObjectResult>(await controller.RecordChartOpened(Guid.NewGuid()));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
    }

    [Fact]
    public void EndpointRetainsPatientViewPermissionAndDoesNotAddAuditPermission()
    {
        var method = typeof(PatientsController).GetMethod(nameof(PatientsController.RecordChartOpened))!;
        var policies = method.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Select(x => x.Policy).ToArray();

        Assert.Contains(PermissionPolicyProvider.Prefix + PermissionKeys.PatientsView, policies);
        Assert.DoesNotContain(policies, x => x?.Contains("Audit", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void MigrationIsAdditiveStructuredPatientScopedAndContentFree()
    {
        var sql = File.ReadAllText(Path.Combine(Root(), "db", "tenant-clinical", "migrations",
            "0043-patient-chart-read-audit.sql"));

        foreach (var field in new[] { "AuditEventUid", "PatientUid", "EventCategory", "ResourceType",
                     "ResourceUid", "Outcome", "RequestCorrelationId", "SourceApplication" })
            Assert.Contains(field, sql);
        Assert.Contains("PatientChartOpened", sql);
        Assert.Contains("ClinicalRead", sql);
        Assert.Contains("WHERE PatientUid = @PatientUid AND IsDeleted = 0", sql);
        Assert.Contains("WHERE UserId = @ClinicalUserId AND IsActive = 1", sql);
        Assert.DoesNotContain("PatientName", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HealthCard", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Diagnosis", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Medication", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HistoricRowsRemainReadableAndExistingMutationAuditIsUntouched()
    {
        var sql = File.ReadAllText(Path.Combine(Root(), "db", "tenant-clinical", "migrations",
            "0043-patient-chart-read-audit.sql"));
        Assert.Equal(8, sql.Split(" ADD ", StringSplitOptions.None).Skip(1).Count(x => x.Contains(" NULL;")));
        Assert.DoesNotContain("ALTER COLUMN", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("INSERT dbo.AuditLog", File.ReadAllText(Path.Combine(Root(), "db",
            "tenant-clinical", "migrations", "0039-patient-demographic-audit.sql")));
    }

    [Fact]
    public void TriggerIsCentralChartActionAndChildFeedsDoNotAudit()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "src", "MicroEMR.Web", "Controllers", "PatientsController.cs"));
        Assert.Equal(1, source.Split("RecordChartOpenedAsync", StringSplitOptions.None).Length - 1);
        var details = source[source.IndexOf("Task<IActionResult> Details", StringComparison.Ordinal)..];
        Assert.True(details.IndexOf("GetByUidAsync", StringComparison.Ordinal) <
                    details.IndexOf("RecordChartOpenedAsync", StringComparison.Ordinal));
        Assert.True(details.IndexOf("RecordChartOpenedAsync", StringComparison.Ordinal) <
                    details.IndexOf("GetByPatientUidAsync", StringComparison.Ordinal));
    }

    [Fact]
    public void RepositoryUsesOnlyResolvedTenantConnectionFactory()
    {
        var constructor = Assert.Single(typeof(ReadAuditRepository).GetConstructors());
        Assert.Equal([typeof(ITenantSqlConnectionFactory)], constructor.GetParameters().Select(x => x.ParameterType));
    }

    [Fact]
    public void MigrationIsNextUniqueManifestEntry()
    {
        var manifest = File.ReadAllText(Path.Combine(Root(), "db", "tenant-clinical", "manifest.json"));
        using var document = JsonDocument.Parse(manifest);
        Assert.Equal(1, document.RootElement.EnumerateArray().Count(x =>
            x.GetProperty("migrationId").GetString() == "0043-patient-chart-read-audit"));
        Assert.True(manifest.IndexOf("0042-scheduling-critical-appointments", StringComparison.Ordinal) <
                    manifest.IndexOf("0043-patient-chart-read-audit", StringComparison.Ordinal));
    }

    private static PatientsController Controller(IPatientService patients, IPatientChartReadAuditService audit)
    {
        var controller = new PatientsController(patients, NullLogger<PatientsController>.Instance, audit);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { TraceIdentifier = "step14-trace" }
        };
        return controller;
    }

    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", ".."));

    private sealed class Actor(long userId) : IAuthenticatedClinicalUserAccessor
    {
        public Task<long> GetRequiredUserIdAsync(CancellationToken cancellationToken = default) => Task.FromResult(userId);
    }

    private sealed class RecordingRepository : IReadAuditRepository
    {
        public (Guid PatientUid, long Actor, string Correlation, string Source) Recorded { get; private set; }
        public Task<Guid> RecordPatientChartOpenedAsync(Guid patientUid, long clinicalUserId,
            string requestCorrelationId, string sourceApplication, CancellationToken cancellationToken = default)
        {
            Recorded = (patientUid, clinicalUserId, requestCorrelationId, sourceApplication);
            return Task.FromResult(Guid.NewGuid());
        }
    }

    private sealed class RecordingAuditService : IPatientChartReadAuditService
    {
        public int Calls { get; private set; }
        public Guid PatientUid { get; private set; }
        public string? CorrelationId { get; private set; }
        public Exception? Failure { get; init; }
        public Task<Guid> RecordOpenedAsync(Guid patientUid, string requestCorrelationId,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            PatientUid = patientUid;
            CorrelationId = requestCorrelationId;
            return Failure is null ? Task.FromResult(Guid.NewGuid()) : Task.FromException<Guid>(Failure);
        }
    }

    private sealed class PatientService(Guid? authoritativePatientUid) : IPatientService
    {
        public Task<PatientDetailsResponse?> GetByUidAsync(Guid patientUid,
            CancellationToken cancellationToken = default) => Task.FromResult(
            authoritativePatientUid.HasValue
                ? new PatientDetailsResponse { PatientUid = authoritativePatientUid.Value }
                : null);
        public Task<PatientSearchResponse> SearchAsync(string? searchText, DateOnly? dateOfBirth, int pageNumber,
            int pageSize, bool includeInactive, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PatientDetailsResponse> CreateAsync(CreatePatientRequest request, long? createdBy,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PatientDetailsResponse?> UpdateDemographicsAsync(Guid patientUid,
            UpdatePatientDemographicsRequest request, long? updatedBy,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
