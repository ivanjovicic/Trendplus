using System.Text.Json;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Api.Services;

public interface ISellProbabilityRsOnnxScorer
{
    Task<SellProbabilityRsModelResult?> TryPredictAsync(
        IReadOnlyDictionary<string, float> features,
        CancellationToken ct = default);
}

public sealed record SellProbabilityRsModelResult(
    string ModelType,
    int Version,
    double RawPrediction,
    double Prediction,
    bool UsedCalibration);

public sealed class SellProbabilityRsOnnxScorer : ISellProbabilityRsOnnxScorer, IDisposable
{
    private sealed record CalibrationPoint(double X, double Y);

    private sealed class CacheEntry
    {
        public required string ModelType { get; init; }
        public required int Version { get; init; }
        public required string OnnxPath { get; init; }
        public required string InputName { get; init; }
        public required string OutputName { get; init; }
        public required string[] FeatureNames { get; init; }
        public required InferenceSession Session { get; init; }
        public IReadOnlyDictionary<string, float> MinValues { get; init; } = new Dictionary<string, float>(StringComparer.Ordinal);
        public IReadOnlyDictionary<string, float> MaxValues { get; init; } = new Dictionary<string, float>(StringComparer.Ordinal);
        public IReadOnlyList<CalibrationPoint>? Calibration { get; init; }
        public DateTime LoadedAtUtc { get; init; } = DateTime.UtcNow;
    }

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SellProbabilityRsOnnxScorer> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly TimeSpan _refreshInterval = TimeSpan.FromMinutes(1);

    private CacheEntry? _cache;
    private DateTime _nextRefreshUtc = DateTime.MinValue;

    public SellProbabilityRsOnnxScorer(
        IServiceScopeFactory scopeFactory,
        ILogger<SellProbabilityRsOnnxScorer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<SellProbabilityRsModelResult?> TryPredictAsync(
        IReadOnlyDictionary<string, float> features,
        CancellationToken ct = default)
    {
        var entry = await GetOrRefreshAsync(ct);
        if (entry is null)
            return null;

        var vector = new float[entry.FeatureNames.Length];
        for (var i = 0; i < entry.FeatureNames.Length; i++)
        {
            var name = entry.FeatureNames[i];
            var value = features.TryGetValue(name, out var v) ? v : 0f;

            if (entry.MinValues.TryGetValue(name, out var min) && value < min)
                value = min;
            if (entry.MaxValues.TryGetValue(name, out var max) && value > max)
                value = max;

            vector[i] = value;
        }

        var tensor = new DenseTensor<float>(new[] { 1, vector.Length });
        for (var i = 0; i < vector.Length; i++)
            tensor[0, i] = vector[i];

        var input = NamedOnnxValue.CreateFromTensor(entry.InputName, tensor);
        using var results = entry.Session.Run(new[] { input });
        var output = results.FirstOrDefault(x => string.Equals(x.Name, entry.OutputName, StringComparison.Ordinal))
            ?? results.First();

        var raw = output.AsEnumerable<float>().FirstOrDefault();
        var rawClamped = Math.Clamp((double)raw, 0d, 1d);

        var calibration = entry.Calibration;
        var usedCalibration = calibration is { Count: > 1 };
        var calibrated = usedCalibration
            ? ApplyCalibration(calibration!, rawClamped)
            : rawClamped;

        return new SellProbabilityRsModelResult(
            ModelType: entry.ModelType,
            Version: entry.Version,
            RawPrediction: rawClamped,
            Prediction: calibrated,
            UsedCalibration: usedCalibration);
    }

    private async Task<CacheEntry?> GetOrRefreshAsync(CancellationToken ct)
    {
        if (_cache is not null && DateTime.UtcNow < _nextRefreshUtc)
            return _cache;

        await _refreshLock.WaitAsync(ct);
        try
        {
            if (_cache is not null && DateTime.UtcNow < _nextRefreshUtc)
                return _cache;

            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<OpenProductTrainingDbContext>();

            var model = await db.ModelVersions
                .AsNoTracking()
                .Where(m => m.ModelType == "sell_probability_rs" && m.IsActive)
                .OrderByDescending(m => m.Version)
                .FirstOrDefaultAsync(ct);

            if (model is null || string.IsNullOrWhiteSpace(model.OnnxPath))
            {
                _nextRefreshUtc = DateTime.UtcNow.Add(_refreshInterval);
                return _cache; // keep last known model if present
            }

            var onnxPath = Path.GetFullPath(model.OnnxPath);
            if (!File.Exists(onnxPath))
            {
                _logger.LogWarning("Active ONNX model file missing: {OnnxPath}", onnxPath);
                _nextRefreshUtc = DateTime.UtcNow.Add(_refreshInterval);
                return _cache;
            }

            // If nothing changed, just extend refresh window.
            if (_cache is not null &&
                _cache.Version == model.Version &&
                string.Equals(_cache.OnnxPath, onnxPath, StringComparison.OrdinalIgnoreCase))
            {
                _nextRefreshUtc = DateTime.UtcNow.Add(_refreshInterval);
                return _cache;
            }

            var featureNames = TryParseFeatureNames(model.FeatureSchemaJson);
            if (featureNames.Length == 0)
            {
                _logger.LogWarning("Active model has empty feature schema. model_type={ModelType} version={Version}", model.ModelType, model.Version);
                _nextRefreshUtc = DateTime.UtcNow.Add(_refreshInterval);
                return _cache;
            }

            var calibration = TryParseCalibration(model.CalibrationJson);
            var minValues = TryParseFloatMap(model.MinFeatureValues);
            var maxValues = TryParseFloatMap(model.MaxFeatureValues);

            var session = new InferenceSession(onnxPath);
            var inputName = session.InputMetadata.Keys.First();
            var outputName = session.OutputMetadata.Keys.First();

            var newEntry = new CacheEntry
            {
                ModelType = model.ModelType,
                Version = model.Version,
                OnnxPath = onnxPath,
                InputName = inputName,
                OutputName = outputName,
                FeatureNames = featureNames,
                Session = session,
                Calibration = calibration,
                MinValues = minValues,
                MaxValues = maxValues,
                LoadedAtUtc = DateTime.UtcNow
            };

            var old = _cache;
            _cache = newEntry;
            _nextRefreshUtc = DateTime.UtcNow.Add(_refreshInterval);

            old?.Session.Dispose();

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Loaded active ONNX model: {ModelType} v{Version} ({Path})", newEntry.ModelType, newEntry.Version, newEntry.OnnxPath);
            }
            return _cache;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static string[] TryParseFeatureNames(string? featureSchemaJson)
    {
        if (string.IsNullOrWhiteSpace(featureSchemaJson))
            return [];

        try
        {
            using var doc = JsonDocument.Parse(featureSchemaJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return [];

            var names = new List<string>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                {
                    var s = n.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        names.Add(s);
                }
            }
            return names.ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyDictionary<string, float> TryParseFloatMap(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new Dictionary<string, float>(StringComparer.Ordinal);

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(raw);
            if (dict is null)
                return new Dictionary<string, float>(StringComparer.Ordinal);

            var output = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (var (k, v) in dict)
            {
                if (v.ValueKind == JsonValueKind.Number && v.TryGetSingle(out var f))
                    output[k] = f;
            }
            return output;
        }
        catch
        {
            return new Dictionary<string, float>(StringComparer.Ordinal);
        }
    }

    private static IReadOnlyList<CalibrationPoint>? TryParseCalibration(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("points", out var pts) || pts.ValueKind != JsonValueKind.Array)
                return null;

            var list = new List<CalibrationPoint>();
            foreach (var el in pts.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object) continue;
                if (!el.TryGetProperty("x", out var xel) || !el.TryGetProperty("y", out var yel)) continue;
                if (!xel.TryGetDouble(out var x) || !yel.TryGetDouble(out var y)) continue;
                list.Add(new CalibrationPoint(x, y));
            }

            list.Sort((a, b) => a.X.CompareTo(b.X));
            return list.Count >= 2 ? list : null;
        }
        catch
        {
            return null;
        }
    }

    private static double ApplyCalibration(IReadOnlyList<CalibrationPoint> points, double x)
    {
        if (points.Count < 2)
            return x;

        if (x <= points[0].X) return points[0].Y;
        if (x >= points[^1].X) return points[^1].Y;

        // Binary search interval
        var lo = 0;
        var hi = points.Count - 1;
        while (hi - lo > 1)
        {
            var mid = (lo + hi) / 2;
            if (x >= points[mid].X) lo = mid;
            else hi = mid;
        }

        var p0 = points[lo];
        var p1 = points[hi];
        var dx = p1.X - p0.X;
        if (Math.Abs(dx) < 1e-12) return p0.Y;
        var t = (x - p0.X) / dx;
        return Math.Clamp(p0.Y + t * (p1.Y - p0.Y), 0d, 1d);
    }

    public void Dispose()
    {
        _cache?.Session.Dispose();
        _refreshLock.Dispose();
    }
}
