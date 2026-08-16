using Microsoft.AspNetCore.Authorization;
using MicroEMR.Web.Controllers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Web.Authorization;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class TemplateAdministrationWebTests
{
    [Fact]
    public void AdministrationControllerRequiresAuthentication()
    {
        var authorize = typeof(TemplateAdministrationController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>();
        Assert.Contains(authorize, x => x.Policy is null);
        Assert.Contains(authorize, x => x.Policy == WebPermissionPolicyProvider.Prefix + PermissionKeys.TemplatesManage);
    }

    [Fact]
    public void BuilderProvidesVisualSchemaOperationsWithoutRawJsonEditor()
    {
        var root=Root();var view=File.ReadAllText(Path.Combine(root,"src","MicroEMR.Web","Views","TemplateAdministration","Builder.cshtml"));
        var script=File.ReadAllText(Path.Combine(root,"src","MicroEMR.Web","ClientApp","template-administration","builder.ts"));
        Assert.Contains("Add Section",view);Assert.Contains("Add Field",script);Assert.Contains("Add Option",view);
        Assert.Contains("Save Draft",view);Assert.Contains("Validate",view);Assert.Contains("Schema Preview",view);Assert.Contains("Publish Version",view);
        Assert.Contains("Edit in New Draft",view);Assert.DoesNotContain("Encounter template is not yet connected",view);
        Assert.DoesNotContain("name=\"DefinitionJson\"",view,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Raw JSON",view,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuilderMaintainsOneDefinitionAndUsesBackendValidationBeforeSaveAndPublish()
    {
        var script=File.ReadAllText(Path.Combine(Root(),"src","MicroEMR.Web","ClientApp","template-administration","builder.ts"));
        Assert.Contains("let definition=data.definition",script);
        Assert.Contains("crypto.randomUUID()",script);
        Assert.Contains("s.order=(si+1)*10",script);Assert.Contains("f.order=(fi+1)*10",script);Assert.Contains("o.order=(oi+1)*10",script);
        Assert.Contains("/TemplateAdministration/Validate",script);
        Assert.Contains("X-Requested-With",script);
        Assert.Contains("response.status===401",script);
        Assert.Contains("const valid=await validate(false)",script);
        Assert.Contains("/TemplateAdministration/Save",script);Assert.Contains("/TemplateAdministration/Publish",script);
        Assert.Contains("templateContent:version.templateContent",script);
        Assert.Contains("Duplicate",File.ReadAllText(Path.Combine(Root(),"src","MicroEMR.Application","Templates","Validation","TemplateDefinitionValidator.cs")));
        Assert.DoesNotContain("DuplicateFieldKey",script);
        Assert.DoesNotContain("UnsupportedFieldType",script);
    }

    [Fact]
    public void ListIncludesFiltersCloneScopeAndCapabilityAwareActions()
    {
        var view=File.ReadAllText(Path.Combine(Root(),"src","MicroEMR.Web","Views","TemplateAdministration","Index.cshtml"));
        Assert.Contains("kindFilter",view);Assert.Contains("scopeFilter",view);Assert.Contains("statusFilter",view);
        Assert.Contains("clone-template",view);Assert.Contains("item.CanEdit",view);Assert.Contains("Model.CanManageClinic",view);
        Assert.Contains("Personal",view);Assert.Contains("System",view);
    }

    [Fact]
    public void PreviewIsReadOnlyAndBuilderDoesNotImplementClinicalPersistence()
    {
        var script=File.ReadAllText(Path.Combine(Root(),"src","MicroEMR.Web","ClientApp","template-administration","builder.ts"));
        Assert.Contains("disabled",script);Assert.Contains("renderPreview",script);
        Assert.DoesNotContain("PatientUid",script,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EncounterUid",script,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DocumentContent",script,StringComparison.OrdinalIgnoreCase);
    }

    private static string Root([System.Runtime.CompilerServices.CallerFilePath]string source="")=>Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!,"..",".."));
}
