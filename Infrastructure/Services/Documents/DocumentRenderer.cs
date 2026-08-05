using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Application.Documents.Models;
using Infrastructure.Configuration;
using Infrastructure.Services.Documents.Internal;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Documents;

public interface IDocumentRenderer
{
    string Format { get; }
    string MimeType { get; }
    Task RenderAsync(Stream output, DocumentGenerationRequest request, string html, CancellationToken ct);
}

public sealed class CsvDocumentRenderer : IDocumentRenderer
{
    private readonly string _delimiter;

    public CsvDocumentRenderer(IOptions<DocumentExportOptions> options)
    {
        _delimiter = ResolveDelimiter(options.Value);
    }

    public string Format => "csv";
    public string MimeType => "text/csv";

    public async Task RenderAsync(Stream output, DocumentGenerationRequest request, string html, CancellationToken ct)
    {
        await using var writer = new StreamWriter(output, new UTF8Encoding(true), leaveOpen: true);
        await writer.WriteLineAsync(string.Join(_delimiter, request.Table.Columns.Select(column => Escape(column.Header, _delimiter))));
        foreach (var row in request.Table.Rows)
        {
            ct.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(string.Join(_delimiter, row.Select(cell => Escape(cell, _delimiter))));
        }

        await writer.FlushAsync(ct);
    }

    private static string Escape(string? value, string delimiter)
    {
        var text = value ?? string.Empty;
        if (text.Contains(delimiter, StringComparison.Ordinal) || text.Contains('"') || text.Contains('\n') || text.Contains('\r'))
        {
            return $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return text;
    }

    private static string ResolveDelimiter(DocumentExportOptions options)
    {
        return string.Equals(options.CsvDelimiter, "semicolon", StringComparison.OrdinalIgnoreCase) ? ";" : ",";
    }
}

public sealed class XlsxDocumentRenderer : IDocumentRenderer
{
    // cellXfs indices in StylesXml
    private const string StyleGeneral = "0";
    private const string StyleHeader = "1";
    private const string StyleNumber = "2";
    private const string StyleCurrency = "3";
    private const string StylePercentUnits = "4";
    private const string StyleDate = "5";

    public string Format => "xlsx";
    public string MimeType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public async Task RenderAsync(Stream output, DocumentGenerationRequest request, string html, CancellationToken ct)
    {
        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        WriteEntry(archive, "[Content_Types].xml", ContentTypesXml);
        WriteEntry(archive, "_rels/.rels", RootRelationshipsXml);
        WriteEntry(archive, "xl/workbook.xml", WorkbookXml);
        WriteEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationshipsXml);
        WriteEntry(archive, "xl/styles.xml", StylesXml);

        var sheetEntry = archive.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Fastest);
        await using var stream = sheetEntry.Open();
        var settings = new XmlWriterSettings
        {
            Async = true,
            Encoding = Encoding.UTF8,
            CloseOutput = false
        };

        await using var writer = XmlWriter.Create(stream, settings);
        await writer.WriteStartDocumentAsync();
        await writer.WriteStartElementAsync(null, "worksheet", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        await writer.WriteStartElementAsync(null, "sheetData", null);

        var columns = request.Table.Columns;
        await WriteHeaderRowAsync(writer, 1, columns);
        for (var rowIndex = 0; rowIndex < request.Table.Rows.Count; rowIndex++)
        {
            ct.ThrowIfCancellationRequested();
            await WriteDataRowAsync(writer, rowIndex + 2, columns, request.Table.Rows[rowIndex]);
        }

        await writer.WriteEndElementAsync();
        await writer.WriteEndElementAsync();
        await writer.WriteEndDocumentAsync();
        await writer.FlushAsync();
    }

    private static async Task WriteHeaderRowAsync(XmlWriter writer, int rowIndex, IReadOnlyList<DocumentColumnDefinition> columns)
    {
        await writer.WriteStartElementAsync(null, "row", null);
        await writer.WriteAttributeStringAsync(null, "r", null, rowIndex.ToString(CultureInfo.InvariantCulture));
        for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
        {
            await WriteInlineStringCellAsync(
                writer,
                $"{GetColumnName(columnIndex + 1)}{rowIndex}",
                columns[columnIndex].Header,
                StyleHeader);
        }

        await writer.WriteEndElementAsync();
    }

    private static async Task WriteDataRowAsync(
        XmlWriter writer,
        int rowIndex,
        IReadOnlyList<DocumentColumnDefinition> columns,
        IReadOnlyList<string?> values)
    {
        await writer.WriteStartElementAsync(null, "row", null);
        await writer.WriteAttributeStringAsync(null, "r", null, rowIndex.ToString(CultureInfo.InvariantCulture));

        var cellCount = Math.Max(columns.Count, values.Count);
        for (var columnIndex = 0; columnIndex < cellCount; columnIndex++)
        {
            var cellRef = $"{GetColumnName(columnIndex + 1)}{rowIndex}";
            var rawValue = columnIndex < values.Count ? values[columnIndex] : null;
            var dataType = columnIndex < columns.Count ? columns[columnIndex].DataType : null;

            if (TryCreateTypedCell(rawValue, dataType, out var typedValue, out var styleIndex))
            {
                await WriteNumericCellAsync(writer, cellRef, typedValue, styleIndex);
            }
            else
            {
                await WriteInlineStringCellAsync(writer, cellRef, rawValue, StyleGeneral);
            }
        }

        await writer.WriteEndElementAsync();
    }

    /// <summary>
    /// Parses export cell text into an Excel numeric value when DataType is number/currency/percent/date.
    /// Percent columns expect percent units (35 = 35%), matching frontend RQ40 contract — not Excel ratio.
    /// </summary>
    public static bool TryCreateTypedCell(
        string? rawValue,
        string? dataType,
        out string invariantNumericValue,
        out string styleIndex)
    {
        invariantNumericValue = string.Empty;
        styleIndex = StyleGeneral;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var normalizedType = (dataType ?? string.Empty).Trim().ToLowerInvariant();
        return normalizedType switch
        {
            "number" => TryParseNumber(rawValue, StyleNumber, out invariantNumericValue, out styleIndex),
            "currency" => TryParseNumber(rawValue, StyleCurrency, out invariantNumericValue, out styleIndex),
            "percent" => TryParseNumber(rawValue, StylePercentUnits, out invariantNumericValue, out styleIndex),
            "date" or "datetime" => TryParseDate(rawValue, out invariantNumericValue, out styleIndex),
            _ => false
        };
    }

    private static bool TryParseNumber(
        string rawValue,
        string style,
        out string invariantNumericValue,
        out string styleIndex)
    {
        invariantNumericValue = string.Empty;
        styleIndex = style;

        if (decimal.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant))
        {
            invariantNumericValue = invariant.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        if (decimal.TryParse(rawValue, NumberStyles.Float, CultureInfo.GetCultureInfo("sr-RS"), out var localized))
        {
            invariantNumericValue = localized.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }

    private static bool TryParseDate(string rawValue, out string invariantNumericValue, out string styleIndex)
    {
        invariantNumericValue = string.Empty;
        styleIndex = StyleDate;

        if (!DateTime.TryParse(
                rawValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed)
            && !DateTime.TryParse(
                rawValue,
                CultureInfo.GetCultureInfo("sr-RS"),
                DateTimeStyles.AllowWhiteSpaces,
                out parsed))
        {
            return false;
        }

        // Excel stores dates as OLE Automation dates (days since 1899-12-30).
        invariantNumericValue = parsed.ToOADate().ToString("G17", CultureInfo.InvariantCulture);
        return true;
    }

    private static async Task WriteNumericCellAsync(XmlWriter writer, string cellRef, string invariantValue, string styleIndex)
    {
        await writer.WriteStartElementAsync(null, "c", null);
        await writer.WriteAttributeStringAsync(null, "r", null, cellRef);
        await writer.WriteAttributeStringAsync(null, "s", null, styleIndex);
        await writer.WriteElementStringAsync(null, "v", null, invariantValue);
        await writer.WriteEndElementAsync();
    }

    private static async Task WriteInlineStringCellAsync(XmlWriter writer, string cellRef, string? value, string styleIndex)
    {
        await writer.WriteStartElementAsync(null, "c", null);
        await writer.WriteAttributeStringAsync(null, "r", null, cellRef);
        await writer.WriteAttributeStringAsync(null, "t", null, "inlineStr");
        await writer.WriteAttributeStringAsync(null, "s", null, styleIndex);
        await writer.WriteStartElementAsync(null, "is", null);
        await writer.WriteElementStringAsync(null, "t", null, value ?? string.Empty);
        await writer.WriteEndElementAsync();
        await writer.WriteEndElementAsync();
    }

    private static string GetColumnName(int columnIndex)
    {
        var name = string.Empty;
        while (columnIndex > 0)
        {
            var modulo = (columnIndex - 1) % 26;
            name = Convert.ToChar('A' + modulo) + name;
            columnIndex = (columnIndex - modulo) / 26;
        }

        return name;
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private const string ContentTypesXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
        "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
        "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
        "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
        "</Types>";

    private const string RootRelationshipsXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
        "</Relationships>";

    private const string WorkbookXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
        "<sheets><sheet name=\"Export\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>";

    private const string WorkbookRelationshipsXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
        "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
        "</Relationships>";

    // Styles: 0 general, 1 header, 2 number 0.00, 3 currency, 4 percent-units (literal %), 5 date
    private const string StylesXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
        "<numFmts count=\"2\">" +
        "<numFmt numFmtId=\"164\" formatCode=\"#,##0.00\"/>" +
        "<numFmt numFmtId=\"165\" formatCode=\"0.00&quot;%&quot;\"/>" +
        "</numFmts>" +
        "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
        "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>" +
        "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
        "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
        "<cellXfs count=\"6\">" +
        "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
        "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
        "<xf numFmtId=\"2\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
        "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
        "<xf numFmtId=\"165\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
        "<xf numFmtId=\"14\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
        "</cellXfs>" +
        "</styleSheet>";
}

public sealed class HtmlDocumentRenderer : IDocumentRenderer
{
    public string Format => "html";
    public string MimeType => "text/html";

    public async Task RenderAsync(Stream output, DocumentGenerationRequest request, string html, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(html);
        await output.WriteAsync(bytes, ct);
    }
}

public sealed class PdfDocumentRenderer : IDocumentRenderer
{
    public string Format => "pdf";
    public string MimeType => "application/pdf";

    public async Task RenderAsync(Stream output, DocumentGenerationRequest request, string html, CancellationToken ct)
    {
        var bytes = DocumentPdfWriter.Build(request);
        await output.WriteAsync(bytes, ct);
    }
}
