using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.PatientDocuments.Repositories;
using MicroEMR.Application.PatientEncounters.Contracts;
using MicroEMR.Application.PatientEncounters.Repositories;
using MicroEMR.Application.PatientEncounters.Services;
using MicroEMR.Application.Patients.Services;
using MicroEMR.Application.Scheduling.Services;
using MicroEMR.Application.Scheduling.Repositories;
using MicroEMR.Application.Templates.Definitions;
using MicroEMR.Application.Templates.Runtime;
using MicroEMR.Application.Templates.Serialization;
using MicroEMR.Application.Templates.Validation;
using MicroEMR.Application.Templates.Variables;
using MicroEMR.Web.Services;
using MicroEMR.Web.Services.PatientEncounters;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class EncounterDraftSaveTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"history\":\"\"}")]
    [InlineData("{\"history\":\"Partial note\"}")]
    public async Task IncompleteDraftSavesButCannotBeSigned(string values)
    {
        var fixture = new Fixture();
        var rowVersion = Convert.ToBase64String(new byte[8]);
        var saved = await fixture.Service.UpdateStructuredDataAsync(fixture.Note.PatientUid,fixture.Note.EncounterUid,
            new() { StructuredDataJson = "{\"schemaVersion\":1,\"values\":"+values+"}", RowVersion=rowVersion },7);
        Assert.NotNull(saved);
        Assert.Equal(1,fixture.Saves);
        Assert.Equal(rowVersion,fixture.SavedVersion);
        Assert.Equal(7,fixture.SavedActor);
        Assert.All(saved.TemplateDefinition!.Sections![0].Fields!,field=>Assert.True(field.Required));
        var error=await Assert.ThrowsAsync<TemplateInstanceValidationException>(()=>fixture.Service.SignAsync(fixture.Note.PatientUid,fixture.Note.EncounterUid,7));
        Assert.Contains(error.Errors,e=>e.Code=="Required");
        Assert.Equal(0,fixture.Signs);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"values\":{\"pulse\":\"wrong type\"}}")]
    [InlineData("{\"schemaVersion\":1,\"values\":{\"unknown\":\"text\"}}")]
    [InlineData("{\"schemaVersion\":2,\"values\":{}}")]
    [InlineData("malformed")]
    public async Task InvalidDraftStillFailsWithoutPersistence(string json)
    {
        var fixture=new Fixture();
        await Assert.ThrowsAsync<TemplateInstanceValidationException>(()=>fixture.Service.UpdateStructuredDataAsync(
            fixture.Note.PatientUid,fixture.Note.EncounterUid,new() {StructuredDataJson=json,RowVersion=Convert.ToBase64String(new byte[8])},7));
        Assert.Equal(0,fixture.Saves);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest,"{\"errors\":{\"values.pulse\":[\"Pulse has an invalid value.\"]}}","Pulse has an invalid value.")]
    [InlineData(HttpStatusCode.BadRequest,"not json","The draft contains invalid template values. Review the fields and try again.")]
    [InlineData(HttpStatusCode.Conflict,"{}","The encounter changed or cannot be edited. Reopen it before saving again.")]
    public async Task WebDistinguishesValidationFromConcurrency(HttpStatusCode status,string body,string message)
    {
        var client=Proxy<IPatientEncounterApiClient>((_,_)=>throw new SafeApiResponseException(status,body));
        var controller=new MicroEMR.Web.Controllers.PatientEncountersController(client,null!,null!,null!,
            NullLogger<MicroEMR.Web.Controllers.PatientEncountersController>.Instance);
        var result=Assert.IsAssignableFrom<ObjectResult>(await controller.UpdateEncounterStructuredData(Guid.NewGuid(),Guid.NewGuid(),"{}","version",default));
        Assert.Equal((int)status,result.StatusCode);
        var json=System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains(message,json);
    }

    [Theory]
    [InlineData("{\"errors\":{\"values.history\":[\"History is required.\"],\"values.pulse\":[\"Pulse is required.\"]}}", "History is required. Pulse is required.")]
    [InlineData("not json", "The encounter has invalid or incomplete template fields.")]
    [InlineData("[]", "The encounter has invalid or incomplete template fields.")]
    public async Task SigningDisplaysValidationErrorsInsteadOfGatewayFailure(string body, string message)
    {
        var client=Proxy<IPatientEncounterApiClient>((_,_)=>throw new SafeApiResponseException(HttpStatusCode.BadRequest,body));
        var controller=new MicroEMR.Web.Controllers.PatientEncountersController(client,null!,null!,null!,
            NullLogger<MicroEMR.Web.Controllers.PatientEncountersController>.Instance);
        var result=Assert.IsType<BadRequestObjectResult>(await controller.SignEncounter(Guid.NewGuid(),Guid.NewGuid(),default));
        var json=System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains(message,json);
        Assert.Contains("save the draft before signing",json);
    }

    private sealed class Fixture
    {
        public PatientEncounterDetailsResponse Note {get;}=new() {PatientUid=Guid.NewGuid(),EncounterUid=Guid.NewGuid(),TemplateUid=Guid.NewGuid(),TemplateVersionUid=Guid.NewGuid(),Status="Open"};
        public int Saves {get;private set;}
        public int Signs {get;private set;}
        public string? SavedVersion {get;private set;}
        public long? SavedActor {get;private set;}
        public PatientEncounterService Service {get;}
        public Fixture()
        {
            var definitions=new TemplateDefinitionSerializer(new TemplateDefinitionValidator());
            var definition=new TemplateDefinition {SchemaVersion=1,Sections=[new() {Id="subjective",Key="subjective",Title="Subjective",Order=0,
                Fields=[new() {Id="history",Key="history",Label="History",Type="TextArea",Required=true,Order=0},
                    new() {Id="pulse",Key="pulse",Label="Pulse",Type="Number",Required=true,Order=1}]}]};
            var version=new DocumentTemplateVersionResponse {TemplateUid=Note.TemplateUid!.Value,TemplateVersionUid=Note.TemplateVersionUid!.Value,
                DefinitionJson=definitions.Process(definition).DefinitionJson!,VersionNumber=1};
            var repository=Proxy<IPatientEncounterRepository>((method,args)=>
            {
                if(method.Name==nameof(IPatientEncounterRepository.GetByUidAsync)) return Task.FromResult<PatientEncounterDetailsResponse?>(Note);
                if(method.Name==nameof(IPatientEncounterRepository.UpdateStructuredDataAsync))
                {
                    var request=(UpdateEncounterStructuredDataRequest)args![2]!;
                    Saves++;SavedVersion=request.RowVersion;SavedActor=(long?)args[7];Note.StructuredDataJson=request.StructuredDataJson;
                    return Task.FromResult<PatientEncounterDetailsResponse?>(Note);
                }
                if(method.Name==nameof(IPatientEncounterRepository.SignAsync)) {Signs++;return Task.FromResult<PatientEncounterDetailsResponse?>(Note);}
                throw new NotSupportedException(method.Name);
            });
            Service=new(repository,Proxy<ISchedulingAppointmentRepository>((m,_)=>throw new NotSupportedException(m.Name)),new AppointmentStatusTransitionService(),
                Proxy<IPatientDocumentRepository>((_,_)=>Task.FromResult<DocumentTemplateDetailsResponse?>(null)),
                Proxy<IDocumentTemplateVersionRepository>((_,_)=>Task.FromResult<DocumentTemplateVersionResponse?>(version)),definitions,
                new TemplateInstanceRuntime(definitions),Proxy<IPatientService>((_,_)=>Task.FromResult<MicroEMR.Application.Patients.Contracts.PatientDetailsResponse?>(new())),
                new TemplateVariableResolver());
        }
    }
    private static T Proxy<T>(Func<MethodInfo,object?[]?,object?> handler) where T:class
    {var proxy=DispatchProxy.Create<T,InterfaceProxy>();((InterfaceProxy)(object)proxy).Handler=handler;return proxy;}
    public class InterfaceProxy:DispatchProxy
    {public Func<MethodInfo,object?[]?,object?> Handler {get;set;}=null!;protected override object? Invoke(MethodInfo? method,object?[]? args)=>Handler(method!,args);}
}
