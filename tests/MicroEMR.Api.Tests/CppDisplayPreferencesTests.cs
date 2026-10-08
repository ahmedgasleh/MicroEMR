using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PatientCpp;
using Xunit;
using ApiController = MicroEMR.Api.Controllers.CppDisplayPreferencesController;
using WebController = MicroEMR.Web.Controllers.CppDisplayPreferencesController;

namespace MicroEMR.Api.Tests;

public sealed class CppDisplayPreferencesTests
{
    [Fact]
    public async Task MissingPreferencesKeepEveryExistingCategoryAndFieldVisible()
    {
        var repository = new Store();
        var result = await Service(repository, 7).GetAsync();
        Assert.Empty(result.HiddenCategories);
        Assert.Empty(result.HiddenFields);
        Assert.Null(result.RowVersion);
        Assert.True(result.IsAvailable);
        Assert.Equal(7, repository.LastUserId);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task CategoryAndDiscreteFieldPreferencesSurviveNewServicesAndSessions()
    {
        var repository = new Store();
        var saved = await Service(repository, 7).SaveAsync(new()
        {
            HiddenCategories = ["Documents"], HiddenFields = ["Allergies.Reaction", "History.RelevantDate"]
        });
        var refreshed = await Service(repository, 7).GetAsync();
        var newSession = await Service(repository, 7).GetAsync();
        Assert.Equal(saved.HiddenCategories, refreshed.HiddenCategories);
        Assert.Equal(saved.HiddenFields, newSession.HiddenFields);
        Assert.Equal(saved.RowVersion, newSession.RowVersion);
        Assert.Equal(7, repository.LastUserId);
    }

    [Fact]
    public async Task PreferencesAreBoundToResolvedUserAndTenantRepository()
    {
        var tenantA = new Store();
        var tenantB = new Store();
        await Service(tenantA, 7).SaveAsync(new() { HiddenCategories = ["Problems"] });
        Assert.Empty((await Service(tenantA, 8).GetAsync()).HiddenCategories);
        Assert.Empty((await Service(tenantB, 7).GetAsync()).HiddenCategories);
        Assert.Equal(new[] { "Problems" }, (await Service(tenantA, 7).GetAsync()).HiddenCategories);
        Assert.DoesNotContain(typeof(SaveCppDisplayPreferencesRequest).GetProperties(), x =>
            x.Name.Contains("UserId") || x.Name.Contains("Tenant") || x.Name.Contains("Patient"));
    }

    [Fact]
    public async Task RestoringDefaultsPersistsAnEmptyOverrideWithoutDeletingThePreference()
    {
        var repository = new Store();
        var saved = await Service(repository, 7).SaveAsync(new() { HiddenFields = ["Results.Abnormality"] });
        var restored = await Service(repository, 7).SaveAsync(new() { RowVersion = saved.RowVersion });
        Assert.Empty(restored.HiddenCategories);
        Assert.Empty(restored.HiddenFields);
        Assert.NotEqual(saved.RowVersion, restored.RowVersion);
        Assert.Single(repository.Records);
        Assert.Empty((await Service(repository, 7).GetAsync()).HiddenFields);
    }

    [Fact]
    public async Task StaleSavesCannotOverwriteAnotherSessionOrCreateSuccessWrites()
    {
        var repository = new Store();
        var service = Service(repository, 7);
        var first = await service.SaveAsync(new() { HiddenCategories = ["Problems"] });
        await Assert.ThrowsAsync<CppDisplayPreferencesConcurrencyException>(() =>
            service.SaveAsync(new() { HiddenCategories = ["Documents"] }));
        var second = await service.SaveAsync(new() { HiddenCategories = ["Medications"], RowVersion = first.RowVersion });
        await Assert.ThrowsAsync<CppDisplayPreferencesConcurrencyException>(() =>
            service.SaveAsync(new() { RowVersion = first.RowVersion }));
        Assert.Equal(2, repository.SaveCount);
        Assert.Equal(second.HiddenCategories, (await service.GetAsync()).HiddenCategories);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("null")]
    [InlineData("{\"HiddenCategories\":[\"Unknown\"]}")]
    [InlineData("{\"HiddenFields\":null}")]
    public async Task InvalidStoredSettingsFallBackToFullDisplayAndCanBeRepaired(string json)
    {
        var repository = new Store();
        repository.Records[7] = new(json, Version(1));
        var response = await Service(repository, 7).GetAsync();
        Assert.Empty(response.HiddenCategories);
        Assert.Empty(response.HiddenFields);
        Assert.Equal(Version(1), response.RowVersion);
        Assert.True(response.IsAvailable);
        var repaired = await Service(repository, 7).SaveAsync(new() { RowVersion = response.RowVersion });
        Assert.Empty(repaired.HiddenFields);
    }

    [Fact]
    public async Task IncompleteSettingsKeepUnspecifiedInformationVisible()
    {
        var repository = new Store();
        repository.Records[7] = new("{\"HiddenCategories\":[\"Documents\"]}", Version(1));
        var response = await Service(repository, 7).GetAsync();
        Assert.Equal(new[] { "Documents" }, response.HiddenCategories);
        Assert.Empty(response.HiddenFields);
    }

    [Fact]
    public async Task UnknownKeysNullCollectionsAndInvalidVersionsAreRejectedBeforeWriting()
    {
        var repository = new Store();
        var service = Service(repository, 7);
        foreach (var request in new SaveCppDisplayPreferencesRequest[]
        {
            new() { HiddenCategories = ["Unknown"] }, new() { HiddenFields = ["Problems.Unknown"] },
            new() { HiddenFields = null! }, new() { HiddenCategories = null! },
            new() { RowVersion = "bad" }, new() { RowVersion = Convert.ToBase64String([1]) }
        })
            await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(request));
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task UnavailableStorageFallsBackWithoutAllowingAnEditorToBlindlyOverwrite()
    {
        var result = await Service(new Store { ReadFailure = new InvalidOperationException() }, 7).GetAsync();
        Assert.Empty(result.HiddenCategories);
        Assert.Empty(result.HiddenFields);
        Assert.False(result.IsAvailable);
        Assert.Null(result.RowVersion);
    }

    [Fact]
    public async Task IdentityAndCancellationFailuresDoNotTurnIntoSuccessfulDefaultResponses()
    {
        var repository = new Store();
        var service = new CppDisplayPreferencesService(repository, new Actor(null), NullLogger<CppDisplayPreferencesService>.Instance);
        await Assert.ThrowsAsync<ClinicalUserResolutionException>(() => service.GetAsync());
        await Assert.ThrowsAsync<ClinicalUserResolutionException>(() => service.SaveAsync(new()));
        Assert.Null(repository.LastUserId);
        var controller = new ApiController(service);
        Assert.IsType<ForbidResult>((await controller.Get(default)).Result);
        repository.ReadFailure = new OperationCanceledException();
        await Assert.ThrowsAsync<OperationCanceledException>(() => Service(repository, 7).GetAsync());
    }

    [Fact]
    public async Task ApiResponsesDistinguishValidationConflictAndSuccess()
    {
        var controller = new ApiController(Service(new Store(), 7));
        Assert.IsType<BadRequestObjectResult>((await controller.Save(new() { HiddenFields = ["Unknown"] }, default)).Result);
        Assert.IsType<OkObjectResult>((await controller.Save(new() { HiddenCategories = ["Documents"] }, default)).Result);
        Assert.IsType<ConflictObjectResult>((await controller.Save(new(), default)).Result);
    }

    [Fact]
    public void EndpointsRequirePatientPermissionAndWebWritesRequireAntiforgery()
    {
        var policies = typeof(ApiController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>();
        Assert.Contains(policies, x => x.Policy == PermissionPolicyProvider.Prefix + PermissionKeys.PatientsView);
        Assert.NotNull(typeof(WebController).GetMethod("Save")!.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true).SingleOrDefault());
        Assert.True(Assert.Single(typeof(ApiController).GetCustomAttributes(typeof(ResponseCacheAttribute), true).Cast<ResponseCacheAttribute>()).NoStore);
        Assert.True(Assert.Single(typeof(WebController).GetCustomAttributes(typeof(ResponseCacheAttribute), true).Cast<ResponseCacheAttribute>()).NoStore);
    }

    [Fact]
    public void MigrationAndRepositoryKeepUserTenantConcurrencyAndConfigurationAuditBoundaries()
    {
        var sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "database", "tenant-clinical", "migrations", "0067-cpp-display-preferences.sql"));
        Assert.Contains("WHERE UserId = @UserId", sql);
        Assert.Contains("UPDLOCK, HOLDLOCK", sql);
        Assert.Contains("@CurrentVersion <> @ExpectedRowVersion", sql);
        Assert.Contains("FOREIGN KEY(UserId) REFERENCES dbo.ApplicationUser(UserId)", sql);
        Assert.Contains("AND IsActive = 1", sql);
        Assert.Contains("INSERT dbo.AuditLog(UserId, ActionName, EntityName, EntityId, OldValue, NewValue)", sql);
        Assert.DoesNotContain("DELETE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE dbo.Patient", sql);
        var source = File.ReadAllText(Path.Combine(Root(), "src", "MicroEMR.Infrastructure", "PatientCpp", "CppDisplayPreferencesRepository.cs"));
        Assert.Contains("ITenantSqlConnectionFactory", source);
        Assert.Contains("CommandType.StoredProcedure", source);
        Assert.Contains("SqlDbType.BigInt).Value = userId", source);
        Assert.Contains("exception.Number == 52601", source);
    }

    [Fact]
    public void EveryExistingDisplayedFieldAndCategoryHasACustomizationTargetWithoutRemovingFullChartLinks()
    {
        var view = File.ReadAllText(Path.Combine(Root(), "src", "MicroEMR.Web", "Views", "Patients", "Details.cshtml"));
        var history = File.ReadAllText(Path.Combine(Root(), "src", "MicroEMR.Web", "ClientApp", "patients", "patient-clinical-history.ts"));
        Assert.Equal(11, CppDisplayCatalog.Categories.Count);
        foreach (var category in CppDisplayCatalog.Categories)
        {
            Assert.Contains($"data-cpp-category=\"{category.Key}\"", view);
            foreach (var field in category.Fields)
                Assert.Contains($"data-cpp-field=\"{field.Key}\"", view + history);
        }
        Assert.Contains("View all allergies", view);
        Assert.Contains("Allergy status not documented.", view);
        Assert.Contains("No Known Allergies", view);
        Assert.Contains("Restricted.", view);
        Assert.DoesNotContain("CppDisplay", File.ReadAllText(Path.Combine(Root(), "src", "MicroEMR.Application", "PatientCpp", "PatientCppService.cs")));
    }

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "MicroEMR.slnx"))) return directory.FullName;
        throw new InvalidOperationException("Repository root not found.");
    }

    private static string Version(long number) => Convert.ToBase64String(BitConverter.GetBytes(number));
    private static CppDisplayPreferencesService Service(Store repository, long userId) =>
        new(repository, new Actor(userId), NullLogger<CppDisplayPreferencesService>.Instance);

    private sealed class Actor(long? id) : IAuthenticatedClinicalUserAccessor
    {
        public Task<long> GetRequiredUserIdAsync(CancellationToken cancellationToken = default) => id.HasValue
            ? Task.FromResult(id.Value) : throw new ClinicalUserResolutionException("No active clinical user.");
    }

    // A new service/actor represents refresh/relogin; repository instances represent tenant stores.
    // This is not live SQL or authenticated browser evidence.
    private sealed class Store : ICppDisplayPreferencesRepository
    {
        public Dictionary<long, CppDisplayPreferencesData> Records { get; } = [];
        public long? LastUserId { get; private set; }
        public int SaveCount { get; private set; }
        public Exception? ReadFailure { get; set; }

        public Task<CppDisplayPreferencesData?> GetAsync(long userId, CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            if (ReadFailure is not null) throw ReadFailure;
            return Task.FromResult(Records.GetValueOrDefault(userId));
        }

        public Task<CppDisplayPreferencesData> SaveAsync(long userId, string settingsJson, string? expectedRowVersion, CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            if (Records.GetValueOrDefault(userId)?.RowVersion != expectedRowVersion) throw new CppDisplayPreferencesConcurrencyException();
            var data = new CppDisplayPreferencesData(settingsJson, Version(++SaveCount));
            Records[userId] = data;
            return Task.FromResult(data);
        }
    }
}
