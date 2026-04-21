using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Application.Common.Interfaces;
using Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Storage;

public sealed class S3FileStorage : IFileStorage, IDisposable
{
    private readonly StorageOptions _options;
    private readonly IAmazonS3 _client;

    public S3FileStorage(IOptions<StorageOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.Bucket))
        {
            throw new InvalidOperationException("Storage:Bucket must be configured for s3 storage provider.");
        }

        var config = new AmazonS3Config
        {
            ForcePathStyle = _options.UsePathStyle
        };

        if (!string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            config.ServiceURL = _options.Endpoint;
            if (!string.IsNullOrWhiteSpace(_options.Region))
            {
                config.AuthenticationRegion = _options.Region;
            }
        }
        else
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(_options.Region);
        }

        var hasAccessKey = !string.IsNullOrWhiteSpace(_options.AccessKey);
        var hasSecretKey = !string.IsNullOrWhiteSpace(_options.SecretKey);
        if (hasAccessKey && hasSecretKey)
        {
            _client = new AmazonS3Client(
                new BasicAWSCredentials(_options.AccessKey, _options.SecretKey),
                config);
        }
        else
        {
            _client = new AmazonS3Client(config);
        }
    }

    public async Task UploadAsync(string key, Stream content, CancellationToken ct = default)
    {
        if (content is null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        var normalizedKey = StorageKeyNormalizer.Normalize(key);
        var request = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = normalizedKey,
            InputStream = content,
            AutoCloseStream = false
        };

        await _client.PutObjectAsync(request, ct);
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken ct = default)
    {
        var normalizedKey = StorageKeyNormalizer.Normalize(key);
        var response = await _client.GetObjectAsync(
            new GetObjectRequest
            {
                BucketName = _options.Bucket,
                Key = normalizedKey
            },
            ct);

        return new S3ObjectResponseStream(response);
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var normalizedKey = StorageKeyNormalizer.Normalize(key);
        await _client.DeleteObjectAsync(
            new DeleteObjectRequest
            {
                BucketName = _options.Bucket,
                Key = normalizedKey
            },
            ct);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        var normalizedKey = StorageKeyNormalizer.Normalize(key);
        try
        {
            await _client.GetObjectMetadataAsync(
                new GetObjectMetadataRequest
                {
                    BucketName = _options.Bucket,
                    Key = normalizedKey
                },
                ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchKey")
        {
            return false;
        }
    }

    public Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (expiry <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(expiry), "Presigned URL expiry must be a positive time span.");
        }

        var normalizedKey = StorageKeyNormalizer.Normalize(key);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = normalizedKey,
            Expires = DateTime.UtcNow.Add(expiry)
        };
        return Task.FromResult(_client.GetPreSignedURL(request));
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    private sealed class S3ObjectResponseStream : Stream
    {
        private readonly GetObjectResponse _response;
        private readonly Stream _inner;

        public S3ObjectResponseStream(GetObjectResponse response)
        {
            _response = response;
            _inner = response.ResponseStream;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _inner.WriteAsync(buffer, offset, count, cancellationToken);
        public override ValueTask DisposeAsync()
        {
            _response.Dispose();
            return ValueTask.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _response.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
