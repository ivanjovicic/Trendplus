using Application.Analytics;

namespace Infrastructure.Services;

public sealed class OperationsAnalyticsIntegrityRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, OperationsAnalyticsIntegritySnapshot> _snapshots =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _generations =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _pendingFamilyMutations = new(StringComparer.Ordinal);

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

    public bool Set(OperationsAnalyticsIntegritySnapshot snapshot)
    {
        lock (_gate)
        {
            if (_pendingFamilyMutations.ContainsKey(snapshot.Family)
                && !string.Equals(snapshot.Status, OperationsAnalyticsIntegrityStates.Unverified, StringComparison.Ordinal))
            {
                // A probe may start after mutation invalidation and still observe
                // pre-commit rows. It cannot restore any non-fail-closed state
                // until the write has completed and advanced the generation again.
                return false;
            }

            if (!string.IsNullOrWhiteSpace(snapshot.SourceGeneration)
                && _generations.TryGetValue(snapshot.Family, out var currentGeneration)
                && !string.Equals(currentGeneration, snapshot.SourceGeneration, StringComparison.Ordinal))
            {
                // A probe that started before a source mutation must never restore
                // the generation it observed when it eventually completes.
                return false;
            }

            _snapshots[snapshot.Family] = snapshot;
            if (!string.IsNullOrWhiteSpace(snapshot.SourceGeneration))
                _generations[snapshot.Family] = snapshot.SourceGeneration;
            return true;
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

    public OperationsAnalyticsIntegritySnapshot BeginFamilyMutation(string family, string trigger, string summary)
    {
        lock (_gate)
        {
            var generation = CreateGeneration();
            _generations[family] = generation;
            _pendingFamilyMutations[family] = _pendingFamilyMutations.GetValueOrDefault(family) + 1;
            var snapshot = CreateUnverifiedSnapshot(family, generation, trigger, summary);
            _snapshots[family] = snapshot;
            return snapshot;
        }
    }

    public OperationsAnalyticsIntegritySnapshot CompleteFamilyMutation(string family, string trigger, string summary)
    {
        lock (_gate)
        {
            var generation = CreateGeneration();
            _generations[family] = generation;
            if (_pendingFamilyMutations.TryGetValue(family, out var pendingCount))
            {
                if (pendingCount <= 1)
                    _pendingFamilyMutations.Remove(family);
                else
                    _pendingFamilyMutations[family] = pendingCount - 1;
            }
            var snapshot = CreateUnverifiedSnapshot(family, generation, trigger, summary);
            _snapshots[family] = snapshot;
            return snapshot;
        }
    }

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
