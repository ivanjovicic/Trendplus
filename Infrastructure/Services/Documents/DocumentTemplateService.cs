using System.Globalization;
using System.Text;
using Application.Documents.Models;
using Domain.Model.Documents;
using Infrastructure.DbContexts;
using Infrastructure.Services.Documents.Internal;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Documents;

public interface IDocumentTemplateRenderer
{
    Task<(DocumentTemplate Template, string Html)> RenderHtmlAsync(DocumentGenerationRequest request, TrendplusDbContext db, CancellationToken ct);
}

public sealed class DocumentTemplateRenderer : IDocumentTemplateRenderer
{
    public async Task<(DocumentTemplate Template, string Html)> RenderHtmlAsync(DocumentGenerationRequest request, TrendplusDbContext db, CancellationToken ct)
    {
        var template = await ResolveTemplateAsync(request, db, ct);
        var content = TemplateSanitizer.Sanitize(template.Content);
        var header = TemplateSanitizer.Sanitize(template.HeaderContent);
        var footer = TemplateSanitizer.Sanitize(template.FooterContent);

        var html = content
            .Replace("{{styles}}", BuildStyles(request.Orientation), StringComparison.Ordinal)
            .Replace("{{orientation}}", request.Orientation, StringComparison.Ordinal)
            .Replace("{{title}}", DocumentHtmlEncoder.Encode(request.Table.TableTitle), StringComparison.Ordinal)
            .Replace("{{generated_at}}", DocumentHtmlEncoder.Encode(DateTime.UtcNow.ToString("f", ResolveCulture(request, template))), StringComparison.Ordinal)
            .Replace("{{requested_by}}", "Trendplus user", StringComparison.Ordinal)
            .Replace("{{header}}", ReplaceTokens(header, request, template), StringComparison.Ordinal)
            .Replace("{{footer}}", ReplaceTokens(footer, request, template), StringComparison.Ordinal)
            .Replace("{{table}}", BuildTable(request), StringComparison.Ordinal)
            .Replace("{{filters}}", BuildNamedValues(request.Table.Filters), StringComparison.Ordinal)
            .Replace("{{metadata}}", BuildNamedValues(request.Table.Metadata), StringComparison.Ordinal)
            .Replace("{{template_version}}", template.Version.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{{table_key}}", DocumentHtmlEncoder.Encode(request.Table.TableKey), StringComparison.Ordinal);

        return (template, html);
    }

    private static async Task<DocumentTemplate> ResolveTemplateAsync(DocumentGenerationRequest request, TrendplusDbContext db, CancellationToken ct)
    {
        var templateName = string.IsNullOrWhiteSpace(request.TemplateName)
            ? "analytics-table-default"
            : request.TemplateName;

        var query = db.DocumentTemplates.AsNoTracking()
            .Where(template => template.Name == templateName && template.IsActive);

        if (request.TemplateVersion.HasValue)
        {
            query = query.Where(template => template.Version == request.TemplateVersion.Value);
        }

        var template = await query
            .OrderByDescending(template => template.Version)
            .FirstOrDefaultAsync(ct);

        if (template is not null)
        {
            return template;
        }

        return await db.DocumentTemplates.AsNoTracking()
            .Where(template => template.Type == DocumentTemplateTypes.AnalyticsTableReport && template.IsActive)
            .OrderByDescending(template => template.Version)
            .FirstAsync(ct);
    }

    private static CultureInfo ResolveCulture(DocumentGenerationRequest request, DocumentTemplate template)
    {
        var cultureName = request.Locale ?? template.Locale;
        return string.IsNullOrWhiteSpace(cultureName)
            ? CultureInfo.InvariantCulture
            : CultureInfo.GetCultureInfo(cultureName);
    }

    private static string ReplaceTokens(string content, DocumentGenerationRequest request, DocumentTemplate template)
    {
        return content
            .Replace("{{title}}", DocumentHtmlEncoder.Encode(request.Table.TableTitle), StringComparison.Ordinal)
            .Replace("{{template_version}}", template.Version.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{{table_key}}", DocumentHtmlEncoder.Encode(request.Table.TableKey), StringComparison.Ordinal);
    }

    private static string BuildTable(DocumentGenerationRequest request)
    {
        var isBlankForm = request.DocumentType is "inventory-balance-blank";
        var builder = new StringBuilder();
        builder.Append(isBlankForm ? "<table class=\"blank-form\"><thead><tr>" : "<table><thead><tr>");
        foreach (var column in request.Table.Columns)
        {
            builder.Append("<th>");
            builder.Append(DocumentHtmlEncoder.Encode(column.Header));
            builder.Append("</th>");
        }

        builder.Append("</tr></thead><tbody>");
        foreach (var row in request.Table.Rows)
        {
            builder.Append("<tr>");
            foreach (var value in row)
            {
                builder.Append("<td>");
                builder.Append(DocumentHtmlEncoder.Encode(value));
                builder.Append("</td>");
            }

            builder.Append("</tr>");
        }

        builder.Append("</tbody></table>");
        return builder.ToString();
    }

    private static string BuildNamedValues(IReadOnlyList<DocumentNamedValue> values)
    {
        if (values.Count == 0)
        {
            return "<p class=\"empty\">No values.</p>";
        }

        var builder = new StringBuilder("<dl class=\"named-values\">");
        foreach (var value in values)
        {
            builder.Append("<dt>");
            builder.Append(DocumentHtmlEncoder.Encode(string.IsNullOrWhiteSpace(value.Label) ? value.Key : value.Label));
            builder.Append("</dt><dd>");
            builder.Append(DocumentHtmlEncoder.Encode(value.Value));
            builder.Append("</dd>");
        }

        builder.Append("</dl>");
        return builder.ToString();
    }

    private static string BuildStyles(string orientation)
    {
        var portrait = string.Equals(orientation, "portrait", StringComparison.OrdinalIgnoreCase);
        var pageSize    = portrait ? "A4 portrait"  : "A4 landscape";
        var pageMargin  = portrait ? "10mm 8mm"     : "10mm";
        // Portrait: 14 columns on ~190 mm — scale down to fit cleanly
        var bodyFont    = portrait ? "9px"          : "11px";
        var thFont      = portrait ? "7.5px"        : "9px";
        var thPadding   = portrait ? "5px 4px"      : "6px 8px";
        var tdFont      = portrait ? "9px"          : "11px";
        var tdPadding   = portrait ? "9px 4px"      : "12px 8px";

        return $"""
            body {{ font-family: "Segoe UI", Arial, sans-serif; font-size: {bodyFont}; color: #111827; background: #f3f4f6; margin: 0; }}
            .sheet {{ max-width: 1280px; margin: 0 auto; padding: 20px 28px 40px; background: #ffffff; }}
            .doc-header {{ border-bottom: 2px solid #0f172a; margin-bottom: 12px; padding-bottom: 10px; }}
            .doc-header h1 {{ margin: 0; font-size: 17px; line-height: 1.3; }}
            .doc-header p {{ margin: 4px 0 0; font-size: 10px; color: #475569; }}
            .meta {{ display: grid; gap: 6px; grid-template-columns: repeat(3, minmax(0, 1fr)); margin-bottom: 10px; font-size: 10px; }}
            .named-values {{ display: grid; grid-template-columns: max-content 1fr; gap: 4px 12px; margin: 0 0 10px; font-size: 10px; }}
            .named-values dt {{ font-weight: 600; color: #0f172a; }}
            .named-values dd {{ margin: 0; color: #334155; }}
            .empty {{ color: #64748b; }}
            .table-section {{ margin-top: 8px; }}
            table {{ width: 100%; border-collapse: collapse; table-layout: fixed; }}
            th, td {{ border: 1px solid #64748b; }}
            th {{
              background: #dde4ef;
              text-align: left;
              font-weight: 700;
              font-size: {thFont};
              line-height: 1.3;
              padding: {thPadding};
              vertical-align: bottom;
              overflow-wrap: break-word;
              word-break: normal;
              hyphens: auto;
              color: #1e293b;
            }}
            td {{
              font-size: {tdFont};
              padding: {tdPadding};
              line-height: 1.4;
              vertical-align: middle;
              word-break: break-word;
              color: #111827;
            }}
            tr:nth-child(even) td {{ background: #f8fafc; }}
            .blank-form td {{ background: #ffffff !important; }}
            .doc-footer {{ display: flex; justify-content: space-between; margin-top: 14px; padding-top: 8px; border-top: 1px solid #94a3b8; color: #475569; font-size: 10px; }}
            @media print {{
              body {{ background: #ffffff; }}
              .sheet {{ margin: 0; padding: 10px; box-shadow: none; }}
              @page {{ size: {pageSize}; margin: {pageMargin}; }}
              thead {{ display: table-header-group; }}
              tr {{ page-break-inside: avoid; page-break-after: auto; }}
              table {{ page-break-inside: auto; }}
            }}
            """;
    }
}
