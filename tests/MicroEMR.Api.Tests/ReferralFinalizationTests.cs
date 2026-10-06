using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.ClinicConfiguration;
using MicroEMR.Application.PatientReferrals;
using MicroEMR.Application.Patients.Contracts;
using MicroEMR.Application.Patients.Repositories;
using MicroEMR.Application.Patients.Services;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ReferralFinalizationTests
{
    [Fact]
    public async Task FinalizesPersistedChoicesWithMandatoryContentTraceabilityHashAndNewVersion()
    {
        var f = new Fixture();
        var sent = await f.Send();
        Assert.Equal("Sent", sent!.Status); Assert.NotEqual(f.Version, sent.RowVersion);
        var artifact = Assert.IsType<ReferralArtifactWrite>(f.Artifact);
        var html = Encoding.UTF8.GetString(artifact.PdfContent);
        foreach (var value in new[] {"Ada Lee", "Dr Referrer", "Clinic Name", "Dr Recipient", "Reason",
            "Clinical summary", "Hypertension", "Peanut", "Amoxicillin", "Subjective content", "Creatinine", "Supporting report"})
            Assert.Contains(value, html);
        Assert.DoesNotContain("Inactive condition", html); Assert.DoesNotContain("Other patient condition", html);
        Assert.DoesNotContain("Draft preview", html);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(artifact.PdfContent)).ToLowerInvariant(), artifact.Sha256);
        using var snapshot = JsonDocument.Parse(artifact.SnapshotJson);
        var root = snapshot.RootElement;
        Assert.Equal(artifact.ArtifactUid, root.GetProperty("ArtifactUid").GetGuid());
        Assert.Equal(artifact.Sha256, root.GetProperty("Sha256").GetString());
        Assert.Equal(artifact.PdfContent.LongLength, root.GetProperty("FileSizeBytes").GetInt64());
        Assert.Equal(f.Version, root.GetProperty("DraftRowVersion").GetString());
        Assert.Equal(5, root.GetProperty("ClinicalSelections").GetArrayLength());
        Assert.Equal(f.Source.Encounter.EncounterUid, root.GetProperty("ClinicalSelections")[3].GetProperty("EncounterUid").GetGuid());
        Assert.DoesNotContain("Subjective content", artifact.SnapshotJson);
        Assert.Equal(1, f.PdfCalls); Assert.Equal(1, f.StoreCalls);
    }

    [Fact]
    public async Task PreservedBytesAndDateSurviveMutableSourcesAndWorkflowChangesWithoutRenderingAgain()
    {
        var f = new Fixture(); await f.Send(); var original = f.Artifact!;
        f.Patient.FirstName = "Changed"; f.ProviderName = "Changed provider";
        f.DocumentTitle = "Changed document"; f.Source.Encounter.SubjectiveNote = "Changed note";
        f.Source.Result.ResultValue = "999"; f.Source.Select();
        f.Source.Calls.Clear(); f.Source.Permissions.Clear();
        var followUp = await f.Service.SetFollowUpDueAsync(f.Patient.PatientUid, f.Source.ReferralUid,
            new() {RowVersion=f.Current.RowVersion,FollowUpDueAtUtc=DateTime.UtcNow.AddDays(-1)});
        // One PC10.02 regression case: overdue Sent clears after response/closure.
        Assert.True(followUp!.IsFollowUpOverdue);
        var response = await f.Service.MarkResponseReceivedAsync(f.Patient.PatientUid,f.Source.ReferralUid,new() {RowVersion=f.Current.RowVersion});
        Assert.False(response!.IsFollowUpOverdue);
        var closed = await f.Service.CloseAsync(f.Patient.PatientUid,f.Source.ReferralUid,new() {RowVersion=f.Current.RowVersion});
        Assert.Equal("Closed",closed!.Status); Assert.False(closed.IsFollowUpOverdue);
        var opened = (await f.Service.OpenArtifactAsync(f.Patient.PatientUid,f.Source.ReferralUid))!;
        using var output = new MemoryStream(); await opened.Content.CopyToAsync(output);
        Assert.Equal(original.PdfContent,output.ToArray()); Assert.Equal(original.Sha256,f.Artifact!.Sha256);
        Assert.Equal(original.SentAtUtc,f.Current.SentAt);
        Assert.Contains("Referral Letter Date:</strong> June 14, 2026",Encoding.UTF8.GetString(output.ToArray()));
        Assert.Equal(1,f.PdfCalls); Assert.Empty(f.Source.Calls);
        await Assert.ThrowsAsync<PatientReferralTransitionException>(()=>f.Service.PreviewLetterAsync(f.Patient.PatientUid,f.Source.ReferralUid));
        Assert.Null(await f.Service.OpenArtifactAsync(Guid.NewGuid(),f.Source.ReferralUid));
        Assert.Null(await f.Service.OpenArtifactAsync(f.Patient.PatientUid,Guid.NewGuid()));
    }

    [Theory]
    [InlineData("stale")]
    [InlineData("selection-race")]
    [InlineData("render")]
    [InlineData("empty-pdf")]
    [InlineData("store")]
    [InlineData("commit-race")]
    [InlineData("missing-encounter")]
    [InlineData("corrected-result")]
    [InlineData("restricted-source")]
    [InlineData("restricted-document")]
    [InlineData("missing-document")]
    [InlineData("audit")]
    public async Task FailedFinalizationLeavesDraftWithoutArtifact(string failure)
    {
        var f = new Fixture {Failure=failure};
        if(failure=="selection-race") f.Source.RowVersion="newer";
        if(failure=="missing-encounter") f.Source.Encounter.PatientUid=Guid.NewGuid();
        if(failure=="corrected-result") f.Source.Result.LifecycleStatus="Superseded";
        if(failure=="restricted-source") f.Source.Permissions.Remove(PermissionKeys.EncountersView);
        if(failure=="audit") f.Source.FailAudit=true;
        await Assert.ThrowsAnyAsync<Exception>(()=>f.Send(failure=="stale"?"stale":f.Version));
        Assert.Equal(ReferralStatus.Draft,f.Current.Status); Assert.Null(f.Artifact);
        if(failure is "stale" or "selection-race") Assert.Equal(0,f.PdfCalls);
        if(failure is not ("store" or "commit-race")) Assert.Equal(0,f.StoreCalls);
    }

    [Fact]
    public async Task MissingRendererCannotFallBackToArtifactlessSend()
    {
        var f=new Fixture();
        var service=new PatientReferralService(f.Repository,f.PatientRepository,f.Actor,new ReferralStatusTransitionService());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.MarkSentAsync(f.Patient.PatientUid,f.Source.ReferralUid,new() {RowVersion=f.Version}));
        Assert.Null(f.Artifact); Assert.Equal(0,f.StoreCalls);
    }

    [Fact]
    public async Task SentSelectionsCannotBeReplacedAndRepeatedSendCannotCreateAnotherArtifact()
    {
        var f=new Fixture(); await f.Send(); var original=f.Artifact;
        await Assert.ThrowsAsync<PatientReferralTransitionException>(()=>f.Service.ReplaceDraftClinicalSelectionsAsync(
            f.Patient.PatientUid,f.Source.ReferralUid,new() {RowVersion=f.Current.RowVersion}));
        await Assert.ThrowsAsync<PatientReferralTransitionException>(()=>f.Send(f.Current.RowVersion));
        Assert.Same(original,f.Artifact); Assert.Equal(1,f.StoreCalls);
    }

    [Theory]
    [InlineData("restricted-source",403)]
    [InlineData("missing-encounter",409)]
    [InlineData("selection-race",409)]
    public async Task SendApiReportsRestrictedUnavailableAndConcurrentSourcesClearly(string failure,int status)
    {
        var f=new Fixture();
        if(failure=="restricted-source") f.Source.Permissions.Remove(PermissionKeys.EncountersView);
        if(failure=="missing-encounter") f.Source.Encounter.PatientUid=Guid.NewGuid();
        if(failure=="selection-race") f.Source.RowVersion="newer";
        var controller=new PatientReferralsController(f.Service,NullLogger<PatientReferralsController>.Instance);
        var result=await controller.MarkSent(f.Patient.PatientUid,f.Source.ReferralUid,new() {RowVersion=f.Version});
        Assert.Equal(status,result.Result switch {ObjectResult r=>r.StatusCode,StatusCodeResult r=>r.StatusCode,_=>null});
        Assert.Null(f.Artifact);
    }

    private sealed class Fixture
    {
        public ReferralClinicalContentTests.Fixture Source {get;}=new();
        public string Version {get;}=Convert.ToBase64String(new byte[8]);
        public PatientDetailsResponse Patient {get;}
        public PatientReferral Current {get;private set;}
        public PatientReferralService Service {get;}
        public IPatientReferralRepository Repository {get;}
        public IPatientRepository PatientRepository {get;}
        public IAuthenticatedClinicalUserAccessor Actor {get;}
        public ReferralArtifactWrite? Artifact; public int PdfCalls; public int StoreCalls;
        public string Failure=""; public string ProviderName="Dr Referrer"; public string DocumentTitle="Supporting report";
        private readonly Guid providerUid=Guid.NewGuid();
        public Fixture()
        {
            Source.RowVersion=Version;
            Source.Select(new("CPP","PROBLEMS"),new("CPP","ALLERGIES"),new("CPP","MEDICATIONS"),
                new("ENCOUNTER",EncounterUid:Source.Encounter.EncounterUid),new("RESULT",ResultUid:Source.Result.PatientResultUid));
            Patient=new() {PatientUid=Source.PatientUid,FirstName="Ada",LastName="Lee",DateOfBirth=new(1980,6,15)};
            Current=Record(ReferralStatus.Draft,Version);
            Repository=Stub<IPatientReferralRepository>((method,args)=>method switch
            {
                "GetByUidAsync"=>Task.FromResult<PatientReferral?>((Guid)args[0]! == Patient.PatientUid && (Guid)args[1]! == Source.ReferralUid?Current:null),
                "GetProviderAsync"=>Task.FromResult<ReferralProvider?>(new(providerUid,ProviderName,"Physician","123","Family Medicine")),
                "SendWithArtifactAsync"=>Store((ReferralArtifactWrite)args[4]!),
                "GetArtifactAsync"=>Task.FromResult<ReferralArtifactContent?>(Artifact is not null && (Guid)args[0]! == Patient.PatientUid && (Guid)args[1]! == Source.ReferralUid
                    ?new(Artifact.ArtifactUid,"application/pdf",Artifact.FileName,Artifact.PdfContent,Artifact.PdfContent.LongLength,Artifact.Sha256,Artifact.SnapshotJson,Artifact.SentAtUtc):null),
                "SetFollowUpDueAsync"=>Change(ReferralStatus.Sent,(DateTime?)args[2]),
                "MarkResponseReceivedAsync"=>Change(ReferralStatus.ResponseReceived,Current.FollowUpDueAt),
                "CloseAsync"=>Change(ReferralStatus.Closed,Current.FollowUpDueAt),
                _=>throw new InvalidOperationException("Unexpected repository operation: "+method)
            });
            PatientRepository=Stub<IPatientRepository>((m,a)=>Task.FromResult<PatientDetailsResponse?>(Patient));
            Actor=Stub<IAuthenticatedClinicalUserAccessor>((m,a)=>Task.FromResult(7L));
            Service=new(Repository,PatientRepository,Actor,new ReferralStatusTransitionService(),
                Stub<IPatientService>((m,a)=>Task.FromResult<PatientDetailsResponse?>(Patient)),
                Stub<IClinicConfigurationService>((m,a)=>Task.FromResult(new ClinicConfigurationResponse("Clinic Name","America/Toronto",null,
                    "416-111-1111","416-222-2222",null,"10 Clinic Street",null,"Toronto","ON","M1M1M1","CA",null,null,null,null))),
                Stub<IReferralDocumentRepository>((m,a)=>
                {
                    Assert.True((bool)a[3]!);
                    if(Failure=="missing-document") throw new ReferralClinicalSelectionRuleException("Supporting document unavailable.");
                    return Task.FromResult<IReadOnlyList<ReferralDocumentLinkResponse>>([new() {DocumentUid=Guid.NewGuid(),Title=DocumentTitle,DocumentType="Report",DocumentStatus="Final"}]);
                }),new ClinicalPrintLayoutRenderer(),Stub<IPdfRenderer>((m,a)=>
                {
                    PdfCalls++;
                    if(Failure=="render") throw new InvalidOperationException("Rendering failed.");
                    return Task.FromResult(Failure=="empty-pdf"?Array.Empty<byte>():Encoding.UTF8.GetBytes((string)a[0]!));
                }),new Clock(),Stub<ICurrentUserPermissionService>((m,a)=>Task.FromResult<IReadOnlySet<string>>(
                    Failure=="restricted-document"?new HashSet<string>():new HashSet<string>(Source.Permissions) {PermissionKeys.DocumentsView})),Source.Service);
        }
        public Task<PatientReferralDetailsResponse?> Send(string? version=null)=>Service.MarkSentAsync(Patient.PatientUid,Source.ReferralUid,new() {RowVersion=version??Version});
        private Task<PatientReferral?> Store(ReferralArtifactWrite artifact)
        {
            StoreCalls++;
            if(Failure=="store") throw new InvalidOperationException("SQL failed.");
            if(Failure=="commit-race") throw new PatientReferralConcurrencyException();
            Artifact=artifact; Current=Record(ReferralStatus.Sent,Convert.ToBase64String([0,0,0,0,0,0,0,1])); return Task.FromResult<PatientReferral?>(Current);
        }
        private Task<PatientReferral?> Change(ReferralStatus status,DateTime? due)
        { Current=Record(status,Guid.NewGuid().ToString(),due); return Task.FromResult<PatientReferral?>(Current); }
        private PatientReferral Record(ReferralStatus status,string version,DateTime? due=null)=>new()
        {
            PatientUid=Patient.PatientUid,ReferralUid=Source.ReferralUid,ReferringProviderUid=providerUid,
            RecipientName="Dr Recipient",RecipientPhone="905-111-1111",RecipientFax="905-222-2222",Reason="Reason",ClinicalSummary="Clinical summary",
            Status=status,RowVersion=version,SentAt=Artifact?.SentAtUtc,ArtifactUid=Artifact?.ArtifactUid,FollowUpDueAt=due
        };
    }
    private sealed class Clock:TimeProvider
    { public override DateTimeOffset GetUtcNow()=>new(2026,6,15,2,0,0,TimeSpan.Zero); }
    private static T Stub<T>(Func<string,object?[],object> call) where T:class
    {
        var proxy=DispatchProxy.Create<T,ReferralLetterCompositionTests.StrictProxy>();
        ((ReferralLetterCompositionTests.StrictProxy)(object)proxy).Call=call;return proxy;
    }
}
