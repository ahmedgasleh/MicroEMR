using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.Patients.Repositories;

namespace MicroEMR.Application.PatientCareTeam;

public sealed class PatientCareTeamService(
    IPatientCareTeamRepository relationships,
    IPatientRepository patients,
    IAuthenticatedClinicalUserAccessor actors) : IPatientCareTeamService
{
    public async Task<IReadOnlyList<PatientCareTeamRelationship>> ListAsync(
        Guid patientUid, CancellationToken cancellationToken = default)
    {
        await RequirePatientAsync(patientUid, cancellationToken);
        return await relationships.ListAsync(patientUid, cancellationToken);
    }

    public Task<IReadOnlyList<CareTeamRelationshipType>> ListActiveTypesAsync(
        CancellationToken cancellationToken = default) => relationships.ListActiveTypesAsync(cancellationToken);

    public async Task<PatientCareTeamRelationship> AddAsync(Guid patientUid,
        AddPatientCareTeamRelationshipRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ProviderUid == Guid.Empty || string.IsNullOrWhiteSpace(request.RelationshipTypeCode) ||
            request.StartDate == default) throw new ArgumentException("Provider, relationship type, and start date are required.");
        var patient = await RequirePatientAsync(patientUid, cancellationToken);
        if (!patient.IsActive) throw new InvalidOperationException("Patient is inactive.");
        var actor = await actors.GetRequiredUserIdAsync(cancellationToken);
        return await relationships.AddAsync(patientUid,
            request with { RelationshipTypeCode = request.RelationshipTypeCode.Trim() }, actor, cancellationToken);
    }

    public async Task<PatientCareTeamRelationship> UpdateAsync(Guid patientUid, Guid relationshipUid,
        UpdatePatientCareTeamRelationshipRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Required(relationshipUid, nameof(relationshipUid));
        if (request.StartDate == default) throw new ArgumentException("Start date is required.");
        Version(request.RowVersion);
        var patient = await RequirePatientAsync(patientUid, cancellationToken);
        if (!patient.IsActive) throw new InvalidOperationException("Patient is inactive.");
        var actor = await actors.GetRequiredUserIdAsync(cancellationToken);
        return await relationships.UpdateAsync(patientUid, relationshipUid, request, actor, cancellationToken);
    }

    public async Task<PatientCareTeamRelationship> EndAsync(Guid patientUid, Guid relationshipUid,
        EndPatientCareTeamRelationshipRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Required(relationshipUid, nameof(relationshipUid));
        if (request.EndDate == default) throw new ArgumentException("End date is required.");
        Version(request.RowVersion);
        await RequirePatientAsync(patientUid, cancellationToken);
        var actor = await actors.GetRequiredUserIdAsync(cancellationToken);
        return await relationships.EndAsync(patientUid, relationshipUid, request, actor, cancellationToken);
    }

    private async Task<MicroEMR.Application.Patients.Contracts.PatientDetailsResponse> RequirePatientAsync(
        Guid patientUid, CancellationToken cancellationToken)
    {
        Required(patientUid, nameof(patientUid));
        return await patients.GetByUidAsync(patientUid, cancellationToken)
            ?? throw new KeyNotFoundException("Patient not found.");
    }

    private static void Required(Guid value, string name)
    {
        if (value == Guid.Empty) throw new ArgumentException($"{name} is required.", name);
    }

    private static void Version(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Row version is required.");
        try { if (Convert.FromBase64String(value).Length == 8) return; }
        catch (FormatException) { }
        throw new ArgumentException("Row version is invalid.");
    }
}
