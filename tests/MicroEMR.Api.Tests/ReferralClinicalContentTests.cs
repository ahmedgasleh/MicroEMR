using System.Reflection;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientAllergies.Contracts;
using MicroEMR.Application.PatientAllergies.Services;
using MicroEMR.Application.PatientEncounters.Contracts;
using MicroEMR.Application.PatientEncounters.Services;
using MicroEMR.Application.PatientMedications.Contracts;
using MicroEMR.Application.PatientMedications.Services;
using MicroEMR.Application.PatientProblems.Contracts;
using MicroEMR.Application.PatientProblems.Services;
using MicroEMR.Application.PatientReferrals;
using MicroEMR.Application.PatientResults;
using MicroEMR.Application.ReadAudit;
using MicroEMR.Application.Templates.Runtime;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ReferralClinicalContentTests
{
    [Fact] public async Task OnlySelectedCategoriesAndRecordsAreRenderedAndEncoded()
    {
        var f=new Fixture(); f.Select(new("CPP","PROBLEMS"),new("CPP","ALLERGIES"),new("CPP","MEDICATIONS"),
            new("ENCOUNTER",EncounterUid:f.Encounter.EncounterUid),new("RESULT",ResultUid:f.Result.PatientResultUid));
        var html=await f.Render();
        foreach(var text in new[] {"Hypertension","Peanut","Amoxicillin","Subjective content","Objective content","Assessment content","Plan content","Creatinine","42","umol/L","Report summary"}) Assert.Contains(text,html);
        Assert.Contains("&lt;b&gt;literal&lt;/b&gt;",html); Assert.DoesNotContain("<b>literal</b>",html);
        Assert.DoesNotContain("Inactive condition",html); Assert.DoesNotContain("Other patient condition",html);
        Assert.Equal(1,f.ChartReads); Assert.Equal(1,f.EncounterReads);
    }
    [Fact] public async Task NoSelectionOrRemovedSelectionDoesNotReadOrIncludeSourceContent()
    {
        var f=new Fixture(); f.Select(new("ENCOUNTER",EncounterUid:f.Encounter.EncounterUid));
        Assert.Contains("Subjective content",await f.Render());
        f.Select(); f.Calls.Clear();
        Assert.Equal(string.Empty,await f.Render()); Assert.Empty(f.Calls);
    }
    [Theory]
    [InlineData("ENCOUNTER",PermissionKeys.EncountersView)]
    [InlineData("RESULT",PermissionKeys.ResultsView)]
    public async Task RestrictedSelectionIsRejectedBeforeAnySourceRead(string kind,string permission)
    {
        var f=new Fixture(); f.Select(kind=="ENCOUNTER"?new(kind,EncounterUid:f.Encounter.EncounterUid):new(kind,ResultUid:f.Result.PatientResultUid));
        f.Permissions.Remove(permission);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Render()); Assert.Empty(f.Calls);
    }
    [Theory]
    [InlineData("ENCOUNTER")]
    [InlineData("RESULT")]
    public async Task CrossPatientSourceCannotAppear(string kind)
    {
        var f=new Fixture();
        f.Select(kind=="ENCOUNTER"?new(kind,EncounterUid:f.Encounter.EncounterUid):new(kind,ResultUid:f.Result.PatientResultUid));
        f.Encounter.PatientUid=Guid.NewGuid(); f.Result.PatientUid=Guid.NewGuid();
        await Assert.ThrowsAsync<ReferralClinicalSelectionRuleException>(()=>f.Render());
    }
    [Fact] public async Task CorrectedOrUnavailableResultIsNotSilentlyReplaced()
    {
        var f=new Fixture(); f.Select(new("RESULT",ResultUid:f.Result.PatientResultUid)); f.Result.LifecycleStatus="Superseded";
        await Assert.ThrowsAsync<ReferralClinicalSelectionRuleException>(()=>f.Render());
    }
    [Fact] public async Task SelectorUsesMetadataListsAndOrdersSignedEncountersFirst()
    {
        var f=new Fixture(); var options=await f.Service.GetOptionsAsync(f.PatientUid);
        Assert.Equal(new[] {"PROBLEMS","ALLERGIES","MEDICATIONS"},options.Categories.Select(x=>x.CppCategoryCode));
        Assert.Equal("Signed",options.Encounters[0].Status); Assert.Single(options.Results);
        Assert.DoesNotContain("encounter-body",f.Calls); Assert.DoesNotContain("result-body",f.Calls);
        Assert.DoesNotContain("problems",f.Calls); Assert.Equal(1,f.ChartReads);
    }
    [Fact] public async Task SelectorsOmitSourcesWithoutPermission()
    {
        var f=new Fixture(); f.Permissions.Remove(PermissionKeys.EncountersView); f.Permissions.Remove(PermissionKeys.ResultsView);
        var options=await f.Service.GetOptionsAsync(f.PatientUid); Assert.Empty(options.Encounters); Assert.Empty(options.Results); Assert.Empty(f.Calls);
    }
    [Fact] public async Task EmptySelectedCategoryIsExplicitAndDoesNotMeanNoKnownAllergies()
    {
        var f=new Fixture(); f.Select(new("CPP","ALLERGIES")); f.Allergies=[];
        var html=await f.Render(); Assert.Contains("No recorded items",html); Assert.DoesNotContain("No known allergies",html);
    }
    [Fact] public async Task ExistingStructuredEncounterRuntimeIsReused()
    {
        var f=new Fixture(); f.Select(new("ENCOUNTER",EncounterUid:f.Encounter.EncounterUid));
        f.Encounter.TemplateDefinition=new(); f.Encounter.StructuredDataJson="{}";
        Assert.Contains("Existing structured snapshot",await f.Render());
        Assert.DoesNotContain("Subjective content",await f.Render());
    }
    [Fact] public async Task SensitiveReadAuditFailurePreventsPreviewOutput()
    {
        var f=new Fixture(); f.Select(new("CPP","PROBLEMS")); f.FailAudit=true;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Render()); Assert.Empty(f.Calls);
    }

    internal sealed class Fixture
    {
        public Guid PatientUid {get;}=Guid.NewGuid(); public Guid ReferralUid {get;}=Guid.NewGuid();
        public HashSet<string> Permissions {get;}=[PermissionKeys.PatientsView,PermissionKeys.ReferralsView,PermissionKeys.ReferralsManage,PermissionKeys.EncountersView,PermissionKeys.ResultsView];
        public List<string> Calls {get;}=[]; public int ChartReads; public int EncounterReads; public bool FailAudit;
        public PatientEncounterDetailsResponse Encounter {get;}
        public PatientResultResponse Result {get;}
        public IReadOnlyList<PatientAllergyListItemResponse> Allergies {get;set;}
        private IReadOnlyList<PatientReferralClinicalSelectionResponse> selected=[];
        public string RowVersion {get;set;}="version";
        public ReferralClinicalContentService Service {get;}
        public Fixture()
        {
            Encounter=new() {EncounterUid=Guid.NewGuid(),PatientUid=PatientUid,EncounterType="Visit",Status="Signed",ProviderName="Dr Source",EncounterDateUtc=new(2026,9,22),SubjectiveNote="Subjective content <b>literal</b>",ObjectiveNote="Objective content",AssessmentNote="Assessment content",PlanNote="Plan content"};
            Result=new() {PatientResultUid=Guid.NewGuid(),PatientUid=PatientUid,ResultName="Creatinine",ResultValue="42",ResultUnit="umol/L",ResultSummary="Report summary",LifecycleStatus="Current"};
            Allergies=[new() {PatientUid=PatientUid,AllergenName="Peanut",Reaction="Rash",Status="Active"}];
            Service=new(Stub<IPatientReferralRepository>((m,a)=>
            {
                Assert.Equal("GetClinicalSelectionsAsync",m); Assert.Equal(PatientUid,a[0]);Assert.Equal(ReferralUid,a[1]);
                return Task.FromResult<PatientReferralClinicalSelectionsResponse?>(new(PatientUid,ReferralUid,RowVersion,selected));
            }),Stub<IPatientProblemService>((m,a)=>
            {
                Assert.Equal("GetByPatientUidAsync",m);Assert.Equal(PatientUid,a[0]);Calls.Add("problems");
                return Task.FromResult<IReadOnlyList<PatientProblemResponse>>([new() {PatientUid=PatientUid,ProblemName="Hypertension",ProblemStatus="Active"},new() {PatientUid=PatientUid,ProblemName="Inactive condition",ProblemStatus="Resolved"},new() {PatientUid=Guid.NewGuid(),ProblemName="Other patient condition",ProblemStatus="Active"}]);
            }),Stub<IPatientAllergyService>((m,a)=> {Assert.Equal(PatientUid,a[0]);Calls.Add("allergies");return Task.FromResult(Allergies);}),
            Stub<IPatientMedicationService>((m,a)=> {Assert.Equal(PatientUid,a[0]);Calls.Add("medications");return Task.FromResult<IReadOnlyList<PatientMedicationListItemResponse>>([new() {PatientUid=PatientUid,MedicationName="Amoxicillin",Status="Active"}]);}),
            Stub<IPatientEncounterService>((m,a)=>
            {
                if(m=="GetByUidAsync") {Assert.Equal(Encounter.EncounterUid,a[0]);Calls.Add("encounter-body");return Task.FromResult<PatientEncounterDetailsResponse?>(Encounter);}
                Assert.Equal("GetByPatientUidAsync",m);Assert.Equal(PatientUid,a[0]);Calls.Add("encounter-list");
                return Task.FromResult<IReadOnlyList<PatientEncounterListItemResponse>>([new() {PatientUid=PatientUid,EncounterUid=Guid.NewGuid(),Status="Open"},new() {PatientUid=PatientUid,EncounterUid=Encounter.EncounterUid,Status="Signed"}]);
            }),Stub<IPatientResultRepository>((m,a)=>
            {
                Assert.Equal(PatientUid,a[0]);
                if(m=="Get") {Assert.Equal(Result.PatientResultUid,a[1]);Calls.Add("result-body");return Task.FromResult<PatientResultResponse?>(Result);}
                Assert.Equal("List",m);Calls.Add("result-list");return Task.FromResult<IReadOnlyList<PatientResultResponse>>([Result]);
            }),Stub<ICurrentUserPermissionService>((m,a)=>Task.FromResult<IReadOnlySet<string>>(Permissions)),
            Stub<IPatientChartReadAuditService>((m,a)=> {if(FailAudit)throw new InvalidOperationException("Audit unavailable");ChartReads++;return Task.FromResult(Guid.NewGuid());}),
            Stub<IStructuredReadAuditService>((m,a)=> {Assert.Equal("RecordAsync",m);Assert.Equal(Encounter.EncounterUid,a[2]);Assert.Equal(PatientUid,a[3]);EncounterReads++;return Task.FromResult(Guid.NewGuid());}),
            Stub<ITemplateInstanceRuntime>((m,a)=>m=="Process"?new TemplateInstanceProcessingResult(true,new(),"{}",[]):"Existing structured snapshot"));
        }
        public void Select(ReferralClinicalSelectionInput? first=null,params ReferralClinicalSelectionInput[] rest)
        {
            IEnumerable<ReferralClinicalSelectionInput> choices=first is null?rest:new[] {first}.Concat(rest);
            selected=choices.Select(x=>new PatientReferralClinicalSelectionResponse(Guid.NewGuid(),x.SelectionKind,x.CppCategoryCode,x.EncounterUid,x.ResultUid,DateTime.UtcNow,7,x.FileUid)).ToArray();
        }
        public Task<string> Render()=>Service.RenderPreviewAsync(PatientUid,ReferralUid);
    }
    private static T Stub<T>(Func<string,object?[],object> call) where T:class
    {
        var proxy=DispatchProxy.Create<T,ReferralLetterCompositionTests.StrictProxy>();
        ((ReferralLetterCompositionTests.StrictProxy)(object)proxy).Call=call;return proxy;
    }
}
