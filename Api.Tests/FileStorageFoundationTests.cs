using System.Text;
using Application.Common.Interfaces;
using Infrastructure.Configuration;
using Infrastructure.Services.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public sealed class FileStorageFoundationTests
{
    [Fact]
    public async Task LocalFileStorage_UploadOpenReadExistsDelete_Works()
    {
        var root = Path.Combine(Path.GetTempPath(), $"trendplus-storage-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var storage = new LocalFileStorage(Options.Create(new StorageOptions
            {
                Provider = "local",
                LocalBasePath = root
            }));

            const string key = "imports/2026-04/sample.txt";
            var payload = "storage-foundation-smoke";
            await storage.UploadAsync(key, new MemoryStream(Encoding.UTF8.GetBytes(payload)));

            Assert.True(await storage.ExistsAsync(key));

            await using (var stream = await storage.OpenReadAsync(key))
            {
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var roundTrip = await reader.ReadToEndAsync();
                Assert.Equal(payload, roundTrip);
            }

            await storage.DeleteAsync(key);
            Assert.False(await storage.ExistsAsync(key));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LocalFileStorage_RejectsPathTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), $"trendplus-storage-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var storage = new LocalFileStorage(Options.Create(new StorageOptions
            {
                Provider = "local",
                LocalBasePath = root
            }));

            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                storage.UploadAsync("../outside.txt", new MemoryStream(new byte[] { 1, 2, 3 })));
            Assert.Contains("traversal", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void AddFileStorage_DefaultsToLocal_WhenProviderIsMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddFileStorage(configuration);

        using var provider = services.BuildServiceProvider();
        var storage = provider.GetRequiredService<IFileStorage>();
        Assert.IsType<LocalFileStorage>(storage);
    }

    [Fact]
    public void AddFileStorage_SelectsS3_WhenProviderIsS3()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Provider"] = "s3",
                ["Storage:Bucket"] = "trendplus-test-bucket",
                ["Storage:Region"] = "us-east-1",
                ["Storage:Endpoint"] = "http://localhost:9000",
                ["Storage:AccessKey"] = "minioadmin",
                ["Storage:SecretKey"] = "minioadmin"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddFileStorage(configuration);

        using var provider = services.BuildServiceProvider();
        var storage = provider.GetRequiredService<IFileStorage>();
        Assert.IsType<S3FileStorage>(storage);
    }

    [Fact]
    public void StorageOptionsValidator_FailsForInvalidProvider()
    {
        var validator = new StorageOptionsValidator();
        var result = validator.Validate(null, new StorageOptions
        {
            Provider = "azure"
        });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void StorageOptionsValidator_FailsForS3WithoutBucket()
    {
        var validator = new StorageOptionsValidator();
        var result = validator.Validate(null, new StorageOptions
        {
            Provider = "s3",
            Bucket = ""
        });

        Assert.False(result.Succeeded);
    }
}
