using Api.Config;

namespace Api.Services;

public sealed record EnterpriseScoringResult(
    double LinearScore,
    double RawProbability,
    double SoftmaxProbability,
    double CalibratedProbability,
    double FinalProbability,
    IReadOnlyDictionary<string, double> StandardizedFeatures,
    IReadOnlyDictionary<string, double> WeightedContributions);

public sealed class EnterpriseScoringLinearModel
{
    private readonly EnterpriseScoringOptions _options;
    private readonly IReadOnlyDictionary<string, double> _weights;
    private readonly IReadOnlyDictionary<string, double> _means;
    private readonly IReadOnlyDictionary<string, double> _stdDevs;
    private readonly double _bias;
    private readonly double _calibrationA;
    private readonly double _calibrationB;

    public EnterpriseScoringLinearModel(EnterpriseScoringOptions options)
    {
        _options = options;
        _weights = new Dictionary<string, double>(
            options.FeatureWeights ?? new Dictionary<string, double>(),
            StringComparer.OrdinalIgnoreCase);
        _means = new Dictionary<string, double>(
            options.FeatureMeans ?? new Dictionary<string, double>(),
            StringComparer.OrdinalIgnoreCase);
        _stdDevs = new Dictionary<string, double>(
            options.FeatureStdDevs ?? new Dictionary<string, double>(),
            StringComparer.OrdinalIgnoreCase);
        _bias = options.Bias;
        _calibrationA = options.CalibrationA;
        _calibrationB = options.CalibrationB;
    }

    public EnterpriseScoringLinearModel(
        EnterpriseScoringOptions options,
        EnterpriseScoringDbParameters dbParameters)
    {
        _options = options;
        _weights = new Dictionary<string, double>(
            dbParameters.FeatureWeights ?? new Dictionary<string, double>(),
            StringComparer.OrdinalIgnoreCase);
        _means = new Dictionary<string, double>(
            dbParameters.FeatureMeans ?? new Dictionary<string, double>(),
            StringComparer.OrdinalIgnoreCase);
        _stdDevs = new Dictionary<string, double>(
            dbParameters.FeatureStdDevs ?? new Dictionary<string, double>(),
            StringComparer.OrdinalIgnoreCase);
        _bias = dbParameters.Bias;
        _calibrationA = dbParameters.CalibrationA;
        _calibrationB = dbParameters.CalibrationB;
    }

    public EnterpriseScoringResult Compute(IReadOnlyDictionary<string, double> rawFeatures)
    {
        var standardized = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var contributions = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        var z = _bias;

        foreach (var (feature, weight) in _weights)
        {
            var raw = rawFeatures.TryGetValue(feature, out var value)
                ? Clamp01(value)
                : 0d;

            var standardizedValue = Standardize(feature, raw);
            var contribution = weight * standardizedValue;

            standardized[feature] = standardizedValue;
            contributions[feature] = contribution;
            z += contribution;
        }

        var rawProbability = Sigmoid(z);
        var softmaxProbability = Softmax([z, 0d])[0];
        var calibratedProbability = Sigmoid((_calibrationA * z) + _calibrationB);
        var softmaxBlend = Clamp01(_options.SoftmaxBlendWeight);
        var finalProbability = Clamp01(
            (calibratedProbability * (1d - softmaxBlend)) +
            (softmaxProbability * softmaxBlend));

        return new EnterpriseScoringResult(
            LinearScore: z,
            RawProbability: rawProbability,
            SoftmaxProbability: softmaxProbability,
            CalibratedProbability: calibratedProbability,
            FinalProbability: finalProbability,
            StandardizedFeatures: standardized,
            WeightedContributions: contributions);
    }

    public double[] Softmax(IReadOnlyList<double> scores)
    {
        if (scores.Count == 0)
            return [];

        var temperature = Math.Max(1e-6, Math.Abs(_options.Temperature));
        var scaled = scores.Select(x => x / temperature).ToArray();
        var max = scaled.Max();
        var exp = scaled.Select(x => Math.Exp(Math.Clamp(x - max, -60d, 60d))).ToArray();
        var sum = exp.Sum();
        if (sum <= 0d)
            return Enumerable.Repeat(1d / scores.Count, scores.Count).ToArray();
        return exp.Select(x => x / sum).ToArray();
    }

    private double Standardize(string feature, double rawValue)
    {
        if (!_means.TryGetValue(feature, out var mean))
            mean = 0d;

        if (!_stdDevs.TryGetValue(feature, out var stdDev) || Math.Abs(stdDev) < 1e-9)
            stdDev = 1d;

        var standardized = (rawValue - mean) / stdDev;
        return Math.Clamp(standardized, -5d, 5d);
    }

    private static double Sigmoid(double x)
    {
        // numerically stable sigmoid
        if (x >= 0)
        {
            var z = Math.Exp(-x);
            return 1d / (1d + z);
        }

        var e = Math.Exp(x);
        return e / (1d + e);
    }

    private static double Clamp01(double value)
        => Math.Clamp(value, 0d, 1d);
}
