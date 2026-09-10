using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Api.Config;

public sealed record AnalyticsConnectionResolution(
    string ConnectionString,
    string Source,
    bool UsedFallback,
    string? Warning);

public static class AnalyticsConnectionResolver
{
    public const string SourceAnalyticsConnection = "AnalyticsConnection";
    public const string SourceDefaultConnectionFallback = "DefaultConnectionFallback";
    // Retained for source compatibility; non-development resolution no longer returns either fallback source.
    public const string SourceMissingAnalyticsFallback = "MissingAnalyticsFallback";
    public const string SourceLoopbackAnalyticsFallback = "LoopbackAnalyticsFallback";

    public static string Resolve(
        IConfiguration configuration,
        bool? isDevelopment = null,
        Action<string>? onWarning = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return ResolveDetailed(
            configuration,
            isDevelopment,
            onWarning).ConnectionString;
    }

    public static AnalyticsConnectionResolution ResolveDetailed(
        IConfiguration configuration,
        bool? isDevelopment = null,
        Action<string>? onWarning = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var allowLoopbackInProduction =
            configuration.GetValue<bool?>("Analytics:AllowLoopbackInProduction") ?? false;

        return ResolveDetailed(
            configuration.GetConnectionString("DefaultConnection"),
            configuration.GetConnectionString("AnalyticsConnection"),
            isDevelopment ?? IsDevelopment(configuration),
            allowLoopbackInProduction,
            onWarning);
    }

    public static string Resolve(
        string? defaultConnection,
        string? analyticsConnection,
        bool isDevelopment,
        Action<string>? onWarning = null)
    {
        return ResolveDetailed(
            defaultConnection,
            analyticsConnection,
            isDevelopment,
            allowLoopbackInProduction: false,
            onWarning).ConnectionString;
    }

    public static AnalyticsConnectionResolution ResolveDetailed(
        string? defaultConnection,
        string? analyticsConnection,
        bool isDevelopment,
        bool allowLoopbackInProduction = false,
        Action<string>? onWarning = null)
    {
        if (string.IsNullOrWhiteSpace(analyticsConnection))
        {
            if (!isDevelopment)
            {
                throw new InvalidOperationException(
                    "AnalyticsConnection must be configured in non-development environments; refusing to fall back to DefaultConnection. Set ConnectionStrings__AnalyticsConnection.");
            }

            if (!string.IsNullOrWhiteSpace(defaultConnection))
            {
                return new AnalyticsConnectionResolution(
                    defaultConnection,
                    SourceDefaultConnectionFallback,
                    UsedFallback: true,
                    Warning: null);
            }

            throw new InvalidOperationException("AnalyticsConnection or DefaultConnection must be configured.");
        }

        if (!isDevelopment &&
            !allowLoopbackInProduction &&
            IsLoopbackConnectionString(analyticsConnection))
        {
            throw new InvalidOperationException(
                "AnalyticsConnection points to a loopback host in non-development; refusing to fall back to DefaultConnection. Set a non-loopback ConnectionStrings__AnalyticsConnection or explicitly configure Analytics:AllowLoopbackInProduction for an intentional exception.");
        }

        return new AnalyticsConnectionResolution(
            analyticsConnection,
            SourceAnalyticsConnection,
            UsedFallback: false,
            Warning: null);
    }

    public static bool IsLoopbackConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var hosts = (builder.Host ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var host in hosts)
            {
                if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                    host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                    host.Equals("::1", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Ignore parse failures and leave the caller on the original target.
        }

        return false;
    }

    public static string SummarizeConnection(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "<missing>";
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var host = string.IsNullOrWhiteSpace(builder.Host) ? "<unknown-host>" : builder.Host;
            var port = builder.Port;
            var database = string.IsNullOrWhiteSpace(builder.Database) ? "<unknown-db>" : builder.Database;
            var username = string.IsNullOrWhiteSpace(builder.Username) ? "<unknown-user>" : builder.Username;
            return $"{host}:{port}/{database} user={username}";
        }
        catch
        {
            return "<unparseable>";
        }
    }

    private static bool IsDevelopment(IConfiguration configuration)
    {
        var environmentName = configuration["DOTNET_ENVIRONMENT"];
        if (string.IsNullOrWhiteSpace(environmentName))
        {
            environmentName = configuration["ASPNETCORE_ENVIRONMENT"];
        }

        return string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);
    }
}
