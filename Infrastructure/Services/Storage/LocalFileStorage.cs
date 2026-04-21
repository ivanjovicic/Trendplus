using Application.Common.Interfaces;
using Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Storage;

public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _basePath;
    private readonly string _basePathPrefix;
    private readonly StringComparison _pathComparison;

    public LocalFileStorage(IOptions<StorageOptions> options)
    {
        var storageOptions = options.Value;
        if (string.IsNullOrWhiteSpace(storageOptions.LocalBasePath))
        {
            throw new InvalidOperationException("Storage:LocalBasePath must be configured for local file storage.");
        }

        _basePath = Path.GetFullPath(storageOptions.LocalBasePath);
        Directory.CreateDirectory(_basePath);

        _basePathPrefix = _basePath.EndsWith(Path.DirectorySeparatorChar)
            ? _basePath
            : _basePath + Path.DirectorySeparatorChar;
        _pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    public async Task UploadAsync(string key, Stream content, CancellationToken ct = default)
    {
        if (content is null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        var filePath = ResolveAbsolutePath(key);
        var directory = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException("Unable to resolve target directory for local storage upload.");
        Directory.CreateDirectory(directory);

        await using var destination = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);
        await content.CopyToAsync(destination, ct);
        await destination.FlushAsync(ct);
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var filePath = ResolveAbsolutePath(key);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Local storage file not found.", filePath);
        }

        Stream stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var filePath = ResolveAbsolutePath(key);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var filePath = ResolveAbsolutePath(key);
        return Task.FromResult(File.Exists(filePath));
    }

    public Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _ = ResolveAbsolutePath(key);
        throw new NotSupportedException("Presigned URLs are not supported for local file storage.");
    }

    private string ResolveAbsolutePath(string key)
    {
        var normalizedKey = StorageKeyNormalizer.Normalize(key);
        var relativePath = normalizedKey.Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = Path.GetFullPath(Path.Combine(_basePath, relativePath));
        if (!absolutePath.StartsWith(_basePathPrefix, _pathComparison) &&
            !string.Equals(absolutePath, _basePath, _pathComparison))
        {
            throw new InvalidOperationException("Resolved local storage path is outside the configured base path.");
        }

        return absolutePath;
    }
}
