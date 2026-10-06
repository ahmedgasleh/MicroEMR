using System.Reflection;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PatientReferrals;
using MicroEMR.Application.Patients.Contracts;
using MicroEMR.Application.Patients.Repositories;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ReferralClinicalSelectionTests
{
    [Fact]
    public async Task DraftReplacePassesReferencesActorAndAggregateVersionAndReturnsNewVersion()
    {
        var f = new Fixture();
        var selections = new[] { new ReferralClinicalSelectionInput("CPP", "PROBLEMS"),
            new ReferralClinicalSelectionInput("CPP", "ALLERGIES"), new ReferralClinicalSelectionInput("CPP", "MEDICATIONS"),
            new ReferralClinicalSelectionInput("ENCOUNTER", EncounterUid: Guid.NewGuid()),
            new ReferralClinicalSelectionInput("RESULT", ResultUid: Guid.NewGuid()) };
        var response = await f.Replace(selections);
        Assert.Equal(f.NewVersion, response!.RowVersion);
        Assert.Equal(selections, f.Written!.Selections);
        Assert.Equal(f.Version, f.Written.RowVersion);
        Assert.Equal(1, f.Writes);
    }

    [Theory]
    [InlineData("CPP")]
    [InlineData("ENCOUNTER")]
    [InlineData("RESULT")]
    public async Task DuplicateReferencesAreRejectedBeforeRepositoryMutation(string kind)
    {
        var f = new Fixture();
        var selection = kind switch
        {
            "CPP" => new ReferralClinicalSelectionInput(kind, "PROBLEMS"),
            "ENCOUNTER" => new ReferralClinicalSelectionInput(kind, EncounterUid: Guid.NewGuid()),
            _ => new ReferralClinicalSelectionInput(kind, ResultUid: Guid.NewGuid())
        };
        await Assert.ThrowsAsync<ArgumentException>(() => f.Replace([selection, selection]));
        Assert.Equal(0, f.Writes);
    }

    [Theory]
    [InlineData("DOCUMENT", null)]
    [InlineData("CPP", "UNKNOWN")]
    [InlineData("CPP", "problems")]
    [InlineData("CPP", "PROBLEMS ")]
    [InlineData("ENCOUNTER", null)]
    [InlineData("RESULT", null)]
    public async Task UnsupportedOrMissingReferenceIsRejected(string kind, string? category)
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<ArgumentException>(() => f.Replace([new(kind, category)]));
        Assert.Equal(0, f.Writes);
    }

    [Fact]
    public async Task AmbiguousReferenceIsRejected()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<ArgumentException>(() => f.Replace([new("CPP", "PROBLEMS", Guid.NewGuid())]));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Replace([new("ENCOUNTER", EncounterUid: Guid.Empty)]));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Replace([new("RESULT", ResultUid: Guid.Empty)]));
        Assert.Equal(0, f.Writes);
    }

    [Theory]
    [InlineData(ReferralStatus.Sent)]
    [InlineData(ReferralStatus.ResponseReceived)]
    [InlineData(ReferralStatus.Closed)]
    public async Task NonDraftCannotReplaceSelections(ReferralStatus status)
    {
        var f = new Fixture(status);
        await Assert.ThrowsAsync<PatientReferralTransitionException>(() => f.Replace([]));
        Assert.Equal(0, f.Writes);
    }

    [Fact]
    public async Task StaleVersionIsRejected()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<PatientReferralConcurrencyException>(() => f.Service.ReplaceDraftClinicalSelectionsAsync(
            f.PatientUid, f.ReferralUid, new() { RowVersion = f.NewVersion }));
        Assert.Equal(0, f.Writes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("AAAA")]
    [InlineData("not-base64")]
    public async Task MalformedVersionIsRejected(string version)
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.ReplaceDraftClinicalSelectionsAsync(
            f.PatientUid, f.ReferralUid, new() { RowVersion = version }));
        Assert.Equal(0, f.Writes);
    }

    [Theory]
    [InlineData(PermissionKeys.PatientsView, "CPP")]
    [InlineData(PermissionKeys.ReferralsView, "CPP")]
    [InlineData(PermissionKeys.ReferralsManage, "CPP")]
    [InlineData(PermissionKeys.EncountersView, "ENCOUNTER")]
    [InlineData(PermissionKeys.ResultsView, "RESULT")]
    public async Task MissingBaseOrSourcePermissionCannotSelect(string missing, string kind)
    {
        var f = new Fixture();
        f.Permissions.Remove(missing);
        var item = kind switch
        {
            "CPP" => new ReferralClinicalSelectionInput(kind, "PROBLEMS"),
            "ENCOUNTER" => new ReferralClinicalSelectionInput(kind, EncounterUid: Guid.NewGuid()),
            _ => new ReferralClinicalSelectionInput(kind, ResultUid: Guid.NewGuid())
        };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Replace([item]));
        Assert.Equal(0, f.Writes);
    }

    [Fact]
    public async Task RemovingExistingRestrictedChoicesCannotBypassSourcePermission()
    {
        var f = new Fixture();
        f.Existing = [new(Guid.NewGuid(), "RESULT", null, null, Guid.NewGuid(), DateTime.UtcNow, 7)];
        f.Permissions.Remove(PermissionKeys.ResultsView);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Replace([]));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetClinicalSelectionsAsync(f.PatientUid, f.ReferralUid));
        Assert.Equal(0, f.Writes);
    }

    [Fact]
    public async Task ReadDoesNotRequireManagePermissionOrMutationActor()
    {
        var f = new Fixture();
        f.Permissions.Remove(PermissionKeys.ReferralsManage);
        var response = await f.Service.GetClinicalSelectionsAsync(f.PatientUid, f.ReferralUid);
        Assert.NotNull(response);
        Assert.Equal(f.Version, response.RowVersion);
        Assert.Equal(0, f.ActorCalls);
    }

    private sealed class Fixture
    {
        public Guid PatientUid { get; } = Guid.NewGuid();
        public Guid ReferralUid { get; } = Guid.NewGuid();
        public string Version { get; } = Convert.ToBase64String([0,0,0,0,0,0,0,1]);
        public string NewVersion { get; } = Convert.ToBase64String([0,0,0,0,0,0,0,2]);
        public HashSet<string> Permissions { get; } = [PermissionKeys.PatientsView, PermissionKeys.ReferralsView,
            PermissionKeys.ReferralsManage, PermissionKeys.EncountersView, PermissionKeys.ResultsView];
        public IReadOnlyList<PatientReferralClinicalSelectionResponse> Existing { get; set; } = [];
        public ReplacePatientReferralClinicalSelectionsRequest? Written { get; private set; }
        public int Writes { get; private set; }
        public int ActorCalls { get; private set; }
        public PatientReferralService Service { get; }

        public Fixture(ReferralStatus status = ReferralStatus.Draft)
        {
            var repository = Stub<IPatientReferralRepository>((method, args) =>
            {
                Assert.Equal(PatientUid, args[0]); Assert.Equal(ReferralUid, args[1]);
                return method switch
                {
                    "GetByUidAsync" => Task.FromResult<PatientReferral?>(new()
                    { PatientUid=PatientUid, ReferralUid=ReferralUid, Status=status, RowVersion=Version, RecipientName="Recipient", Reason="Reason" }),
                    "GetClinicalSelectionsAsync" => Task.FromResult<PatientReferralClinicalSelectionsResponse?>(new(PatientUid,ReferralUid,Version,Existing)),
                    "ReplaceDraftClinicalSelectionsAsync" => Write(args),
                    _ => throw new InvalidOperationException($"Unexpected call {method}")
                };
            });
            Service = new(repository, Stub<IPatientRepository>((method,args) =>
            {
                Assert.Equal("GetByUidAsync", method); Assert.Equal(PatientUid,args[0]);
                return Task.FromResult<PatientDetailsResponse?>(new() { PatientUid=PatientUid });
            }), Stub<IAuthenticatedClinicalUserAccessor>((method,_) =>
            {
                Assert.Equal("GetRequiredUserIdAsync",method); ActorCalls++; return Task.FromResult(7L);
            }), new ReferralStatusTransitionService(), permissions: Stub<ICurrentUserPermissionService>((method,_) =>
            {
                Assert.Equal("GetEffectivePermissionsAsync",method);
                return Task.FromResult<IReadOnlySet<string>>(Permissions);
            }));
        }

        private Task<PatientReferralClinicalSelectionsResponse?> Write(object?[] args)
        {
            Written = (ReplacePatientReferralClinicalSelectionsRequest)args[2]!;
            Assert.Equal(7L,args[3]); Writes++;
            return Task.FromResult<PatientReferralClinicalSelectionsResponse?>(new(PatientUid,ReferralUid,NewVersion,Existing));
        }

        public Task<PatientReferralClinicalSelectionsResponse?> Replace(IReadOnlyList<ReferralClinicalSelectionInput> selections) =>
            Service.ReplaceDraftClinicalSelectionsAsync(PatientUid,ReferralUid,new() { RowVersion=Version,Selections=selections });
    }

    private static T Stub<T>(Func<string,object?[],object> call) where T : class
    {
        var proxy = DispatchProxy.Create<T,ReferralLetterCompositionTests.StrictProxy>();
        ((ReferralLetterCompositionTests.StrictProxy)(object)proxy).Call = call;
        return proxy;
    }
}
