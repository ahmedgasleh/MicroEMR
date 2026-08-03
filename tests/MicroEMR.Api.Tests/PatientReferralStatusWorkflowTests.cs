using Microsoft.Extensions.Configuration;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PatientReferrals;
using MicroEMR.Application.Patients.Contracts;
using MicroEMR.Application.Patients.Repositories;
using MicroEMR.Infrastructure.Provisioning;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PatientReferralStatusWorkflowTests
{
    private static readonly string RowVersion = Convert.ToBase64String(new byte[8]);

    [Fact]
    public void CanonicalTransitionServiceAllowsOnlyTheThreeForwardTransitions()
    {
        var service = new ReferralStatusTransitionService();
        Assert.True(service.CanTransition(ReferralStatus.Draft, ReferralStatus.Sent));
        Assert.True(service.CanTransition(ReferralStatus.Sent, ReferralStatus.ResponseReceived));
        Assert.True(service.CanTransition(ReferralStatus.ResponseReceived, ReferralStatus.Closed));

        var allowed = new HashSet<(ReferralStatus, ReferralStatus)>
        {
            (ReferralStatus.Draft, ReferralStatus.Sent),
            (ReferralStatus.Sent, ReferralStatus.ResponseReceived),
            (ReferralStatus.ResponseReceived, ReferralStatus.Closed)
        };
        foreach (var current in Enum.GetValues<ReferralStatus>())
        foreach (var target in Enum.GetValues<ReferralStatus>())
            if (!allowed.Contains((current, target)))
                Assert.Throws<PatientReferralTransitionException>(
                    () => service.EnsureCanTransition(current, target));
    }

    [Fact]
    public async Task ApplicationTransitionsInOrderUseActorAndReturnNewRowVersions()
    {
        var patientUid = Guid.NewGuid();
        var repository = new WorkflowRepository(Referral(patientUid));
        var actor = new Actor(73);
        var service = Service(patientUid, repository, actor);

        var sent = await service.MarkSentAsync(patientUid, repository.Current.ReferralUid,
            new ReferralStatusTransitionRequest { RowVersion = repository.Current.RowVersion });
        var received = await service.MarkResponseReceivedAsync(patientUid, repository.Current.ReferralUid,
            new ReferralStatusTransitionRequest { RowVersion = repository.Current.RowVersion });
        var closed = await service.CloseAsync(patientUid, repository.Current.ReferralUid,
            new ReferralStatusTransitionRequest { RowVersion = repository.Current.RowVersion });

        Assert.Equal("Sent", sent!.Status);
        Assert.Equal("ResponseReceived", received!.Status);
        Assert.Equal("Closed", closed!.Status);
        Assert.NotEqual(sent.RowVersion, received.RowVersion);
        Assert.NotEqual(received.RowVersion, closed.RowVersion);
        Assert.Equal([73L, 73L, 73L], repository.Actors);
        Assert.Equal(3, actor.CallCount);
    }

    [Fact]
    public async Task WrongPatientCannotTransitionAndDoesNotResolveActor()
    {
        var patientUid = Guid.NewGuid();
        var otherPatientUid = Guid.NewGuid();
        var repository = new WorkflowRepository(Referral(patientUid));
        var actor = new Actor(73);
        var service = Service([patientUid, otherPatientUid], repository, actor);

        var result = await service.MarkSentAsync(otherPatientUid, repository.Current.ReferralUid,
            new ReferralStatusTransitionRequest { RowVersion = RowVersion });

        Assert.Null(result);
        Assert.Equal(0, actor.CallCount);
        Assert.Empty(repository.Actors);
    }

    [Fact]
    public async Task StaleRowVersionIsRejectedWithoutASecondMutation()
    {
        var patientUid = Guid.NewGuid();
        var repository = new WorkflowRepository(Referral(patientUid));
        var service = Service(patientUid, repository, new Actor(73));

        await service.MarkSentAsync(patientUid, repository.Current.ReferralUid,
            new ReferralStatusTransitionRequest { RowVersion = RowVersion });

        await Assert.ThrowsAsync<PatientReferralTransitionException>(() =>
            service.MarkSentAsync(patientUid, repository.Current.ReferralUid,
                new ReferralStatusTransitionRequest { RowVersion = RowVersion }));
        Assert.Single(repository.Actors);
    }

    [Fact]
    public async Task UnmappedActorCannotTransition()
    {
        var patientUid = Guid.NewGuid();
        var repository = new WorkflowRepository(Referral(patientUid));
        var service = Service(patientUid, repository,
            new Actor(new ClinicalUserResolutionException("not mapped")));

        await Assert.ThrowsAsync<ClinicalUserResolutionException>(() =>
            service.MarkSentAsync(patientUid, repository.Current.ReferralUid,
                new ReferralStatusTransitionRequest { RowVersion = RowVersion }));
        Assert.Empty(repository.Actors);
    }

    [Fact]
    public async Task MigrationHasExplicitAtomicPatientScopedConcurrencyTimestampAndAuditRules()
    {
        var source = new FileTenantDatabaseMigrationSource(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenantProvisioning:SqlAssetsPath"] = Path.Combine(AppContext.BaseDirectory, "database")
            }).Build());
        var migration = Assert.Single(await source.GetAvailableMigrationsAsync(),
            item => item.MigrationId == "0022-patient-referral-status-workflow");
        var sql = migration.Script;

        Assert.Contains("PatientReferral_MarkSent", sql);
        Assert.Contains("PatientReferral_MarkResponseReceived", sql);
        Assert.Contains("PatientReferral_Close", sql);
        Assert.Equal(3, Count(sql, "WITH (UPDLOCK, HOLDLOCK)"));
        Assert.Equal(3, Count(sql, "r.PatientUid = @PatientUid AND r.ReferralUid = @ReferralUid"));
        Assert.Equal(3, Count(sql, "@RowVersion <> @ExpectedRowVersion"));
        Assert.Contains("SET Status = N'Sent', SentAt = @ChangedAt", sql);
        Assert.Contains("SET Status = N'ResponseReceived', ResponseReceivedAt = @ChangedAt", sql);
        Assert.Contains("SET Status = N'Closed', ClosedAt = @ChangedAt", sql);
        Assert.Equal(3, Count(sql, "UpdatedAt = @ChangedAt"));
        Assert.Equal(3, Count(sql, "INSERT dbo.AuditLog"));
    }

    private static int Count(string value, string fragment) =>
        value.Split(fragment, StringSplitOptions.None).Length - 1;

    private static PatientReferralService Service(Guid patientUid, WorkflowRepository repository, Actor actor) =>
        Service([patientUid], repository, actor);

    private static PatientReferralService Service(Guid[] patientUids, WorkflowRepository repository, Actor actor) =>
        new(repository, new Patients(patientUids), actor, new ReferralStatusTransitionService());

    private static PatientReferral Referral(Guid patientUid) => new()
    {
        ReferralUid = Guid.NewGuid(), PatientUid = patientUid, RecipientName = "Specialist",
        Reason = "Assessment", Status = ReferralStatus.Draft, CreatedAt = DateTime.UtcNow,
        CreatedBy = 12, RowVersion = RowVersion
    };

    private sealed class Actor : IAuthenticatedClinicalUserAccessor
    {
        private readonly long _id;
        private readonly Exception? _error;
        public Actor(long id) => _id = id;
        public Actor(Exception error) => _error = error;
        public int CallCount { get; private set; }
        public Task<long> GetRequiredUserIdAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return _error is null ? Task.FromResult(_id) : Task.FromException<long>(_error);
        }
    }

    private sealed class WorkflowRepository(PatientReferral referral) : IPatientReferralRepository
    {
        public PatientReferral Current { get; private set; } = referral;
        public List<long> Actors { get; } = [];
        public Task<IReadOnlyList<PatientReferral>> GetByPatientUidAsync(Guid patientUid,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PatientReferral>>(
                Current.PatientUid == patientUid ? [Current] : []);
        public Task<PatientReferral?> GetByUidAsync(Guid patientUid, Guid referralUid,
            CancellationToken cancellationToken = default) => Task.FromResult<PatientReferral?>(
                Current.PatientUid == patientUid && Current.ReferralUid == referralUid ? Current : null);
        public Task<PatientReferral> CreateAsync(Guid patientUid, CreatePatientReferralRequest request,
            long createdBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PatientReferral?> MarkSentAsync(Guid patientUid, Guid referralUid, string rowVersion,
            long updatedBy, CancellationToken cancellationToken = default) =>
            Change(ReferralStatus.Sent, rowVersion, updatedBy);
        public Task<PatientReferral?> MarkResponseReceivedAsync(Guid patientUid, Guid referralUid, string rowVersion,
            long updatedBy, CancellationToken cancellationToken = default) =>
            Change(ReferralStatus.ResponseReceived, rowVersion, updatedBy);
        public Task<PatientReferral?> CloseAsync(Guid patientUid, Guid referralUid, string rowVersion,
            long updatedBy, CancellationToken cancellationToken = default) =>
            Change(ReferralStatus.Closed, rowVersion, updatedBy);

        private Task<PatientReferral?> Change(ReferralStatus status, string rowVersion, long actor)
        {
            if (rowVersion != Current.RowVersion) throw new PatientReferralConcurrencyException();
            Actors.Add(actor);
            var at = DateTime.UtcNow;
            Current = new PatientReferral
            {
                ReferralUid = Current.ReferralUid, PatientUid = Current.PatientUid,
                RecipientName = Current.RecipientName, Reason = Current.Reason,
                Status = status, CreatedAt = Current.CreatedAt, CreatedBy = Current.CreatedBy,
                UpdatedAt = at, UpdatedBy = actor,
                SentAt = status >= ReferralStatus.Sent ? Current.SentAt ?? at : Current.SentAt,
                ResponseReceivedAt = status >= ReferralStatus.ResponseReceived ? Current.ResponseReceivedAt ?? at : Current.ResponseReceivedAt,
                ClosedAt = status == ReferralStatus.Closed ? at : Current.ClosedAt,
                RowVersion = Convert.ToBase64String([0, 0, 0, 0, 0, 0, 0, (byte)(Actors.Count + 1)])
            };
            return Task.FromResult<PatientReferral?>(Current);
        }
    }

    private sealed class Patients(Guid[] patientUids) : IPatientRepository
    {
        private readonly HashSet<Guid> _uids = patientUids.ToHashSet();
        public Task<PatientDetailsResponse?> GetByUidAsync(Guid patientUid,
            CancellationToken cancellationToken = default) => Task.FromResult(
                _uids.Contains(patientUid) ? new PatientDetailsResponse { PatientUid = patientUid } : null);
        public Task<PatientSearchResponse> SearchAsync(string? searchText, DateOnly? dateOfBirth, int pageNumber,
            int pageSize, bool includeInactive, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PatientDetailsResponse> CreateAsync(CreatePatientRequest request, long? createdBy,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PatientDetailsResponse?> UpdateDemographicsAsync(Guid patientUid,
            UpdatePatientDemographicsRequest request, long? updatedBy,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
