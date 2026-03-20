using System.IO.Compression;
using System.Text;
using Application.Documents.Models;
using Infrastructure.Configuration;
using Infrastructure.Services.Documents;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public class DocumentRendererTests
{
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
                    new DocumentColumnDefinition { Key = "date", Header = "Date" },
                    new DocumentColumnDefinition { Key = "revenue", Header = "Revenue" }
                },
                Rows = new List<List<string?>>
                {
                    new() { "2026-03-18", "1250.50" }
                }
            }
        };
    }
}
