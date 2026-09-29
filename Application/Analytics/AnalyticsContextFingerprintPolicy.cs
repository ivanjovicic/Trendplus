using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Application.Analytics;

/// <summary>
/// Machine-comparable identity for analytics results that claim to describe the
/// same business context. The descriptor supplements the existing human-readable
/// period and data-scope metadata; it never changes the metric calculation.
/// </summary>
public sealed class AnalyticsContextDescriptor
{
    public string ContractVersion { get; init; } = AnalyticsContextFingerprintPolicy.ContractVersion;
    public string State { get; init; } = AnalyticsContextFingerprintPolicy.StateUnavailable;
    public string? Fingerprint { get; init; }
    public string? UnavailableReason { get; init; }
    public string DateBoundaryConvention { get; init; } = AnalyticsContextFingerprintPolicy.DefaultDateBoundaryConvention;
    public DateTime? RequestedPeriodFromUtc { get; init; }
    public DateTime? RequestedPeriodToUtc { get; init; }
    public DateTime? EffectivePeriodFromUtc { get; init; }
    public DateTime? EffectivePeriodToUtc { get; init; }
    public DateTime? ObservedPeriodFromUtc { get; init; }
    public DateTime? ObservedPeriodToUtc { get; init; }
    public string? RequestedDataScope { get; init; }
    public string? EffectiveDataScope { get; init; }
    public string? DataScopeSource { get; init; }
    public string? PopulationKey { get; init; }
    public IReadOnlyDictionary<string, string?> PopulationFilters { get; init; } =
        new Dictionary<string, string?>(StringComparer.Ordinal);
    public string? SourceDataset { get; init; }
    public string? SourceGeneration { get; init; }
    public string? FormulaVersion { get; init; }
    public string? MaterializerGeneration { get; init; }
    public string? CacheGeneration { get; init; }
    public string? RowLimitSemantics { get; init; }
}

public static class AnalyticsContextFingerprintPolicy
{
    public const string ContractVersion = "analytics_context_v1";
    public const string DefaultDateBoundaryConvention = "half_open_utc";
    public const string StateAvailable = "available";
    public const string StateEmpty = "empty";
    public const string StateDegraded = "degraded";
    public const string StateUnavailable = "unavailable";

    public static string ResolveResultState(bool success, bool isPartial, bool isEmpty)
    {
        if (!success)
        {
            return StateUnavailable;
        }

        if (isEmpty)
        {
            return StateEmpty;
        }

        return isPartial ? StateDegraded : StateAvailable;
    }

    public static AnalyticsContextDescriptor Create(
        string? sourceDataset,
        string? sourceGeneration,
        string? formulaVersion,
        string? materializerGeneration,
        string? rowLimitSemantics,
        DateTime? requestedPeriodFromUtc,
        DateTime? requestedPeriodToUtc,
        DateTime? effectivePeriodFromUtc,
        DateTime? effectivePeriodToUtc,
        DateTime? observedPeriodFromUtc,
        DateTime? observedPeriodToUtc,
        string? requestedDataScope,
        string? effectiveDataScope,
        string? dataScopeSource,
        string? populationKey,
        IReadOnlyDictionary<string, string?>? populationFilters,
        string? cacheGeneration = null,
        string? resultState = StateAvailable,
        string? unavailableReason = null,
        string? dateBoundaryConvention = DefaultDateBoundaryConvention)
    {
        var normalizedFilters = NormalizeFilters(populationFilters);
        var normalizedBoundary = Normalize(dateBoundaryConvention);
        var missing = new List<string>();

        AddMissing(missing, nameof(sourceDataset), sourceDataset);
        AddMissing(missing, nameof(sourceGeneration), sourceGeneration);
        AddMissing(missing, nameof(formulaVersion), formulaVersion);
        AddMissing(missing, nameof(materializerGeneration), materializerGeneration);
        AddMissing(missing, nameof(rowLimitSemantics), rowLimitSemantics);
        AddMissing(missing, nameof(dateBoundaryConvention), dateBoundaryConvention);

        var resolvedState = NormalizeState(resultState);
        var canFingerprint = missing.Count == 0 && !string.Equals(resolvedState, StateUnavailable, StringComparison.Ordinal);
        var canonical = canFingerprint
            ? BuildCanonicalValue(
                normalizedBoundary,
                requestedPeriodFromUtc,
                requestedPeriodToUtc,
                effectivePeriodFromUtc,
                effectivePeriodToUtc,
                observedPeriodFromUtc,
                observedPeriodToUtc,
                requestedDataScope,
                effectiveDataScope,
                dataScopeSource,
                populationKey,
                normalizedFilters,
                sourceDataset,
                sourceGeneration,
                formulaVersion,
                materializerGeneration,
                cacheGeneration,
                rowLimitSemantics)
            : null;

        return new AnalyticsContextDescriptor
        {
            ContractVersion = ContractVersion,
            State = canFingerprint ? resolvedState : StateUnavailable,
            Fingerprint = canonical is null ? null : ComputeFingerprint(canonical),
            UnavailableReason = canFingerprint
                ? unavailableReason
                : unavailableReason ?? $"missing_context_fields:{string.Join(',', missing)}",
            DateBoundaryConvention = string.IsNullOrWhiteSpace(dateBoundaryConvention)
                ? DefaultDateBoundaryConvention
                : dateBoundaryConvention.Trim(),
            RequestedPeriodFromUtc = NormalizeUtc(requestedPeriodFromUtc),
            RequestedPeriodToUtc = NormalizeUtc(requestedPeriodToUtc),
            EffectivePeriodFromUtc = NormalizeUtc(effectivePeriodFromUtc),
            EffectivePeriodToUtc = NormalizeUtc(effectivePeriodToUtc),
            ObservedPeriodFromUtc = NormalizeUtc(observedPeriodFromUtc),
            ObservedPeriodToUtc = NormalizeUtc(observedPeriodToUtc),
            RequestedDataScope = NormalizeNullable(requestedDataScope),
            EffectiveDataScope = NormalizeNullable(effectiveDataScope),
            DataScopeSource = NormalizeNullable(dataScopeSource),
            PopulationKey = NormalizeNullable(populationKey),
            PopulationFilters = normalizedFilters,
            SourceDataset = NormalizeNullable(sourceDataset),
            SourceGeneration = NormalizeNullable(sourceGeneration),
            FormulaVersion = NormalizeNullable(formulaVersion),
            MaterializerGeneration = NormalizeNullable(materializerGeneration),
            CacheGeneration = NormalizeNullable(cacheGeneration),
            RowLimitSemantics = NormalizeNullable(rowLimitSemantics)
        };
    }

    private static string BuildCanonicalValue(
        string dateBoundaryConvention,
        DateTime? requestedPeriodFromUtc,
        DateTime? requestedPeriodToUtc,
        DateTime? effectivePeriodFromUtc,
        DateTime? effectivePeriodToUtc,
        DateTime? observedPeriodFromUtc,
        DateTime? observedPeriodToUtc,
        string? requestedDataScope,
        string? effectiveDataScope,
        string? dataScopeSource,
        string? populationKey,
        IReadOnlyDictionary<string, string?> populationFilters,
        string? sourceDataset,
        string? sourceGeneration,
        string? formulaVersion,
        string? materializerGeneration,
        string? cacheGeneration,
        string? rowLimitSemantics)
    {
        var fields = new List<string>
        {
            ContractVersion,
            Normalize(dateBoundaryConvention),
            FormatDate(requestedPeriodFromUtc),
            FormatDate(requestedPeriodToUtc),
            FormatDate(effectivePeriodFromUtc),
            FormatDate(effectivePeriodToUtc),
            FormatDate(observedPeriodFromUtc),
            FormatDate(observedPeriodToUtc),
            Normalize(requestedDataScope),
            Normalize(effectiveDataScope),
            Normalize(dataScopeSource),
            Normalize(populationKey),
            Normalize(sourceDataset),
            Normalize(sourceGeneration),
            Normalize(formulaVersion),
            Normalize(materializerGeneration),
            Normalize(cacheGeneration),
            Normalize(rowLimitSemantics)
        };

        fields.AddRange(populationFilters.Select(pair =>
            $"{Normalize(pair.Key)}={Normalize(pair.Value)}"));

        return string.Join("\n", fields);
    }

    private static IReadOnlyDictionary<string, string?> NormalizeFilters(
        IReadOnlyDictionary<string, string?>? filters)
        => (filters ?? new Dictionary<string, string?>())
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .OrderBy(pair => pair.Key.Trim(), StringComparer.Ordinal)
            .ToDictionary(
                pair => pair.Key.Trim(),
                pair => NormalizeNullable(pair.Value),
                StringComparer.Ordinal);

    private static string ComputeFingerprint(string canonicalValue)
        => $"sha256:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalValue))).ToLowerInvariant()}";

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? "<unknown>" : value.Trim().ToLowerInvariant();

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FormatDate(DateTime? value)
        => value.HasValue
            ? NormalizeUtc(value)!.Value.ToString("O", CultureInfo.InvariantCulture)
            : "<unknown>";

    private static DateTime? NormalizeUtc(DateTime? value)
        => value.HasValue
            ? value.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
                : value.Value.ToUniversalTime()
            : null;

    private static string NormalizeState(string? state)
        => state?.Trim().ToLowerInvariant() switch
        {
            StateAvailable => StateAvailable,
            StateEmpty => StateEmpty,
            StateDegraded => StateDegraded,
            _ => StateUnavailable
        };

    private static void AddMissing(ICollection<string> missing, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            missing.Add(name);
        }
    }
}
