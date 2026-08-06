using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Api.Config;

/// <summary>
/// Controls whether OpenAPI/Swagger UI is exposed for the current environment.
/// Secure default: enabled in Development only unless explicitly overridden.
/// </summary>
public sealed class SwaggerExposureOptions
{
    public const string SectionName = "Swagger";

    /// <summary>
    /// When set, overrides the environment default.
    /// When null, Development => enabled; otherwise disabled.
    /// </summary>
    public bool? Enabled { get; set; }
}

public static class SwaggerExposurePolicy
{
    public static bool IsEnabled(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var configured = configuration.GetValue<bool?>($"{SwaggerExposureOptions.SectionName}:Enabled");
        if (configured.HasValue)
        {
            return configured.Value;
        }

        return environment.IsDevelopment();
    }
}
