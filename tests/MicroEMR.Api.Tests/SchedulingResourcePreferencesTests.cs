using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.Scheduling.Contracts;
using MicroEMR.Application.Scheduling.Preferences;
using MicroEMR.Application.Scheduling.Repositories;
using MicroEMR.Infrastructure.Scheduling;
using MicroEMR.Infrastructure.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class SchedulingResourcePreferencesTests
{
    private static readonly Guid Provider = Guid.NewGuid(), Room = Guid.NewGuid(), Equipment = Guid.NewGuid();
    private static readonly ScheduleResourceResponse[] Active = [
        new() {ResourceUid=Provider,ResourceType="Provider",IsActive=true},
        new() {ResourceUid=Room,ResourceType="Room",IsActive=true},
        new() {ResourceUid=Equipment,ResourceType="Equipment",IsActive=true}];

    [Fact]
    public async Task MissingDefaultsDoesNotCreateARecord()
    {
        var store=new Store();var result=await Service(store).GetAsync();
        Assert.False(result.HasSavedDefaults);Assert.True(result.IsAvailable);Assert.Null(result.RowVersion);
        Assert.Empty(result.ResourceUids);Assert.Empty(store.Records);Assert.Equal(0,store.Saves);
    }

    [Fact]
    public async Task ExplicitSavePersistsOrderedDeduplicatedResourcesAcrossNewServiceAndUserSessions()
    {
        var store=new Store();
        var saved=await Service(store).SaveAsync(new(){ResourceUids=[Room,Provider,Room,Equipment]});
        Assert.Equal(new[]{Room,Provider,Equipment},saved.ResourceUids);
        var reloaded=await Service(store).GetAsync();
        Assert.Equal(saved.ResourceUids,reloaded.ResourceUids);Assert.Equal(saved.RowVersion,reloaded.RowVersion);
        Assert.True(reloaded.HasSavedDefaults);Assert.Equal(1,store.Saves);
        Assert.False((await Service(store,8).GetAsync()).HasSavedDefaults);
        await Service(store,8).SaveAsync(new(){ResourceUids=[Provider]});
        Assert.Equal(new[]{Room,Provider,Equipment},(await Service(store,7).GetAsync()).ResourceUids);
        Assert.False((await Service(new Store(),7).GetAsync()).HasSavedDefaults); // Separate tenant store.
    }

    [Fact]
    public async Task InvalidForeignEmptyAndInactiveSelectionsDoNotOverwriteDefaults()
    {
        var store=new Store();var old=await Service(store).SaveAsync(new(){ResourceUids=[Provider]});
        foreach(var invalid in new List<Guid>[] {[],[Guid.Empty],[Guid.NewGuid()],Enumerable.Repeat(Room,201).ToList()})
            await Assert.ThrowsAsync<ArgumentException>(()=>Service(store).SaveAsync(new(){ResourceUids=invalid,RowVersion=old.RowVersion}));
        await Assert.ThrowsAsync<ArgumentException>(()=>Service(store,active:[]).SaveAsync(new(){ResourceUids=[Room],RowVersion=old.RowVersion}));
        Assert.Equal(new[]{Provider},(await Service(store).GetAsync()).ResourceUids);Assert.Equal(1,store.Saves);
    }

    [Fact]
    public async Task ReadsOmitUnavailableResourcesWithoutRewritingSavedDataOrOrder()
    {
        var store=new Store();await Service(store).SaveAsync(new(){ResourceUids=[Room,Provider,Equipment]});
        var original=store.Records[7];
        var partial=await Service(store,active:[Active[2],Active[0]]).GetAsync();
        Assert.Equal(new[]{Provider,Equipment},partial.ResourceUids);Assert.Equal(1,partial.UnavailableResourceCount);
        var missing=await Service(store,active:[]).GetAsync();Assert.True(missing.HasSavedDefaults);
        Assert.Empty(missing.ResourceUids);Assert.Equal(3,missing.UnavailableResourceCount);
        Assert.Equal(original,store.Records[7]);Assert.Equal(1,store.Saves);
    }

    [Fact]
    public async Task AvailabilityAndStorageFailuresReturnFallbackButIdentityFailureIsNotSuppressed()
    {
        var store=new Store { ReadFailure=new InvalidOperationException("internal details") };
        Assert.False((await Service(store).GetAsync()).IsAvailable);
        Assert.Equal(0,store.Saves);
        await Assert.ThrowsAsync<ClinicalUserResolutionException>(()=>Service(store,user:null).GetAsync());
        await Assert.ThrowsAsync<ClinicalUserResolutionException>(()=>Service(store,user:null).SaveAsync(new(){ResourceUids=[Provider]}));
    }

    [Fact]
    public async Task StaleAndMalformedVersionsCannotOverwriteSavedDefaults()
    {
        var store=new Store();var initial=await Service(store).SaveAsync(new(){ResourceUids=[Provider]});
        var saved=await Service(store).SaveAsync(new(){ResourceUids=[Room],RowVersion=initial.RowVersion});
        await Assert.ThrowsAsync<SchedulingResourcePreferencesConcurrencyException>(()=>Service(store).SaveAsync(new(){ResourceUids=[Equipment],RowVersion=initial.RowVersion}));
        await Assert.ThrowsAsync<SchedulingResourcePreferencesConcurrencyException>(()=>Service(store).SaveAsync(new(){ResourceUids=[Equipment]}));
        await Assert.ThrowsAsync<ArgumentException>(()=>Service(store).SaveAsync(new(){ResourceUids=[Equipment],RowVersion="invalid"}));
        Assert.Equal(saved.ResourceUids,(await Service(store).GetAsync()).ResourceUids);Assert.Equal(2,store.Saves);
    }

    [Fact]
    public void EndpointsUseSchedulingViewAntiforgeryNoStoreAndHaveNoClientIdentityAuthority()
    {
        foreach(var type in new[]{typeof(MicroEMR.Api.Controllers.SchedulingResourcePreferencesController),typeof(MicroEMR.Web.Controllers.SchedulingResourcePreferencesController)})
        {
            Assert.Contains(type.GetCustomAttributes<AuthorizeAttribute>(),x=>x.Policy=="Permission:Scheduling.View");
            Assert.Empty(type.GetCustomAttributes<AllowAnonymousAttribute>());
            Assert.True(type.GetCustomAttribute<ResponseCacheAttribute>()!.NoStore);
        }
        Assert.NotNull(typeof(MicroEMR.Web.Controllers.SchedulingResourcePreferencesController).GetMethod("Save")!.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.Equal(new[]{"ResourceUids","RowVersion"},typeof(SaveSchedulingResourcePreferencesRequest).GetProperties().Select(x=>x.Name));
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<SaveSchedulingResourcePreferencesRequest>("{\"UserId\":8}"));
        Assert.Contains(typeof(ITenantSqlConnectionFactory),typeof(SchedulingResourcePreferencesRepository).GetConstructors().Single().GetParameters().Select(x=>x.ParameterType));
    }

    [Fact]
    public async Task ApiRejectsValidationIdentityAndConcurrencyWithSafeResponses()
    {
        var store=new Store();var api=new MicroEMR.Api.Controllers.SchedulingResourcePreferencesController(Service(store),NullLogger<MicroEMR.Api.Controllers.SchedulingResourcePreferencesController>.Instance);
        Assert.IsType<BadRequestObjectResult>((await api.Save(new(),default)).Result);
        await api.Save(new(){ResourceUids=[Provider]},default);
        Assert.IsType<ConflictObjectResult>((await api.Save(new(){ResourceUids=[Room]},default)).Result);
        var denied=new MicroEMR.Api.Controllers.SchedulingResourcePreferencesController(Service(store,user:null),NullLogger<MicroEMR.Api.Controllers.SchedulingResourcePreferencesController>.Instance);
        Assert.IsType<ForbidResult>((await denied.Get(default)).Result);
    }

    private static SchedulingResourcePreferencesService Service(Store store,long? user=7,ScheduleResourceResponse[]? active=null)=>new(store,
        CppSelectivePrintTests.Stub<ISchedulingReadRepository>((method,_)=>method=="GetActiveResourcesAsync"?Task.FromResult<IReadOnlyList<ScheduleResourceResponse>>(active??Active):throw new InvalidOperationException("Unexpected scheduling operation.")),
        new Actor(user),NullLogger<SchedulingResourcePreferencesService>.Instance);

    private sealed class Actor(long? user):IAuthenticatedClinicalUserAccessor
    {
        public Task<long> GetRequiredUserIdAsync(CancellationToken token=default)=>user.HasValue?Task.FromResult(user.Value):throw new ClinicalUserResolutionException("No active clinical user.");
    }
    private sealed class Store:ISchedulingResourcePreferencesRepository
    {
        public Dictionary<long,SchedulingResourcePreferenceData> Records {get;}=[];
        public int Saves {get;private set;}
        public Exception? ReadFailure {get;init;}
        public Task<SchedulingResourcePreferenceData?> GetAsync(long userId,CancellationToken token=default)
        { if(ReadFailure is not null)throw ReadFailure;return Task.FromResult(Records.GetValueOrDefault(userId)); }
        public Task<SchedulingResourcePreferenceData> SaveAsync(long userId,string json,string? version,CancellationToken token=default)
        {
            if(Records.GetValueOrDefault(userId)?.RowVersion!=version)throw new SchedulingResourcePreferencesConcurrencyException();
            var saved=new SchedulingResourcePreferenceData(json,Convert.ToBase64String(BitConverter.GetBytes((long)++Saves)));
            Records[userId]=saved;return Task.FromResult(saved);
        }
    }
}
