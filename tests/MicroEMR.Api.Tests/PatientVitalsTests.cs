using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.PatientVitals;
using MicroEMR.Application.PatientVitals.Contracts;
using MicroEMR.Application.PatientVitals.Services;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PatientVitalsTests
{
    [Fact]
    public void PartialVitals_AreValidAndDoNotAcceptBrowserBmi()
    {
        var request = new CreatePatientVitalRequest
        {
            RecordedAt = DateTime.UtcNow,
            BloodPressureSystolic = 120,
            BloodPressureDiastolic = 80
        };

        Assert.Empty(Validate(request));
        Assert.Null(typeof(CreatePatientVitalRequest).GetProperty("Bmi"));
    }

    [Fact]
    public void InvalidMeasurements_AreRejected()
    {
        var invalidRequests = new[]
        {
            new CreatePatientVitalRequest { RecordedAt = DateTime.UtcNow, OxygenSaturation = 101 },
            new CreatePatientVitalRequest { RecordedAt = DateTime.UtcNow, HeightCm = 0m },
            new CreatePatientVitalRequest { RecordedAt = DateTime.UtcNow, WeightKg = 0m }
        };

        Assert.All(invalidRequests, request => Assert.NotEmpty(Validate(request)));
    }

    [Fact]
    public async Task Create_UsesResolvedClinicalActor()
    {
        var service = new StubPatientVitalService();
        var controller = CreateController(service, 73);

        await controller.Create(Guid.NewGuid(), new CreatePatientVitalRequest { RecordedAt = DateTime.UtcNow }, default);

        Assert.Equal(73, service.LastActorId);
    }

    [Fact]
    public async Task Update_UsesResolvedClinicalActor()
    {
        var service = new StubPatientVitalService();
        var controller = CreateController(service, 84);

        await controller.Update(Guid.NewGuid(), Guid.NewGuid(), new UpdatePatientVitalRequest
        {
            RecordedAt = DateTime.UtcNow,
            RowVersion = Convert.ToBase64String(new byte[8])
        }, default);

        Assert.Equal(84, service.LastActorId);
    }

    [Fact]
    public async Task Update_StaleRowVersionReturnsSafeConflict()
    {
        var controller = CreateController(new StubPatientVitalService
        {
            UpdateException = new PatientVitalConcurrencyException("database detail")
        }, 42);

        var result = await controller.Update(Guid.NewGuid(), Guid.NewGuid(), new UpdatePatientVitalRequest
        {
            RecordedAt = DateTime.UtcNow,
            RowVersion = Convert.ToBase64String(new byte[8])
        }, default);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.DoesNotContain("database detail", conflict.Value!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StabilityMigration_ProtectsOrderingIsolationBmiAndConcurrency()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "database", "tenant-clinical", "migrations", "0020-vitals-stability.sql");
        var sql = File.ReadAllText(path);

        Assert.Contains("ORDER BY pv.RecordedAt DESC", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHERE pv.PatientUid = @PatientUid", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PatientUid = @PatientUid AND PatientVitalUid = @PatientVitalUid AND RowVersion = @RowVersion", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@WeightKg / POWER(@HeightCm / 100.0, 2)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@HeightCm IS NOT NULL AND @WeightKg IS NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("THROW 51401", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("THROW 51402", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, true);
        return results;
    }

    private static PatientVitalsController CreateController(StubPatientVitalService service, long actorId)
    {
        var controller = new PatientVitalsController(service, NullLogger<PatientVitalsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        MicroEMR.Api.ClinicalUsers.ClinicalUserActorContext.Set(controller.HttpContext, actorId);
        return controller;
    }

    private sealed class StubPatientVitalService : IPatientVitalService
    {
        public long? LastActorId { get; private set; }
        public Exception? UpdateException { get; init; }

        public Task<IReadOnlyList<PatientVitalResponse>> GetByPatientUidAsync(Guid patientUid, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PatientVitalResponse>>([]);

        public Task<PatientVitalResponse?> GetByUidAsync(Guid patientUid, Guid patientVitalUid, CancellationToken cancellationToken = default) =>
            Task.FromResult<PatientVitalResponse?>(null);

        public Task<PatientVitalResponse?> CreateAsync(Guid patientUid, CreatePatientVitalRequest request, long? createdBy, CancellationToken cancellationToken = default)
        {
            LastActorId = createdBy;
            return Task.FromResult<PatientVitalResponse?>(new PatientVitalResponse { PatientUid = patientUid, PatientVitalUid = Guid.NewGuid() });
        }

        public Task<PatientVitalResponse?> UpdateAsync(Guid patientUid, Guid patientVitalUid, UpdatePatientVitalRequest request, long? updatedBy, CancellationToken cancellationToken = default)
        {
            LastActorId = updatedBy;
            return UpdateException is null
                ? Task.FromResult<PatientVitalResponse?>(new PatientVitalResponse { PatientUid = patientUid, PatientVitalUid = patientVitalUid })
                : Task.FromException<PatientVitalResponse?>(UpdateException);
        }
    }
}
