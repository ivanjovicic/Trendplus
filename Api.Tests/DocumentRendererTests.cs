using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Application.Documents.Models;
using Infrastructure.Configuration;
using Infrastructure.Services.Documents;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public class DocumentRendererTests
{
    private static readonly XNamespace Ssml = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    [Fact]
    public async Task CsvRenderer_WritesHeaderAndRows()
    {
        var renderer = new CsvDocumentRenderer(Options.Create(new DocumentExportOptions()));
        var request = CreateRequest();

        await using var stream = new MemoryStream();
        await renderer.RenderAsync(stream, request, string.Empty, CancellationToken.None);

        var bytes = stream.ToArray();
        Assert.True(bytes.Length >= 3);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        var text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("Date,Revenue", text);
        Assert.Contains("2026-03-18,1250.50", text);
    }

    [Fact]
    public async Task XlsxRenderer_WritesWorksheetEntry()
    {
        var renderer = new XlsxDocumentRenderer();
        var request = CreateRequest();

        await using var stream = new MemoryStream();
        await renderer.RenderAsync(stream, request, string.Empty, CancellationToken.None);
        stream.Position = 0;

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        Assert.NotNull(archive.GetEntry("xl/worksheets/sheet1.xml"));
    }

    [Fact]
    public async Task XlsxRenderer_WritesTypedNumericCurrencyPercentAndDateCells()
    {
        var renderer = new XlsxDocumentRenderer();
        var request = new DocumentGenerationRequest
        {
            Format = "xlsx",
            Orientation = "landscape",
            TemplateName = "analytics-table-default",
            DocumentType = "analytics-table-report",
            Table = new DocumentTablePayload
            {
                TableKey = "typed-cells",
                TableTitle = "Typed cells",
                Columns =
                [
                    new DocumentColumnDefinition { Key = "name", Header = "Name", DataType = "text" },
                    new DocumentColumnDefinition { Key = "units", Header = "Units", DataType = "number" },
                    new DocumentColumnDefinition { Key = "revenue", Header = "Revenue", DataType = "currency" },
                    new DocumentColumnDefinition { Key = "marginPct", Header = "Margin %", DataType = "percent" },
                    new DocumentColumnDefinition { Key = "date", Header = "Date", DataType = "date" }
                ],
                Rows =
                [
                    ["Supplier A", "12", "1250.50", "35", "2026-03-18"]
                ]
            }
        };

        await using var stream = new MemoryStream();
        await renderer.RenderAsync(stream, request, string.Empty, CancellationToken.None);
        stream.Position = 0;

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var sheetXml = await ReadEntryTextAsync(archive, "xl/worksheets/sheet1.xml");
        var stylesXml = await ReadEntryTextAsync(archive, "xl/styles.xml");
        var sheet = XDocument.Parse(sheetXml);

        Assert.Contains("numFmtId=\"164\"", stylesXml, StringComparison.Ordinal);
        Assert.Contains("0.00&quot;%&quot;", stylesXml, StringComparison.Ordinal);

        var dataCells = sheet.Root!
            .Element(Ssml + "sheetData")!
            .Elements(Ssml + "row")
            .Skip(1)
            .Take(1)
            .Elements(Ssml + "c")
            .ToList();

        Assert.Equal(5, dataCells.Count);

        Assert.Equal("inlineStr", dataCells[0].Attribute("t")?.Value);
        Assert.Equal("Supplier A", dataCells[0].Element(Ssml + "is")?.Element(Ssml + "t")?.Value);

        Assert.Null(dataCells[1].Attribute("t"));
        Assert.Equal("12", dataCells[1].Element(Ssml + "v")?.Value);
        Assert.Equal("2", dataCells[1].Attribute("s")?.Value);

        Assert.Null(dataCells[2].Attribute("t"));
        Assert.Equal("1250.50", dataCells[2].Element(Ssml + "v")?.Value);
        Assert.Equal("3", dataCells[2].Attribute("s")?.Value);

        Assert.Null(dataCells[3].Attribute("t"));
        Assert.Equal("35", dataCells[3].Element(Ssml + "v")?.Value);
        Assert.Equal("4", dataCells[3].Attribute("s")?.Value);

        Assert.Null(dataCells[4].Attribute("t"));
        var oaDate = double.Parse(dataCells[4].Element(Ssml + "v")!.Value, CultureInfo.InvariantCulture);
        Assert.Equal(new DateTime(2026, 3, 18, 0, 0, 0, DateTimeKind.Utc).ToOADate(), oaDate, 5);
        Assert.Equal("5", dataCells[4].Attribute("s")?.Value);
    }

    [Theory]
    [InlineData("number", "42.5", true, "42.5", "2")]
    [InlineData("currency", "1250.50", true, "1250.50", "3")]
    [InlineData("percent", "35", true, "35", "4")]
    [InlineData("percent", "0.35", true, "0.35", "4")]
    [InlineData("text", "42.5", false, "", "0")]
    [InlineData("number", "not-a-number", false, "", "0")]
    public void TryCreateTypedCell_RespectsDataTypeContract(
        string dataType,
        string raw,
        bool expected,
        string expectedValue,
        string expectedStyle)
    {
        var ok = XlsxDocumentRenderer.TryCreateTypedCell(raw, dataType, out var value, out var style);
        Assert.Equal(expected, ok);
        if (expected)
        {
            Assert.Equal(expectedValue, value);
            Assert.Equal(expectedStyle, style);
        }
    }

    [Fact]
    public async Task PdfRenderer_WritesPdfHeader()
    {
        var renderer = new PdfDocumentRenderer();
        var request = CreateRequest();

        await using var stream = new MemoryStream();
        await renderer.RenderAsync(stream, request, string.Empty, CancellationToken.None);

        var bytes = stream.ToArray();
        var prefix = Encoding.ASCII.GetString(bytes.Take(8).ToArray());
        Assert.StartsWith("%PDF-1.", prefix);
    }

    [Fact]
    public async Task PdfRenderer_GenerateSamplePdf_ShouldNotThrow()
    {
        var renderer = new PdfDocumentRenderer();
        var request = CreateRequest();

        await using var stream = new MemoryStream();
        var exception = await Record.ExceptionAsync(() => renderer.RenderAsync(stream, request, string.Empty, CancellationToken.None));

        Assert.Null(exception);
        Assert.NotEmpty(stream.ToArray());
    }

    [Fact]
    public async Task LocalDocumentStorage_SanitizesFileName()
    {
        var root = Path.Combine(Path.GetTempPath(), $"trendplus-doc-tests-{Guid.NewGuid():N}");
        try
        {
            var storage = new LocalDocumentStorage(Options.Create(new DocumentExportOptions
            {
                StorageRoot = root
            }));

            var stored = await storage.SaveAsync(Guid.NewGuid(), "bad\0name<>:\"/\\\\|?*.csv", async (stream, _) =>
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes("x"));
            });

            Assert.DoesNotContain('\0', stored.FileName);
            Assert.True(stored.FileName.Length <= 128);
            Assert.True(File.Exists(stored.FullPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static async Task<string> ReadEntryTextAsync(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path);
        Assert.NotNull(entry);
        await using var entryStream = entry!.Open();
        using var reader = new StreamReader(entryStream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private static DocumentGenerationRequest CreateRequest()
    {
        return new DocumentGenerationRequest
        {
            Format = "csv",
            Orientation = "landscape",
            TemplateName = "analytics-table-default",
            DocumentType = "analytics-table-report",
            Table = new DocumentTablePayload
            {
                TableKey = "daily-sales",
                TableTitle = "Daily Sales",
                Columns = new List<DocumentColumnDefinition>
                {
                    new DocumentColumnDefinition { Key = "date", Header = "Date", DataType = "date" },
                    new DocumentColumnDefinition { Key = "revenue", Header = "Revenue", DataType = "currency" }
                },
                Rows = new List<List<string?>>
                {
                    new() { "2026-03-18", "1250.50" }
                }
            }
        };
    }
}
