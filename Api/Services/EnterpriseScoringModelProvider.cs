using System.Globalization;
using System.Text.Json;
using Api.Config;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services;

public sealed record EnterpriseScoringDbParameters(
    long ModelId,
    string ModelType,
    int Version,
    IReadOnlyDictionary<string, double> FeatureWeights,
    IReadOnlyDictionary<string, double> FeatureMeans,
    IReadOnlyDictionary<string, double> FeatureStdDevs,
    double Bias,
    double CalibrationA,
    double CalibrationB,
    RuntimeScoringTuningOptions? RuntimeTuning);

public interface IEnterpriseScoringModelProvider
{
    Task<EnterpriseScoringDbParameters?> TryGetActiveAsync(CancellationToken ct = default);
}

public sealed class EnterpriseScoringModelProvider : IEnterpriseScoringModelProvider, IDisposable
{
    private sealed record CacheEntry(EnterpriseScoringDbParameters? Parameters, DateTime ExpiresAtUtc);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<RuntimeScoringOptions> _options;
    private readonly ILogger<EnterpriseScoringModelProvider> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private CacheEntry? _cache;

    public EnterpriseScoringModelProvider(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<RuntimeScoringOptions> options,
        ILogger<EnterpriseScoringModelProvider> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<EnterpriseScoringDbParameters?> TryGetActiveAsync(CancellationToken ct = default)
    {
        var enterpriseOptions = _options.CurrentValue.Enterprise;
        if (!enterpriseOptions.Enabled || !enterpriseOptions.PreferModelVersionParameters)
            return null;

        var now = DateTime.UtcNow;
        if (_cache is not null && now < _cache.ExpiresAtUtc)
            return _cache.Parameters;

        await _refreshLock.WaitAsync(ct);
        try
        {
            now = DateTime.UtcNow;
            if (_cache is not null && now < _cache.ExpiresAtUtc)
                return _cache.Parameters;

            var refreshInterval = TimeSpan.FromSeconds(Math.Max(10, enterpriseOptions.ModelRefreshSeconds));
            var modelType = string.IsNullOrWhiteSpace(enterpriseOptions.ModelType)
                ? "enterprise_scoring"
                : enterpriseOptions.ModelType.Trim();

            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<OpenProductTrainingDbContext>();
            var model = await db.ModelVersions
                .AsNoTracking()
                .Where(m => m.IsActive && m.ModelType == modelType)
                .OrderByDescending(m => m.Version)
                .Select(m => new
                {
                    m.Id,
                    m.ModelType,
                    m.Version,
                    m.FeatureImportanceJson,
                    m.MetricsJson,
                    m.CalibrationJson,
                    m.RuntimeTuningJson
                })
                .FirstOrDefaultAsync(ct);

            if (model is null)
            {
                _cache = new CacheEntry(_cache?.Parameters, now.Add(refreshInterval));
                return _cache.Parameters;
            }

            if (!TryParseParameters(
                    model.FeatureImportanceJson,
                    model.MetricsJson,
                    model.CalibrationJson,
                    model.RuntimeTuningJson,
                    enterpriseOptions,
                    _options.CurrentValue.Tuning,
                    out var parsed))
            {
                _logger.LogWarning(
                    "Active enterprise model_version lacks parseable weights/calibration. model_type={ModelType} version={Version}",
                    model.ModelType,
                    model.Version);

                _cache = new CacheEntry(_cache?.Parameters, now.Add(refreshInterval));
                return _cache.Parameters;
            }

            var parameters = new EnterpriseScoringDbParameters(
                ModelId: model.Id,
                ModelType: model.ModelType,
                Version: model.Version,
                FeatureWeights: parsed.FeatureWeights,
                FeatureMeans: parsed.FeatureMeans,
                FeatureStdDevs: parsed.FeatureStdDevs,
                Bias: parsed.Bias,
                CalibrationA: parsed.CalibrationA,
                CalibrationB: parsed.CalibrationB,
                RuntimeTuning: parsed.RuntimeTuning);

            _cache = new CacheEntry(parameters, now.Add(refreshInterval));
            return parameters;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed loading enterprise scoring model from model_version; using cached/config fallback.");
            return _cache?.Parameters;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private sealed record ParsedParameters(
        IReadOnlyDictionary<string, double> FeatureWeights,
        IReadOnlyDictionary<string, double> FeatureMeans,
        IReadOnlyDictionary<string, double> FeatureStdDevs,
        double Bias,
        double CalibrationA,
        double CalibrationB,
        RuntimeScoringTuningOptions? RuntimeTuning);

    private static bool TryParseParameters(
        string? featureImportanceJson,
        string? metricsJson,
        string? calibrationJson,
        string? runtimeTuningJson,
        EnterpriseScoringOptions defaults,
        RuntimeScoringTuningOptions runtimeTuningDefaults,
        out ParsedParameters parsed)
    {
        parsed = default!;

        if (!TryParseWeightsAndScaler(featureImportanceJson, metricsJson, out var weights, out var means, out var stds, out var bias))
            return false;

        var calibrationA = defaults.CalibrationA;
        var calibrationB = defaults.CalibrationB;
        TryParseCalibration(calibrationJson, ref calibrationA, ref calibrationB);

        var runtimeTuning = TryParseRuntimeTuning(runtimeTuningJson, runtimeTuningDefaults);
        parsed = new ParsedParameters(weights, means, stds, bias, calibrationA, calibrationB, runtimeTuning);
        return true;
    }

    private static bool TryParseWeightsAndScaler(
        string? primaryJson,
        string? secondaryJson,
        out IReadOnlyDictionary<string, double> weights,
        out IReadOnlyDictionary<string, double> means,
        out IReadOnlyDictionary<string, double> stds,
        out double bias)
    {
        weights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        means = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        stds = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        bias = 0d;

        if (TryParseWeightsDocument(primaryJson, out weights, out means, out stds, out bias))
            return true;

        if (TryParseWeightsDocument(secondaryJson, out weights, out means, out stds, out bias))
            return true;

        return false;
    }

    private static bool TryParseWeightsDocument(
        string? json,
        out IReadOnlyDictionary<string, double> weights,
        out IReadOnlyDictionary<string, double> means,
        out IReadOnlyDictionary<string, double> stds,
        out double bias)
    {
        weights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        means = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        stds = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        bias = 0d;

        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            var featureColumns = TryReadStringArray(root, "feature_columns");

            // 1) canonical array format: feature_columns + weights + bias
            var weightArray = TryReadDoubleArray(root, "weights");
            if (featureColumns.Count > 0 && weightArray.Count == featureColumns.Count && weightArray.Count > 0)
            {
                weights = BuildMap(featureColumns, weightArray, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                // 2) map format: feature_weights
                var featureWeights = TryReadDoubleObject(root, "feature_weights");
                if (featureWeights.Count > 0)
                {
                    weights = new Dictionary<string, double>(featureWeights, StringComparer.OrdinalIgnoreCase);
                    featureColumns = featureWeights.Keys.ToList();
                }
                else
                {
                    return false;
                }
            }

            if (TryReadDouble(root, out var b, "bias", "intercept", "model_bias"))
                bias = b;

            // Scaler arrays aligned to feature_columns.
            var meanArray = TryReadDoubleArray(root, "scaler_mean");
            if (featureColumns.Count > 0 && meanArray.Count == featureColumns.Count)
                means = BuildMap(featureColumns, meanArray, StringComparer.OrdinalIgnoreCase);

            var scaleArray = TryReadDoubleArray(root, "scaler_scale", "feature_std_devs");
            if (featureColumns.Count > 0 && scaleArray.Count == featureColumns.Count)
                stds = BuildMap(featureColumns, scaleArray, StringComparer.OrdinalIgnoreCase);

            // Optional map override.
            var meanMap = TryReadDoubleObject(root, "feature_means");
            if (meanMap.Count > 0)
                means = new Dictionary<string, double>(meanMap, StringComparer.OrdinalIgnoreCase);

            var stdMap = TryReadDoubleObject(root, "feature_std_devs", "feature_stds", "stds");
            if (stdMap.Count > 0)
                stds = new Dictionary<string, double>(stdMap, StringComparer.OrdinalIgnoreCase);

            return weights.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static void TryParseCalibration(string? calibrationJson, ref double calibrationA, ref double calibrationB)
    {
        if (string.IsNullOrWhiteSpace(calibrationJson))
            return;

        try
        {
            using var doc = JsonDocument.Parse(calibrationJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return;

            if (TryReadDouble(root, out var a, "platt_A", "a", "A"))
                calibrationA = a;

            if (TryReadDouble(root, out var b, "platt_B", "b", "B"))
                calibrationB = b;
        }
        catch
        {
            // keep defaults
        }
    }

    private static Dictionary<string, double> BuildMap(IReadOnlyList<string> keys, IReadOnlyList<double> values, IEqualityComparer<string> comparer)
    {
        var map = new Dictionary<string, double>(comparer);
        for (var i = 0; i < Math.Min(keys.Count, values.Count); i++)
        {
            if (!string.IsNullOrWhiteSpace(keys[i]))
                map[keys[i]] = values[i];
        }

        return map;
    }

    private static List<string> TryReadStringArray(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Array)
                continue;

            var output = new List<string>();
            foreach (var item in el.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var value = item.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                        output.Add(value.Trim());
                }
            }

            if (output.Count > 0)
                return output;
        }

        return [];
    }

    private static List<double> TryReadDoubleArray(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Array)
                continue;

            var output = new List<double>();
            foreach (var item in el.EnumerateArray())
            {
                if (TryReadNumber(item, out var value))
                    output.Add(value);
            }

            if (output.Count > 0)
                return output;
        }

        return [];
    }

    private static Dictionary<string, double> TryReadDoubleObject(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Object)
                continue;

            var output = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in el.EnumerateObject())
            {
                if (TryReadNumber(prop.Value, out var value))
                    output[prop.Name] = value;
            }

            if (output.Count > 0)
                return output;
        }

        return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryReadDouble(JsonElement root, out double value, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var el))
                continue;

            if (TryReadNumber(el, out value))
                return true;
        }

        value = 0d;
        return false;
    }

    private static bool TryReadNumber(JsonElement element, out double value)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out value))
            return true;

        if (element.ValueKind == JsonValueKind.String &&
            double.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            return true;

        value = 0d;
        return false;
    }

    private static RuntimeScoringTuningOptions? TryParseRuntimeTuning(
        string? runtimeTuningJson,
        RuntimeScoringTuningOptions defaults)
    {
        if (string.IsNullOrWhiteSpace(runtimeTuningJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(runtimeTuningJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            var tuning = CloneTuning(defaults);
            var hasAny = false;

            if (TryReadIntIgnoreCase(root, out var marketplaceItemsPerUnit, "MarketplaceCoverageItemsPerUnit"))
            {
                tuning.MarketplaceCoverageItemsPerUnit = marketplaceItemsPerUnit;
                hasAny = true;
            }

            if (TryReadIntIgnoreCase(root, out var marketplaceMaxUnits, "MarketplaceCoverageMaxUnits"))
            {
                tuning.MarketplaceCoverageMaxUnits = marketplaceMaxUnits;
                hasAny = true;
            }

            if (TryReadIntIgnoreCase(root, out var sourceCoverageNormalizationMaxUnits, "SourceCoverageNormalizationMaxUnits"))
            {
                tuning.SourceCoverageNormalizationMaxUnits = sourceCoverageNormalizationMaxUnits;
                hasAny = true;
            }

            if (TryReadDoubleIgnoreCase(root, out var priceFitExponentialDecay, "PriceFitExponentialDecay"))
            {
                tuning.PriceFitExponentialDecay = priceFitExponentialDecay;
                hasAny = true;
            }

            if (TryReadDoubleIgnoreCase(root, out var dealTanhMultiplier, "DealTanhMultiplier"))
            {
                tuning.DealTanhMultiplier = dealTanhMultiplier;
                hasAny = true;
            }

            if (TryReadDoubleIgnoreCase(root, out var confidenceBase, "ConfidenceBase"))
            {
                tuning.ConfidenceBase = confidenceBase;
                hasAny = true;
            }

            if (TryReadDoubleIgnoreCase(root, out var confidenceTrainingBonus, "ConfidenceTrainingBonus"))
            {
                tuning.ConfidenceTrainingBonus = confidenceTrainingBonus;
                hasAny = true;
            }

            if (TryReadDoubleIgnoreCase(root, out var confidencePerSource, "ConfidencePerSource"))
            {
                tuning.ConfidencePerSource = confidencePerSource;
                hasAny = true;
            }

            if (TryReadDoubleIgnoreCase(root, out var confidenceSourceCap, "ConfidenceSourceCap"))
            {
                tuning.ConfidenceSourceCap = confidenceSourceCap;
                hasAny = true;
            }

            if (TryReadDoubleIgnoreCase(root, out var confidenceImageDivisor, "ConfidenceImageDivisor"))
            {
                tuning.ConfidenceImageDivisor = confidenceImageDivisor;
                hasAny = true;
            }

            if (TryReadDoubleIgnoreCase(root, out var confidenceImageCap, "ConfidenceImageCap"))
            {
                tuning.ConfidenceImageCap = confidenceImageCap;
                hasAny = true;
            }

            if (TryReadDoubleIgnoreCase(root, out var confidenceBaselineBonus, "ConfidenceBaselineBonus"))
            {
                tuning.ConfidenceBaselineBonus = confidenceBaselineBonus;
                hasAny = true;
            }

            if (TryReadDoubleIgnoreCase(root, out var confidenceCap, "ConfidenceCap"))
            {
                tuning.ConfidenceCap = confidenceCap;
                hasAny = true;
            }

            return hasAny ? tuning : null;
        }
        catch
        {
            return null;
        }
    }

    private static RuntimeScoringTuningOptions CloneTuning(RuntimeScoringTuningOptions source)
        => new()
        {
            MarketplaceCoverageItemsPerUnit = source.MarketplaceCoverageItemsPerUnit,
            MarketplaceCoverageMaxUnits = source.MarketplaceCoverageMaxUnits,
            SourceCoverageNormalizationMaxUnits = source.SourceCoverageNormalizationMaxUnits,
            PriceFitExponentialDecay = source.PriceFitExponentialDecay,
            DealTanhMultiplier = source.DealTanhMultiplier,
            ConfidenceBase = source.ConfidenceBase,
            ConfidenceTrainingBonus = source.ConfidenceTrainingBonus,
            ConfidencePerSource = source.ConfidencePerSource,
            ConfidenceSourceCap = source.ConfidenceSourceCap,
            ConfidenceImageDivisor = source.ConfidenceImageDivisor,
            ConfidenceImageCap = source.ConfidenceImageCap,
            ConfidenceBaselineBonus = source.ConfidenceBaselineBonus,
            ConfidenceCap = source.ConfidenceCap
        };

    private static bool TryReadDoubleIgnoreCase(JsonElement root, out double value, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetPropertyIgnoreCase(root, name, out var element))
                continue;

            if (TryReadNumber(element, out value))
                return true;
        }

        value = 0d;
        return false;
    }

    private static bool TryReadIntIgnoreCase(JsonElement root, out int value, params string[] names)
    {
        if (TryReadDoubleIgnoreCase(root, out var number, names))
        {
            value = Convert.ToInt32(Math.Round(number, MidpointRounding.AwayFromZero), CultureInfo.InvariantCulture);
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement root, string name, out JsonElement value)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in root.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    public void Dispose()
    {
        _refreshLock.Dispose();
    }
}
