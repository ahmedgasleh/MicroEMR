using System.ComponentModel.DataAnnotations;
using System.Reflection;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.Patients.Contracts;
using MicroEMR.Application.Patients.Exceptions;
using MicroEMR.Application.Patients.Repositories;
using MicroEMR.Application.Patients.Services;
using MicroEMR.Infrastructure.Patients;
using MicroEMR.Infrastructure.Tenancy;
using MicroEMR.Web.Authorization;
using Xunit;
using ApiPatientsController = MicroEMR.Api.Controllers.PatientsController;
using WebPatientsController = MicroEMR.Web.Controllers.PatientsController;
using WebCreatePatientRequest = MicroEMR.Web.Models.Patients.CreatePatientRequest;
using WebEditPatientDemographicsViewModel = MicroEMR.Web.Models.Patients.EditPatientDemographicsViewModel;

namespace MicroEMR.Api.Tests;

public sealed class PatientDemographicCertificationTests
{
    [Fact]
    public void ValidCreateAndUpdateDemographicsPassServerValidation()
    {
        Assert.Empty(Validate(ValidCreate()));
        Assert.Empty(Validate(ValidUpdate()));
        Assert.Empty(Validate(new WebCreatePatientRequest
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            DateOfBirth = new DateOnly(1815, 12, 10),
            CountryCode = "CA"
        }));
        Assert.Empty(Validate(new WebEditPatientDemographicsViewModel
        {
            PatientUid = Guid.NewGuid(),
            FirstName = "Ada",
            LastName = "Lovelace",
            DateOfBirth = new DateOnly(1815, 12, 10),
            CountryCode = "CA",
            RowVersion = Convert.ToBase64String(new byte[8])
        }));
    }

    [Fact]
    public void MissingAndWhitespaceMandatoryValuesAreRejected()
    {
        var create = ValidCreate();
        create.FirstName = "   ";
        create.LastName = "\t";
        create.DateOfBirth = null;

        var errors = Validate(create);

        Assert.Contains(errors, x => x.MemberNames.Contains(nameof(create.FirstName)));
        Assert.Contains(errors, x => x.MemberNames.Contains(nameof(create.LastName)));
        Assert.Contains(errors, x => x.MemberNames.Contains(nameof(create.DateOfBirth)));
    }

    [Fact]
    public void FutureBirthDateAndInvalidEmailAreRejectedForCreateAndUpdate()
    {
        var futureCreate = ValidCreate();
        futureCreate.DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var invalidEmailCreate = ValidCreate();
        invalidEmailCreate.Email = "not-an-email";

        var futureUpdate = ValidUpdate();
        futureUpdate.DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var invalidEmailUpdate = ValidUpdate();
        invalidEmailUpdate.Email = "not-an-email";

        Assert.Contains(Validate(futureCreate), x => x.MemberNames.Contains(nameof(futureCreate.DateOfBirth)));
        Assert.Contains(Validate(invalidEmailCreate), x => x.MemberNames.Contains(nameof(invalidEmailCreate.Email)));
        Assert.Contains(Validate(futureUpdate), x => x.MemberNames.Contains(nameof(futureUpdate.DateOfBirth)));
        Assert.Contains(Validate(invalidEmailUpdate), x => x.MemberNames.Contains(nameof(invalidEmailUpdate.Email)));
    }

    [Fact]
    public async Task ServiceForwardsResolvedActorForValidCreateAndUpdate()
    {
        var repository = new RecordingPatientRepository();
        var service = new PatientService(repository);
        var patientUid = Guid.NewGuid();

        await service.CreateAsync(ValidCreate(), 41);
        await service.UpdateDemographicsAsync(patientUid, ValidUpdate(), 42);

        Assert.Equal(41, repository.CreatedBy);
        Assert.Equal((patientUid, 42L), repository.UpdatedBy);
    }

    [Fact]
    public void PatientWritesRequireEditPermissionAtApiAndWebLayers()
    {
        AssertPermission(
            typeof(ApiPatientsController).GetMethod(nameof(ApiPatientsController.Create))!,
            PermissionKeys.PatientsEdit);
        AssertPermission(
            typeof(ApiPatientsController).GetMethod(nameof(ApiPatientsController.UpdateDemographics))!,
            PermissionKeys.PatientsEdit);

        var webEditActions = typeof(WebPatientsController).GetMethods()
            .Where(x => x.Name == nameof(WebPatientsController.Edit))
            .ToArray();
        Assert.Equal(2, webEditActions.Length);
        Assert.All(webEditActions, x => AssertPermission(x, PermissionKeys.PatientsEdit));
    }

    [Fact]
    public void RepositoryRemainsTenantScopedAndUpdateRemainsConcurrent()
    {
        var constructor = Assert.Single(typeof(PatientRepository).GetConstructors());
        Assert.Contains(
            typeof(ITenantSqlConnectionFactory),
            constructor.GetParameters().Select(x => x.ParameterType));

        var source = File.ReadAllText(Path.Combine(
            Root(), "db", "patient_stored_procedures.sql"));
        Assert.Contains("AND RowVersion = @RowVersion", source);
        Assert.Contains("THROW 51021", source);
    }

    [Fact]
    public void DemographicAuditMigrationIsAtomicActorAttributedAndConcurrent()
    {
        var migration = File.ReadAllText(Path.Combine(
            Root(), "db", "tenant-clinical", "migrations",
            "0039-patient-demographic-audit.sql"));
        var manifest = File.ReadAllText(Path.Combine(
            Root(), "db", "tenant-clinical", "manifest.json"));

        Assert.Contains("0039-patient-demographic-audit", manifest);
        Assert.Equal(2, Count(migration, "INSERT dbo.AuditLog"));
        Assert.Equal(2, Count(migration, "BEGIN TRANSACTION"));
        Assert.Equal(2, Count(migration, "COMMIT TRANSACTION"));
        Assert.Contains("@CreatedBy, @PatientId, N'Create', N'Patient'", migration);
        Assert.Contains("@UpdatedBy, @PatientId, N'UpdateDemographics', N'Patient'", migration);
        Assert.Contains("@OldValue, @NewValue", migration);
        Assert.Contains("AND RowVersion = @RowVersion", migration);
        Assert.Contains("THROW 51021", migration);
    }

    [Fact]
    public async Task InvalidUpdateDoesNotReachRepositoryOrBypassConcurrency()
    {
        var repository = new RecordingPatientRepository();
        var service = new PatientService(repository);
        var request = ValidUpdate();
        request.DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateDemographicsAsync(Guid.NewGuid(), request, 42));
        Assert.Null(repository.UpdatedBy);

        repository.ThrowConcurrency = true;
        await Assert.ThrowsAsync<PatientDemographicsConcurrencyException>(() =>
            service.UpdateDemographicsAsync(Guid.NewGuid(), ValidUpdate(), 42));
    }

    private static CreatePatientRequest ValidCreate() => new()
    {
        FirstName = "Ada",
        LastName = "Lovelace",
        DateOfBirth = new DateOnly(1815, 12, 10),
        Email = "ada@example.test",
        CountryCode = "CA"
    };

    private static UpdatePatientDemographicsRequest ValidUpdate() => new()
    {
        FirstName = "Ada",
        LastName = "Lovelace",
        DateOfBirth = new DateOnly(1815, 12, 10),
        Email = "ada@example.test",
        CountryCode = "CA",
        IsActive = true,
        RowVersion = Convert.ToBase64String(new byte[8])
    };

    private static IReadOnlyList<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        return results;
    }

    private static void AssertPermission(MemberInfo member, string expected)
    {
        var permission = Assert.Single(
            member.GetCustomAttributes(inherit: true)
                .OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(),
            x => string.Equals(
                x.Policy,
                WebPermissionPolicyProvider.Prefix + expected,
                StringComparison.Ordinal) ||
                string.Equals(
                    x.Policy,
                    MicroEMR.Api.Authorization.PermissionPolicyProvider.Prefix + expected,
                    StringComparison.Ordinal));
        Assert.NotNull(permission.Policy);
    }

    private static string Root() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static int Count(string value, string fragment) =>
        (value.Length - value.Replace(fragment, string.Empty, StringComparison.Ordinal).Length)
        / fragment.Length;

    private sealed class RecordingPatientRepository : IPatientRepository
    {
        public long? CreatedBy { get; private set; }
        public (Guid PatientUid, long Actor)? UpdatedBy { get; private set; }
        public bool ThrowConcurrency { get; set; }

        public Task<PatientDetailsResponse> CreateAsync(
            CreatePatientRequest request,
            long? createdBy,
            CancellationToken cancellationToken = default)
        {
            CreatedBy = createdBy;
            return Task.FromResult(new PatientDetailsResponse());
        }

        public Task<PatientDetailsResponse?> UpdateDemographicsAsync(
            Guid patientUid,
            UpdatePatientDemographicsRequest request,
            long? updatedBy,
            CancellationToken cancellationToken = default)
        {
            if (ThrowConcurrency)
                throw new PatientDemographicsConcurrencyException();
            UpdatedBy = (patientUid, updatedBy!.Value);
            return Task.FromResult<PatientDetailsResponse?>(new PatientDetailsResponse());
        }

        public Task<PatientSearchResponse> SearchAsync(
            string? searchText,
            DateOnly? dateOfBirth,
            int pageNumber,
            int pageSize,
            bool includeInactive,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<PatientDetailsResponse?> GetByUidAsync(
            Guid patientUid,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
