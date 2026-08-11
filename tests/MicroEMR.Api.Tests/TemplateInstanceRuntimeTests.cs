using System.Reflection;
using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.PatientDocuments.Repositories;
using MicroEMR.Application.PatientDocuments.Services;
using MicroEMR.Application.Templates.Definitions;
using MicroEMR.Application.Templates.Runtime;
using MicroEMR.Application.Templates.Serialization;
using MicroEMR.Application.Templates.Services;
using MicroEMR.Application.Templates.Contracts;
using MicroEMR.Application.Templates.Validation;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class TemplateInstanceRuntimeTests
{
    private readonly TemplateDefinitionSerializer _definitions = new(new TemplateDefinitionValidator());
    private TemplateInstanceRuntime Runtime => new(_definitions);

    [Fact]
    public void ValidSchemaV1Values_AreAccepted()
    {
        var result = Runtime.Process(Definition(), """{"schemaVersion":1,"values":{"text":"a","area":"b","number":72.5,"date":"2026-08-11","boolean":true,"checkbox":false,"select":"mild","radio":"yes"}}""");
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("number", "\"72\"")]
    [InlineData("date", "\"08/11/2026\"")]
    [InlineData("boolean", "\"true\"")]
    [InlineData("select", "\"unknown\"")]
    [InlineData("radio", "\"unknown\"")]
    public void InvalidTypedValues_AreRejected(string key, string value)
    {
        var result = Runtime.Process(Definition(required: false), $"{{\"schemaVersion\":1,\"values\":{{\"{key}\":{value}}}}}");
        Assert.Contains(result.Errors, x => x.Path == $"values.{key}");
    }

    [Fact]
    public void RequiredUnknownAndStaticTextValues_AreRejected()
    {
        var result = Runtime.Process(Definition(), """{"schemaVersion":1,"values":{"static":"x","unknown":"x"}}""");
        Assert.Contains(result.Errors, x => x.Code == "Required");
        Assert.Contains(result.Errors, x => x.Code == "UnknownField");
        Assert.Contains(result.Errors, x => x.Code == "StaticTextValue");
    }

    [Fact]
    public void MissingOptionalValues_AreAccepted()
    {
        Assert.True(Runtime.Process(Definition(required: false), """{"schemaVersion":1,"values":{}}""").IsValid);
    }

    [Fact]
    public void Snapshot_IsDeterministicAndUsesOptionLabels()
    {
        var definition = Definition(required: false);
        var processed = Runtime.Process(definition, """{"schemaVersion":1,"values":{"radio":"yes","select":"mild","boolean":false,"text":"Complaint"}}""");
        var snapshot = Runtime.RenderSnapshot(definition, processed.Data!).Replace("\r\n", "\n");
        Assert.Contains("Clinical\n--------", snapshot);
        Assert.Contains("Complaint", snapshot);
        Assert.Contains("Boolean\nNo", snapshot);
        Assert.Contains("Select\nMild", snapshot);
        Assert.Contains("Radio\nYes", snapshot);
        Assert.DoesNotContain("Number\n", snapshot);
    }

    [Fact]
    public async Task ExistingStructuredDocument_LoadsItsExactHistoricalVersion()
    {
        var versionUid = Guid.NewGuid();
        var documentRepository = Proxy<IPatientDocumentRepository>((method, _) => method.Name switch
        {
            nameof(IPatientDocumentRepository.GetByUidAsync) => Task.FromResult<PatientDocumentDetailsResponse?>(new()
            {
                DocumentUid=Guid.NewGuid(), TemplateVersionUid=versionUid,
                StructuredDataJson="{\"schemaVersion\":1,\"values\":{}}"
            }),
            _ => throw new NotSupportedException(method.Name)
        });
        var versionRepository = Proxy<IDocumentTemplateVersionRepository>((method, arguments) => method.Name switch
        {
            nameof(IDocumentTemplateVersionRepository.GetByUidAsync) when (Guid)arguments![0]! == versionUid =>
                Task.FromResult<DocumentTemplateVersionResponse?>(new(){TemplateVersionUid=versionUid,VersionNumber=1,DefinitionJson=_definitions.Process(Definition(false)).DefinitionJson!}),
            _ => throw new NotSupportedException(method.Name)
        });
        var service = new PatientDocumentService(documentRepository, versionRepository, _definitions, Runtime, new TemplateAuthorizationService());
        var loaded = await service.GetByUidAsync(Guid.NewGuid());
        Assert.Equal(1, loaded!.TemplateVersionNumber);
        Assert.NotNull(loaded.TemplateDefinition);
    }

    [Fact]
    public async Task ActivePublishedDocumentTemplate_IsInstantiatedWithExactVersion()
    {
        var templateUid=Guid.NewGuid();var versionUid=Guid.NewGuid();
        var documents=Proxy<IPatientDocumentRepository>((method,args)=>method.Name switch
        {
            nameof(IPatientDocumentRepository.GetTemplateByUidAsync)=>Task.FromResult<DocumentTemplateDetailsResponse?>(Template(templateUid)),
            nameof(IPatientDocumentRepository.CreateAsync)=>Task.FromResult(new PatientDocumentDetailsResponse{DocumentUid=Guid.NewGuid(),TemplateUid=templateUid,TemplateVersionUid=versionUid,StructuredDataJson=((CreatePatientDocumentRequest)args![1]!).StructuredDataJson}),
            _=>throw new NotSupportedException(method.Name)
        });
        var versions=Proxy<IDocumentTemplateVersionRepository>((method,_)=>method.Name switch
        {
            nameof(IDocumentTemplateVersionRepository.GetByTemplateUidAsync)=>Task.FromResult<IReadOnlyList<DocumentTemplateVersionResponse>>([Version(templateUid,versionUid,"Published",true)]),
            nameof(IDocumentTemplateVersionRepository.GetByUidAsync)=>Task.FromResult<DocumentTemplateVersionResponse?>(Version(templateUid,versionUid,"Published",true)),
            _=>throw new NotSupportedException(method.Name)
        });
        var created=await Service(documents,versions).CreateAsync(Guid.NewGuid(),new(){TemplateUid=templateUid,Title="Title",DocumentType="ignored"},42,new TemplateAccessContext(42,false));
        Assert.Equal(versionUid,created.TemplateVersionUid);Assert.True(created.IsStructured);
    }

    [Fact]
    public async Task DraftInactiveEncounterAndUnauthorizedPersonalTemplates_CannotBeInstantiated()
    {
        foreach(var template in new[]{Template(Guid.NewGuid(),active:false),Template(Guid.NewGuid(),kind:"Encounter"),Template(Guid.NewGuid(),scope:"Personal",owner:99)})
        {
            var documents=Proxy<IPatientDocumentRepository>((method,_)=>method.Name==nameof(IPatientDocumentRepository.GetTemplateByUidAsync)?Task.FromResult<DocumentTemplateDetailsResponse?>(template):throw new NotSupportedException(method.Name));
            var versions=Proxy<IDocumentTemplateVersionRepository>((method,_)=>throw new InvalidOperationException(method.Name));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service(documents,versions).CreateAsync(Guid.NewGuid(),new(){TemplateUid=template.TemplateUid,Title="Title",DocumentType="Type"},42,new TemplateAccessContext(42,false)));
        }
        var publishedTemplate=Template(Guid.NewGuid());
        var docRepo=Proxy<IPatientDocumentRepository>((method,_)=>method.Name==nameof(IPatientDocumentRepository.GetTemplateByUidAsync)?Task.FromResult<DocumentTemplateDetailsResponse?>(publishedTemplate):throw new NotSupportedException(method.Name));
        var draftRepo=Proxy<IDocumentTemplateVersionRepository>((method,_)=>method.Name==nameof(IDocumentTemplateVersionRepository.GetByTemplateUidAsync)?Task.FromResult<IReadOnlyList<DocumentTemplateVersionResponse>>([Version(publishedTemplate.TemplateUid,Guid.NewGuid(),"Draft",false)]):throw new NotSupportedException(method.Name));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(docRepo,draftRepo).CreateAsync(Guid.NewGuid(),new(){TemplateUid=publishedTemplate.TemplateUid,Title="Title",DocumentType="Type"},42,new TemplateAccessContext(42,false)));
    }

    [Fact]
    public void Migration_AddsNullableJsonAndPreservesLegacyMode()
    {
        var sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"database","tenant-clinical","migrations","0035-patient-document-structured-data.sql"));
        Assert.Contains("ADD StructuredDataJson NVARCHAR(MAX) NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("StructuredDataJson IS NULL OR ISJSON", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@TemplateVersionUid", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("template.TemplateKind=N'Document'", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("storage mode cannot be changed", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE dbo.PatientDocumentContent SET StructuredDataJson", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StructuredDraft_DetailsPageOpensRuntimeEditorImmediately()
    {
        var view=File.ReadAllText(Path.Combine(Root(),"src","MicroEMR.Web","Views","PatientDocuments","Details.cshtml"));
        Assert.Contains("isDraft && Model.IsStructured",view,StringComparison.Ordinal);
        Assert.Contains("_TemplateRuntimeForm",view,StringComparison.Ordinal);
    }

    [Fact]
    public void PatientChartQuickAction_TransitionsStructuredTemplatesToRuntimeEditor()
    {
        var root=Root();
        var modal=File.ReadAllText(Path.Combine(root,"src","MicroEMR.Web","Views","Patients","_SummaryQuickActionModals.cshtml"));
        var chart=File.ReadAllText(Path.Combine(root,"src","MicroEMR.Web","Views","Patients","Details.cshtml"));
        var controller=File.ReadAllText(Path.Combine(root,"src","MicroEMR.Web","Controllers","PatientDocumentsController.cs"));
        Assert.Contains("summaryDocumentStructuredMessage",modal,StringComparison.Ordinal);
        Assert.Contains("template.isStructured",chart,StringComparison.Ordinal);
        Assert.Contains("hasSchemaSections",controller,StringComparison.Ordinal);
    }

    private static TemplateDefinition Definition(bool required=true) => new()
    {
        SchemaVersion=1, Sections=[new(){Id="clinical-section",Key="clinical",Title="Clinical",Order=10,Fields=[
            Field("static",TemplateFieldTypes.StaticText,10,false,content:"Instructions"),
            Field("text",TemplateFieldTypes.Text,20,required), Field("area",TemplateFieldTypes.TextArea,30,false),
            Field("number",TemplateFieldTypes.Number,40,false), Field("date",TemplateFieldTypes.Date,50,false),
            Field("boolean",TemplateFieldTypes.Boolean,60,false), Field("checkbox",TemplateFieldTypes.Checkbox,70,false),
            Field("select",TemplateFieldTypes.Select,80,false,true), Field("radio",TemplateFieldTypes.Radio,90,false,true)
        ]}]
    };

    private static TemplateFieldDefinition Field(string key,string type,int order,bool required,bool options=false,string? content=null) => new()
    {
        Id=$"{key}-field",Key=key,Type=type,Label=char.ToUpperInvariant(key[0])+key[1..],Order=order,Required=required,Content=content,
        Options=options?[new(){Value="mild",Label="Mild",Order=10},new(){Value="yes",Label="Yes",Order=20}]:null
    };

    private PatientDocumentService Service(IPatientDocumentRepository documents,IDocumentTemplateVersionRepository versions)=>
        new(documents,versions,_definitions,Runtime,new TemplateAuthorizationService());
    private static DocumentTemplateDetailsResponse Template(Guid uid,bool active=true,string kind="Document",string scope="Clinic",long? owner=null)=>new()
        {TemplateUid=uid,TemplateName="Template",DocumentType="Type",Category="Category",TemplateKind=kind,TemplateScope=scope,OwnerUserId=owner,IsActive=active};
    private DocumentTemplateVersionResponse Version(Guid templateUid,Guid versionUid,string status,bool current)=>new()
        {TemplateUid=templateUid,TemplateVersionUid=versionUid,VersionNumber=1,Status=status,IsCurrent=current,DefinitionJson=_definitions.Process(Definition(false)).DefinitionJson!};

    private static T Proxy<T>(Func<MethodInfo,object?[]?,object?> handler) where T:class
    {
        var proxy=DispatchProxy.Create<T,InterfaceProxy>();((InterfaceProxy)(object)proxy).Handler=handler;return proxy;
    }
    public class InterfaceProxy:DispatchProxy
    {
        public Func<MethodInfo,object?[]?,object?> Handler {get;set;}=null!;
        protected override object? Invoke(MethodInfo? targetMethod,object?[]? args)=>Handler(targetMethod!,args);
    }

    private static string Root()
    {
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null&&!File.Exists(Path.Combine(directory.FullName,"MicroEMR.slnx")))directory=directory.Parent;
        return directory?.FullName??throw new DirectoryNotFoundException();
    }
}
