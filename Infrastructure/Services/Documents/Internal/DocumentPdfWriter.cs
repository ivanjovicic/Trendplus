using System.Globalization;
using System.Text;
using Application.Documents.Models;

namespace Infrastructure.Services.Documents.Internal;

internal static class DocumentPdfWriter
{
    private const int MaxCellLength = 120;

    public static byte[] Build(DocumentGenerationRequest request)
    {
        var isPortrait = string.Equals(request.Orientation, "portrait", StringComparison.OrdinalIgnoreCase);
        var pageWidth = isPortrait ? 595 : 842;
        var pageHeight = isPortrait ? 842 : 595;
        const int margin = 32;
        const int lineHeight = 14;
        var linesPerPage = Math.Max(10, (pageHeight - (margin * 2)) / lineHeight);

        var lines = new List<string>
        {
            request.Table.TableTitle,
            $"Generated: {DateTime.UtcNow.ToString("u", CultureInfo.InvariantCulture)}",
            $"Rows: {request.Table.Rows.Count}",
            string.Empty
        };

        if (request.IncludeFiltersAndMetadata)
        {
            foreach (var filter in request.Table.Filters)
            {
                lines.Add($"Filter - {filter.Label}: {filter.Value}");
            }

            if (request.Table.Filters.Count > 0)
            {
                lines.Add(string.Empty);
            }
        }

        var header = string.Join(" | ", request.Table.Columns.Select(c => c.Header));
        lines.Add(header);
        lines.Add(new string('-', Math.Min(120, header.Length + 10)));
        foreach (var row in request.Table.Rows)
        {
            lines.Add(string.Join(" | ", row.Select(cell => SanitizeCell(cell))));
        }

        var pageContents = new List<string>();
        for (var index = 0; index < lines.Count; index += linesPerPage)
        {
            pageContents.Add(BuildPageContent(lines.Skip(index).Take(linesPerPage).ToList(), pageHeight, margin, lineHeight));
        }

        return BuildPdfDocument(pageContents, pageWidth, pageHeight);
    }

    private static string BuildPageContent(IReadOnlyList<string> lines, int pageHeight, int margin, int lineHeight)
    {
        var builder = new StringBuilder();
        builder.AppendLine("BT");
        builder.AppendLine("/F1 9 Tf");
        var y = pageHeight - margin;
        foreach (var line in lines)
        {
            builder.AppendLine($"1 0 0 1 {margin} {y} Tm");
            builder.AppendLine($"({EscapePdf(line)}) Tj");
            y -= lineHeight;
        }
        builder.AppendLine("ET");
        return builder.ToString();
    }

    private static byte[] BuildPdfDocument(IReadOnlyList<string> pageContents, int pageWidth, int pageHeight)
    {
        var objects = new List<string>();
        objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
        objects.Add(string.Empty);
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        const int fontId = 3;

        var pageIds = new List<int>();
        foreach (var pageContent in pageContents)
        {
            var pageId = objects.Count + 1;
            var contentId = objects.Count + 2;
            pageIds.Add(pageId);
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {pageWidth} {pageHeight}] /Resources << /Font << /F1 {fontId} 0 R >> >> /Contents {contentId} 0 R >>");
            objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(pageContent)} >>\nstream\n{pageContent}endstream");
        }

        objects[1] = $"<< /Type /Pages /Count {pageIds.Count} /Kids [{string.Join(" ", pageIds.Select(id => $"{id} 0 R"))}] >>";

        var builder = new StringBuilder();
        builder.Append("%PDF-1.4\n");
        var offsets = new List<int> { 0 };

        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append($"xref\n0 {objects.Count + 1}\n");
        builder.Append("0000000000 65535 f \n");
        for (var index = 1; index < offsets.Count; index++)
        {
            builder.Append($"{offsets[index]:D10} 00000 n \n");
        }

        builder.Append($"trailer << /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static string EscapePdf(string value)
    {
        return (value ?? string.Empty)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }

    private static string SanitizeCell(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormKD);
        var ascii = new string(normalized.Where(ch => ch <= sbyte.MaxValue && !char.IsControl(ch)).ToArray());
        if (ascii.Length <= MaxCellLength)
        {
            return ascii;
        }

        return $"{ascii[..MaxCellLength]}...";
    }
}
