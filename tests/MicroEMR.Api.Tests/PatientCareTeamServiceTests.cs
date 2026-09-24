using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PatientCareTeam;
using MicroEMR.Application.Patients.Contracts;
using MicroEMR.Application.Patients.Repositories;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PatientCareTeamServiceTests
{
    [Fact]
    public async Task AddUsesClinicalActorAndNormalizesTypeCode()
    {
        var repository = new RecordingCareTeamRepository();
        var service = new PatientCareTeamService(repository, new PatientLookup(true), new Actor());
        var patientUid = Guid.NewGuid();
        var request = new AddPatientCareTeamRelationshipRequest(Guid.NewGuid(), " PCP ", true, new DateOnly(2026, 1, 1));

        await service.AddAsync(patientUid, request);

        Assert.Equal(patientUid, repository.PatientUid);
        Assert.Equal("PCP", repository.Request!.RelationshipTypeCode);
        Assert.Equal(42, repository.ActorUserId);
    }

    [Fact]
    public async Task AddRejectsInactivePatientBeforeWriting()
    {
        var repository = new RecordingCareTeamRepository();
        var service = new PatientCareTeamService(repository, new PatientLookup(false), new Actor());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddAsync(Guid.NewGuid(),
            new AddPatientCareTeamRelationshipRequest(Guid.NewGuid(), "PCP", false, new DateOnly(2026, 1, 1))));

        Assert.Null(repository.Request);
    }

    private sealed class Actor : IAuthenticatedClinicalUserAccessor
    {
        public Task<long> GetRequiredUserIdAsync(CancellationToken cancellationToken = default) => Task.FromResult(42L);
    }

    private sealed class PatientLookup(bool active) : IPatientRepository
    {
        public Task<PatientDetailsResponse?> GetByUidAsync(Guid patientUid, CancellationToken cancellationToken = default) =>
            Task.FromResult<PatientDetailsResponse?>(new PatientDetailsResponse { PatientUid = patientUid, IsActive = active });
        public Task<PatientSearchResponse> SearchAsync(string? searchText, DateOnly? dateOfBirth, int pageNumber,
            int pageSize, bool includeInactive, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PatientDetailsResponse> CreateAsync(CreatePatientRequest request, long? createdBy,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PatientDetailsResponse?> UpdateDemographicsAsync(Guid patientUid, UpdatePatientDemographicsRequest request,
            long? updatedBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingCareTeamRepository : IPatientCareTeamRepository
    {
        public Guid PatientUid { get; private set; }
        public AddPatientCareTeamRelationshipRequest? Request { get; private set; }
        public long ActorUserId { get; private set; }
        public Task<IReadOnlyList<PatientCareTeamRelationship>> ListAsync(Guid patientUid,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CareTeamRelationshipType>> ListActiveTypesAsync(
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PatientCareTeamRelationship> AddAsync(Guid patientUid, AddPatientCareTeamRelationshipRequest request,
            long actorUserId, CancellationToken cancellationToken = default)
        {
            PatientUid = patientUid;
            Request = request;
            ActorUserId = actorUserId;
            return Task.FromResult(new PatientCareTeamRelationship(Guid.NewGuid(), patientUid, request.ProviderUid, "Provider",
                request.RelationshipTypeCode, "Primary Care Physician", request.IsPrimary, request.StartDate, null, true,
                DateTime.UtcNow, actorUserId, null, null, "AAAAAAAAAAA="));
        }
        public Task<PatientCareTeamRelationship> UpdateAsync(Guid patientUid, Guid relationshipUid,
            UpdatePatientCareTeamRelationshipRequest request, long actorUserId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PatientCareTeamRelationship> EndAsync(Guid patientUid, Guid relationshipUid,
            EndPatientCareTeamRelationshipRequest request, long actorUserId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
