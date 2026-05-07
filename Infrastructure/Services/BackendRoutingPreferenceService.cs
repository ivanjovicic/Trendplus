using Microsoft.Extensions.Configuration;

namespace Infrastructure.Services;

public enum BackendProvider
{
    Render,
    Fly
}

public sealed class BackendRoutingPreference
{
    public BackendProvider PrimaryProvider { get; set; } = BackendProvider.Render;
    public bool FallbackEnabled { get; set; } = true;
    public BackendProvider FallbackProvider { get; set; } = BackendProvider.Fly;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = "startup";
}

public sealed class BackendRoutingPreferenceUpdate
{
    public string PrimaryProvider { get; set; } = "render";
    public bool FallbackEnabled { get; set; }
    public string? FallbackProvider { get; set; }
}

/// <summary>
/// Runtime admin preference for frontend backend routing order.
/// This is intentionally lightweight and process-local.
/// </summary>
public sealed class BackendRoutingPreferenceService
{
    private readonly object _lock = new();
    private BackendRoutingPreference _state;

    public BackendRoutingPreferenceService(IConfiguration configuration)
    {
        var primaryRaw = configuration["BackendRouting:PrimaryProvider"];
        var fallbackRaw = configuration["BackendRouting:FallbackProvider"];
        var fallbackEnabled = configuration.GetValue<bool?>("BackendRouting:FallbackEnabled") ?? true;

        var primary = ParseProviderOrDefault(primaryRaw, BackendProvider.Render);
        var fallback = ParseProviderOrDefault(fallbackRaw, primary == BackendProvider.Render ? BackendProvider.Fly : BackendProvider.Render);

        if (fallback == primary)
        {
            fallback = primary == BackendProvider.Render ? BackendProvider.Fly : BackendProvider.Render;
        }

        _state = new BackendRoutingPreference
        {
            PrimaryProvider = primary,
            FallbackEnabled = fallbackEnabled,
            FallbackProvider = fallback,
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedBy = "startup"
        };
    }

    public BackendRoutingPreference Get()
    {
        lock (_lock)
        {
            return new BackendRoutingPreference
            {
                PrimaryProvider = _state.PrimaryProvider,
                FallbackEnabled = _state.FallbackEnabled,
                FallbackProvider = _state.FallbackProvider,
                UpdatedAtUtc = _state.UpdatedAtUtc,
                UpdatedBy = _state.UpdatedBy
            };
        }
    }

    public bool TryUpdate(BackendRoutingPreferenceUpdate input, string updatedBy, out BackendRoutingPreference updated, out string? error)
    {
        var primary = ParseProvider(input.PrimaryProvider);
        if (primary is null)
        {
            updated = Get();
            error = "Primary provider must be either 'render' or 'fly'.";
            return false;
        }

        var fallbackEnabled = input.FallbackEnabled;
        BackendProvider fallback = primary.Value == BackendProvider.Render ? BackendProvider.Fly : BackendProvider.Render;

        if (fallbackEnabled)
        {
            var parsedFallback = ParseProvider(input.FallbackProvider);
            if (parsedFallback is null)
            {
                updated = Get();
                error = "Fallback provider must be either 'render' or 'fly' when fallback is enabled.";
                return false;
            }

            if (parsedFallback.Value == primary.Value)
            {
                updated = Get();
                error = "Fallback provider must differ from the primary provider.";
                return false;
            }

            fallback = parsedFallback.Value;
        }

        lock (_lock)
        {
            _state.PrimaryProvider = primary.Value;
            _state.FallbackEnabled = fallbackEnabled;
            _state.FallbackProvider = fallback;
            _state.UpdatedAtUtc = DateTime.UtcNow;
            _state.UpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? "api" : updatedBy;
            updated = Get();
            error = null;
            return true;
        }
    }

    private static BackendProvider ParseProviderOrDefault(string? raw, BackendProvider fallback)
    {
        var parsed = ParseProvider(raw);
        return parsed ?? fallback;
    }

    private static BackendProvider? ParseProvider(string? raw)
    {
        if (string.Equals(raw, "render", StringComparison.OrdinalIgnoreCase))
        {
            return BackendProvider.Render;
        }

        if (string.Equals(raw, "fly", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "fly.io", StringComparison.OrdinalIgnoreCase))
        {
            return BackendProvider.Fly;
        }

        return null;
    }
}
