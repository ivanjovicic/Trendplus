using Application.Analytics;

namespace Infrastructure.Services;

public sealed class OperationsAnalyticsIntegrityRegistry
{
    private readonly object _gate = new();
    private OperationsAnalyticsIntegritySnapshot _snapshot = OperationsAnalyticsIntegritySnapshot.Unverified(
        "bootstrap",
        "bootstrap",
        "Operations integrity monitor has not completed an initial bounded probe.");

    public OperationsAnalyticsIntegritySnapshot Current
    {
        get
        {
            lock (_gate)
                return _snapshot;
        }
    }

    public void Set(OperationsAnalyticsIntegritySnapshot snapshot)
    {
        lock (_gate)
            _snapshot = snapshot;
    }

    public void MarkUnverified(string trigger, string summary)
    {
        Set(OperationsAnalyticsIntegritySnapshot.Unverified(
            $"{trigger}-{DateTime.UtcNow:yyyyMMddHHmmss}",
            trigger,
            summary));
    }
}
