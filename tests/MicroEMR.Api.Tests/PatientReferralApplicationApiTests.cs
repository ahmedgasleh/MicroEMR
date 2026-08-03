using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PatientReferrals;
using MicroEMR.Application.Patients.Contracts;
using MicroEMR.Application.Patients.Repositories;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PatientReferralApplicationTests
{
    [Fact]
    public async Task CreateUsesResolvedActorAndReturnsDraftDetails()
    {
        var patientUid = Guid.NewGuid();
        var repository = new StubReferralRepository();
        var service = CreateService(patientUid, repository, new StubActorAccessor(73));

        var result = await service.CreateAsync(patientUid, ValidRequest());

        Assert.Equal(73, repository.LastCreatedBy);
        Assert.Equal("Draft", result.Status);
        Assert.Equal(patientUid, result.PatientUid);
        Assert.False(string.IsNullOrWhiteSpace(result.RowVersion));
    }

    [Fact]
    public async Task CreateForMissingPatientFailsWithoutResolvingActorOrWriting()
    {
        var repository = new StubReferralRepository();
        var actor = new StubActorAccessor(73);
        var service = CreateService(null, repository, actor);

        await Assert.ThrowsAsync<PatientReferralPatientNotFoundException>(
            () => service.CreateAsync(Guid.NewGuid(), ValidRequest()));

        Assert.Equal(0, actor.CallCount);
        Assert.Null(repository.LastCreatedBy);
    }

    [Fact]
    public async Task UnmappedAuthenticatedUserCannotCreate()
    {
        var patientUid = Guid.NewGuid();
        var repository = new StubReferralRepository();
        var service = CreateService(
            patientUid,
            repository,
            new StubActorAccessor(new ClinicalUserResolutionException("not mapped")));

        await Assert.ThrowsAsync<ClinicalUserResolutionException>(
            () => service.CreateAsync(patientUid, ValidRequest()));
        Assert.Null(repository.LastCreatedBy);
    }

    [Fact]
    public async Task ListReturnsOnlyRepositoryResultsForPatient()
    {
        var patientUid = Guid.NewGuid();
        var repository = new StubReferralRepository
        {
            Referrals = [Referral(patientUid, "First"), Referral(patientUid, "Second")]
        };
        var service = CreateService(patientUid, repository, new StubActorAccessor(1));

        var results = await service.GetByPatientUidAsync(patientUid);

        Assert.Equal(2, results.Count);
        Assert.Equal(["First", "Second"], results.Select(item => item.RecipientName));
        Assert.All(results, item => Assert.Equal(patientUid, item.PatientUid));
        Assert.All(results, item => Assert.Null(
            typeof(PatientReferralListItemResponse).GetProperty(nameof(PatientReferral.ClinicalSummary))));
    }

    [Fact]
    public async Task TenantScopedServiceCannotSeeAnotherTenantRepositoryData()
    {
        var patientUid = Guid.NewGuid();
        var tenantA = CreateService(patientUid, new StubReferralRepository
        {
            Referrals = [Referral(patientUid, "Tenant A Recipient")]
        }, new StubActorAccessor(1));
        var tenantB = CreateService(patientUid, new StubReferralRepository(), new StubActorAccessor(2));

        var tenantAResults = await tenantA.GetByPatientUidAsync(patientUid);
        var tenantBResults = await tenantB.GetByPatientUidAsync(patientUid);

        Assert.Single(tenantAResults);
        Assert.Empty(tenantBResults);
    }

    [Fact]
    public async Task GetUsesBothPatientAndReferralUidAndDoesNotReturnWrongPatientReferral()
    {
        var patientUid = Guid.NewGuid();
        var otherPatientUid = Guid.NewGuid();
        var referral = Referral(patientUid, "Specialist");
        var repository = new StubReferralRepository { Referrals = [referral] };
        var patientRepository = new StubPatientRepository(patientUid, otherPatientUid);
        var service = new PatientReferralService(repository, patientRepository, new StubActorAccessor(1));

        var matching = await service.GetByUidAsync(patientUid, referral.ReferralUid);
        var wrongPatient = await service.GetByUidAsync(otherPatientUid, referral.ReferralUid);

        Assert.NotNull(matching);
        Assert.Null(wrongPatient);
        Assert.Contains((patientUid, referral.ReferralUid), repository.GetRequests);
        Assert.Contains((otherPatientUid, referral.ReferralUid), repository.GetRequests);
    }

    [Fact]
    public void RequestValidatesRequiredAndSchemaLengthFieldsAndHasNoServerFields()
    {
        var invalid = new CreatePatientReferralRequest
        {
            RecipientName = new string('x', 201),
            RecipientOrganization = new string('x', 201),
            RecipientPhone = new string('1', 31),
            RecipientFax = new string('1', 31),
            Reason = new string('x', 1001)
        };

        Assert.NotEmpty(Validate(invalid));
        var properties = typeof(CreatePatientReferralRequest).GetProperties()
            .Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("PatientUid", properties);
        Assert.DoesNotContain("Status", properties);
        Assert.DoesNotContain("CreatedBy", properties);
        Assert.DoesNotContain("CreatedAtUtc", properties);
        Assert.DoesNotContain("TenantUid", properties);
    }

    private static PatientReferralService CreateService(
        Guid? existingPatientUid,
        StubReferralRepository repository,
        IAuthenticatedClinicalUserAccessor actor) =>
        new(repository, new StubPatientRepository(existingPatientUid is null ? [] : [existingPatientUid.Value]), actor);

    private static CreatePatientReferralRequest ValidRequest() => new()
    {
        RecipientName = "Dr. Specialist",
        RecipientOrganization = "Specialist Clinic",
        RecipientPhone = "555-0100",
        RecipientFax = "555-0101",
        Reason = "Assessment requested",
        ClinicalSummary = "Relevant clinical summary"
    };

    private static PatientReferral Referral(Guid patientUid, string recipient) => new()
    {
        ReferralUid = Guid.NewGuid(),
        PatientUid = patientUid,
        RecipientName = recipient,
        Reason = "Assessment requested",
        Status = ReferralStatus.Draft,
        CreatedAt = DateTime.UtcNow,
        CreatedBy = 73,
        RowVersion = Convert.ToBase64String(new byte[8])
    };

    private static IReadOnlyList<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, true);
        return results;
    }

    private sealed class StubActorAccessor : IAuthenticatedClinicalUserAccessor
    {
        private readonly long _actorId;
        private readonly Exception? _exception;

        public StubActorAccessor(long actorId) => _actorId = actorId;
        public StubActorAccessor(Exception exception) => _exception = exception;
        public int CallCount { get; private set; }

        public Task<long> GetRequiredUserIdAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return _exception is null
                ? Task.FromResult(_actorId)
                : Task.FromException<long>(_exception);
        }
    }

    private sealed class StubReferralRepository : IPatientReferralRepository
    {
        public IReadOnlyList<PatientReferral> Referrals { get; init; } = [];
        public long? LastCreatedBy { get; private set; }
        public List<(Guid PatientUid, Guid ReferralUid)> GetRequests { get; } = [];

        public Task<IReadOnlyList<PatientReferral>> GetByPatientUidAsync(
            Guid patientUid, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PatientReferral>>(
                Referrals.Where(item => item.PatientUid == patientUid).ToArray());

        public Task<PatientReferral?> GetByUidAsync(
            Guid patientUid, Guid referralUid, CancellationToken cancellationToken = default)
        {
            GetRequests.Add((patientUid, referralUid));
            return Task.FromResult(Referrals.SingleOrDefault(
                item => item.PatientUid == patientUid && item.ReferralUid == referralUid));
        }

        public Task<PatientReferral> CreateAsync(
            Guid patientUid, CreatePatientReferralRequest request, long createdBy,
            CancellationToken cancellationToken = default)
        {
            LastCreatedBy = createdBy;
            return Task.FromResult(new PatientReferral
            {
                ReferralUid = Guid.NewGuid(),
                PatientUid = patientUid,
                RecipientName = request.RecipientName,
                RecipientOrganization = request.RecipientOrganization,
                RecipientPhone = request.RecipientPhone,
                RecipientFax = request.RecipientFax,
                Reason = request.Reason,
                ClinicalSummary = request.ClinicalSummary,
                Status = ReferralStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = createdBy,
                RowVersion = Convert.ToBase64String(new byte[8])
            });
        }
    }

    private sealed class StubPatientRepository(params Guid[] patientUids) : IPatientRepository
    {
        private readonly HashSet<Guid> _patientUids = patientUids.ToHashSet();

        public Task<PatientDetailsResponse?> GetByUidAsync(Guid patientUid, CancellationToken cancellationToken = default) =>
            Task.FromResult(_patientUids.Contains(patientUid)
                ? new PatientDetailsResponse { PatientUid = patientUid }
                : null);

        public Task<PatientSearchResponse> SearchAsync(string? searchText, DateOnly? dateOfBirth, int pageNumber,
            int pageSize, bool includeInactive, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PatientDetailsResponse> CreateAsync(CreatePatientRequest request, long? createdBy,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<PatientDetailsResponse?> UpdateDemographicsAsync(Guid patientUid,
            UpdatePatientDemographicsRequest request, long? updatedBy,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

public sealed class PatientReferralApiTests
{
    [Fact]
    public async Task CreateReturnsCreatedResourceAndLocationValues()
    {
        var patientUid = Guid.NewGuid();
        var service = new StubReferralService { CreateResult = Details(patientUid) };
        var controller = CreateController(service);

        var action = await controller.Create(patientUid, ValidRequest());

        var created = Assert.IsType<CreatedAtActionResult>(action.Result);
        Assert.Equal(nameof(PatientReferralsController.Get), created.ActionName);
        Assert.Equal(patientUid, created.RouteValues!["patientUid"]);
        Assert.Equal(service.CreateResult.ReferralUid, created.RouteValues["referralUid"]);
    }

    [Fact]
    public async Task CreateRejectsWhitespaceRequiredFieldsWithoutCallingService()
    {
        var service = new StubReferralService();
        var controller = CreateController(service);

        var action = await controller.Create(Guid.NewGuid(), new CreatePatientReferralRequest
        {
            RecipientName = " ",
            Reason = " "
        });

        var response = Assert.IsType<ObjectResult>(action.Result);
        var problem = Assert.IsType<ValidationProblemDetails>(response.Value);
        Assert.Contains(nameof(CreatePatientReferralRequest.RecipientName), problem.Errors.Keys);
        Assert.Contains(nameof(CreatePatientReferralRequest.Reason), problem.Errors.Keys);
        Assert.Equal(0, service.CreateCalls);
    }

    [Fact]
    public async Task ListReturnsRequestedPatientResults()
    {
        var patientUid = Guid.NewGuid();
        var service = new StubReferralService
        {
            ListResult = [new PatientReferralListItemResponse
            {
                ReferralUid = Guid.NewGuid(), PatientUid = patientUid, RecipientName = "Recipient",
                Reason = "Reason", Status = "Draft", RowVersion = "row-version"
            }]
        };
        var controller = CreateController(service);

        var action = await controller.GetAll(patientUid);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        Assert.Same(service.ListResult, ok.Value);
        Assert.Equal(patientUid, service.LastPatientUid);
    }

    [Fact]
    public async Task DetailsWrongPatientCombinationReturnsNotFound()
    {
        var service = new StubReferralService { DetailsResult = null };
        var controller = CreateController(service);

        var action = await controller.Get(Guid.NewGuid(), Guid.NewGuid());

        Assert.IsType<NotFoundResult>(action.Result);
    }

    private static PatientReferralsController CreateController(StubReferralService service) =>
        new(service, NullLogger<PatientReferralsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static CreatePatientReferralRequest ValidRequest() => new()
    {
        RecipientName = "Dr. Specialist",
        Reason = "Assessment requested"
    };

    private static PatientReferralDetailsResponse Details(Guid patientUid) => new()
    {
        ReferralUid = Guid.NewGuid(), PatientUid = patientUid, RecipientName = "Recipient",
        Reason = "Reason", Status = "Draft", CreatedBy = 73, RowVersion = "row-version"
    };

    private sealed class StubReferralService : IPatientReferralService
    {
        public IReadOnlyList<PatientReferralListItemResponse> ListResult { get; init; } = [];
        public PatientReferralDetailsResponse? DetailsResult { get; init; }
        public PatientReferralDetailsResponse CreateResult { get; init; } = Details(Guid.NewGuid());
        public int CreateCalls { get; private set; }
        public Guid? LastPatientUid { get; private set; }

        public Task<IReadOnlyList<PatientReferralListItemResponse>> GetByPatientUidAsync(
            Guid patientUid, CancellationToken cancellationToken = default)
        {
            LastPatientUid = patientUid;
            return Task.FromResult(ListResult);
        }

        public Task<PatientReferralDetailsResponse?> GetByUidAsync(
            Guid patientUid, Guid referralUid, CancellationToken cancellationToken = default) =>
            Task.FromResult(DetailsResult);

        public Task<PatientReferralDetailsResponse> CreateAsync(
            Guid patientUid, CreatePatientReferralRequest request,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            LastPatientUid = patientUid;
            return Task.FromResult(CreateResult);
        }
    }
}
