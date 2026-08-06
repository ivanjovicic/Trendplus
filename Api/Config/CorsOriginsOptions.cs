using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Api.Config;

/// <summary>
/// Single source of truth for browser CORS allow-list (API policy + health/ready preflight).
/// </summary>
public sealed class CorsOriginsOptions
{
    public const string SectionName = "Cors";

    /// <summary>Allowed browser origins (scheme + host + optional port).</summary>
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
}

public static class CorsOriginsResolver
{
    public static readonly string[] DevelopmentDefaults =
    {
        "http://localhost:5173",
        "http://localhost:5174",
        "http://localhost:8080",
        "https://trendplus.vercel.app"
    };

    public static string[] Resolve(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var configured = ReadConfiguredOrigins(configuration);
        if (configured.Length > 0)
        {
            return configured;
        }

        if (environment.IsDevelopment())
        {
            return DevelopmentDefaults.ToArray();
        }

        throw new InvalidOperationException(
            "Cors:AllowedOrigins must be configured with at least one origin outside Development.");
    }

    public static HashSet<string> ToSet(IEnumerable<string> origins) =>
        new(origins.Where(static o => !string.IsNullOrWhiteSpace(o)), StringComparer.OrdinalIgnoreCase);

    private static string[] ReadConfiguredOrigins(IConfiguration configuration)
    {
        var section = configuration.GetSection($"{CorsOriginsOptions.SectionName}:AllowedOrigins");
        var values = section.Get<string[]>() ?? Array.Empty<string>();
        return values
            .Where(static o => !string.IsNullOrWhiteSpace(o))
            .Select(static o => o.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
