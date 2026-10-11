using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.PatientEncounters;
using MicroEMR.Application.PatientEncounters.Contracts;
using MicroEMR.Application.PatientEncounters.Services;
using MicroEMR.Application.Templates.Definitions;
using MicroEMR.Application.Templates.Output;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class EncounterContributionTests
{
    [Fact]
    public async Task ApiUsesResolvedClinicalActorInsteadOfClaims()
    {
        long actor = 0;
        var service = Proxy<IEncounterContributionService>((_,args) => { actor=(long)args![3]!; return Task.FromResult(true); });
        var controller = new EncounterContributionsController(service, NullLogger<EncounterContributionsController>.Instance)
        { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        controller.HttpContext.User = new(new System.Security.Claims.ClaimsIdentity([new("sub","999"),new("UserId","999")],"test"));
        ClinicalUserActorContext.Set(controller.HttpContext, 7);
        Assert.IsType<OkResult>(await controller.Save(Guid.NewGuid(),Guid.NewGuid(),new(),default));
        Assert.Equal(7,actor);
    }

    [Theory]
    [InlineData("originalContributorId")]
    [InlineData("editorId")]
    [InlineData("createdAtUtc")]
    public void SpoofedIdentityAndTimestampAreRejected(string property)
    {
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<SaveEncounterContributionRequest>(
            "{\""+property+"\":999}",new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void ContributionEndpointRequiresViewEditAndAuthentication()
    {
        var type=typeof(EncounterContributionsController);
        Assert.Contains(type.GetCustomAttributes<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(), a => a.Policy is null);
        Assert.NotNull(type.GetCustomAttribute<RequirePermissionAttribute>());
        Assert.NotNull(type.GetMethod("Save")!.GetCustomAttribute<RequirePermissionAttribute>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingEditPermissionOrAuthenticationCannotAuthorizeContribution(bool authenticated)
    {
        var requirement = new PermissionRequirement(MicroEMR.Application.AccessProfiles.PermissionKeys.EncountersEdit);
        var user = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new("sub", "reader")], authenticated ? "test" : null));
        var context = new Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext([requirement], user, null);
        var permissions = Proxy<MicroEMR.Application.AccessProfiles.ICurrentUserPermissionService>((_,_)=>Task.FromResult(false));
        var handler = new PermissionAuthorizationHandler(permissions, NullLogger<PermissionAuthorizationHandler>.Instance);
        await handler.HandleAsync(context);
        Assert.False(context.HasSucceeded);
        Assert.Equal(PermissionPolicyProvider.Prefix + requirement.PermissionKey,
            typeof(EncounterContributionsController).GetMethod("Save")!.GetCustomAttribute<RequirePermissionAttribute>()!.Policy);
    }

    [Theory]
    [InlineData("Signed")]
    [InlineData("Closed")]
    [InlineData("Cancelled")]
    public async Task ServiceRejectsNonOpenNotes(string status)
    {
        var encounter=Note(); encounter.Status=status;
        await Assert.ThrowsAsync<EncounterNoteNotEditableException>(()=>Service(encounter).SaveAsync(encounter.PatientUid,encounter.EncounterUid,Request(),7));
    }

    [Fact]
    public async Task ServiceRejectsWrongPatientUnknownBlockAndSection()
    {
        var note=Note(); var service=Service(note);
        Assert.False(await service.SaveAsync(Guid.NewGuid(),note.EncounterUid,Request(),7));
        var request=Request();request.ContributionUid=Guid.NewGuid();
        Assert.False(await service.SaveAsync(note.PatientUid,note.EncounterUid,request,7));
        request=Request();request.SectionKey="other";
        await Assert.ThrowsAsync<ArgumentException>(()=>service.SaveAsync(note.PatientUid,note.EncounterUid,request,7));
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("AA==")]
    public async Task ServiceRequiresEightByteRowVersion(string version)
    {
        var note=Note();var request=Request();request.RowVersion=version;
        await Assert.ThrowsAsync<ArgumentException>(()=>Service(note).SaveAsync(note.PatientUid,note.EncounterUid,request,7));
    }

    [Fact]
    public async Task ServiceSupportsHistoricalTemplateSectionOnly()
    {
        var note=Note();note.TemplateVersionUid=Guid.NewGuid();
        note.TemplateDefinition=new() { Sections=[new() { Key="shared",Title="Shared" }] };
        var request=Request();request.SectionKey="shared";
        Assert.True(await Service(note).SaveAsync(note.PatientUid,note.EncounterUid,request,8));
        request.SectionKey="subjective";
        await Assert.ThrowsAsync<ArgumentException>(()=>Service(note).SaveAsync(note.PatientUid,note.EncounterUid,request,8));
    }

    [Fact]
    public void PrintOutputPreservesOriginalAndRevisionIdentityAndEscapesContent()
    {
        var block=new EncounterContributionResponse { SectionKey="subjective",SectionTitle="Subjective",OriginalContributorId=7,
            OriginalContributorDisplayName="A",CreatedAtUtc=DateTime.UtcNow,
            Revisions=[new(1,"original",7,"A",DateTime.UtcNow),new(2,"<script>final</script>",8,"B",DateTime.UtcNow)] };
        var output=EncounterContributionOutput.Include(new([new("subjective","Subjective",0,
            [new(null,"Legacy","old content","Text",false,0),new(null,null,"Generated","StaticText",true,1)])]),[block]);
        var html=new TemplateHtmlRenderer().Render(output);
        Assert.Contains("Original contributor: A (clinical user 7)",html);
        Assert.Contains("Revision 2: B (clinical user 8)",html);
        Assert.Contains("Earlier contribution identity unavailable",html);
        Assert.Contains("&lt;script&gt;final&lt;/script&gt;",html);
        Assert.DoesNotContain("<script>",html);
        Assert.Equal("Generated",output.Sections[0].Items[1].DisplayValue);
        Assert.Null(output.Sections[0].Items[1].Label);
        Assert.Equal(3,output.Sections[0].Items.Count);
    }

    [Fact]
    public void LegacyOnlyNotesReceiveNoInventedAuthor()
    {
        var output=EncounterContributionOutput.Include(new([new("subjective","Subjective",0,[new(null,"Text","Historical","Text",false,0)])]),[]);
        var html=new TemplateHtmlRenderer().Render(output);
        Assert.Contains("Historical",html);Assert.Contains(EncounterContributionOutput.Unattributed,html);
        Assert.DoesNotContain("Original contributor",html);
    }

    private static PatientEncounterDetailsResponse Note()=>new() {PatientUid=Guid.NewGuid(),EncounterUid=Guid.NewGuid(),Status="Open"};
    private static SaveEncounterContributionRequest Request()=>new() {SectionKey="subjective",Content="Clinical text",RowVersion=Convert.ToBase64String(new byte[8])};
    private static EncounterContributionService Service(PatientEncounterDetailsResponse note)=>new(
        Proxy<IPatientEncounterService>((_,_)=>Task.FromResult<PatientEncounterDetailsResponse?>(note)),
        Proxy<IEncounterContributionRepository>((_,_)=>Task.FromResult(true)));
    private static T Proxy<T>(Func<MethodInfo,object?[]?,object?> handler) where T:class
    {var proxy=DispatchProxy.Create<T,InterfaceProxy>();((InterfaceProxy)(object)proxy).Handler=handler;return proxy;}
    public class InterfaceProxy:DispatchProxy
    {public Func<MethodInfo,object?[]?,object?> Handler {get;set;}=null!;protected override object? Invoke(MethodInfo? method,object?[]? args)=>Handler(method!,args);}
}
