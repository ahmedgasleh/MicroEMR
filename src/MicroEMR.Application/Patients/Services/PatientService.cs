using System.ComponentModel.DataAnnotations;
using MicroEMR.Application.Patients.Contracts;
using MicroEMR.Application.Patients.Repositories;

namespace MicroEMR.Application.Patients.Services;

public sealed class PatientService : IPatientService
{
    private readonly IPatientRepository _patientRepository;

    public PatientService (
        IPatientRepository patientRepository )
    {
        _patientRepository = patientRepository;
    }

    public Task<PatientSearchResponse> SearchAsync (
        string? searchText,
        DateOnly? dateOfBirth,
        int pageNumber,
        int pageSize,
        bool includeInactive,
        CancellationToken cancellationToken = default )
    {
        return _patientRepository.SearchAsync(
            searchText,
            dateOfBirth,
            pageNumber,
            pageSize,
            includeInactive,
            cancellationToken);
    }

    public Task<PatientDetailsResponse?> GetByUidAsync (
        Guid patientUid,
        CancellationToken cancellationToken = default )
    {
        return _patientRepository.GetByUidAsync(
            patientUid,
            cancellationToken);
    }

    public Task<PatientDetailsResponse> CreateAsync (
        CreatePatientRequest request,
        long? createdBy,
        CancellationToken cancellationToken = default )
    {
        if (request.DateOfBirth.HasValue &&
            request.DateOfBirth.Value >
            DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ArgumentException(
                "Date of birth cannot be in the future.");
        }

        ValidateContacts(request.AlternativeContacts);
        return _patientRepository.CreateAsync(
            request,
            createdBy,
            cancellationToken);
    }

    public Task<PatientDetailsResponse?> UpdateDemographicsAsync(
        Guid patientUid,
        UpdatePatientDemographicsRequest request,
        long? updatedBy,
        CancellationToken cancellationToken = default)
    {
        if (request.DateOfBirth.HasValue &&
            request.DateOfBirth.Value >
            DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ArgumentException(
                "Date of birth cannot be in the future.");
        }

        ValidateContacts(request.AlternativeContacts);
        return _patientRepository.UpdateDemographicsAsync(
            patientUid,
            request,
            updatedBy,
            cancellationToken);
    }

    private static void ValidateContacts(List<PatientAlternativeContact>? contacts)
    {
        if (contacts is null) return;
        foreach (var contact in contacts)
        {
            if (contact is null) throw new ArgumentException("Alternative contact cannot be null.");
            var results = new List<ValidationResult>();
            if (!Validator.TryValidateObject(contact, new ValidationContext(contact), results, true))
                throw new ArgumentException(string.Join(" ", results.Select(x => x.ErrorMessage)));
            contact.Purposes = contact.Purposes.Distinct(StringComparer.Ordinal).ToList();
        }
    }
}
