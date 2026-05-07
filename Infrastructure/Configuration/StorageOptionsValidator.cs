using Microsoft.Extensions.Options;

namespace Infrastructure.Configuration;

public sealed class StorageOptionsValidator : IValidateOptions<StorageOptions>
{
    public ValidateOptionsResult Validate(string? name, StorageOptions options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail("Storage options are missing.");
        }

        var provider = NormalizeProvider(options.Provider);
        if (provider is not ("local" or "s3"))
        {
            return ValidateOptionsResult.Fail("Storage:Provider must be either 'local' or 's3'.");
        }

        if (provider == "local" && string.IsNullOrWhiteSpace(options.LocalBasePath))
        {
            return ValidateOptionsResult.Fail("Storage:LocalBasePath is required for local storage provider.");
        }

        if (provider == "s3")
        {
            if (string.IsNullOrWhiteSpace(options.Bucket))
            {
                return ValidateOptionsResult.Fail("Storage:Bucket is required for s3 storage provider.");
            }

            if (options.UploadTimeoutSeconds <= 0)
            {
                return ValidateOptionsResult.Fail("Storage:UploadTimeoutSeconds must be greater than zero.");
            }

            if (options.MaxErrorRetryCount < 0)
            {
                return ValidateOptionsResult.Fail("Storage:MaxErrorRetryCount must be zero or greater.");
            }

            var hasAccessKey = !string.IsNullOrWhiteSpace(options.AccessKey);
            var hasSecretKey = !string.IsNullOrWhiteSpace(options.SecretKey);
            if (hasAccessKey ^ hasSecretKey)
            {
                return ValidateOptionsResult.Fail("Storage:AccessKey and Storage:SecretKey must both be set, or both omitted.");
            }
        }

        return ValidateOptionsResult.Success;
    }

    private static string NormalizeProvider(string? provider) =>
        string.IsNullOrWhiteSpace(provider) ? "local" : provider.Trim().ToLowerInvariant();
}
