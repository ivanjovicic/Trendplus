using Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using System.Text;
using Application.Common.Interfaces;

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
    private readonly IFileStorage? _fileStorage;
    private readonly StorageOptions? _storageOptions;

    // Preserve existing constructor for tests/compatibility
    public LocalDocumentStorage(IOptions<DocumentExportOptions> options)
    {
        _options = options.Value;
    }

    // DI constructor used in runtime - optional file storage is injected
    public LocalDocumentStorage(IOptions<DocumentExportOptions> options, IFileStorage fileStorage, IOptions<StorageOptions> storageOptions)
        : this(options)
    {
        _fileStorage = fileStorage;
        _storageOptions = storageOptions?.Value;
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
        var descriptor = new StoredDocumentDescriptor
        {
            RelativePath = relativePath.Replace('\\', '/'),
            FullPath = fullPath,
            FileName = sanitizedFileName,
            SizeBytes = fileInfo.Length
        };

        // If a remote file storage provider is configured, upload the newly created local file
        try
        {
            var provider = _storageOptions?.Provider?.Trim().ToLowerInvariant() ?? "local";
            if (_fileStorage is not null && provider != "local")
            {
                // Use the relative path as the storage key (normalized by the storage backend)
                await using var readStream = File.OpenRead(fullPath);
                await _fileStorage.UploadAsync(descriptor.RelativePath, readStream, ct);
            }
        }
        catch (Exception ex)
        {
            // Do not fail the document generation if remote upload fails; keep local file as fallback.
            // Log to console for visibility; DI logger not injected here to keep constructor compatibility.
            Console.WriteLine($"Warning: file storage upload failed for {descriptor.RelativePath}: {ex.Message}");
        }

        return descriptor;
    }

    public async Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct = default)
    {
        // Prefer the configured IFileStorage when the key exists there; otherwise fall back to local FS
        if (_fileStorage is not null)
        {
            try
            {
                if (await _fileStorage.ExistsAsync(relativePath, ct))
                {
                    return await _fileStorage.OpenReadAsync(relativePath, ct);
                }
            }
            catch (Exception ex)
            {
                // Failover to local filesystem if storage backend check fails
                Console.WriteLine($"Warning: file storage check/open failed for {relativePath}: {ex.Message}");
            }
        }

        var fullPath = Path.Combine(Path.GetFullPath(_options.StorageRoot), relativePath.Replace('/', Path.DirectorySeparatorChar));
        Stream stream = File.OpenRead(fullPath);
        return stream;
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
