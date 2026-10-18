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
    public async Task AlertKeysPreserveExistingOverridesDefaultVisibleAndPersistAcrossSessions()
    {
        var store=new Store();var service=Service(store,7);
        var old=await service.SaveAsync(new(){HiddenFields=["Allergies.Reaction"],HiddenCategories=["Documents"]});
        var initial=await service.GetAsync();Assert.Equal(old.HiddenFields,initial.HiddenFields);Assert.DoesNotContain("Alerts",initial.HiddenCategories);
        Assert.Equal(new[]{"Alerts.Description","Alerts.Notes","Alerts.Type","Alerts.Priority"},Assert.Single(CppDisplayCatalog.Categories,x=>x.Key=="Alerts").Fields.Select(x=>x.Key));
        var hidden=await service.SaveAsync(new(){HiddenCategories=["Documents","Alerts"],HiddenFields=["Allergies.Reaction","Alerts.Notes"],RowVersion=old.RowVersion});
        var restored=await Service(store,7).GetAsync();Assert.Equal(hidden.HiddenCategories,restored.HiddenCategories);Assert.Equal(hidden.HiddenFields,restored.HiddenFields);Assert.Equal(hidden.RowVersion,restored.RowVersion);
        var reset=await service.SaveAsync(new(){RowVersion=hidden.RowVersion});Assert.Empty(reset.HiddenFields);Assert.Empty(reset.HiddenCategories);
    }

    [Fact]
    public async Task AllergyOnsetAndStagePreserveExistingPreferencesAndNativeDefaults()
    {
        var store=new Store();var service=Service(store,7);var old=await service.SaveAsync(new(){HiddenFields=["Allergies.Reaction"]});
        var initial=await service.GetAsync();Assert.Equal(old.HiddenFields,initial.HiddenFields);Assert.DoesNotContain("Allergies.LifeStage",initial.HiddenFields);
        var keys=Assert.Single(CppDisplayCatalog.Categories,x=>x.Key=="Allergies").Fields.Select(x=>x.Key);Assert.Contains("Allergies.OnsetDate",keys);Assert.Contains("Allergies.LifeStage",keys);Assert.DoesNotContain("Allergies.DrugDIN",keys);
        var hidden=await service.SaveAsync(new(){HiddenFields=["Allergies.Reaction","Allergies.OnsetDate","Allergies.LifeStage"],RowVersion=old.RowVersion});Assert.Equal(hidden.HiddenFields,(await Service(store,7).GetAsync()).HiddenFields);
        var reset=await service.SaveAsync(new(){RowVersion=hidden.RowVersion});Assert.Empty(reset.HiddenFields);
    }

    [Fact]
    public async Task ImmunizationFieldsPreserveExistingDateKeyAndDefaultNewFieldsVisible()
    {
        var store=new Store();var service=Service(store,7);
        var old=await service.SaveAsync(new(){HiddenFields=["Immunizations.AdministrationDate"]});
        var initial=await service.GetAsync();Assert.Equal(old.HiddenFields,initial.HiddenFields);
        var keys=Assert.Single(CppDisplayCatalog.Categories,x=>x.Key=="Immunizations").Fields.Select(x=>x.Key).ToArray();
        foreach(var key in new[]{"VaccineName","AdministrationDate","SourceType","DIN","Manufacturer","ImmunizationType"})Assert.Contains("Immunizations."+key,keys);
        Assert.DoesNotContain("Immunizations.Manufacturer",initial.HiddenFields);
        var hidden=await service.SaveAsync(new(){HiddenFields=["Immunizations.AdministrationDate","Immunizations.DIN","Immunizations.Manufacturer","Immunizations.ImmunizationType"],RowVersion=old.RowVersion});
        Assert.Equal(hidden.HiddenFields,(await Service(store,7).GetAsync()).HiddenFields);
        var reset=await service.SaveAsync(new(){RowVersion=hidden.RowVersion});Assert.Empty(reset.HiddenFields);Assert.Empty(reset.HiddenCategories);
    }

    [Fact]
    public async Task ProblemsLifeStagePreservesOldOnsetKeyAndSavedPreferences()
    {
        var store=new Store();var service=Service(store,7);
        var old=await service.SaveAsync(new(){HiddenCategories=["Problems"],HiddenFields=["Problems.OnsetDate"]});
        var initial=await service.GetAsync();Assert.Contains("Problems",initial.HiddenCategories);Assert.Contains("Problems.OnsetDate",initial.HiddenFields);Assert.DoesNotContain("Problems.LifeStage",initial.HiddenFields);
        var updated=await service.SaveAsync(new(){HiddenCategories=initial.HiddenCategories.ToList(),HiddenFields=["Problems.OnsetDate","Problems.LifeStage"],RowVersion=old.RowVersion});
        Assert.Equal(updated.HiddenFields,(await Service(store,7).GetAsync()).HiddenFields);
        var restored=await service.SaveAsync(new(){RowVersion=updated.RowVersion});Assert.Empty(restored.HiddenCategories);Assert.Empty(restored.HiddenFields);
    }

    [Fact]
    public async Task HistoryClinicalFieldsPreserveOldKeysAndDefaultNewFieldsVisible()
    {
        var store=new Store();var service=Service(store,7);
        var old=await service.SaveAsync(new(){HiddenCategories=["Documents"],HiddenFields=["History.RelevantDate"]});
        var keys=Assert.Single(CppDisplayCatalog.Categories,x=>x.Key=="History").Fields.Select(x=>x.Key).ToArray();
        Assert.Equal(9,keys.Length);Assert.Contains("History.Description",keys);
        foreach(var key in new[]{"OnsetDate","ResolutionDate","ProcedureDate","LifeStage","ClinicalStatus","Notes"})Assert.Contains("History."+key,keys);
        var initial=await service.GetAsync();Assert.Equal(old.HiddenFields,initial.HiddenFields);Assert.DoesNotContain("History.Notes",initial.HiddenFields);
        var saved=await service.SaveAsync(new(){HiddenCategories=["Documents","History"],HiddenFields=["History.RelevantDate","History.Notes"],RowVersion=old.RowVersion});
        Assert.Equal(saved.HiddenFields,(await Service(store,7).GetAsync()).HiddenFields);
        var reset=await service.SaveAsync(new(){RowVersion=saved.RowVersion});Assert.Empty(reset.HiddenFields);Assert.Empty(reset.HiddenCategories);
    }

    [Fact]
    public async Task MedicationClassificationPreservesExistingCategoryAndFieldPreferenceKeys()
    {
        var store = new Store();
        var saved = await Service(store,7).SaveAsync(new() { HiddenCategories=["Medications"], HiddenFields=["Medications.Strength"] });
        var reloaded = await Service(store,7).GetAsync();
        Assert.Equal(saved.RowVersion,reloaded.RowVersion);
        Assert.Contains("Medications",reloaded.HiddenCategories);
        Assert.Contains("Medications.Strength",reloaded.HiddenFields);
        Assert.Equal("Medications (ongoing by default)",Assert.Single(CppDisplayCatalog.Categories,x=>x.Key=="Medications").Label);
        Assert.DoesNotContain("MedicationScope",typeof(SaveCppDisplayPreferencesRequest).GetProperties().Select(x=>x.Name));
    }

    [Fact]
    public async Task RiskFactorsCategoryDefaultsAndFourFieldPreferencesPreserveFamilySettings()
    {
        var store = new Store();
        var initial = await Service(store,7).GetAsync();
        var family = await Service(store,7).SaveAsync(new() { HiddenCategories=["FamilyHistory"],HiddenFields=["FamilyHistory.Notes"],RowVersion=initial.RowVersion });
        Assert.DoesNotContain("RiskFactors",family.HiddenCategories);
        var risk = await Service(store,7).SaveAsync(new() { HiddenCategories=["FamilyHistory","RiskFactors"],HiddenFields=["FamilyHistory.Notes","RiskFactors.ExposureDetails","RiskFactors.LifeStage"],RowVersion=family.RowVersion });
        var loaded = await Service(store,7).GetAsync();
        Assert.Equal(risk.HiddenFields,loaded.HiddenFields);
        Assert.Equal(risk.HiddenCategories,loaded.HiddenCategories);
        Assert.Equal(risk.RowVersion,loaded.RowVersion);
        Assert.Contains("FamilyHistory",loaded.HiddenCategories); Assert.Contains("RiskFactors",loaded.HiddenCategories);
        Assert.Contains("FamilyHistory.Notes",loaded.HiddenFields); Assert.Contains("RiskFactors.ExposureDetails",loaded.HiddenFields);
        Assert.Equal(4,Assert.Single(CppDisplayCatalog.Categories,x=>x.Key=="RiskFactors").Fields.Count);
    }

    [Fact]
    public async Task ExistingPreferencesPreserveDefaultsForNewFamilyCategoryAndCanHideItsFields()
    {
        var store=new Store();
        await Service(store,7).SaveAsync(new() { HiddenCategories=["Documents"],HiddenFields=["Allergies.Reaction"] });
        var old=await Service(store,7).GetAsync();
        Assert.DoesNotContain("FamilyHistory",old.HiddenCategories);
        var saved=await Service(store,7).SaveAsync(new() { HiddenCategories=["FamilyHistory"],HiddenFields=["FamilyHistory.Treatment","FamilyHistory.LifeStage"],RowVersion=old.RowVersion });
        Assert.Equal(saved.HiddenFields,(await Service(store,7).GetAsync()).HiddenFields);
        Assert.Equal(6,Assert.Single(CppDisplayCatalog.Categories,x=>x.Key=="FamilyHistory").Fields.Count);
    }

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
        var familyView = File.ReadAllText(Path.Combine(Root(), "src", "MicroEMR.Web", "Views", "Patients", "_FamilyHistorySummary.cshtml"));
        var riskView = File.ReadAllText(Path.Combine(Root(), "src", "MicroEMR.Web", "Views", "Patients", "_RiskFactorsSummary.cshtml"));
        view += File.ReadAllText(Path.Combine(Root(), "src", "MicroEMR.Web", "Views", "Patients", "_ChartAlertsSummary.cshtml"));
        Assert.Equal(14, CppDisplayCatalog.Categories.Count);
        foreach (var category in CppDisplayCatalog.Categories)
        {
            Assert.Contains($"data-cpp-category=\"{category.Key}\"", view + familyView + riskView);
            foreach (var field in category.Fields)
                if (category.Key == "RiskFactors") Assert.Contains($"\"{field.Key.Split('.')[1]}\"", riskView);
                else if (category.Key == "FamilyHistory") Assert.Contains($"\"{field.Key.Split('.')[1]}\"", familyView);
                else if(category.Key=="History" && field.Key is not ("History.HistoryType" or "History.Description" or "History.RelevantDate"))
                {
                    var fields=File.ReadAllText(Path.Combine(Root(),"src","MicroEMR.Web","ClientApp","patients","history-clinical-fields.ts"));
                    Assert.Contains($"\"{field.Key.Split('.')[1]}\"",fields);
                    Assert.Contains("data-cpp-field=\"History.${key}\"",history);
                }
                else Assert.Contains($"data-cpp-field=\"{field.Key}\"", view + history);
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
