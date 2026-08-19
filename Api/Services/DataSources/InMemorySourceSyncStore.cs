namespace Api.Services.DataSources;

public sealed class InMemorySourceSyncStore : ISourceSyncStore
{
    private readonly Dictionary<string, SourceSyncCheckpointRecord> _checkpoints = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SourceSyncAppliedRowRecord> _rows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SourceSyncAppliedRowRecord> _stagedRows = new(StringComparer.Ordinal);
    private SourceSyncCheckpointRecord? _stagedCheckpoint;
    private SourceSyncCrashMode _crashMode;

    public SourceSyncCrashMode CrashMode
    {
        get => _crashMode;
        set => _crashMode = value;
    }

    public IReadOnlyCollection<SourceSyncAppliedRowRecord> Rows => _rows.Values;

    public SourceSyncCheckpointRecord? GetCheckpoint(SourceSyncIdentity identity)
        => _checkpoints.TryGetValue(Key(identity), out var record) ? record : null;

    public SourceSyncAppliedRowRecord? GetApplied(SourceSyncIdentity identity, string externalKey)
    {
        var key = RowKey(identity, externalKey);
        if (_stagedRows.TryGetValue(key, out var staged))
            return staged;
        return _rows.TryGetValue(key, out var record) ? record : null;
    }

    public void StageApplied(SourceSyncAppliedRowRecord row)
        => _stagedRows[RowKey(row.Identity, row.ExternalKey)] = row;

    public void StageCheckpoint(SourceSyncCheckpointRecord checkpoint)
        => _stagedCheckpoint = checkpoint;

    public void Commit()
    {
        if (_crashMode == SourceSyncCrashMode.BeforeDestinationCommit)
        {
            Rollback();
            throw new InvalidOperationException("Simulated crash before destination commit.");
        }

        foreach (var pair in _stagedRows)
            _rows[pair.Key] = pair.Value;
        _stagedRows.Clear();

        if (_crashMode == SourceSyncCrashMode.AfterDestinationBeforeCheckpoint)
        {
            _stagedCheckpoint = null;
            throw new InvalidOperationException("Simulated crash after destination commit, before checkpoint.");
        }

        if (_stagedCheckpoint is not null)
        {
            _checkpoints[Key(_stagedCheckpoint.Identity)] = _stagedCheckpoint;
            _stagedCheckpoint = null;
        }
    }

    public void Rollback()
    {
        _stagedRows.Clear();
        _stagedCheckpoint = null;
    }

    private static string Key(SourceSyncIdentity identity)
        => $"{identity.ConnectionId}\n{identity.MappingProfileId}\n{identity.SourceStream}";

    private static string RowKey(SourceSyncIdentity identity, string externalKey)
        => $"{Key(identity)}\n{externalKey}";
}
