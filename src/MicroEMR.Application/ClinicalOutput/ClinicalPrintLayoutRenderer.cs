using System.Net;

namespace MicroEMR.Application.ClinicalOutput;

public interface IClinicalPrintLayoutRenderer
{
    string Render(string title, string clinicalHtml);
}

public sealed class ClinicalPrintLayoutRenderer : IClinicalPrintLayoutRenderer
{
    public string Render(string title, string clinicalHtml) => $$"""
        <!DOCTYPE html>
        <html lang="en"><head><meta charset="utf-8"><title>{{WebUtility.HtmlEncode(title)}}</title>
        <style>
        @page { size: Letter; margin: 0.65in; }
        * { box-sizing: border-box; }
        body { margin: 0; color: #202124; font: 11pt Arial, Helvetica, sans-serif; line-height: 1.4; }
        .clinical-print-title { margin: 0 0 18pt; font-size: 18pt; }
        .template-section { margin: 0 0 18pt; }
        .template-section-title { margin: 0 0 9pt; padding-bottom: 4pt; border-bottom: 1px solid #888; font-size: 14pt; break-after: avoid; }
        .template-field, .template-static-text { margin: 0 0 9pt; break-inside: avoid; overflow-wrap: anywhere; }
        .template-field-label { display: block; margin-bottom: 2pt; }
        .template-field-value { white-space: normal; }
        table { width: 100%; border-collapse: collapse; }
        th, td { padding: 4pt; border: 1px solid #999; text-align: left; }
        tr { break-inside: avoid; }
        </style></head><body><main class="clinical-print-output">
        <h1 class="clinical-print-title">{{WebUtility.HtmlEncode(title)}}</h1>{{clinicalHtml}}
        </main></body></html>
        """;
}
