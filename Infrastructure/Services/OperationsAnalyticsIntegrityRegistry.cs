using Application.Analytics;

namespace Infrastructure.Services;

public sealed class OperationsAnalyticsIntegrityRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, OperationsAnalyticsIntegritySnapshot> _snapshots =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _generations =
        new(StringComparer.Ordinal);

    public OperationsAnalyticsIntegrityRegistry()
    {
        foreach (var definition in OperationsAnalyticsIntegrityFamilies.Enrolled)
        {
            var generation = CreateGeneration();
            _generations[definition.Family] = generation;
            _snapshots[definition.Family] = CreateUnverifiedSnapshot(
                definition.Family,
                generation,
                "bootstrap",
                "Operations integrity monitor has not completed an initial bounded probe.");
        }
    }

    public OperationsAnalyticsIntegritySnapshot Current
    {
        get
        {
            lock (_gate)
                return _snapshots[OperationsAnalyticsIntegrityFamilies.SupplierShoeType];
        }
    }

    public IReadOnlyList<OperationsAnalyticsIntegritySnapshot> CurrentByFamily
    {
        get
        {
            lock (_gate)
                return _snapshots.Values.ToArray();
        }
    }

    public OperationsAnalyticsIntegritySnapshot GetCurrent(string family)
    {
        lock (_gate)
        {
            return _snapshots.TryGetValue(family, out var snapshot)
                ? snapshot
                : CreateUnverifiedSnapshot(
                    family,
                    GetOrCreateGenerationUnsafe(family),
                    "unknown_family",
                    $"No enrolled integrity family exists for '{family}'.");
        }
    }

    public string GetGeneration(string family)
    {
        lock (_gate)
            return GetOrCreateGenerationUnsafe(family);
    }

    public void Set(OperationsAnalyticsIntegritySnapshot snapshot)
    {
        lock (_gate)
        {
            _snapshots[snapshot.Family] = snapshot;
            if (!string.IsNullOrWhiteSpace(snapshot.SourceGeneration))
                _generations[snapshot.Family] = snapshot.SourceGeneration;
        }
    }

    public void MarkUnverified(
        string trigger,
        string summary,
        IEnumerable<string>? families = null)
    {
        var affectedFamilies = families?.ToArray()
            ?? OperationsAnalyticsIntegrityFamilies.ResolveAffected(trigger);

        lock (_gate)
        {
            foreach (var family in affectedFamilies.Distinct(StringComparer.Ordinal))
            {
                var generation = CreateGeneration();
                _generations[family] = generation;
                _snapshots[family] = CreateUnverifiedSnapshot(family, generation, trigger, summary);
            }
        }
    }

    public void MarkFamilyUnverified(string family, string trigger, string summary)
        => MarkUnverified(trigger, summary, [family]);

    private string GetOrCreateGenerationUnsafe(string family)
    {
        if (_generations.TryGetValue(family, out var generation))
            return generation;

        generation = CreateGeneration();
        _generations[family] = generation;
        return generation;
    }

    private static OperationsAnalyticsIntegritySnapshot CreateUnverifiedSnapshot(
        string family,
        string generation,
        string trigger,
        string summary)
    {
        var snapshot = OperationsAnalyticsIntegritySnapshot.Unverified(
            $"{family}-{trigger}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}",
            trigger,
            summary);
        snapshot.Family = family;
        snapshot.SourceGeneration = generation;
        return snapshot;
    }

    private static string CreateGeneration()
        => $"integrity-generation-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";
}
