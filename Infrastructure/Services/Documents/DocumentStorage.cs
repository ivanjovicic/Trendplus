using Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using System.Text;

namespace Infrastructure.Services.Documents;

public sealed class StoredDocumentDescriptor
{
    public string RelativePath { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}

public interface IDocumentStorage
{
    Task<StoredDocumentDescriptor> SaveAsync(
        Guid documentId,
        string fileName,
        Func<Stream, CancellationToken, Task> writer,
        CancellationToken ct = default);

    Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct = default);
}

public sealed class LocalDocumentStorage : IDocumentStorage
{
    private readonly DocumentExportOptions _options;

    public LocalDocumentStorage(IOptions<DocumentExportOptions> options)
    {
        _options = options.Value;
    }

    public async Task<StoredDocumentDescriptor> SaveAsync(
        Guid documentId,
        string fileName,
        Func<Stream, CancellationToken, Task> writer,
        CancellationToken ct = default)
    {
        var root = Path.GetFullPath(_options.StorageRoot);
        Directory.CreateDirectory(root);

        var sanitizedFileName = SanitizeFileName(fileName);
        var relativePath = Path.Combine(
            DateTime.UtcNow.ToString("yyyy"),
            DateTime.UtcNow.ToString("MM"),
            $"{documentId}-{sanitizedFileName}");

        var fullPath = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using (var stream = File.Create(fullPath))
        {
            await writer(stream, ct);
            await stream.FlushAsync(ct);
        }

        var fileInfo = new FileInfo(fullPath);
        return new StoredDocumentDescriptor
        {
            RelativePath = relativePath.Replace('\\', '/'),
            FullPath = fullPath,
            FileName = sanitizedFileName,
            SizeBytes = fileInfo.Length
        };
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(Path.GetFullPath(_options.StorageRoot), relativePath.Replace('/', Path.DirectorySeparatorChar));
        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult(stream);
    }

    private static string SanitizeFileName(string fileName)
    {
        var source = string.IsNullOrWhiteSpace(fileName) ? "document" : fileName.Trim();
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(source.Length);

        foreach (var ch in source)
        {
            if (char.IsControl(ch) || invalidChars.Contains(ch))
            {
                builder.Append('_');
                continue;
            }

            builder.Append(ch);
        }

        var sanitized = builder.ToString().Trim(' ', '.');
        if (sanitized.Length == 0)
        {
            sanitized = "document";
        }

        if (sanitized.Length > 128)
        {
            var extension = Path.GetExtension(sanitized);
            var baseName = Path.GetFileNameWithoutExtension(sanitized);
            var maxBaseLength = Math.Max(1, 128 - extension.Length);
            sanitized = $"{baseName[..Math.Min(baseName.Length, maxBaseLength)]}{extension}";
        }

        return sanitized;
    }
}
