namespace Api.Services.DataSources;

/// <summary>
/// Worker-facing wrapper: apply a mapped batch through the checkpoint engine
/// against the durable PostgreSQL store. Does not write back to customer sources.
/// </summary>
public sealed class SourceCheckpointSyncService
{
    private readonly EfSourceSyncStore _store;
    private readonly SourceCheckpointSyncEngine _engine;

    public SourceCheckpointSyncService(EfSourceSyncStore store, SourceCheckpointSyncEngine engine)
    {
        _store = store;
        _engine = engine;
    }

    public SourceSyncBatchResult Apply(SourceSyncBatchRequest request, DateTime? utcNow = null)
        => _engine.Apply(_store, request, utcNow ?? DateTime.UtcNow);
}
