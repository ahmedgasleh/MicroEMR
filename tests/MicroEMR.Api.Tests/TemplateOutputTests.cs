using System.Text.Json;
using MicroEMR.Application.Templates.Definitions;
using MicroEMR.Application.Templates.Output;
using MicroEMR.Application.Templates.Runtime;
using MicroEMR.Application.Templates.Variables;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class TemplateOutputTests
{
    private readonly TemplateOutputBuilder _builder = new(new TemplateVariableResolver());

    [Fact]
    public void Builder_ProducesOrderedTypedDisplayOutput_ForAllFieldTypes()
    {
        var output = _builder.Build(Definition(), Data());

        Assert.Equal(new[] { "first", "second" }, output.Sections.Select(x => x.Key));
        var items = output.Sections[0].Items;
        Assert.Equal(new[] { "static", "text", "area", "number", "date", "boolean", "checkbox", "select", "radio" },
            items.Select(x => x.Key));
        Assert.Equal("12.50", items.Single(x => x.Key == "number").DisplayValue);
        Assert.Equal("Yes", items.Single(x => x.Key == "boolean").DisplayValue);
        Assert.Equal("No", items.Single(x => x.Key == "checkbox").DisplayValue);
        Assert.Equal("Visible label", items.Single(x => x.Key == "select").DisplayValue);
        Assert.Equal("Radio label", items.Single(x => x.Key == "radio").DisplayValue);
        Assert.DoesNotContain(items, x => x.Key == "empty");
    }

    [Fact]
    public void HtmlRenderer_EncodesAllSources_AndPreservesNewlinesWithoutExecutableMarkup()
    {
        var definition = Definition();
        var section = definition.Sections!.Single(x => x.Key == "first");
        section.Title = "<script>title</script>";
        section.Fields!.Single(x => x.Key == "static").Content = "<img src=x onerror=alert(1)>";
        section.Fields!.Single(x => x.Key == "text").Label = "<b onclick=x>Label</b>";
        var data = Data();
        data.Values["text"] = Json("<script>alert(1)</script>\nnext");

        var html = new TemplateHtmlRenderer().Render(_builder.Build(definition, data));

        Assert.Contains("&lt;script&gt;title&lt;/script&gt;", html);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", html);
        Assert.Contains("&lt;b onclick=x&gt;Label&lt;/b&gt;", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;<br>next", html);
        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Builder_ResolvesOnlyRegisteredVariables_WithoutMutatingDefinition()
    {
        var definition = Definition();
        var field = definition.Sections!.Single(x => x.Key == "first").Fields!.Single(x => x.Key == "static");
        field.Content = "Patient: {{Patient.FullName}}";
        var context = new TemplateVariableContext("Ada <L>", new(1815, 12, 10), "Dr Test",
            new(2026, 8, 12, 1, 0, 0, DateTimeKind.Utc), new(2026, 8, 12));

        var output = _builder.Build(definition, Data(), context);

        Assert.Equal("Patient: Ada <L>", output.Sections[0].Items[0].DisplayValue);
        Assert.Equal("Patient: {{Patient.FullName}}", field.Content);
        field.Content = "{{System.Environment}}";
        Assert.Throws<TemplateVariableResolutionException>(() => _builder.Build(definition, Data(), context));
    }

    private static TemplateDefinition Definition() => new()
    {
        SchemaVersion = 1,
        Sections =
        [
            new() { Id = "second-section", Key = "second", Title = "Second", Order = 20, Fields = [] },
            new() { Id = "first-section", Key = "first", Title = "First", Order = 10, Fields =
            [
                Field("radio", TemplateFieldTypes.Radio, 90, [new() { Value="r", Label="Radio label", Order=10 }]),
                Field("static", TemplateFieldTypes.StaticText, 10, content:"Instructions"),
                Field("text", TemplateFieldTypes.Text, 20), Field("area", TemplateFieldTypes.TextArea, 30),
                Field("number", TemplateFieldTypes.Number, 40), Field("date", TemplateFieldTypes.Date, 50),
                Field("boolean", TemplateFieldTypes.Boolean, 60), Field("checkbox", TemplateFieldTypes.Checkbox, 70),
                Field("select", TemplateFieldTypes.Select, 80, [new() { Value="v", Label="Visible label", Order=10 }]),
                Field("empty", TemplateFieldTypes.Text, 100)
            ]}
        ]
    };

    private static TemplateFieldDefinition Field(string key, string type, int order,
        List<TemplateFieldOption>? options = null, string? content = null) => new()
    { Id=$"{key}-field", Key=key, Type=type, Label=key, Order=order, Options=options, Content=content };

    private static TemplateInstanceData Data() => new() { SchemaVersion=1, Values=new(StringComparer.Ordinal)
    {
        ["text"]=Json("text"), ["area"]=Json("line one\nline two"), ["number"]=Json(12.50m),
        ["date"]=Json("2026-08-12"), ["boolean"]=Json(true), ["checkbox"]=Json(false),
        ["select"]=Json("v"), ["radio"]=Json("r"), ["empty"]=Json("")
    }};

    private static JsonElement Json<T>(T value) => JsonSerializer.SerializeToElement(value);
}
