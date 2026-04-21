using Application.Common.Interfaces;
using Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Storage;

public static class FileStorageServiceCollectionExtensions
{
    public static IServiceCollection AddFileStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.Section))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<StorageOptions>, StorageOptionsValidator>();

        var provider = ResolveProviderName(configuration[$"{StorageOptions.Section}:Provider"]);
        switch (provider)
        {
            case "local":
                services.AddSingleton<IFileStorage, LocalFileStorage>();
                break;
            case "s3":
                services.AddSingleton<IFileStorage, S3FileStorage>();
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported storage provider '{provider}'. Supported values are 'local' and 's3'.");
        }

        return services;
    }

    public static string ResolveProviderName(string? rawProvider)
    {
        return string.IsNullOrWhiteSpace(rawProvider)
            ? "local"
            : rawProvider.Trim().ToLowerInvariant();
    }
}
