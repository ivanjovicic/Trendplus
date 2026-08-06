using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;

namespace Api.Config;

public enum EmbeddingServiceRuntimeMode
{
    Mock = 0,
    Disabled = 1,
    Python = 2
}

public sealed record EmbeddingServiceRuntimeSettings(
    EmbeddingServiceRuntimeMode Mode,
    Uri? BaseAddress,
    TimeSpan Timeout)
{
    public bool UseMock => Mode == EmbeddingServiceRuntimeMode.Mock;
    public bool IsDisabled => Mode == EmbeddingServiceRuntimeMode.Disabled;
}

public static class EmbeddingServiceRuntimePolicy
{
    private const int DefaultTimeoutSeconds = 30;
    private const int MinTimeoutSeconds = 1;
    private const int MaxTimeoutSeconds = 120;

    public static EmbeddingServiceRuntimeSettings Resolve(
        IConfiguration configuration,
        string environmentName)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);

        var useMock = configuration.GetValue("EmbeddingService:UseMock", true);
        var enabled = configuration.GetValue<bool?>("EmbeddingService:Enabled");
        var timeoutSeconds = configuration.GetValue("EmbeddingService:Timeout", DefaultTimeoutSeconds);
        if (timeoutSeconds is < MinTimeoutSeconds or > MaxTimeoutSeconds)
        {
            throw new InvalidOperationException(
                $"EmbeddingService:Timeout must be between {MinTimeoutSeconds} and {MaxTimeoutSeconds} seconds.");
        }

        var timeout = TimeSpan.FromSeconds(timeoutSeconds);

        // Explicit quarantine: Enabled=false keeps the API up without mock or remote calls.
        if (enabled == false)
        {
            if (useMock && IsProduction(environmentName))
            {
                throw new InvalidOperationException(
                    "EmbeddingService:UseMock must be false in production.");
            }

            return new EmbeddingServiceRuntimeSettings(
                Mode: EmbeddingServiceRuntimeMode.Disabled,
                BaseAddress: null,
                Timeout: timeout);
        }

        if (useMock)
        {
            if (IsProduction(environmentName))
            {
                throw new InvalidOperationException(
                    "EmbeddingService:UseMock must be false in production.");
            }

            return new EmbeddingServiceRuntimeSettings(
                Mode: EmbeddingServiceRuntimeMode.Mock,
                BaseAddress: null,
                Timeout: timeout);
        }

        var baseUrl = configuration["EmbeddingService:BaseUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            // Incomplete real-service config: quarantine instead of crashing the whole API.
            return new EmbeddingServiceRuntimeSettings(
                Mode: EmbeddingServiceRuntimeMode.Disabled,
                BaseAddress: null,
                Timeout: timeout);
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseAddress))
        {
            throw new InvalidOperationException(
                "EmbeddingService:BaseUrl must be an absolute HTTP or HTTPS URL.");
        }

        if (baseAddress.Scheme != Uri.UriSchemeHttp && baseAddress.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "EmbeddingService:BaseUrl must use the HTTP or HTTPS scheme.");
        }

        if (IsProduction(environmentName) && !IsPrivateNetworkAddress(baseAddress))
        {
            throw new InvalidOperationException(
                "EmbeddingService:BaseUrl must point to a private network address in production.");
        }

        return new EmbeddingServiceRuntimeSettings(
            Mode: EmbeddingServiceRuntimeMode.Python,
            BaseAddress: baseAddress,
            Timeout: timeout);
    }

    private static bool IsProduction(string environmentName)
    {
        return string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsPrivateNetworkAddress(Uri uri)
    {
        if (uri.IsLoopback)
        {
            return false;
        }

        if (!IPAddress.TryParse(uri.Host, out var address))
        {
            return false;
        }

        return IsPrivateNetworkAddress(address);
    }

    private static bool IsPrivateNetworkAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10
                || bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31
                || bytes[0] == 192 && bytes[1] == 168;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 0xfc || bytes[0] == 0xfd;
        }

        return false;
    }
}
