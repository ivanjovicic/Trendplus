namespace Application.Documents.Interfaces;

public interface IDocumentQueueStore
{
    Task<IReadOnlyList<Guid>> ClaimNextQueuedAsync(int batchSize, CancellationToken ct = default);
}
