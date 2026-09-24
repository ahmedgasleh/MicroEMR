namespace MicroEMR.Application.PatientCareTeam;

public sealed record CareTeamRelationshipType(int RelationshipTypeId, string Code, string DisplayName, int DisplayOrder);

public sealed record PatientCareTeamRelationship(
    Guid RelationshipUid, Guid PatientUid, Guid ProviderUid, string ProviderDisplayName,
    string RelationshipTypeCode, string RelationshipTypeDisplayName, bool IsPrimary,
    DateOnly StartDate, DateOnly? EndDate, bool IsActive, DateTime CreatedAt,
    long CreatedBy, DateTime? UpdatedAt, long? UpdatedBy, string RowVersion);

public sealed record AddPatientCareTeamRelationshipRequest(
    Guid ProviderUid, string RelationshipTypeCode, bool IsPrimary, DateOnly StartDate);

public sealed record UpdatePatientCareTeamRelationshipRequest(
    bool IsPrimary, DateOnly StartDate, string RowVersion);

public sealed record EndPatientCareTeamRelationshipRequest(DateOnly EndDate, string RowVersion);

public interface IPatientCareTeamRepository
{
    Task<IReadOnlyList<PatientCareTeamRelationship>> ListAsync(Guid patientUid, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CareTeamRelationshipType>> ListActiveTypesAsync(CancellationToken cancellationToken = default);
    Task<PatientCareTeamRelationship> AddAsync(Guid patientUid, AddPatientCareTeamRelationshipRequest request,
        long actorUserId, CancellationToken cancellationToken = default);
    Task<PatientCareTeamRelationship> UpdateAsync(Guid patientUid, Guid relationshipUid,
        UpdatePatientCareTeamRelationshipRequest request, long actorUserId, CancellationToken cancellationToken = default);
    Task<PatientCareTeamRelationship> EndAsync(Guid patientUid, Guid relationshipUid,
        EndPatientCareTeamRelationshipRequest request, long actorUserId, CancellationToken cancellationToken = default);
}

public interface IPatientCareTeamService
{
    Task<IReadOnlyList<PatientCareTeamRelationship>> ListAsync(Guid patientUid, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CareTeamRelationshipType>> ListActiveTypesAsync(CancellationToken cancellationToken = default);
    Task<PatientCareTeamRelationship> AddAsync(Guid patientUid, AddPatientCareTeamRelationshipRequest request,
        CancellationToken cancellationToken = default);
    Task<PatientCareTeamRelationship> UpdateAsync(Guid patientUid, Guid relationshipUid,
        UpdatePatientCareTeamRelationshipRequest request, CancellationToken cancellationToken = default);
    Task<PatientCareTeamRelationship> EndAsync(Guid patientUid, Guid relationshipUid,
        EndPatientCareTeamRelationshipRequest request, CancellationToken cancellationToken = default);
}
