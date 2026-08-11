using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.Templates.Contracts;
using MicroEMR.Application.Templates.Definitions;
using MicroEMR.Application.Templates.Serialization;
using MicroEMR.Application.Templates.Services;
using MicroEMR.Application.Templates.Validation;
using Xunit;
using MicroEMR.Api.ClinicalUsers;
using Microsoft.AspNetCore.Http;

namespace MicroEMR.Api.Tests;

public sealed class TemplateDefinitionEngineTests
{
    private readonly TemplateDefinitionSerializer _serializer=new(new TemplateDefinitionValidator());

    [Fact] public void MinimalSchema_IsValid()=>Assert.True(_serializer.Process("{\"schemaVersion\":1,\"sections\":[]}").IsValid);

    [Theory]
    [InlineData("StaticText")][InlineData("Text")][InlineData("TextArea")][InlineData("Number")][InlineData("Date")]
    [InlineData("Boolean")][InlineData("Checkbox")][InlineData("Select")][InlineData("Radio")]
    public void SupportedFieldTypes_AreValid(string type)
    {
        var field=Field("field-one","fieldOne",type,10);
        if(type==TemplateFieldTypes.StaticText){field.Label=null;field.Content="Instructions";}
        if(TemplateFieldTypes.UsesOptions(type))field.Options=[new(){Value="one",Label="One",Order=10}];
        Assert.True(_serializer.Process(Definition(field)).IsValid);
    }

    [Theory]
    [InlineData("id","DuplicateSectionId")][InlineData("key","DuplicateSectionKey")]
    public void DuplicateSections_AreRejected(string property,string code)
    {
        var a=Section("first","first",10);var b=Section(property=="id"?"first":"second",property=="key"?"first":"second",20);
        Assert.Contains(_serializer.Process(new TemplateDefinition{SchemaVersion=1,Sections=[a,b]}).Errors,x=>x.Code==code);
    }

    [Theory]
    [InlineData("id","DuplicateFieldId")][InlineData("key","DuplicateFieldKey")]
    public void DuplicateFieldsAcrossSections_AreRejected(string property,string code)
    {
        var first=Field("first-field","firstField","Text",10);var second=Field(property=="id"?"first-field":"second-field",property=="key"?"firstField":"secondField","Text",10);
        Assert.Contains(_serializer.Process(new TemplateDefinition{SchemaVersion=1,Sections=[Section("one","one",10,first),Section("two","two",20,second)]}).Errors,x=>x.Code==code);
    }

    [Fact] public void InvalidKey_IsRejected(){var f=Field("field-one","../../bad key","Text",10);Assert.Contains(_serializer.Process(Definition(f)).Errors,x=>x.Code=="InvalidKey");}
    [Fact] public void UnsupportedSchemaVersion_IsRejected(){var result=_serializer.Process(new TemplateDefinition{SchemaVersion=2,Sections=[]});Assert.Contains(result.Errors,x=>x.Code=="UnsupportedSchemaVersion");}
    [Fact] public void UnsupportedFieldType_IsRejected(){var result=_serializer.Process(Definition(Field("field-one","fieldOne","Script",10)));Assert.Contains(result.Errors,x=>x.Code=="UnsupportedFieldType");}
    [Theory][InlineData("Select")][InlineData("Radio")]
    public void ChoiceWithoutOptions_IsRejected(string type){var result=_serializer.Process(Definition(Field("field-one","fieldOne",type,10)));Assert.Contains(result.Errors,x=>x.Code=="OptionsRequired");}
    [Fact] public void DuplicateOptionValues_AreRejected(){var f=Field("field-one","fieldOne","Select",10);f.Options=[new(){Value="same",Label="One"},new(){Value="same",Label="Two"}];Assert.Contains(_serializer.Process(Definition(f)).Errors,x=>x.Code=="DuplicateOptionValue");}
    [Fact] public void MalformedJson_IsStructuredError(){var result=_serializer.Process("{broken");Assert.Equal("MalformedJson",Assert.Single(result.Errors).Code);}
    [Fact] public void UnknownOrExecutableProperty_IsRejected(){var result=_serializer.Process("{\"schemaVersion\":1,\"sections\":[],\"onClick\":\"alert(1)\"}");Assert.Equal("MalformedJson",Assert.Single(result.Errors).Code);}
    [Fact] public void ExecutableText_IsRejected(){var f=Field("static-one","staticOne","StaticText",10);f.Label=null;f.Content="<script>alert(1)</script>";Assert.Contains(_serializer.Process(Definition(f)).Errors,x=>x.Code=="ExecutableContentNotAllowed");}
    [Fact] public void ExcessiveSections_AreRejected(){var d=new TemplateDefinition{SchemaVersion=1,Sections=Enumerable.Range(0,TemplateDefinitionLimits.MaximumSections+1).Select(i=>Section($"s-{i}",$"s{i}",i)).ToList()};Assert.Contains(_serializer.Process(d).Errors,x=>x.Code=="TooManySections");}

    [Fact]
    public void Serialization_NormalizesOrderingAndPreservesIdentity()
    {
        var later=Field("later-field","laterField","Text",20);var earlier=Field("earlier-field","earlierField","Text",10);
        var result=_serializer.Process(new TemplateDefinition{SchemaVersion=1,Sections=[Section("later","later",20),Section("earlier","earlier",10,later,earlier)]});
        Assert.True(result.IsValid);Assert.Equal("earlier",result.Definition!.Sections![0].Id);Assert.Equal("earlier-field",result.Definition.Sections[0].Fields![0].Id);
        Assert.Equal(result.DefinitionJson,_serializer.Process(result.DefinitionJson).DefinitionJson);
    }

    [Fact]
    public void Authorization_EnforcesOwnerClinicAndSystemRules()
    {
        var service=new TemplateAuthorizationService();var owner=new TemplateAccessContext(10,false);var other=new TemplateAccessContext(11,false);var admin=new TemplateAccessContext(11,true);
        var personal=Template("Personal",10);Assert.True(service.CanMutate(personal,owner));Assert.False(service.CanMutate(personal,other));Assert.False(service.CanView(personal,other));Assert.True(service.CanMutate(personal,admin));
        Assert.False(service.CanMutate(Template("Clinic",null),owner));Assert.True(service.CanMutate(Template("Clinic",null),admin));Assert.False(service.CanMutate(Template("System",null),admin));
    }

    [Fact]
    public void ClinicalActorContext_ReadProbe_DoesNotRequireMutationActor()
    {
        var context=new DefaultHttpContext();
        Assert.False(ClinicalUserActorContext.TryGet(context,out _));
        ClinicalUserActorContext.Set(context,42);
        Assert.True(ClinicalUserActorContext.TryGet(context,out var userId));Assert.Equal(42,userId);
    }

    private static TemplateDefinition Definition(params TemplateFieldDefinition[] fields)=>new(){SchemaVersion=1,Sections=[Section("section-one","sectionOne",10,fields)]};
    private static TemplateSectionDefinition Section(string id,string key,int order,params TemplateFieldDefinition[] fields)=>new(){Id=id,Key=key,Title=id,Order=order,Fields=[..fields]};
    private static TemplateFieldDefinition Field(string id,string key,string type,int order)=>new(){Id=id,Key=key,Type=type,Label=id,Order=order};
    private static DocumentTemplateDetailsResponse Template(string scope,long? owner)=>new(){TemplateScope=scope,OwnerUserId=owner};
}
