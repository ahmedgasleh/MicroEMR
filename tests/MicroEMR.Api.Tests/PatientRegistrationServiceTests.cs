using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.PatientCareTeam;
using MicroEMR.Application.Patients.Contracts;
using MicroEMR.Application.Patients.Services;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PatientRegistrationServiceTests
{
    [Fact]
    public async Task NoProvidersCreatesPatientWithoutAssignments()
    {
        var patients = new PatientWriter();
        var careTeam = new CareTeamWriter();
        var result = await Service(patients, careTeam).RegisterAsync(new(new CreatePatientRequest(), null, null, null), 42);

        Assert.Equal(patients.PatientUid, result.Patient.PatientUid);
        Assert.Equal(42, patients.ActorUserId);
        Assert.Empty(careTeam.Assignments);
        Assert.Empty(result.FailedCareTeamRoles);
    }

    [Fact]
    public async Task SelectedProvidersUseExistingRolesAsPrimary()
    {
        var patients = new PatientWriter();
        var careTeam = new CareTeamWriter();
        var referring = Guid.NewGuid();
        var attending = Guid.NewGuid();
        var pcp = Guid.NewGuid();

        var result = await Service(patients, careTeam).RegisterAsync(
            new(new CreatePatientRequest(), referring, attending, pcp), 42);

        Assert.Empty(result.FailedCareTeamRoles);
        Assert.Equal(3, careTeam.Assignments.Count);
        Assert.Equal(["REFERRING", "ATTENDING", "PCP"], careTeam.Assignments.Select(item => item.RelationshipTypeCode));
        Assert.Equal([referring, attending, pcp], careTeam.Assignments.Select(item => item.ProviderUid));
        Assert.All(careTeam.Assignments, item =>
        {
            Assert.True(item.IsPrimary);
            Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), item.StartDate);
        });
    }

    [Theory]
    [InlineData("REFERRING")]
    [InlineData("ATTENDING")]
    [InlineData("PCP")]
    public async Task ASelectedRoleCreatesOneRelationship(string code)
    {
        var careTeam = new CareTeamWriter();
        var providerUid = Guid.NewGuid();
        var request = new RegisterPatientRequest(new CreatePatientRequest(),
            code == "REFERRING" ? providerUid : null,
            code == "ATTENDING" ? providerUid : null,
            code == "PCP" ? providerUid : null);

        await Service(new PatientWriter(), careTeam).RegisterAsync(request, 42);

        var assignment = Assert.Single(careTeam.Assignments);
        Assert.Equal(code, assignment.RelationshipTypeCode);
        Assert.Equal(providerUid, assignment.ProviderUid);
    }

    [Fact]
    public async Task FailedAssignmentIsReportedAfterPatientCreationAndOthersContinue()
    {
        var patients = new PatientWriter();
        var careTeam = new CareTeamWriter { FailingCode = "ATTENDING" };
        var result = await Service(patients, careTeam).RegisterAsync(
            new(new CreatePatientRequest(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), 42);

        Assert.Equal(patients.PatientUid, result.Patient.PatientUid);
        Assert.Equal(["Referring Physician", "Primary Care Physician"], careTeam.SucceededRoles);
        Assert.Equal(["Attending Physician"], result.FailedCareTeamRoles);
    }

    private static PatientRegistrationService Service(PatientWriter patients, CareTeamWriter careTeam) =>
        new(patients, careTeam, NullLogger<PatientRegistrationService>.Instance);

    private sealed class PatientWriter : IPatientService
    {
        public Guid PatientUid { get; } = Guid.NewGuid();
        public long? ActorUserId { get; private set; }
        public Task<PatientDetailsResponse> CreateAsync(CreatePatientRequest request, long? createdBy,
            CancellationToken cancellationToken = default)
        {
            ActorUserId = createdBy;
            return Task.FromResult(new PatientDetailsResponse { PatientUid = PatientUid });
        }
        public Task<PatientDetailsResponse?> GetByUidAsync(Guid patientUid, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PatientSearchResponse> SearchAsync(string? searchText, DateOnly? dateOfBirth, int pageNumber, int pageSize,
            bool includeInactive, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PatientDetailsResponse?> UpdateDemographicsAsync(Guid patientUid, UpdatePatientDemographicsRequest request,
            long? updatedBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class CareTeamWriter : IPatientCareTeamService
    {
        public string? FailingCode { get; init; }
        public List<AddPatientCareTeamRelationshipRequest> Assignments { get; } = [];
        public List<string> SucceededRoles { get; } = [];
        public Task<PatientCareTeamRelationship> AddAsync(Guid patientUid, AddPatientCareTeamRelationshipRequest request,
            CancellationToken cancellationToken = default)
        {
            Assignments.Add(request);
            if (request.RelationshipTypeCode == FailingCode) throw new InvalidOperationException("Provider became inactive.");
            SucceededRoles.Add(request.RelationshipTypeCode switch
            {
                "REFERRING" => "Referring Physician",
                "ATTENDING" => "Attending Physician",
                _ => "Primary Care Physician"
            });
            return Task.FromResult(new PatientCareTeamRelationship(Guid.NewGuid(), patientUid, request.ProviderUid,
                "Provider", request.RelationshipTypeCode, request.RelationshipTypeCode, true, request.StartDate,
                null, true, DateTime.UtcNow, 42, null, null, "AAAAAAAAAAA="));
        }
        public Task<IReadOnlyList<PatientCareTeamRelationship>> ListAsync(Guid patientUid, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CareTeamRelationshipType>> ListActiveTypesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PatientCareTeamRelationship> UpdateAsync(Guid patientUid, Guid relationshipUid,
            UpdatePatientCareTeamRelationshipRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PatientCareTeamRelationship> EndAsync(Guid patientUid, Guid relationshipUid,
            EndPatientCareTeamRelationshipRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
