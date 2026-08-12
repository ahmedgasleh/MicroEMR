using System.Net;
using System.Text;

namespace MicroEMR.Application.Templates.Output;

public interface ITemplateHtmlRenderer
{
    string Render(TemplateOutputDocument document);
}

public sealed class TemplateHtmlRenderer : ITemplateHtmlRenderer
{
    public string Render(TemplateOutputDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var html = new StringBuilder("<article class=\"template-document\">");
        foreach (var section in document.Sections)
        {
            html.Append("<section class=\"template-section\">")
                .Append("<h2 class=\"template-section-title\">").Append(Encode(section.Title)).Append("</h2>");
            foreach (var item in section.Items)
            {
                if (item.IsStaticContent)
                {
                    html.Append("<div class=\"template-static-text\">").Append(EncodeLines(item.DisplayValue)).Append("</div>");
                    continue;
                }
                html.Append("<div class=\"template-field template-field-")
                    .Append(EncodeAttribute(item.FieldType.ToLowerInvariant())).Append("\">")
                    .Append("<strong class=\"template-field-label\">").Append(Encode(item.Label ?? string.Empty)).Append("</strong>")
                    .Append("<div class=\"template-field-value\">").Append(EncodeLines(item.DisplayValue)).Append("</div></div>");
            }
            html.Append("</section>");
        }
        return html.Append("</article>").ToString();
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
    private static string EncodeAttribute(string value) => WebUtility.HtmlEncode(value);
    private static string EncodeLines(string value) => Encode(value).Replace("\r\n", "<br>", StringComparison.Ordinal)
        .Replace("\n", "<br>", StringComparison.Ordinal).Replace("\r", "<br>", StringComparison.Ordinal);
}
